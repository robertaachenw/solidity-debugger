using System;
using System.Diagnostics;
using System.Threading;
using Meadow.Plugin;
using Meadow.Shared;

namespace Meadow.DebugAdapterServer
{
    public class SetupProcess : IDisposable
    {
        private readonly SdbgSetupStep _config;
        private Process _process;
        private bool _done;
        private bool _stdout;
        private string _outputBuffer = string.Empty;

        public SetupProcess(SdbgSetupStep config, bool stdout = false)
        {
            _config = config;
            _stdout = stdout;
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (_stdout)
            {
                Console.Write(e.Data);
            }
            
            PluginLoader.Default.Svc.Log(e.Data);

            if (!_done && !string.IsNullOrEmpty(_config.ExpectedOutput) && !string.IsNullOrEmpty(e.Data))
            {
                _outputBuffer += e.Data;

                if (_outputBuffer.Contains(_config.ExpectedOutput, StringComparison.InvariantCulture))
                {
                    PluginLoader.Default.Svc.Log($"Setup step complete: {_config.Cmdline}");
                    _done = true;
                }
            }
        }

        public void Run()
        {
            if (string.IsNullOrEmpty(_config.Cmdline))
            {
                throw new Exception("dbg.contract.json -> setupSteps -> cmdline is missing");
            }

            _process = new Process();
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.RedirectStandardOutput = true;
            _process.StartInfo.RedirectStandardError = true;
            _process.OutputDataReceived += OnOutputDataReceived;
            _process.ErrorDataReceived += OnOutputDataReceived;
            _process.StartInfo.WorkingDirectory = Globals.CurrentProject.Path;

            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                    _process.StartInfo.FileName = "cmd";
                    _process.StartInfo.Arguments = "/c ";
                    break;

                default:
                    _process.StartInfo.FileName = "sh";
                    _process.StartInfo.Arguments = "-c ";
                    break;
            }

            if (_config.Cmdline.Contains('"', StringComparison.Ordinal) ||
                _config.Cmdline.Contains('\'', StringComparison.Ordinal))
            {
                _process.StartInfo.Arguments += _config.Cmdline;
            }
            else
            {
                _process.StartInfo.Arguments += $"\"{_config.Cmdline}\"";
            }

            PluginLoader.Default.Svc.Log(
                $"Starting process: {_process.StartInfo.FileName} {_process.StartInfo.Arguments}");
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _done = string.IsNullOrEmpty(_config.ExpectedOutput);

            var processDied = 0;

            while (!_done)
            {
                Thread.Sleep(50);

                if (_process.HasExited) processDied += 1;

                if (!_done && processDied > 2)
                {
                    throw new Exception(
                        $"Process ended without expected output (cmdline=\"{_config.Cmdline}\" expectedOutput=\"{_config.ExpectedOutput}\")");
                }
            }
        }

        public void WaitForExit()
        {
            _process?.WaitForExit();
        }

        public void Dispose()
        {
            if (_process != null)
            {
                if (!_process.HasExited)
                {
                    PluginLoader.Default.Svc.Log($"Closing process {_process.Id}");

                    try
                    {
                        ProcessExtensions.KillTree(_process);
                    }
                    catch (Exception ex)
                    {
                        PluginLoader.Default.Svc.Log(ex.ToString());
                    }
                }

                _process.Dispose();
                _process = null;
            }
        }
    }
}