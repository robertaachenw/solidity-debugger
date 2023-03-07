using System;
using System.Buffers.Text;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Meadow.CoverageReport.Debugging;
using Meadow.CoverageReport.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using Meadow.CoverageReport.Debugging.Variables;
using Meadow.JsonRpc.Types.Debugging;
using Meadow.CoverageReport.Debugging.Variables.Enums;
using System.Globalization;
using System.Runtime.InteropServices;
using Meadow.CoverageReport.Debugging.Variables.UnderlyingTypes;
using Meadow.CoverageReport.Debugging.Variables.Pairing;
using Meadow.JsonRpc.Client;
using System.Text;
using Meadow.Core.Utils;
using Meadow.Plugin;
using Meadow.Shared;
using SolcNet;
using StackFrame = Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.StackFrame;


namespace Meadow.DebugAdapterServer
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class MeadowSolidityDebugAdapter : DebugAdapterBase
    {
        private bool _protocolTrace = false;
        
        private enum EvmAddressSpaces
        {
            Memory,
            Stack,
            CallData,
        }
        
        class ExampleCustomRequestWithResponse : DebugRequestWithResponse<ExampleRequestArgs, ExampleRequestResponse>
        {
            public ExampleCustomRequestWithResponse() : base("customRequestExample")
            {
            }
        }

        class ExampleRequestArgs
        {
            public string SessionID { get; set; }
        }

        class ExampleRequestResponse : ResponseBody
        {
            public string Response { get; set; }
        }


        //#region Constants
        private string _contractsDirectory = "Contracts";
        private const int SIMULTANEOUS_TRACE_COUNT = 1;
        //#endregion

        //#region Fields
        readonly TaskCompletionSource<object> _terminatedTcs = new TaskCompletionSource<object>();

        public readonly TaskCompletionSource<object> CompletedInitializationRequest = new TaskCompletionSource<object>();
        public readonly TaskCompletionSource<object> CompletedLaunchRequest = new TaskCompletionSource<object>();
        public readonly TaskCompletionSource<object> CompletedConfigurationDoneRequest = new TaskCompletionSource<object>();
        private System.Threading.SemaphoreSlim _processTraceSemaphore = new System.Threading.SemaphoreSlim(SIMULTANEOUS_TRACE_COUNT, SIMULTANEOUS_TRACE_COUNT);

        readonly ConcurrentDictionary<string, int[]> _sourceBreakpoints;

        readonly bool _useContractsSubDir;

        private readonly HashSet<string> _sourceDirs = new HashSet<string>();
        //#endregion

        //#region Properties
        public Task Terminated => _terminatedTcs.Task;
        public SolidityMeadowConfigurationProperties ConfigurationProperties { get; private set; }
        public ConcurrentDictionary<int, MeadowDebugAdapterThreadState> ThreadStates { get; }
        public ReferenceContainer ReferenceContainer;
        public bool Exiting { get; private set; }
        //#endregion

        //#region Events
        public delegate void ExitingEventHandler(MeadowSolidityDebugAdapter sender);
        public event ExitingEventHandler OnDebuggerDisconnect;
        //#endregion

        const string EXCEPTION_BREAKPOINT_FILTER_ALL = "All Exceptions";
        const string EXCEPTION_BREAKPOINT_FILTER_UNHANDLED = "Unhandled Exceptions";

        HashSet<string> _exceptionBreakpointFilters = new HashSet<string>();
        int _threadIDCounter = 0;
        private int _breakPointsHit = 0;

        //#region Constructor
        public MeadowSolidityDebugAdapter(bool useContractsSubDir)
        {
            _sourceBreakpoints = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 
                new ConcurrentDictionary<string, int[]>(StringComparer.OrdinalIgnoreCase) : 
                new ConcurrentDictionary<string, int[]>();
            
            _useContractsSubDir = useContractsSubDir;

            // Initialize our thread state lookup.
            ThreadStates = new ConcurrentDictionary<int, MeadowDebugAdapterThreadState>();

            // Initialize our reference collection
            ReferenceContainer = new ReferenceContainer();
        }
        //#endregion

        //#region Functions
        public void InitializeStream(Stream input, Stream output)
        {
            InitializeProtocolClient(input, output);

            Protocol.RegisterRequestType<ExampleCustomRequestWithResponse, ExampleRequestArgs, ExampleRequestResponse>(r =>
            {
                r.SetResponse(new ExampleRequestResponse { Response = "good" });
            });
        }

        public void SendProblemMessage(string message, string exception)
        {
            var msg = $"{message} => {exception}";
            Protocol.SendEvent(new OutputEvent { Output = msg, Category = OutputEvent.CategoryValue.Stderr });
            Protocol.SendEvent(new ProblemDebugEvent { Message = msg, Exception = exception });
        }

        public async Task ProcessExecutionTraceAnalysis(IJsonRpcClient rpcClient, ExecutionTraceAnalysis traceAnalysis, bool expectingException)
        {
            // We don't have real threads here, only a unique execution context
            // per RPC callback (eth_call or eth_sendTransactions).
            // So just use a rolling ID for threads.
            var threadId = System.Threading.Interlocked.Increment(ref _threadIDCounter);

            // Create a thread state for this thread
            var threadState = new MeadowDebugAdapterThreadState(rpcClient, traceAnalysis, threadId, expectingException);

            // Acquire the semaphore for processing a trace.
            await _processTraceSemaphore.WaitAsync();

            // Set the thread state in our lookup
            ThreadStates[threadId] = threadState;

            // If we're not exiting, step through our 
            if (!Exiting)
            {
                // Send an event that our thread has exited.
                Protocol.SendEvent(new ThreadEvent(ThreadEvent.ReasonValue.Started, threadState.ThreadId));

                // Continue our execution.
                ContinueExecution(threadState, DesiredControlFlow.Continue);

                // Lock execution is complete.
                await threadState.Semaphore.WaitAsync();

                // Send an event that our thread has exited.
                Protocol.SendEvent(new ThreadEvent(ThreadEvent.ReasonValue.Exited, threadState.ThreadId));
            }

            // Remove the thread from our lookup.
            ThreadStates.Remove(threadId, out _);

            // Unlink our data for our thread id.
            ReferenceContainer.UnlinkThreadId(threadId);

            // Release the semaphore for processing a trace.
            _processTraceSemaphore.Release();
        }

        private void ContinueExecution(
            MeadowDebugAdapterThreadState threadState, 
            DesiredControlFlow controlFlowAction = DesiredControlFlow.Continue, 
            int stepsPriorToAction = 0)
        {
            // Unlink our data for our thread id.
            ReferenceContainer.UnlinkThreadId(threadState.ThreadId);

            // Create a variable to track if we have finished stepping through the execution.
            var finishedExecution = false;

            // Determine the direction to take steps prior to any evaluation.
            if (stepsPriorToAction >= 0)
            {
                // Loop for each step to take forward.
                for (var i = 0; i < stepsPriorToAction; i++)
                {
                    // Take a step forward, if we could not, we finished execution, so we can stop looping.
                    if (!threadState.IncrementStep())
                    {
                        finishedExecution = true;
                        break;
                    }
                }
            }
            else
            {
                // Loop for each step to take backward
                for (var i = 0; i > stepsPriorToAction; i--)
                {
                    // Take a step backward, if we could not, we can stop early as we won't be able to step backwards anymore.
                    if (!threadState.DecrementStep())
                    {
                        break;
                    }
                }
            }

            // If we haven't finished execution, 
            if (!finishedExecution)
            {
                // Determine how to handle our desired control flow.
                switch (controlFlowAction)
                {
                    case DesiredControlFlow.Continue:
                        {
                            // Process the execution trace analysis
                            while (threadState.CurrentStepIndex.HasValue && !Exiting)
                            {
                                // If we encountered an event at this point, stop
                                if (EvaluateCurrentStep(threadState))
                                {
                                    return;
                                }

                                // If we couldn't step forward, this trace has been fully processed.
                                if (!threadState.IncrementStep())
                                {
                                    break;
                                }
                            }

                            // If we exited this way, our execution has concluded because we could not step any further (or there were never any steps).
                            finishedExecution = true;
                            break;
                        }

                    case DesiredControlFlow.StepBackwards:
                        {
                            // Decrement our step
                            bool decrementedStep = threadState.DecrementStep();

                            // If we stepped successfully, we evaluate, and if an event is encountered, we stop.
                            if (decrementedStep && EvaluateCurrentStep(threadState))
                            {
                                return;
                            }

                            // Send our continued event to force refresh of state.
                            Protocol.SendEvent(new ContinuedEvent(threadState.ThreadId));

                            // Signal our breakpoint event has occurred for this thread.
                            Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Step) { ThreadId = threadState.ThreadId });
                            break;
                        }

                    case DesiredControlFlow.StepInto:
                    case DesiredControlFlow.StepOver:
                    case DesiredControlFlow.StepOut:
                        {
                            // Here we handle Step Into + Step Over + Step Out
                            // The following conditions need to be checked:
                            //      (1) We step once unconditionally
                            //      (2) Each step should be evaluated for breakpoints/exceptions and checked to see if execution ended.
                            //      (3) If step into: break instantly.
                            //      (4) If step out: break if current scope is the parent of the initial scope.
                            //      (5) If step over: break if the current scope is the same OR the parent of the initial scope.

                            // Define our initial scope
                            ExecutionTraceScope initialTraceScope = null;
                            SourceFileLine[] initialSourceLines = Array.Empty<SourceFileLine>();
                            // Obtain the initial scope if we're stepping over or out (it will be needed for comparisons later).
                            if (threadState.CurrentStepIndex.HasValue && controlFlowAction != DesiredControlFlow.StepInto)
                            {
                                initialTraceScope = threadState.ExecutionTraceAnalysis.GetScope(threadState.CurrentStepIndex.Value);
                                initialSourceLines = threadState.ExecutionTraceAnalysis.GetSourceLines(threadState.CurrentStepIndex.Value);
                            }

                            // Loop for through all the steps.
                            do
                            {
                                // Increment our step
                                finishedExecution = !threadState.IncrementStep();

                                if (finishedExecution)
                                {
                                    Environment.Exit(0);
                                }
                                
                                // If we stepped successfully, we evaluate, and if an event is encountered, we stop.
                                if (!finishedExecution && EvaluateCurrentStep(threadState))
                                {
                                    return;
                                }

                                // If this isn't a basic single step (step into), we'll want to determine if we want to step more, or halt.
                                if (controlFlowAction != DesiredControlFlow.StepInto)
                                {
                                    // Obtain our current execution trace point.
                                    ExecutionTraceScope currentTraceScope = threadState.ExecutionTraceAnalysis.GetScope(threadState.CurrentStepIndex.Value);

                                    // Check if we reached our target, if not, we iterate to step some more.
                                    var currentScopeIsParent = currentTraceScope == initialTraceScope.Parent;
                                    var currentScopeIsSame = currentTraceScope == initialTraceScope;
                                    if (controlFlowAction == DesiredControlFlow.StepOver)
                                    {
                                        // We're handling a step over.

                                        // If this isn't the same scope or parent, we keep step some more.
                                        if (!currentScopeIsParent && !currentScopeIsSame)
                                        {
                                            continue;
                                        }
                                        else if (currentScopeIsSame)
                                        {
                                            // If the scope is the same, we verify the line is different (since entering/exiting a function will create two steps at the same point, we skip the latter point).
                                            SourceFileLine[] currentSourceLines = threadState.ExecutionTraceAnalysis.GetSourceLines(threadState.CurrentStepIndex.Value);
                                            if (currentSourceLines?.Length == 0 ||
                                                (initialSourceLines.Length >= 1 &&
                                                currentSourceLines[0].LineNumber == initialSourceLines[0].LineNumber &&
                                                currentSourceLines[0].SourceFileMapParent?.SourceFileIndex == initialSourceLines[0].SourceFileMapParent?.SourceFileIndex))
                                            {
                                                continue;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // We're handling a step out.

                                        // If this isn't the parent scope, we keep step some more.
                                        if (!currentScopeIsParent)
                                        {
                                            continue;
                                        }
                                    }
                                }

                                // Send our continued event to force refresh of state.
                                Protocol.SendEvent(new ContinuedEvent(threadState.ThreadId));

                                // Signal our breakpoint event has occurred for this thread.
                                Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Step) { ThreadId = threadState.ThreadId });
                                break;
                            }
                            // Loop while our execution hasn't finioshed.
                            while (!finishedExecution);

                            break;
                        }
            }
            }

            // If we finished execution, signal our thread
            if (finishedExecution)
            {
                threadState.Semaphore.Release();
            }
        }

        private bool EvaluateCurrentStep(MeadowDebugAdapterThreadState threadState, bool exceptions = true, bool breakpoints = true)
        {
            // Evaluate exceptions and breakpoints at this point in execution.
            return (exceptions && HandleExceptions(threadState)) || 
                   (breakpoints && HandleBreakpoint(threadState));
        }

        private bool HandleExceptions(MeadowDebugAdapterThreadState threadState)
        {
            bool ShouldProcessException()
            {
                lock (_exceptionBreakpointFilters)
                {
                    if (_exceptionBreakpointFilters.Contains(EXCEPTION_BREAKPOINT_FILTER_ALL))
                    {
                        return true;
                    }

                    if (!threadState.ExpectingException && _exceptionBreakpointFilters.Contains(EXCEPTION_BREAKPOINT_FILTER_UNHANDLED))
                    {
                        return true;
                    }

                    return false;
                }
            }

            // Try to obtain an exception at this point
            if (ShouldProcessException() && threadState.CurrentStepIndex.HasValue)
            {
                // Obtain an exception at the current point.
                ExecutionTraceException traceException = threadState.ExecutionTraceAnalysis.GetException(threadState.CurrentStepIndex.Value);

                // If we have an exception, throw it and return the appropriate status.
                if (traceException != null)
                {
                    // Send our continued event to force refresh of state.
                    Protocol.SendEvent(new ContinuedEvent(threadState.ThreadId));

                    // Send our exception event.
                    var stoppedEvent = new StoppedEvent(StoppedEvent.ReasonValue.Exception)
                    {
                        Text = traceException.Message,
                        ThreadId = threadState.ThreadId
                    };
                    Protocol.SendEvent(stoppedEvent);
                    return true;
                }
            }

            // We did not find an exception here, return false
            return false;
        }

        private bool FindBreakpoint(string sourceFilePath, out int[] breakpointLines)
        {
            if (_sourceBreakpoints.TryGetValue(sourceFilePath, out breakpointLines))
            {
                return true;
            }

            var sourceFilePathUnix = "/" + sourceFilePath.Replace("\\", "/", StringComparison.Ordinal);
            var sourceFilePathWin = "\\" + sourceFilePath.Replace("/", "\\", StringComparison.Ordinal);
            
            foreach (var bpFilePath in _sourceBreakpoints.Keys)
            {
                if (bpFilePath.EndsWith(sourceFilePathUnix, StringComparison.Ordinal) ||
                    bpFilePath.EndsWith(sourceFilePathWin, StringComparison.Ordinal))
                {
                    breakpointLines = _sourceBreakpoints[bpFilePath];
                    return true;
                }
            }
            
            return false;
        }

        private bool HandleBreakpoint(MeadowDebugAdapterThreadState threadState)
        {
            // Verify we have a valid step at this point.
            if (!threadState.CurrentStepIndex.HasValue)
            {
                return false;
            }

            // Loop through all the source lines at this step.
            var sourceLines = threadState.ExecutionTraceAnalysis.GetSourceLines(threadState.CurrentStepIndex.Value);
            foreach (var sourceLine in sourceLines)
            {
                // Verify our source path.
                var sourceFilePath = sourceLine.SourceFileMapParent?.SourceFilePath;

                // Resolve relative path properly so it can simply be looked up.
                var success = FindBreakpoint(sourceFilePath, out var breakpointLines);

                // If we have a breakpoint at this line number..
                bool containsBreakpoint = success && breakpointLines.Any(x => x == sourceLine.LineNumber);

                if (Globals.CurrentProject != null &&
                    Globals.CurrentProject.ProjectJson.BreakOnEntry &&
                    _breakPointsHit == 0)
                {
                    PluginLoader.Default.Svc.Log("Break on entry triggered");
                    containsBreakpoint = true;
                }
                
                if (containsBreakpoint)
                {
                    _breakPointsHit++;
                    
                    // Send our continued event to force refresh of state.
                    Protocol.SendEvent(new ContinuedEvent(threadState.ThreadId));

                    // Signal our breakpoint event has occurred for this thread.
                    Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Breakpoint) { ThreadId = threadState.ThreadId });

                    PluginLoader.Default.Svc.Log($"Breakpoint hit on step {threadState.CurrentStepIndex}");
                    
                    return true;
                }
            }

            return false;
        }
        
        

        protected override void HandleInitializeRequestAsync(IRequestResponder<InitializeArguments, InitializeResponse> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] Initialize Request");
            }
            
            var response = new InitializeResponse
            {
                SupportsConfigurationDoneRequest = true,
                SupportsEvaluateForHovers = true,
                SupportsStepBack = true,
                SupportsStepInTargetsRequest = true,
                SupportTerminateDebuggee = false,
                SupportsRestartRequest = false,
                SupportsRestartFrame = false,
                SupportedChecksumAlgorithms = new List<ChecksumAlgorithm> { ChecksumAlgorithm.SHA256 },
                SupportsExceptionInfoRequest = true,
                SupportsReadMemoryRequest = true,
            };

            response.ExceptionBreakpointFilters = new List<ExceptionBreakpointsFilter>
            {
                new ExceptionBreakpointsFilter
                {
                    Filter = EXCEPTION_BREAKPOINT_FILTER_ALL,
                    Label = EXCEPTION_BREAKPOINT_FILTER_ALL,
                    Default = false
                },
                new ExceptionBreakpointsFilter
                {
                    Filter = EXCEPTION_BREAKPOINT_FILTER_UNHANDLED,
                    Label = EXCEPTION_BREAKPOINT_FILTER_UNHANDLED,
                    Default = true
                }
            };

            responder.SetResponse(response);
            Protocol.SendEvent(new InitializedEvent());
            CompletedInitializationRequest.SetResult(null);
        }


        private void WriteOutputWindow(string line, bool isError = false)
        {
            Protocol.SendEvent(new OutputEvent { Output = line + "\n", Category = isError ? OutputEvent.CategoryValue.Stderr : OutputEvent.CategoryValue.Console });
        }
        
        
        protected override void HandleReadMemoryRequestAsync(IRequestResponder<ReadMemoryArguments, ReadMemoryResponse> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] ReadMemory Request");
            }
            
            var reply = new ReadMemoryResponse
            {
                Address = "0x0",
                Data = Convert.ToBase64String(new byte[] { (byte)'E', (byte)'R', (byte)'R' })
            };

            try
            {
                if (ThreadStates.Count > 0)
                {
                    var threadState = ThreadStates.Values.ToArray()[0];
                    var i = (int)threadState.CurrentStepIndex;
                    var tracePoint = threadState.ExecutionTrace.Tracepoints[i];

                    using (var memoryStream = new MemoryStream())
                    {
                        // read entire address space
                        if (responder.Arguments.MemoryReference.Equals(EvmAddressSpaces.Memory.ToString(), StringComparison.Ordinal))
                        {
                            for (var j = 0; j < tracePoint.Memory.Length; ++j)
                            {
                                memoryStream.Write(tracePoint.Memory[j].GetBytes());
                            }
                        }
                        else if (responder.Arguments.MemoryReference.Equals(EvmAddressSpaces.Stack.ToString(), StringComparison.Ordinal))
                        {
                            for (var j = 0; j < tracePoint.Stack.Length; ++j)
                            {
                                memoryStream.Write(tracePoint.Stack[j].GetBytes());
                            }
                        }
                        else if (responder.Arguments.MemoryReference.Equals(EvmAddressSpaces.CallData.ToString(), StringComparison.Ordinal))
                        {
                            if (tracePoint.CallData != null)
                            {
                                memoryStream.Write(tracePoint.CallData);
                                WriteOutputWindow($"calldata = {tracePoint.CallData.ToHexString(hexPrefix: true)}");
                            }
                        }

                        // slice requested portion
                        memoryStream.Position = 0;
                        var mem = memoryStream.ToArray();
                        
                        if (responder.Arguments.Offset != null)
                        {
                            var off = Math.Max(0, Math.Min((int)responder.Arguments.Offset, mem.Length));
                            mem = mem.Length > off ? mem.Slice(off, mem.Length - off) : Array.Empty<byte>();
                        }
                        
                        reply.Data = Convert.ToBase64String(mem);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteOutputWindow(ex.ToString(), true);
            }
            
            responder.SetResponse(reply);
        }

        protected override void HandleSetExceptionBreakpointsRequestAsync(IRequestResponder<SetExceptionBreakpointsArguments> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] ExceptionBreakpoints Request");
            }
            
            lock (_exceptionBreakpointFilters)
            {
                _exceptionBreakpointFilters = new HashSet<string>(responder.Arguments.Filters);
                var response = new SetExceptionBreakpointsResponse();
                responder.SetResponse(response);
            }
        }

        protected override void HandleAttachRequestAsync(IRequestResponder<AttachArguments> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] Attach Request");
            }
            
            SetupConfigurationProperties(responder.Arguments.ConfigurationProperties);
            responder.SetResponse(new AttachResponse());
            CompletedLaunchRequest.SetResult(null);
        }

        protected override void HandleLaunchRequestAsync(IRequestResponder<LaunchArguments> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] Launch Request");
            }
            
            SetupConfigurationProperties(responder.Arguments.ConfigurationProperties);
            responder.SetResponse(new LaunchResponse());
            CompletedLaunchRequest.SetResult(null);
        }

        private void AddSourceDir(string dirPath)
        {
            if (string.IsNullOrEmpty(dirPath))
            {
                return;
            }

            if (!Directory.Exists(dirPath))
            {
                return;
            }
            
            _sourceDirs.Add(dirPath);

            var absolutePath = Path.GetFullPath(dirPath);
            if (absolutePath != dirPath)
            {
                _sourceDirs.Add(absolutePath);
            }

            var nodeModules = new List<string>();
            nodeModules.Add(Path.Combine(dirPath, "node_modules"));
            nodeModules.Add(Path.Combine(dirPath, "..", "node_modules"));

            foreach (var nodePath in nodeModules)
            {
                if (Directory.Exists(nodePath))
                {
                    _sourceDirs.Add(Path.GetFullPath(nodePath));
                }
            }
        }
        
        void SetupConfigurationProperties(Dictionary<string, JToken> configProperties)
        {
            ConfigurationProperties = JObject.FromObject(configProperties).ToObject<SolidityMeadowConfigurationProperties>();

            if (string.IsNullOrEmpty(ConfigurationProperties.WorkspaceDirectory) &&
                !string.IsNullOrEmpty(ConfigurationProperties.Program) &&
                File.Exists(ConfigurationProperties.Program))
            {
                var dirPath = Path.GetDirectoryName(ConfigurationProperties.Program);
                if (!string.IsNullOrEmpty(dirPath))
                {
                    AddSourceDir(dirPath);
                }
            }
            else if (!string.IsNullOrEmpty(ConfigurationProperties.WorkspaceDirectory) &&
                     Directory.Exists(ConfigurationProperties.WorkspaceDirectory))
            {
                AddSourceDir(ConfigurationProperties.WorkspaceDirectory);
            }

            if (_useContractsSubDir)
            {
                var dirEntries = Directory.GetDirectories(ConfigurationProperties.WorkspaceDirectory, "*contracts", SearchOption.TopDirectoryOnly);
                var contractsDir = dirEntries.FirstOrDefault(d => Path.GetFileName(d).Equals("contracts", StringComparison.OrdinalIgnoreCase));
                if (contractsDir == null)
                {
                    throw new Exception("No contracts directory found");
                }

                _contractsDirectory = Path.GetFileName(contractsDir);
            }
        }

        protected override void HandleConfigurationDoneRequestAsync(IRequestResponder<ConfigurationDoneArguments> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] ConfigurationDone Request");
            }
            
            responder.SetResponse(new ConfigurationDoneResponse());
            CompletedConfigurationDoneRequest.SetResult(null);
        }

        protected override void HandleDisconnectRequestAsync(IRequestResponder<DisconnectArguments> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] Disconnect Request");
            }
            
            // Set our exiting status
            Exiting = true;

            // Loop for each thread
            foreach (var threadStateKeyValuePair in ThreadStates)
            {
                // Release the execution lock on this thread state.
                // NOTE: This already happens when setting exiting status,
                // but only if the thread is currently stepping/continuing,
                // and not paused. This will allow it to continue if stopped.
                threadStateKeyValuePair.Value.Semaphore.Release();
            }

            // Set our response to the disconnect request.
            responder.SetResponse(new DisconnectResponse());
            Protocol.SendEvent(new TerminatedEvent());
            Protocol.SendEvent(new ExitedEvent(0));
            _terminatedTcs.TrySetResult(null);

            // If we have an event, invoke it
            OnDebuggerDisconnect?.Invoke(this);
        }

        public void SendTerminateAndExit()
        {
            Protocol.SendEvent(new TerminatedEvent());
            Protocol.SendEvent(new ExitedEvent(0));
        }

        //#region Step, Continue, Pause

        protected override void HandleStepInRequestAsync(IRequestResponder<StepInArguments> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] StepIn Request");
            }
            
            // Set our response
            responder.SetResponse(new StepInResponse());

            // Obtain the current thread state
            bool success = ThreadStates.TryGetValue(responder.Arguments.ThreadId, out var threadState);
            if (success)
            {
                // Continue executing
                ContinueExecution(threadState, DesiredControlFlow.StepInto);
            }
        }

        protected override void HandleNextRequestAsync(IRequestResponder<NextArguments> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] Next Request");
            }
            
            // Set our response
            responder.SetResponse(new NextResponse());

            // Obtain the current thread state
            bool success = ThreadStates.TryGetValue(responder.Arguments.ThreadId, out var threadState);
            if (success)
            {
                // Continue executing
                ContinueExecution(threadState, DesiredControlFlow.StepOver);
            }
        }


        protected override void HandleStepInTargetsRequestAsync(IRequestResponder<StepInTargetsArguments, StepInTargetsResponse> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] StepInTargets Request");
            }
            
            responder.SetResponse(new StepInTargetsResponse());
        }

        protected override void HandleStepOutRequestAsync(IRequestResponder<StepOutArguments> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] StepOut Request");
            }
            
            // Set our response
            responder.SetResponse(new StepOutResponse());

            // Obtain the current thread state
            bool success = ThreadStates.TryGetValue(responder.Arguments.ThreadId, out var threadState);
            if (success)
            {
                // Continue executing
                ContinueExecution(threadState, DesiredControlFlow.StepOut);
            }
        }

        protected override void HandleStepBackRequestAsync(IRequestResponder<StepBackArguments> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] StepBack Request");
            }
            
            // Set our response
            responder.SetResponse(new StepBackResponse());

            // Obtain the current thread state
            bool success = ThreadStates.TryGetValue(responder.Arguments.ThreadId, out var threadState);
            if (success)
            {
                // Continue executing
                ContinueExecution(threadState, DesiredControlFlow.StepBackwards);
            }
        }

        protected override void HandleContinueRequestAsync(IRequestResponder<ContinueArguments, ContinueResponse> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] Continue Request");
            }
            
            // Set our response
            responder.SetResponse(new ContinueResponse());

            // Obtain the current thread state
            bool success = ThreadStates.TryGetValue(responder.Arguments.ThreadId, out var threadState);
            if (success)
            {
                // Continue executing, taking one step before continuing, as evaluation occurs before steps occur, and we want
                // to ensure we advanced position from our last and don't re-evaluate the same trace point. We only do this on
                // startup since we want the initial trace point to be evaluated. After that, we want to force advancement by
                // at least one step before continuation/re-evaluation.
                ContinueExecution(threadState, DesiredControlFlow.Continue, 1);
            }
        }

        protected override void HandleReverseContinueRequestAsync(IRequestResponder<ReverseContinueArguments> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] ReverseContinue Request");
            }
            
            responder.SetResponse(new ReverseContinueResponse());
        }

        protected override void HandlePauseRequestAsync(IRequestResponder<PauseArguments> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] Pause Request");
            }
            
            responder.SetResponse(new PauseResponse());
        }
        //#endregion


        //#region Threads, Scopes, Variables Requests

        protected override void HandleThreadsRequestAsync(IRequestResponder<ThreadsArguments, ThreadsResponse> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] Threads Request");
            }
            
            // Create a list of threads from our thread states.
            List<Thread> threads = ThreadStates.Values.Select(x => new Thread(x.ThreadId, $"thread_{x.ThreadId}")).ToList();
            responder.SetResponse(new ThreadsResponse(threads));
        }

        protected override void HandleStackTraceRequestAsync(IRequestResponder<StackTraceArguments, StackTraceResponse> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] StackTrace Request");
            }
            
            // Create a list of stack frames or try to get cached ones.
            var cachedStackFrames = ReferenceContainer.TryGetStackFrames(
                responder.Arguments.ThreadId, 
                out var stackFrames);

            // Verify we have a thread state for this thread, and a valid step to represent in it.
            if (!cachedStackFrames && 
                ThreadStates.TryGetValue(responder.Arguments.ThreadId, out var threadState) && 
                threadState.CurrentStepIndex.HasValue)
            {
                // Initialize our stack frame list
                stackFrames = new List<StackFrame>();

                // Obtain the callstack
                var callstack = threadState.ExecutionTraceAnalysis.GetCallStack(threadState.CurrentStepIndex.Value);

                
                
                // Loop through our scopes.
                for (var i = 0; i < callstack.Length; i++)
                {
                    // Grab our current call frame
                    var currentStackFrame = callstack[i];

                    // If the scope could not be resolved within a function, and no lines could be resolved, skip to the next frame.
                    // as this is not a code section we can describe in any meaningful way.
                    if (!currentStackFrame.ResolvedFunction && currentStackFrame.CurrentPositionLines.Length == 0)
                    {
                         continue;
                    }

                    // If we couldn't resolve the current position or there were no lines representing it
                    if (currentStackFrame.Error || currentStackFrame.CurrentPositionLines.Length == 0)
                    {
                        continue;
                    }

                    // Obtain the method name we are executing in.
                    var frameName = currentStackFrame.FunctionName;
                    if (string.IsNullOrEmpty(frameName))
                    {
                        frameName = "<undefined>";
                    }

                    // Determine the bounds of our stack frame.
                    int startLine = 0;
                    int startColumn = 1;
                    int endLine = 0;
                    int endColumn = 0;

                    // Loop through all of our lines for this position.
                    for (int x = 0; x < currentStackFrame.CurrentPositionLines.Length; x++)
                    {
                        // Obtain our indexed line.
                        SourceFileLine line = currentStackFrame.CurrentPositionLines[x];

                        // Set our start position if relevant.
                        if (x == 0 || line.LineNumber <= startLine)
                        {
                            // Set our starting line number.
                            startLine = line.LineNumber;

                            // TODO: Determine our column start
                        }

                        // Set our end position if relevant.
                        if (x == 0 || line.LineNumber >= endLine)
                        {
                            // Set our ending line number.
                            endLine = line.LineNumber;

                            // TODO: Determine our column
                            endColumn = line.Length;
                        }
                    }

                    // Format agnostic path to platform specific path
                    var sourceFilePath = currentStackFrame.CurrentPositionLines[0].SourceFileMapParent.SourceFilePath;
                    if (Path.DirectorySeparatorChar == '\\')
                    {
                        sourceFilePath = sourceFilePath.Replace('/', Path.DirectorySeparatorChar);
                    }

                    string sourcePath;
                    if (Path.IsPathRooted(sourceFilePath))
                    {
                        sourcePath = sourceFilePath;
                    }
                    else if (_useContractsSubDir)
                    {
                        sourcePath = Path.Join(ConfigurationProperties.WorkspaceDirectory, _contractsDirectory, sourceFilePath);
                    }
                    else
                    {
                        sourcePath = Path.Join(ConfigurationProperties.WorkspaceDirectory, sourceFilePath);
                        if (!File.Exists(sourcePath))
                        {
                            foreach (var sourceDir in _sourceDirs)
                            {
                                var filePath = Path.Join(sourceDir, sourceFilePath);
                                if (File.Exists(filePath))
                                {
                                    sourcePath = filePath;
                                    break;
                                }
                            }
                        }
                    }

                    // Console.WriteLine(threadState.ExecutionTrace);
                    // PluginLoader.Default.Svc.Log($"## '{currentStackFrame.CurrentPositionLines[0].Parent}'");
                    
                    // Create our source object
                    Source stackFrameSource = new Source()
                    {
                        Name = currentStackFrame.CurrentPositionLines[0].SourceFileMapParent.SourceFileName,
                        Path = sourcePath
                    };

                    // Create our stack frame
                    var stackFrame = new StackFrame()
                    {
                        Id = ReferenceContainer.GetStackFrameId(i),
                        Name = frameName,
                        Line = startLine,
                        Column = startColumn,
                        Source = stackFrameSource,
                        EndColumn = endColumn
                    };

                    if (endLine > 0)
                    {
                        stackFrame.EndLine = endLine;
                    }
                    
                    // Add the stack frame to our reference list
                    ReferenceContainer.LinkStackFrame(
                        threadState.ThreadId, 
                        stackFrame, 
                        currentStackFrame.CurrentPositionTraceIndex);
                    
                    // Add our stack frame to the result list
                    stackFrames.Add(stackFrame);
                }

            }

            // Return our stack frames in our response.
            var result = new StackTraceResponse(stackFrames);
            responder.SetResponse(result);
        }

        protected override void HandleScopesRequestAsync(IRequestResponder<ScopesArguments, ScopesResponse> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] Scopes Request");
            }
            
            // Create our scope list
            var scopeList = new List<Scope>();

            // If there is a thread linked
            if (ReferenceContainer.IsThreadLinked)
            {
                // We'll want to set the current stack frame
                ReferenceContainer.SetCurrentStackFrame(responder.Arguments.FrameId);

                // Add the relevant scopes for this stack frame.
                scopeList.Add(new Scope("Local Variables", ReferenceContainer.LocalScopeId, false));
                scopeList.Add(new Scope("State Variables", ReferenceContainer.StateScopeId, false));
            }

            // Set our response.
            var result = new ScopesResponse(scopeList);
            responder.SetResponse(result);
        }

        private bool IsNestedVariableType(VarGenericType genericType)
        {
            // Check the type of variable this is
            return genericType == VarGenericType.Array ||
                genericType == VarGenericType.ByteArrayDynamic ||
                genericType == VarGenericType.ByteArrayFixed ||
                genericType == VarGenericType.Mapping ||
                genericType == VarGenericType.Struct;
        }

        private string GetVariableValueString(UnderlyingVariableValuePair variableValuePair)
        {
            // Determine how to format our value string.
            switch (variableValuePair.Variable.GenericType)
            {
                case VarGenericType.Array:
                    return $"{variableValuePair.Variable.BaseType} (size: {((object[])variableValuePair.Value).Length})";
                case VarGenericType.ByteArrayDynamic:
                    return $"{variableValuePair.Variable.BaseType} (size: {((Memory<byte>)variableValuePair.Value).Length})";
                case VarGenericType.ByteArrayFixed:
                    return variableValuePair.Variable.BaseType;
                case VarGenericType.Mapping:
                    return $"{variableValuePair.Variable.BaseType} (size: {((MappingKeyValuePair[])variableValuePair.Value).Length})";
                case VarGenericType.String:
                    return $"\"{variableValuePair.Value}\"";
                case VarGenericType.Struct:
                    return variableValuePair.Variable.BaseType;
                default:
                    // As a fallback, try to turn the object into a string.
                    return variableValuePair.Value?.ToString() ?? "null";
            }
        }

        protected override void HandleVariablesRequestAsync(IRequestResponder<VariablesArguments, VariablesResponse> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] Variables Request");
            }
            
            // Obtain relevant variable resolving information.
            var isLocalVariableScope = false;
            var isStateVariableScope = false;
            var isParentVariableScope = false;
            var parentVariableValuePair = new UnderlyingVariableValuePair(null, null);

            // Try to obtain the variable reference as a local variable scope.
            isLocalVariableScope = ReferenceContainer.ResolveLocalVariable(
                responder.Arguments.VariablesReference, 
                out var threadId, 
                out var traceIndex);
            
            if (!isLocalVariableScope)
            {
                // Try to obtain the variable reference as a state variable scope.
                isStateVariableScope = ReferenceContainer.ResolveStateVariable(
                    responder.Arguments.VariablesReference, 
                    out threadId, 
                    out traceIndex);
                
                if (!isStateVariableScope)
                {
                    // Try to obtain the variable reference as a sub-variable scope.
                    isParentVariableScope = ReferenceContainer.ResolveParentVariable(
                        responder.Arguments.VariablesReference, 
                        out threadId, 
                        out parentVariableValuePair);
                    
                    if (!isParentVariableScope && responder.Arguments.VariablesReference > (int)SystemGlobals.Base)
                    {
                        ReferenceContainer.ResolveStateVariable(
                            ReferenceContainer.StateScopeId, 
                            out threadId, 
                            out traceIndex);
                    }
                }
            }

            // Using our thread id, obtain our thread state
            ThreadStates.TryGetValue(threadId, out var threadState);

            // Verify the thread state is valid
            if (threadState == null)
            {
                responder.SetResponse(new VariablesResponse());
                return;
            }

            // Obtain the trace index for this scope.
            var variableList = new List<Variable>();
            
            try
            {
                ResolveVariables(
                    variableList, 
                    responder.Arguments.VariablesReference, 
                    isLocalVariableScope, 
                    isStateVariableScope, 
                    isParentVariableScope, 
                    traceIndex,
                    parentVariableValuePair,
                    threadState);
            }
            catch (Exception ex)
            {
                LogException(ex, threadState);
            }

            try
            {
                ResolveSystemGlobals(
                    ref variableList, 
                    responder.Arguments.VariablesReference, 
                    isLocalVariableScope, 
                    isStateVariableScope, 
                    isParentVariableScope, 
                    traceIndex,
                    parentVariableValuePair,
                    threadState);
            }
            catch (Exception ex)
            {
                LogException(ex, threadState);
            }

            // Respond with our variable list.
            responder.SetResponse(new VariablesResponse(variableList));
        }

        private enum SystemGlobals
        {
            Base = 0x7fff0000,
            Msg,
            Tx,
            Block,
            Emit,
            Evm
        }
        
        private void ResolveSystemGlobals(
            ref List<Variable> dapOutput,
            int variablesReference,
            bool isLocalVariableScope, 
            bool isStateVariableScope,
            bool isParentVariableScope,
            int traceIndex,
            UnderlyingVariableValuePair parentVariableValuePair,
            MeadowDebugAdapterThreadState threadState)
        {
            if (isStateVariableScope)
            {
                dapOutput.Add(new Variable("evm", "(size: 3)", (int)SystemGlobals.Evm));

                var eventCount = threadState.ExecutionTraceAnalysis.Events.Keys
                    .Count(i => i <= threadState.CurrentStepIndex);
                dapOutput.Add(new Variable("emit", $"(size: {eventCount})", (int)SystemGlobals.Emit));
                
                dapOutput.Add(new Variable("msg", "(size: 4)", (int)SystemGlobals.Msg));
                dapOutput.Add(new Variable("tx", "(size: 2)", (int)SystemGlobals.Tx));
                dapOutput.Add(new Variable("block", "(size: 5)", (int)SystemGlobals.Block));
                
            }
            else if (!isLocalVariableScope && !isParentVariableScope)
            {
                const string NOT_IMPLEMENTED = "(not implemented)";
                
                switch (variablesReference)
                {
                    case (int)SystemGlobals.Evm:
                        dapOutput.Add(new Variable("Memory", "(view)", 0) { MemoryReference = EvmAddressSpaces.Memory.ToString() });
                        dapOutput.Add(new Variable("CallData", "(view)", 0) { MemoryReference = EvmAddressSpaces.CallData.ToString() });
                        dapOutput.Add(new Variable("Stack", "(view)", 0) { MemoryReference = EvmAddressSpaces.Stack.ToString() });
                        break;
                    
                    case (int)SystemGlobals.Msg:
                        dapOutput.Add(new Variable("sender", NOT_IMPLEMENTED, 0));
                        dapOutput.Add(new Variable("value", NOT_IMPLEMENTED, 0));
                        dapOutput.Add(new Variable("data", NOT_IMPLEMENTED, 0));
                        dapOutput.Add(new Variable("sig", NOT_IMPLEMENTED, 0));
                        break;

                    case (int)SystemGlobals.Tx:
                        dapOutput.Add(new Variable("gasprice", NOT_IMPLEMENTED, 0));
                        dapOutput.Add(new Variable("origin", NOT_IMPLEMENTED, 0));
                        break;
                    
                    case (int)SystemGlobals.Block:
                        dapOutput.Add(new Variable("coinbase", NOT_IMPLEMENTED, 0));
                        dapOutput.Add(new Variable("difficulty", NOT_IMPLEMENTED, 0));
                        dapOutput.Add(new Variable("gaslimit", NOT_IMPLEMENTED, 0));
                        dapOutput.Add(new Variable("number", NOT_IMPLEMENTED, 0));
                        dapOutput.Add(new Variable("timestamp", NOT_IMPLEMENTED, 0));
                        break;
                        
                    case (int)SystemGlobals.Emit:
                        var eventTraceIndices = threadState.ExecutionTraceAnalysis.Events.Keys
                            .Where(i => i <= threadState.CurrentStepIndex).ToList();
                        eventTraceIndices.Sort();
                        
                        foreach (var eventIndex in eventTraceIndices)
                        {
                            var (eventName, eventArgs) = threadState.ExecutionTraceAnalysis.Events[eventIndex];

                            var args = new List<string>();
                            foreach (var argName in eventArgs.Keys)
                            {
                                var (argType, argValue) = eventArgs[argName];
                                args.Add($"{argType} {argName} = {argValue}");
                            }

                            var eventLine = $"{eventName}({string.Join(',', args)})";
                            dapOutput.Add(new Variable($"[{eventIndex}]", eventLine, 0));
                        }

                        break;
                    
                    default:
                        break;
                }
                if (variablesReference == (int)SystemGlobals.Msg)
                {
                }
            }
        }

        private void ResolveVariables(
            List<Variable> variableList,
            int variablesReference,
            bool isLocalVariableScope, 
            bool isStateVariableScope,
            bool isParentVariableScope,
            int traceIndex,
            UnderlyingVariableValuePair parentVariableValuePair,
            MeadowDebugAdapterThreadState threadState)
        { 
            // Obtain our local variables at this point in execution
            var variablePairs = Array.Empty<VariableValuePair>();
            if (isLocalVariableScope)
            {
                variablePairs = threadState.ExecutionTraceAnalysis.GetLocalVariables(traceIndex, threadState.RpcClient);
            }
            else if (isStateVariableScope)
            {
                variablePairs = threadState.ExecutionTraceAnalysis.GetStateVariables(traceIndex, threadState.RpcClient);
            }
            else if (isParentVariableScope)
            {
                // We're loading sub-variables for a variable.
                switch (parentVariableValuePair.Variable.GenericType)
                {
                    case VarGenericType.Struct:
                        {
                            // Cast our to an enumerable type.
                            variablePairs = ((IEnumerable<VariableValuePair>)parentVariableValuePair.Value).ToArray();
                            break;
                        }

                    case VarGenericType.Array:
                        {
                            // Cast our variable
                            var arrayVariable = ((VarArray)parentVariableValuePair.Variable);

                            // Cast to an object array.
                            var arrayValue = (object[])parentVariableValuePair.Value;

                            // Loop for each element
                            for (int i = 0; i < arrayValue.Length; i++)
                            {
                                // Create an underlying variable value pair for this element
                                var underlyingVariableValuePair = new UnderlyingVariableValuePair(arrayVariable.ElementObject, arrayValue[i]);

                                // Check if this is a nested variable type
                                bool nestedType = IsNestedVariableType(arrayVariable.ElementObject.GenericType);
                                int variablePairReferenceId = 0;
                                if (nestedType)
                                {
                                    // Create a new reference id for this variable if it's a nested type.
                                    variablePairReferenceId = ReferenceContainer.GetUniqueId();

                                    // Link our reference for any nested types.
                                    ReferenceContainer.LinkSubVariableReference(variablesReference, variablePairReferenceId, threadState.ThreadId, underlyingVariableValuePair);
                                }

                                // Obtain the value string for this variable and add it to our list.
                                string variableValueString = GetVariableValueString(underlyingVariableValuePair);
                                Variable variable = ReferenceContainer.CreateVariable($"[{i}]", variableValueString, variablePairReferenceId, underlyingVariableValuePair.Variable.BaseType);
                                variableList.Add(variable);
                            }


                            break;
                        }

                    case VarGenericType.ByteArrayDynamic:
                    case VarGenericType.ByteArrayFixed:
                        {
                            // Cast our to an enumerable type.
                            var bytes = (Memory<byte>)parentVariableValuePair.Value;
                            for (int i = 0; i < bytes.Length; i++)
                            {
                                Variable variable = ReferenceContainer.CreateVariable($"[{i}]", bytes.Span[i].ToString(CultureInfo.InvariantCulture), 0, "byte");
                                variableList.Add(variable);
                            }

                            break;
                        }

                    case VarGenericType.Mapping:
                        {
                            // Obtain our mapping's key-value pairs.
                            var mappingKeyValuePairs = (MappingKeyValuePair[])parentVariableValuePair.Value;
                            variablePairs = new VariableValuePair[mappingKeyValuePairs.Length * 2];

                            // Loop for each key and value pair to add.
                            int variableIndex = 0;
                            for (int i = 0; i < mappingKeyValuePairs.Length; i++)
                            {
                                // Set our key and value in our variable value pair enumeration.
                                variablePairs[variableIndex++] = mappingKeyValuePairs[i].Key;
                                variablePairs[variableIndex++] = mappingKeyValuePairs[i].Value;
                            }

                            break;
                        }
                }
            }

            // Loop for each local variables
            foreach (VariableValuePair variablePair in variablePairs)
            {
                // Create an underlying variable value pair for this pair.
                var underlyingVariableValuePair = new UnderlyingVariableValuePair(variablePair);

                // Check if this is a nested variable type
                bool nestedType = IsNestedVariableType(variablePair.Variable.GenericType);
                int variablePairReferenceId = 0;
                if (nestedType)
                {
                    // Create a new reference id for this variable if it's a nested type.
                    variablePairReferenceId = ReferenceContainer.GetUniqueId();

                    // Link our reference for any nested types.
                    ReferenceContainer.LinkSubVariableReference(variablesReference, variablePairReferenceId, threadState.ThreadId, underlyingVariableValuePair);
                }

                // Obtain the value string for this variable and add it to our list.
                string variableValueString = GetVariableValueString(underlyingVariableValuePair);
                
                Variable variable = ReferenceContainer.CreateVariable(
                    variablePair.Variable.Name, 
                    variableValueString, 
                    variablePairReferenceId, 
                    variablePair.Variable.BaseType);
                
                variableList.Add(variable);
            }
        }

        protected override void HandleEvaluateRequestAsync(IRequestResponder<EvaluateArguments, EvaluateResponse> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("DAP: Evaluate Request");
            }
            
            // Obtain an evaluation for this variable expression.
            EvaluateResponse evalResponse = ReferenceContainer.GetVariableEvaluateResponse(responder.Arguments.Expression);

            // Set the response accordingly.
            responder.SetResponse(evalResponse ?? new EvaluateResponse());
        }

        protected override void HandleExceptionInfoRequestAsync(IRequestResponder<ExceptionInfoArguments, ExceptionInfoResponse> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] ExceptionInfo Request");
            }
            
            // Obtain the current thread state
            bool success = ThreadStates.TryGetValue(responder.Arguments.ThreadId, out var threadState);
            if (success)
            {
                var ex = threadState.ExecutionTraceAnalysis.GetException(threadState.CurrentStepIndex.Value);

                // Get the exception call stack lines.
                var exStackTrace = threadState.ExecutionTraceAnalysis.GetCallStackString(ex.TraceIndex.Value);

                responder.SetResponse(new ExceptionInfoResponse("Error", ExceptionBreakMode.Always)
                {
                    Description = ex.Message,
                    Details = new ExceptionDetails
                    {
                        Message = ex.Message,
                        FormattedDescription = ex.Message,
                        StackTrace = exStackTrace
                    }
                });
            }
        }

        //#endregion


        private string ConvertVSCodePathToInternalPath(string vsCodePath, string workspacePath)
        {
            var relativeFilePath = vsCodePath.Substring(workspacePath.Length + 1);
            string internalPath;

            // Strip our contracts folder from our VS Code Path
            if (_useContractsSubDir)
            {
                int index = relativeFilePath.IndexOf(_contractsDirectory, StringComparison.InvariantCultureIgnoreCase);
                internalPath = relativeFilePath.Trim().Substring(index + _contractsDirectory.Length + 1);
            }
            else
            {
                internalPath = relativeFilePath.Trim();
            }

            // Make path platform agnostic
            internalPath = internalPath.Replace('\\', '/');

            // Return our internal path.
            return internalPath;
        }

        protected override void HandleSetBreakpointsRequestAsync(IRequestResponder<SetBreakpointsArguments, SetBreakpointsResponse> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] SetBreakpoints Request");
            }
            
            // Ignore breakpoints for files that are not solidity sources
            if (!responder.Arguments.Source.Path.EndsWith(".sol", StringComparison.OrdinalIgnoreCase))
            {
                responder.SetResponse(new SetBreakpointsResponse());
                return;
            }

            if (File.Exists(responder.Arguments.Source.Path))
            {
                var dirPath = Path.GetDirectoryName(responder.Arguments.Source.Path);
                if (!string.IsNullOrEmpty(dirPath))
                {
                    AddSourceDir(dirPath);
                }
            }
            
            var sourceFilePath = responder.Arguments.Source.Path;

            if (!string.IsNullOrEmpty(ConfigurationProperties.WorkspaceDirectory) && 
                responder.Arguments.Source.Path.StartsWith(ConfigurationProperties.WorkspaceDirectory, StringComparison.OrdinalIgnoreCase))
            {
                // Obtain our internal path from a vs code path
                sourceFilePath = ConvertVSCodePathToInternalPath(responder.Arguments.Source.Path, ConfigurationProperties.WorkspaceDirectory);
            }
            else
            {
                var pathRoot = Path.GetPathRoot(sourceFilePath);
                if (pathRoot.Length > 0)
                {
                    sourceFilePath = pathRoot.ToLowerInvariant() + sourceFilePath.Substring(pathRoot.Length);
                }

                sourceFilePath = sourceFilePath.Replace('\\', '/');
                
                //throw new Exception($"Unexpected breakpoint source path: {responder.Arguments.Source.Path}, workspace: {ConfigurationProperties.WorkspaceDirectory}");
            }

            if (responder.Arguments.SourceModified.GetValueOrDefault())
            {
                throw new Exception("Debugging modified sources is not supported.");
            }

            _sourceBreakpoints[sourceFilePath] = responder.Arguments.Breakpoints.Select(b => b.Line).ToArray();
            
            responder.SetResponse(new SetBreakpointsResponse());
        }

        protected override void HandleSetDebuggerPropertyRequestAsync(IRequestResponder<SetDebuggerPropertyArguments> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] SetDebuggerProperty Request");
            }
            
            base.HandleSetDebuggerPropertyRequestAsync(responder);
        }

        protected override void HandleLoadedSourcesRequestAsync(IRequestResponder<LoadedSourcesArguments, LoadedSourcesResponse> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] LoadedSources Request");
            }
            
            base.HandleLoadedSourcesRequestAsync(responder);
        }

        void LogException(Exception ex, MeadowDebugAdapterThreadState threadState)
        {
            // Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.ThreadEvent
            var msg = new StringBuilder();
            msg.AppendLine(ex.ToString());
            msg.AppendLine();
            msg.AppendLine("Solidity call stack: ");
            try
            {
                msg.AppendLine(threadState.ExecutionTraceAnalysis.GetCallStackString(threadState.CurrentStepIndex.Value));
            }
            catch (Exception callstackResolveEx)
            {
                msg.AppendLine("Exception resolving callstack: " + callstackResolveEx.ToString());
            }

            var exceptionEvent = new OutputEvent
            {
                Category = OutputEvent.CategoryValue.Stderr,
                Output = msg.ToString()
            };
            Protocol.SendEvent(exceptionEvent);
        }

        protected override void HandleProtocolError(Exception ex)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] ProtocolError");
            }
            
            var exceptionEvent = new OutputEvent
            {
                Category = OutputEvent.CategoryValue.Stderr,
                Output = ex.ToString()
            };
            Protocol.SendEvent(exceptionEvent);
        }

        /*
        protected override ResponseBody HandleProtocolRequest(string requestType, object requestArgs)
        {
            return base.HandleProtocolRequest(requestType, requestArgs);
        }*/

        protected override void HandleRestartRequestAsync(IRequestResponder<RestartArguments> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] Restart Request");
            }
            
            throw new NotImplementedException();
        }

        protected override void HandleTerminateThreadsRequestAsync(IRequestResponder<TerminateThreadsArguments> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] TerminateThreads Request");
            }
            
            throw new NotImplementedException();
        }

        protected override void HandleSourceRequestAsync(IRequestResponder<SourceArguments, SourceResponse> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] Source Request");
            }

            if (responder.Arguments.Source != null && !string.IsNullOrEmpty(responder.Arguments.Source.Path))
            {
                var sourceFileResolver = new SourceFileResolver(Globals.CurrentProject.Path, new Dictionary<string, string>());

                var contents = string.Empty;
                var error = string.Empty;
                sourceFileResolver.ReadFileDelegate(responder.Arguments.Source.Path, ref contents, ref error);

                if (!string.IsNullOrEmpty(contents))
                {
                    responder.SetResponse(new SourceResponse()
                    {
                        Content = contents
                    });
                    return;
                }
            }
            
            throw new NotImplementedException();
        }

        protected override void HandleSetVariableRequestAsync(IRequestResponder<SetVariableArguments, SetVariableResponse> responder)
        {
            if (_protocolTrace)
            {
                PluginLoader.Default.Svc.Log("[DAP] SetVariable Request");
            }
            
            throw new NotImplementedException();
        }
        //#endregion
    }
}