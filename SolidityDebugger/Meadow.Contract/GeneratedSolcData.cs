﻿using Meadow.Core.Utils;
using Meadow.JsonRpc.Types.Debugging;
using Newtonsoft.Json;
using SolcNet.DataDescription.Output;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using PeterO.Cbor;

namespace Meadow.Contract
{

    public static class GeneratedSolcData<TContract> where TContract : BaseContract
    {
        public static readonly GeneratedSolcData Default;

        static GeneratedSolcData()
        {
            Default = GeneratedSolcData.Create(typeof(TContract).Assembly);
        }
    }

    public class GeneratedSolcData
    {
        public static GeneratedSolcData Default => _default.Value;
        static readonly Lazy<GeneratedSolcData> _default = new Lazy<GeneratedSolcData>(() => new GeneratedSolcData());

        public const string SOLC_OUTPUT_DATA_FILE = "SolcOutputData";

        public Assembly Assembly { get; private set; }

        readonly Lazy<ResourceManager> _resourceManager;
        private Lazy<SolcBytecodeInfo[]> _solcBytecodeInfo;
        private Lazy<SolcSourceInfo[]> _solcSourceInfo;
        readonly Lazy<string> _solidityCompilerVersion;
        readonly Lazy<Dictionary<(string FilePath, string ContractName), SolcNet.DataDescription.Output.Abi[]>> _contractAbis;

        public SolcBytecodeInfo[] SolcBytecodeInfo => _solcBytecodeInfo.Value;
        public SolcSourceInfo[] SolcSourceInfo => _solcSourceInfo.Value;
        public string SolidityCompilerVersion => _solidityCompilerVersion.Value;

        public (SolcSourceInfo[] SolcSourceInfo, SolcBytecodeInfo[] SolcBytecodeInfo) GetSolcData()
        {
            return (_solcSourceInfo.Value, _solcBytecodeInfo.Value);
        }

        public void SetSolcData(SolcSourceInfo[] solcSourceInfo, SolcBytecodeInfo[] solcBytecodeInfo)
        {
            _solcSourceInfo = new Lazy<SolcSourceInfo[]>(() => solcSourceInfo);
            _solcBytecodeInfo = new Lazy<SolcBytecodeInfo[]>(() => solcBytecodeInfo);
        }

        static ConcurrentDictionary<Assembly, GeneratedSolcData> _assemblyCache = new ConcurrentDictionary<Assembly, GeneratedSolcData>();

        public static GeneratedSolcData Create(Assembly assembly)
        {
            if (_assemblyCache.TryGetValue(assembly, out var data))
            {
                return data;
            }

            var solcCache = new GeneratedSolcData(assembly);
            _assemblyCache.TryAdd(assembly, solcCache);
            return solcCache;
        }

        private GeneratedSolcData(Assembly assembly = null)
        {
            if (assembly != null)
            {
                Assembly = assembly;
            }
            else
            {
                var sourceAttrs = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(a => new { Assembly = a, Attr = a.GetCustomAttribute<GeneratedSolcDataAttribute>() })
                    .Where(a => a.Attr != null)
                    .ToArray();

                if (sourceAttrs.Length == 0)
                {
                    throw new Exception($"Could not find any assemblies with {nameof(GeneratedSolcDataAttribute)}");
                }

                if (sourceAttrs.Length > 1)
                {
                    throw new Exception($"Found multiple assemblies with {nameof(GeneratedSolcDataAttribute)}: " + string.Join(", ", sourceAttrs.Select(a => a.Assembly.FullName)));
                }

                Assembly = sourceAttrs[0].Assembly;
            }


            _resourceManager = new Lazy<ResourceManager>(GetResourceManager);
            _solcBytecodeInfo = new Lazy<SolcBytecodeInfo[]>(GetSolcBytecodeInfo);
            _solcSourceInfo = new Lazy<SolcSourceInfo[]>(GetSolcSourceInfo);
            _solidityCompilerVersion = new Lazy<string>(GetSolidityCompilerVersion);
            _contractAbis = new Lazy<Dictionary<(string FilePath, string ContractName), SolcNet.DataDescription.Output.Abi[]>>(GetContractAbiJson);
        }

