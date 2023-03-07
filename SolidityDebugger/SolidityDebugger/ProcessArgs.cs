using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Meadow.DebugSolSources
{
    class ProcessArgs
    {
        [Option("-d|--directory", "Directory of the .sol source files.", CommandOptionType.SingleValue)]
        public string Directory { get; }

        [Option("-e|--entry", "The contract entry point in the form of 'ContractName.FunctionName'", CommandOptionType.SingleValue)]
        public string Entry { get; }

        [Option("-f|--singleFile", "A single solidity file to debug.", CommandOptionType.SingleValue)]
        public string SingleFile { get; }

        [Option("-p|--tcpPort", "Use TCP transport instead of stdin/stdout. Optionally specify port.", CommandOptionType.SingleValue)]
        public string TcpPort { get; }

        [Option("-a|--solcJsPath", "Solidity compiler javascript file", CommandOptionType.SingleValue)]
        public string SolcJsPath { get; }
        
        [Option("-P|--sdbg-project-path", "Path to SDBG project directory", CommandOptionType.SingleValue)]
        public string SdbgProjectPath { get; }
        
        [Option("-C|--sdbg-contract-name", "Name of contract to debug", CommandOptionType.SingleValue)]
        public string SdbgContractName { get; }
        
        [Option("-B|--build-only", "Build and then exit without starting debugger", CommandOptionType.NoValue)]
        public bool BuildOnly { get; }

        [Option("-M|--test-v8", "Test V8", CommandOptionType.NoValue)]
        public bool TestV8Only { get; }

        
        public static ProcessArgs Parse(string[] args)
        {
            var app = new CommandLineApplication<ProcessArgs>(throwOnUnexpectedArg: true);
            app.Conventions.UseDefaultConventions();
            app.Parse(args);
            var result = app.Model;
            
            return result;
        }
    }
}
