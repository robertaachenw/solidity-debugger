using System.IO;
using Meadow.Contract;
using Meadow.Core;
using Meadow.Shared;
using Newtonsoft.Json.Linq;

namespace Meadow.Plugin.Builtin.Hardhat
{
    public class HardhatPlugin : IPlugin
    {
        private HardhatProject _project;

        public void OnEvmStateInit(PluginServices svc, ICheatCodeRegistry cheatCodeRegistry)
        {
            cheatCodeRegistry.Register(
                "LoadHardhatProject",
                new[] { SolidityType.String },
                LoadHardhatProject,
                null);

            if (Globals.CurrentProject != null &&
                !string.IsNullOrEmpty(Globals.CurrentContractName) &&
                Globals.CurrentContractJson.SymbolSources != null &&
                Globals.CurrentContractJson.SymbolSources.Hardhat != null &&
                Globals.CurrentContractJson.SymbolSources.Hardhat.ProjectPaths != null)
            {
                foreach (var relPath in Globals.CurrentContractJson.SymbolSources.Hardhat.ProjectPaths)
                {
                    var fullPath = Path.Combine(Globals.CurrentProject.Path, relPath);
                    
                    LoadHardhatProject(
                        string.Empty,
                        new[] { SolidityType.String },
                        new object[] { fullPath },
                        null,
                        null
                        );
                }
            }
        }

        /// <summary>
        /// event LoadHardhatProject(string projectPath);
        /// </summary>
        private void LoadHardhatProject(string name, SolidityType[] argtypes, object[] argvalues, object context, object evmmessage)
        {
            var param = argvalues[0] as string;
            PluginLoader.Default.Svc.Log($"Loading Hardhat project: \"{param}\"");
            _project = new HardhatProject(param);
        }
        
        public void OnExecutionTraceAnalysis(PluginServices svc, IPatchExecutionTraceAnalysis e)
        {
            if (_project == null)
            {
                // nothing to do
                return;
            }
            
            PluginLoader.Default.Svc.StartBenchmark();
            PluginLoader.Default.Svc.Log("Loading symbols from Hardhat project");

            _project.GetSolcOutput(
                out var solcOutput,
                out var sourceCodes
                );

            // update _solcData
            foreach (var jToken in solcOutput["sources"])
            {
                var item = (JProperty)jToken;
                var fileName = item.Name;
                var id = item.Value.Value<int>("id");
                var astObj = (JObject)item.Value["ast"];
                var sourceContent = sourceCodes.ContainsKey(fileName) ? sourceCodes[fileName] : File.ReadAllText(fileName);  
                
                e.SolcSourceInfos.Add(
                    new SolcSourceInfo 
                    { 
                        AstJson = astObj,
                        FileName = fileName,
                        ID = id,
                        SourceCode = sourceContent
                    });
            }
        
            foreach (var jToken in solcOutput["contracts"])
            {
                var solFile = (JProperty)jToken;
                foreach (var jToken1 in solFile.Value)
                {
                    var solContract = (JProperty)jToken1;
                    var fileName = solFile.Name;
                    var contractName = solContract.Name;
            
                    var bytecodeObj = solContract.Value["evm"]["bytecode"];
                    var deployedBytecodeObj = solContract.Value["evm"]["deployedBytecode"];
            
                    var sourceMap = bytecodeObj.Value<string>("sourceMap");
                    var sourceMapDeployed = deployedBytecodeObj.Value<string>("sourceMap");
            
                    var opcodes = bytecodeObj.Value<string>("opcodes");
                    var opcodesDeployed = deployedBytecodeObj.Value<string>("opcodes");
            
                    var bytecode = bytecodeObj.Value<string>("object");
                    var bytecodeDeployed = deployedBytecodeObj.Value<string>("object");
            
                    e.SolcBytecodeInfos.Add(new SolcBytecodeInfo
                    {
                        FilePath = fileName,
                        ContractName = contractName,
                        SourceMap = sourceMap,
                        Opcodes = opcodes,
                        SourceMapDeployed = sourceMapDeployed,
                        OpcodesDeployed = opcodesDeployed,
                        Bytecode = bytecode,
                        BytecodeDeployed = bytecodeDeployed
                    });
                }
            }
            
            e.Reload = true;
            PluginLoader.Default.Svc.StopBenchmark();
        }
    }
}