        public ResourceManager GetResourceManager()
        {
            var manifestResourceNames = Assembly.GetManifestResourceNames();
            var solcDataResourceName = manifestResourceNames.FirstOrDefault(r => r.EndsWith($".{SOLC_OUTPUT_DATA_FILE}.sol.resources", StringComparison.OrdinalIgnoreCase));

            if (solcDataResourceName == null)
            {
                throw new Exception($"Assembly {Assembly.FullName} does not contain the SolcOutputData embedded resources");
            }

            solcDataResourceName = solcDataResourceName.Substring(0, solcDataResourceName.Length - ".resources".Length);
            var resourceManager = new ResourceManager(solcDataResourceName, Assembly);

            return resourceManager;
        }

        SolcBytecodeInfo[] GetSolcBytecodeInfo()
        {
            var resourceManager = _resourceManager.Value;
            var serializerSettings = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error };
            var byteCodeDataJsonString = resourceManager.GetString("ByteCodeData", CultureInfo.InvariantCulture);
            var d = JsonConvert.DeserializeObject<SolcBytecodeInfo[]>(byteCodeDataJsonString, serializerSettings);
            return d;
        }

        SolcSourceInfo[] GetSolcSourceInfo()
        {
            var resourceManager = _resourceManager.Value;
            var serializerSettings = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error };

