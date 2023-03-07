using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Meadow.Shared
{
    public class SdbgProject
    {
        public const string ProjectJsonFileName = "dbg.project.json";
        public const string ContractJsonFileName = "dbg.contract.json";
        public const string EntryPointContractName = "DbgEntry";
        public const string EntryPointFunctionName = "main";
        public const string BuildDirName = "artifacts-dbg";

        private readonly string _projectDir;
        private readonly string _projectJsonFile;
        private readonly string _contractsDir;

        public SdbgProjectJson ProjectJson => SdbgProjectJson.FromJsonString(File.ReadAllText(_projectJsonFile));

        public string Path => _projectDir;
        
        public SdbgProject(string projectDir)
        {
            _projectDir = projectDir;
            
            if (!Directory.Exists(_projectDir))
            {
                throw new DirectoryNotFoundException(_projectDir);
            }

            _projectJsonFile = System.IO.Path.Combine(_projectDir, ProjectJsonFileName);
            if (!File.Exists(_projectJsonFile))
            {
                throw new FileNotFoundException(_projectJsonFile);
            }

            _contractsDir = System.IO.Path.Combine(_projectDir, ProjectJson.ContractsDir);
        }

        public IEnumerable<string> ContractNames
        {
            get
            {
                var result = new List<string>();
                
                foreach (var contractDir in Directory.EnumerateDirectories(_contractsDir))
                {
                    var contractJsonFile = System.IO.Path.Combine(contractDir, ContractJsonFileName);
                    if (File.Exists(contractJsonFile))
                    {
                        result.Add(System.IO.Path.GetFileName(contractDir));
                    }
                }

                return result;
            }
        }

        public SdbgContractJson GetContractJson(string contractName)
        {
            var result = new JObject();
            
            result.Merge(
                JObject.Parse(File.ReadAllText(_projectJsonFile)),
                new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Union }
            );

            result.Merge(
                JObject.Parse(
                    File.ReadAllText(
                        System.IO.Path.Combine(_contractsDir, contractName, ContractJsonFileName)
                    )
                ),
                new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Union }
            );
            
            return JsonConvert.DeserializeObject<SdbgContractJson>(JsonConvert.SerializeObject(result));
        }

        public string GetContractPath(string contractName)
        {
            return System.IO.Path.Combine(_contractsDir, contractName);
        }

    }
}