using Meadow.Core.Utils;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Meadow.Plugin;
using Meadow.Shared;
using Microsoft.ClearScript.V8;
using SolcNet;

namespace Meadow.DebugSolSources
{
    class AppOptions
    {
        const string GENERATED_DATA_DIR = ".meadow-generated";

        public string SourceOutputDir { get; private set; }
        public string BuildOutputDir { get; private set; }
        public string SolCompilationSourcePath { get; private set; }

        public string SingleFile { get; private set; }

        public string EntryContractName { get; private set; }
        public string EntryContractFunctionName { get; private set; }

        public bool TcpTransportEnabled { get; private set; } = false;
        public int TcpTransportPort { get; private set; } = 0;

        public string SolcJsPath { get; private set; } = string.Empty;
        public string EvmVersion { get; private set; } = string.Empty;

        public bool BuildOnly { get; private set; } = false;
        
        public static AppOptions ParseSdbgArgs(ProcessArgs args)
        {
            if (string.IsNullOrEmpty(args.SdbgProjectPath) || string.IsNullOrEmpty(args.SdbgContractName))
            {
                return null;
            }

            // parse project json
            var project = new SdbgProject(args.SdbgProjectPath);

            // parse contract json
            if (!project.ContractNames.Contains(args.SdbgContractName))
            {
                throw new Exception(
                    $"No contract named {args.SdbgContractName} in project. project.ContractNames=[{string.Join(',', project.ContractNames)}]");
            }

            var contractJson = project.GetContractJson(args.SdbgContractName);

            Globals.CurrentProject = project;
            Globals.CurrentContractName = args.SdbgContractName;
            
            // build AppOptions
            var opts = new AppOptions
            {
                EntryContractName = SdbgProject.EntryPointContractName,
                EntryContractFunctionName = SdbgProject.EntryPointFunctionName,
                SolCompilationSourcePath = project.GetContractPath(args.SdbgContractName)
            };

            if (string.IsNullOrEmpty(contractJson.SolcVersion))
            {
                throw new MissingFieldException("contractJson.SolcVersion");
            }

            opts.SolcJsPath = SdbgHome.SolcJsPath(contractJson.SolcVersion);
            opts.EvmVersion = Globals.CurrentContractJson.EvmVersion;
            
            if (!string.IsNullOrEmpty(args.TcpPort))
            {
                opts.TcpTransportEnabled = true;
                opts.TcpTransportPort = int.Parse(args.TcpPort);
            }
            
            opts.BuildOutputDir = Path.Combine(args.SdbgProjectPath, SdbgProject.BuildDirName);
            if (!Directory.Exists(opts.BuildOutputDir))
            {
                Directory.CreateDirectory(opts.BuildOutputDir);
            }

            opts.SourceOutputDir = Path.Combine(opts.BuildOutputDir, "src");
            if (!Directory.Exists(opts.SourceOutputDir))
            {
                Directory.CreateDirectory(opts.SourceOutputDir);
            }

            opts.BuildOnly = args.BuildOnly;
            Globals.IsDebuggerAttached = !opts.BuildOnly;
            
            PluginLoader.Default.Svc.Log($"# ProjectPath = {args.SdbgProjectPath}");
            PluginLoader.Default.Svc.Log($"# SolcJsPath = {opts.SolcJsPath}");

            if (!string.IsNullOrEmpty(opts.EvmVersion))
            {
                PluginLoader.Default.Svc.Log($"# EvmVersion = {opts.EvmVersion}");
            }
            
            return opts;
        }
        
        public static AppOptions ParseProcessArgs(string[] args)
        {
            // Parse process arguments.
            if (args.Length == 0)
            {
                ProcessArgs.Parse(new[] { "--help" });
                Environment.Exit(1);
            }
            
            var processArgs = ProcessArgs.Parse(args);

            /*
             * v8 test
             */
            if (processArgs.TestV8Only)
            {
                using var engine = new V8ScriptEngine();
                engine.AddHostType("Console", typeof(Console));
                engine.Execute("Console.WriteLine('V8 OKAY');");
                Environment.Exit(1);
            }
            
            /*
             * sdbg mode (default)
             */
            var opts = ParseSdbgArgs(processArgs);
            if (opts != null)
            {
                return opts;
            }

            /*
             * old flow
             */
            opts = new AppOptions();

            if (string.IsNullOrEmpty(processArgs.SolcJsPath))
            {
                throw new Exception("solc.js path not specified");
            }
            else
            {
                opts.SolcJsPath = processArgs.SolcJsPath;
                var test = new SolcLibPortable(opts.SolcJsPath, ".", string.Empty);
            }
            
            if (!string.IsNullOrEmpty(processArgs.TcpPort))
            {
                opts.TcpTransportEnabled = true;
                opts.TcpTransportPort = int.Parse(processArgs.TcpPort);
            }
            
            opts.EntryContractName = null;
            opts.EntryContractFunctionName = null;
            
            if (!string.IsNullOrWhiteSpace(processArgs.Entry))
            {
                var entryParts = processArgs.Entry.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
                opts.EntryContractName = entryParts[0];
                if (entryParts.Length > 1)
                {
                    opts.EntryContractFunctionName = entryParts[1];
                }
            }

            string workspaceDir;
            if (!string.IsNullOrEmpty(processArgs.Directory))
            {
                workspaceDir = processArgs.Directory.Replace('\\', '/');
            }
            else if (!string.IsNullOrEmpty(processArgs.SingleFile))
            {
                // If workspace is not provided, derive a determistic temp directory for the single file.
                workspaceDir = Path.Combine(Path.GetTempPath(), HexUtil.GetHexFromBytes(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(processArgs.SingleFile))));
                Directory.CreateDirectory(workspaceDir);
                workspaceDir = workspaceDir.Replace('\\', '/');
            }
            else
            {
                throw new Exception("A directory or single file for debugging must be specified.");
            }

            string outputDir = workspaceDir + "/" + GENERATED_DATA_DIR;
            opts.SourceOutputDir = outputDir + "/src";
            opts.BuildOutputDir = outputDir + "/build";

            opts.SolCompilationSourcePath = workspaceDir;

            if (!string.IsNullOrEmpty(processArgs.SingleFile))
            {
                // Normalize file path.
                opts.SingleFile = processArgs.SingleFile.Replace('\\', '/');

                // Check if provided file is inside the workspace directory.
                if (opts.SingleFile.StartsWith(workspaceDir, StringComparison.OrdinalIgnoreCase))
                {
                    opts.SingleFile = opts.SingleFile.Substring(workspaceDir.Length).Trim('/');
                }
                else
                {
                    // File is outside of workspace so setup special pathing.
                    opts.SolCompilationSourcePath = opts.SingleFile;
                }
            }

            return opts;
        }

    }
}