            var sourceListJsonString = resourceManager.GetString("SourcesList", CultureInfo.InvariantCulture);
            return JsonConvert.DeserializeObject<SolcSourceInfo[]>(sourceListJsonString, serializerSettings);
        }

        string GetSolidityCompilerVersion()
        {
            var resourceManager = _resourceManager.Value;
            var ver = resourceManager.GetString("SolidityCompilerVersion", CultureInfo.InvariantCulture);
            return ver;
        }

        Dictionary<(string FilePath, string ContractName), SolcNet.DataDescription.Output.Abi[]> GetContractAbiJson()
        {
            var resourceManager = _resourceManager.Value;
            var ver = resourceManager.GetString("ContractAbiJson", CultureInfo.InvariantCulture);
            var dict = JsonConvert.DeserializeObject<Dictionary<string, SolcNet.DataDescription.Output.Abi[]>>(ver);
            var formatted = dict.ToDictionary(
                k =>
                {
                    // Parse full contract path format "{relative file path}/{contract name}"
                    // Example: "SomeDir/SomeFile.sol/SomeContract"
                    var pathIndex = k.Key.LastIndexOf('/');
                    var filePath = k.Key.Substring(0, pathIndex);
                    var contractName = k.Key.Substring(pathIndex + 1);
                    return (filePath, contractName);
                }, k => k.Value);
            return formatted;
        }

        public Abi[] GetContractJsonAbi(string solFile, string contractName)
        {
            var abi = _contractAbis.Value[(solFile, contractName)];
            return abi;
        }

        public Abi[] GetContractJsonAbi<TContract>() where TContract : BaseContract
        {
            var contractAttr = TypeAttributeCache<TContract, SolidityContractAttribute>.Attribute;
            return GetContractJsonAbi(contractAttr.FilePath, contractAttr.ContractName);
        }

        public IReadOnlyDictionary<(string FilePath, string ContractName), SolcNet.DataDescription.Output.Abi[]> ContractAbis => _contractAbis.Value;

        ConcurrentDictionary<(string CodeHex, bool IsDeployed), SolcBytecodeInfo> _codeHexCache = new ConcurrentDictionary<(string CodeHex, bool IsDeployed), SolcBytecodeInfo>();


        public static string StripMetadata(string hex)
        {
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hex = hex.Substring(2);
            }
            
            hex = hex.ToLowerInvariant().Trim();

            if (hex.Length < 32)
            {
                return hex;
            }
            
            try
            {
                using (var stream = new MemoryStream(hex.HexToBytes()))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        stream.Position = stream.Length - 2;
                        var cborSize = BitConverter.ToUInt16(reader.ReadBytes(2).Reverse().ToArray(), 0);
                        if (cborSize >= stream.Length || cborSize == 0)
                        {
                            return hex;
                        }

                        stream.Position = stream.Length - cborSize - 2;
                        CBORObject.Read(stream);

                        var strippedHex = hex.Substring(0, hex.Length - (cborSize * 2) - 4);
                        return strippedHex;
                    }
                }
            }
            catch
            {
                return hex;
            }
        }
        
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="codeHex"></param>
        /// <param name="isDeployed"></param>
        /// <returns></returns>
        public bool GetSolcBytecodeInfoByCodeMatch(string codeHex, bool isDeployed, out SolcBytecodeInfo match)
        {
            if (_codeHexCache.TryGetValue((codeHex, isDeployed), out var val))
            {
                match = val;
                return true;
            }

            if (isDeployed)
            {
                var info = SolcBytecodeInfo.FirstOrDefault(s => s.BytecodeDeployed == codeHex);

                if (info == null)
                {
                    var codeHex2 = StripMetadata(codeHex);
                    info = SolcBytecodeInfo.FirstOrDefault(s => s.BytecodeDeployed == codeHex2);

                    if (info == null)
                    {
                        var codeHex2Bytes = codeHex2.HexToBytes();
                        
                        foreach (var item in SolcBytecodeInfo.Where(s => s.BytecodeDeployed.Length == codeHex2.Length))
                        {
                            var itemBytes = item.BytecodeDeployed.HexToBytes();
                            if (codeHex2Bytes.Length != itemBytes.Length) continue;
                            var isEqual = true;
                            
                            for (var i = 0; i < codeHex2Bytes.Length; ++i)
                            {
                                var byteFromNetwork = codeHex2Bytes[i];
                                var byteFromCompiler = itemBytes[i];
                                if (byteFromNetwork == byteFromCompiler || byteFromCompiler == 0) continue;
                                isEqual = false;
                                break;
                            }

                            if (isEqual)
                            {
                                info = item;
                                break;
                            }
                        }
                    }
                }
                
                if (info == null)
                {
                    match = null;
                    return false;
                }

                _codeHexCache.TryAdd((codeHex, isDeployed), info);
                match = info;
                return true;
            }
            else
            {
                // Find entry with the longest matching prefix
                SolcBytecodeInfo info = null;
                int longestPrefix = 0;
                foreach (var entry in SolcBytecodeInfo)
                {
                    var commonLen = Math.Min(entry.Bytecode.Length, codeHex.Length);
                    int matchingLen = 0;
                    for (var i = 0; i < commonLen; i++)
                    {
                        if (entry.Bytecode[i] == codeHex[i])
                        {
                            matchingLen++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (matchingLen > longestPrefix)
                    {
                        longestPrefix = matchingLen;
                        info = entry;
                    }
                }

                if (info == null)
                {
                    match = null;
                    return false;
                }

                if (longestPrefix < 2)
                {
                    match = null;
                    return false;
                }

                _codeHexCache.TryAdd((codeHex, isDeployed), info);
                match = info;
                return true;
            }
        }

        /// <summary>
        /// Match coverage contract addresses with deployed contracts that the client keeps track of.
        /// </summary>
        public (CompoundCoverageMap, SolcBytecodeInfo)[] MatchCoverageData(CompoundCoverageMap[] coverageMapData)
        {
            var contractInstances = new List<(CompoundCoverageMap, SolcBytecodeInfo)>();
            foreach (var cov in coverageMapData)
            {
                if (cov.UndeployedMap != null)
                {
                    string codeHex = cov.UndeployedMap.Code.ToHexString(hexPrefix: false);
                    if (!GetSolcBytecodeInfoByCodeMatch(codeHex, isDeployed: false, out var info))
                    {
                        throw new Exception($"Could not match coverage data undeployed code to solc outputs. Address: {cov.ContractAddress}");
                    }

                    contractInstances.Add((cov, info));
                }

                if (cov.DeployedMap != null)
                {
                    string codeHex = cov.DeployedMap.Code.ToHexString(hexPrefix: false);
                    if (!GetSolcBytecodeInfoByCodeMatch(codeHex, isDeployed: true, out var info))
                    {
                        throw new Exception($"Could not match coverage data deployed code to solc outputs. Address: {cov.ContractAddress}");
                    }

                    contractInstances.Add((cov, info));
                }
            }

            return contractInstances.Distinct().ToArray();
        }

        ConcurrentDictionary<(string FilePath, string ContractName), SolcBytecodeInfo> _contractBytecodeInfoCache = new ConcurrentDictionary<(string FilePath, string ContractName), SolcBytecodeInfo>();

        public SolcBytecodeInfo GetSolcBytecodeInfo(string filePath, string contractName)
        {
            if (_contractBytecodeInfoCache.TryGetValue((filePath, contractName), out var val))
            {
                return val;
            }

            var infos = SolcBytecodeInfo.Where(s => s.FilePath == filePath && s.ContractName == contractName).ToArray();
            if (infos.Length == 0)
            {
                throw new Exception($"Could not find generated solc bytecode data in resx for contract {contractName} in {filePath}");
            }

            if (infos.Length > 1)
            {
                throw new Exception($"Multiple solc entries for contract '${contractName}' ${filePath}");
            }

            var info = infos[0];
            _contractBytecodeInfoCache.TryAdd((filePath, contractName), info);
            return info;
        }

    }
}
