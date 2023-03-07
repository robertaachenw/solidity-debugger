using Meadow.CoverageReport.Debugging;
using Meadow.DebugAdapterServer;
using Meadow.DebugAdapterServer.DebuggerTransport;
using Meadow.JsonRpc.Client;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Meadow.Plugin;
using Meadow.Shared;
using Thread = System.Threading.Thread;


namespace Meadow.DebugAdapterServer
{
    public class SolidityDebugger : IDisposable
    {
        const string DEBUG_SESSION_ID = "DEBUG_SESSION_ID";
        const string DEBUG_STOP_ON_ENTRY = "DEBUG_STOP_ON_ENTRY";

        public static bool IsSolidityDebuggerAttached { get; set; }

        public static string SolidityDebugSessionID => Environment.GetEnvironmentVariable(DEBUG_SESSION_ID);

        public static bool DebugStopOnEntry => (Environment.GetEnvironmentVariable(DEBUG_STOP_ON_ENTRY) ?? string.Empty).Equals("true", StringComparison.OrdinalIgnoreCase);

        public static bool HasSolidityDebugAttachRequest => !string.IsNullOrWhiteSpace(SolidityDebugSessionID);


        private Thread _messageLogThread = null;
        private bool _shuttingDown = false;
        private List<SetupProcess> _setupProcesses = new List<SetupProcess>(); 
        
        public static SolidityDebugger AttachSolidityDebugger(IDebuggerTransport debuggerTransport, CancellationTokenSource cancelToken = null, bool useContractsSubDir = true)
        {
            var debuggingInstance = new SolidityDebugger(debuggerTransport, useContractsSubDir);

            debuggingInstance.InitializeDebugConnection();
            IsSolidityDebuggerAttached = true;
            debuggingInstance.SetupRpcDebuggingHook();

            debuggingInstance.OnDebuggerDisconnect += () =>
            {
                // If the C# debugger is not attached, we don't care about running the rest of the tests
                // so exit program
                if (!Debugger.IsAttached)
                {
                    cancelToken?.Cancel();
                }
            };

            return debuggingInstance;
        }

        public MeadowSolidityDebugAdapter DebugAdapter { get; }

        readonly IDebuggerTransport _debuggerTransport;

#pragma warning disable CA1710 // Identifiers should have correct suffix
        public event Action OnDebuggerDisconnect;
#pragma warning restore CA1710 // Identifiers should have correct suffix

        private SolidityDebugger(IDebuggerTransport debuggerTransport, bool useContractsSubDir)
        {
            _debuggerTransport = debuggerTransport;
            DebugAdapter = new MeadowSolidityDebugAdapter(useContractsSubDir);
            DebugAdapter.OnDebuggerDisconnect += DebugAdapter_OnDebuggerDisconnect;
            DebugAdapter.OnDebuggerDisconnect += TeardownRpcDebuggingHook;
        }

        private void MessageLogThreadProc()
        {
            var exitCounter = 0;
            
            while (exitCounter < 2)
            {
                if (_shuttingDown)
                {
                    exitCounter += 1;
                }
                
                var result = Globals.MessageLog.TryTake(
                    out var line,
                    new TimeSpan(0, 0, 1));
                
                if (!result)
                {
                    continue;
                }
                
                DebugAdapter.Protocol.SendEvent(new OutputEvent { Output = line, Category = OutputEvent.CategoryValue.Stdout });
            }
        }

        private void SetupDebugEnvironment()
        {
            if (Globals.CurrentProject == null || string.IsNullOrEmpty(Globals.CurrentContractName))
            {
                return;
            }
            
            // run processes
            if (Globals.CurrentContractJson.PreDebugSteps != null)
            {
                foreach (var stepConfig in Globals.CurrentContractJson.PreDebugSteps)
                {
                    try
                    {
                        var p = new SetupProcess(stepConfig);
                        p.Run();
                        _setupProcesses.Add(p);
                    }
                    catch
                    {
                        Thread.Sleep(500);
                        throw;
                    }
                }
            }
            
            // fork config
            if (Globals.CurrentContractJson.ForkConfig != null &&
                Globals.CurrentContractJson.ForkConfig.Enabled &&
                !string.IsNullOrEmpty(Globals.CurrentContractJson.ForkConfig.Url))
            {
                Globals.ForkUrl = Globals.CurrentContractJson.ForkConfig.Url;
                Globals.ForkBlockNumber = (ulong)Globals.CurrentContractJson.ForkConfig.BlockNumber;
                Globals.ForkEnabled = true;
                PluginLoader.Default.Svc.Log(
                    $"Debugger will fork to '{Globals.ForkUrl}' at block '{(Globals.ForkBlockNumber == 0 ? "latest" : Globals.ForkBlockNumber.ToString())}'");
            }
        }
        
        private void InitializeDebugConnection()
        {
            // Connect IPC stream to debug adapter handler.
            DebugAdapter.InitializeStream(_debuggerTransport.InputStream, _debuggerTransport.OutputStream);

            // Starts the debug protocol dispatcher background thread.
            DebugAdapter.Protocol.Run();

            // Forward message log to debugger output
            _messageLogThread = new Thread(MessageLogThreadProc);
            _messageLogThread.Start();
            
            // Run setup processes
            SetupDebugEnvironment();
            
            // Wait until the debug protocol handshake has completed.
            DebugAdapter.CompletedConfigurationDoneRequest.Task.Wait();

            DebugAdapter.Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Breakpoint) { ThreadId = 1 });
        }

        public void SetupRpcDebuggingHook()
        {
            // Set our method to execute for our hook.
            JsonRpcClient.JsonRpcExecutionAnalysis = RpcExecutionCallback;
        }
        
        public void TeardownRpcDebuggingHook()
        {
            // Teardown our hook by setting the target as null.
            TeardownRpcDebuggingHook(null);
        }

        private void TeardownRpcDebuggingHook(MeadowSolidityDebugAdapter debugAdapter)
        {
            // Teardown our hook by setting the target as null.
            JsonRpcClient.JsonRpcExecutionAnalysis = null;
        }

        private void DebugAdapter_OnDebuggerDisconnect(MeadowSolidityDebugAdapter sender)
        {
            TeardownRpcDebuggingHook(sender);
            OnDebuggerDisconnect?.Invoke();
        }


        public async Task RpcExecutionCallback(IJsonRpcClient client, bool expectingException)
        {
            // Obtain an execution trace from our client.
            var executionTrace = await client.GetExecutionTrace();
            if (executionTrace == null)
            {
                throw new Exception("Failed to create execution trace. If you are forking mainnet, check that your node is running.");
            }
            
            var executionTraceAnalysis = new ExecutionTraceAnalysis(executionTrace);

            // Process our execution trace in the debug adapter.
            await DebugAdapter.ProcessExecutionTraceAnalysis(client, executionTraceAnalysis, expectingException);

            await Task.CompletedTask;
        }

        public void Dispose()
        {
            foreach (var p in _setupProcesses)
            {
                try
                {
                    p.Dispose();
                }
                catch
                {
                    // ignored
                }
            }
            
            _shuttingDown = true;
            _messageLogThread?.Join();

            if (DebugAdapter.Protocol?.IsRunning == true)
            {
                // Cleanly close down debugging
                DebugAdapter.SendTerminateAndExit();
                DebugAdapter.Protocol.Stop(2000);
                DebugAdapter.Protocol.WaitForReader();
            }

            _debuggerTransport.Dispose();
        }
    }
}
