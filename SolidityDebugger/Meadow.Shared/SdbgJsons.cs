using Newtonsoft.Json;

namespace Meadow.Shared
{
    public class SdbgForkConfig
    {
        [JsonProperty("enable", Required = Required.Always)]
        public bool Enabled;

        [JsonProperty("url", Required = Required.Always)]
        public string Url;

        [JsonProperty("blockNumber", Required = Required.DisallowNull)]
        public long BlockNumber;
    }

    public class SdbgSymbolSourceEtherscan
    {
        [JsonProperty("url", Required = Required.Always)]
        public string Url;
        
        [JsonProperty("apiKey", Required = Required.Always)]
        public string ApiKey;
        
        [JsonProperty("contractAddrs", Required = Required.Always)]
        public string[] ContractAddrs;
    }

    public class SdbgSymbolSourceHardhat
    {
        [JsonProperty("projectPaths", Required = Required.Always)]
        public string[] ProjectPaths;
    }

    public class SdbgSymbolSources
    {
        [JsonProperty("etherscan", Required = Required.Default)]
        public SdbgSymbolSourceEtherscan Etherscan;

        [JsonProperty("hardhat", Required = Required.Default)]
        public SdbgSymbolSourceHardhat Hardhat;
    }


    public class SdbgSetupStep
    {
        [JsonProperty("cmdline", Required = Required.Default)]
        public string Cmdline;

        [JsonProperty("expectedOutput", Required = Required.Default)]
        public string ExpectedOutput;
    }
    
    
    public class SdbgContractJson
    {
        public static SdbgContractJson FromJsonString(string jsonStr)
        {
            return JsonConvert.DeserializeObject<SdbgContractJson>(jsonStr, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
        }

        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(this, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
        }

        public override string ToString() => ToJsonString();
        
        [JsonProperty("solc", Required = Required.Default)]
        public string SolcVersion;

        [JsonProperty("entryPoint", Required = Required.Default)]
        public string EntryPoint;
        
        [JsonProperty("sourceDirs", Required = Required.Default)]
        public string[] SourceDirs;

        [JsonProperty("fork", Required = Required.Default)]
        public SdbgForkConfig ForkConfig;

        [JsonProperty("symbols", Required = Required.Default)]
        public SdbgSymbolSources SymbolSources;

        [JsonProperty("preDebugSteps", Required = Required.Default)]
        public SdbgSetupStep[] PreDebugSteps;
        
        [JsonProperty("preBuildSteps", Required = Required.Default)]
        public SdbgSetupStep[] PreBuildSteps;
        
        [JsonProperty("verbose", Required = Required.Default)]
        public bool Verbose;
    }

    public class SdbgProjectJson : SdbgContractJson
    {
        public static new SdbgProjectJson FromJsonString(string jsonStr)
        {
            return JsonConvert.DeserializeObject<SdbgProjectJson>(jsonStr, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
        }
        
        [JsonProperty("contractsDir", Required = Required.Always)]
        public string ContractsDir;
        
        [JsonProperty("selectedContract", Required = Required.DisallowNull)]
        public string SelectedContract;
        
        [JsonProperty("autoOpen", Required = Required.Default)]
        public bool AutoOpen;
        
        [JsonProperty("breakOnEntry", Required = Required.Default)]
        public bool BreakOnEntry;
    }
}
