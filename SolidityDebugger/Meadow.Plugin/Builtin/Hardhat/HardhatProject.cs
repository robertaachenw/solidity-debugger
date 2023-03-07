using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Meadow.Shared;
using Newtonsoft.Json.Linq;
using PeterO.Cbor;

namespace Meadow.Plugin.Builtin.Hardhat
{
    public class HardhatProject
    {
        private const string ArtifactsSubdir = "artifacts";

        private const string HardhatJsonSchemaArtifact = "hh-sol-artifact-1";
        private const string HardhatJsonSchemaDbg = "hh-sol-dbg-1";
        private const string HardhatJsonSchemaBuildInfo = "hh-sol-build-info-1";
        
        private static readonly string[] ReservedKeys =
        {
            "begin", "start", "length", "end", "version", "offset"
        };
        
        private static readonly string[] IncludeDirs = { "", "node_modules" };

        private const int AstMaxId = 1000000;
        private int _astOffset;
        private string _projectDir;

        private Dictionary<string, string> _compiledSources = new Dictionary<string, string>();


        private static byte[] HexToBytes(string hex)
        {
            var numberChars = hex.Length;
            var bytes = new byte[numberChars / 2];
            for (var i = 0; i < numberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        private string GetFileHash(string filePath)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(File.ReadAllBytes(filePath))).Replace("-", string.Empty).ToLowerInvariant();
        }
        
        private JObject ReadAnyJsonFile(string filePath, out string fileFormat)
        {
            var json = JObject.Parse(File.ReadAllText(filePath));
            if (json == null)
            {
                throw new Exception($"{filePath}: invalid json file");
            }

            var fmt = json.Value<string>("_format");
            if (string.IsNullOrEmpty(fmt))
            {
                throw new Exception($"{filePath}: not a hardhat json");
            }

            fileFormat = fmt;
            return json;
        }

        private JObject ReadJsonFile(string filePath, string fileFormat)
        {
            var result = ReadAnyJsonFile(filePath, out var fmt);
            
            if (!fmt.Equals(fileFormat, StringComparison.Ordinal))
            {
                throw new Exception($"{filePath}: expected {fileFormat}, got {fmt}");
            }

            return result;
        }
        
        private static IEnumerable<JToken> AllTokens(JObject obj) 
        {
            var toSearch = new Stack<JToken>(obj.Children());
            while (toSearch.Count > 0) {
                var inspected = toSearch.Pop();
                yield return inspected;
                foreach (var child in inspected) 
                {
                    toSearch.Push(child);
                }
            }
        }
        
        private static void ApplyAstOffset(ref JObject solcOutput, int shiftAmount)
        {
            PluginLoader.Default.Svc.StartBenchmark();
            
            // collect all IDs
            var allIds = new HashSet<int>(
                AllTokens(solcOutput)
                    .Where(t => t.Type == JTokenType.Property)
                    .Select(t => (JProperty)t)
                    .Where(p => 
                        p.Name.Equals("id", StringComparison.Ordinal) || 
                        p.Name.Equals("astId", StringComparison.Ordinal))
                    .Where(p => int.TryParse($"{p.Value}", out _))
                    .Select(p => int.Parse($"{p.Value}"))
                );
            
            // Convert simple integer fields
            var intFields = AllTokens(solcOutput)
                .Where(t => t.Type == JTokenType.Property)
                .Select(t => (JProperty)t)
                .Where(p => p.Value.Type == JTokenType.Integer)
                .Where(p => !ReservedKeys.Contains(p.Name));
            
            foreach (var field in intFields)
            {
                if (int.TryParse($"{field.Value}", out var value))
                {
                    if (allIds.Contains(value))
                    {
                        field.Value = value + shiftAmount;
                    }
                }
            }
            
            // Convert arrays of integers
            var arrayFields = AllTokens(solcOutput)
                .Where(t => t.Type == JTokenType.Property)
                .Select(t => (JProperty)t)
                .Where(p =>p.Value.Type == JTokenType.Array)
                .Where(p => !ReservedKeys.Contains(p.Name));
            
            foreach (var field in arrayFields)
            {
                var array = field.Value as JArray;
                if (array == null || array.Count == 0) continue;

                for (var i = 0; i < array.Count; ++i)
                {
                    if (array[i].Type == JTokenType.Integer)
                    {
                        var val = array[i].ToObject<Int32>();
                        if (allIds.Contains(val))
                        {
                            array[i] = val + shiftAmount;
                        }
                    }
                }
            }
            
            PluginLoader.Default.Svc.StopBenchmark();
        }
                
        private static string ApplyAstOffsetSourceMapItem(string input, int shiftAmount)
        {
            PluginLoader.Default.Svc.StartBenchmark();

            var rebuildParts = new List<string>();
            
            foreach (var entry in input.Split(new char[] { ';' }))
            {
                var parts = entry.Split(new char[] { ':' });

                if (parts.Length >= 3 && parts[2].Length > 0)
                {
                    parts[2] = $"{int.Parse(parts[2]) + shiftAmount}";
                }
                
                rebuildParts.Add(string.Join(":", parts));
            }

            var rebuild = string.Join(";", rebuildParts);
            
            PluginLoader.Default.Svc.StopBenchmark();
            return rebuild;
        }
        
        private static void ApplyAstOffsetSourceMap(ref JObject solcOutput, int shiftAmount)
        {
            PluginLoader.Default.Svc.StartBenchmark();
            
            var sourceMapFields = AllTokens(solcOutput).Where(t => 
                t.Type == JTokenType.Property && 
                (
                    ((JProperty)t).Name.Equals("src", StringComparison.Ordinal) || 
                    ((JProperty)t).Name.Equals("sourceMap", StringComparison.Ordinal)
                )
            );
    
            foreach (var e in sourceMapFields)
            {
                if (!(e is JProperty sourceMap)) continue;

                var sourceMapValue = $"{sourceMap.Value}";
                sourceMap.Value = ApplyAstOffsetSourceMapItem(sourceMapValue, shiftAmount);
            }
            
            PluginLoader.Default.Svc.StopBenchmark();
            
        }

        private static string RemoveBytecodeMetadata(string hex)
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
                using (var stream = new MemoryStream(HexToBytes(hex)))
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

        private static void RemoveBytecodeMetadata(ref JObject solcOutput)
        {
            PluginLoader.Default.Svc.StartBenchmark();
            
            foreach (var jToken in solcOutput["contracts"])
            {
                var solFile = (JProperty)jToken;
                foreach (var jToken1 in solFile.Value)
                {
                    var solContract = (JProperty)jToken1;

                    foreach (var bcType in new[] { "bytecode", "deployedBytecode" })
                    {
                        var bytecode = solContract.Value["evm"][bcType].Value<string>("object");
                        
                        var strippedBytecode = RemoveBytecodeMetadata(bytecode);
                        
                        solcOutput["contracts"][solFile.Name][solContract.Name]["evm"][bcType]["object"] =
                            strippedBytecode;
                    }
                }
            }
            
            PluginLoader.Default.Svc.StopBenchmark();
        }

        private string GetSourceFilePath(string relPath)
        {
            foreach (var includeDir in IncludeDirs)
            {
                var fullPath = Path.GetFullPath(Path.Combine(_projectDir, includeDir, relPath));
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }
        
        private static void Rename(JToken token, string newName)
        {
            var parent = token.Parent;
            if (parent == null)
            {
                return;
            }
            var newToken = new JProperty(newName, token);
            parent.Replace(newToken);
        }

        private void ApplyAbsoluteFilePaths(ref JObject solcOutput)
        {
            PluginLoader.Default.Svc.StartBenchmark();
            
            foreach (var root in new[] {"sources", "contracts"})
            {
                bool modified;
                do
                {
                    modified = false;
                    foreach (var token in solcOutput.Value<JObject>(root))
                    {
                        var relPath = token.Key;
                        if (Path.IsPathRooted(relPath))
                        {
                            // already modified
                            continue;
                        }

                        var fullPath = GetSourceFilePath(relPath);

                        if (_compiledSources.ContainsKey(relPath))
                        {
                            _compiledSources[fullPath] = _compiledSources[relPath];
                        }
                        
                        Rename(token.Value, fullPath);
                        modified = true;
                        break;
                    }
                } while (modified);
            }

            var astProps = AllTokens(solcOutput)
                .Where(t => t.Type == JTokenType.Property)
                .Select(t => (JProperty)t)
                .Where(p =>
                    p.Name.Equals("absolutePath", StringComparison.Ordinal) ||
                    p.Name.Equals("file", StringComparison.Ordinal));

            foreach (var p in astProps)
            {
                var relPath = p.Value.ToString();
                if (Path.IsPathRooted(relPath))
                {
                    continue;
                }

                var fullPath = GetSourceFilePath(relPath);
                if (string.IsNullOrEmpty(fullPath))
                {
                    continue;
                }

                p.Value = fullPath;
            }
            
            PluginLoader.Default.Svc.StopBenchmark();
        }


        private void GetSolcSourcesFromBuildJson(JObject buildJson)
        {
            foreach (var token in buildJson["input"]["sources"].Children())
            {
                try
                {
                    var p = (JProperty)token;
                    var src = p.Value["content"].ToString();
                    
                    if (!string.IsNullOrEmpty(p.Name) && !string.IsNullOrEmpty(src))
                    {
                        _compiledSources[p.Name] = src;
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }
        
        
        private JObject GetSolcOutputFromBuildJson(JObject buildJson)
        {
            var solcOutput = buildJson.Value<JObject>("output");

            _astOffset += AstMaxId;
            
            ApplyAstOffset(ref solcOutput, _astOffset);
            ApplyAstOffsetSourceMap(ref solcOutput, _astOffset);

            RemoveBytecodeMetadata(ref solcOutput);
            
            ApplyAbsoluteFilePaths(ref solcOutput);
            
            return solcOutput;
        }

        private string GetBuildJsonFile(string artifactJsonFile)
        {
            // read artifact.json
            var artifactJson = ReadJsonFile(artifactJsonFile, HardhatJsonSchemaArtifact);
            
            // read artifact.dbg.json
            var dbgFilePath = Path.Combine(
                Path.GetDirectoryName(artifactJsonFile) ?? ".",
                Path.GetFileNameWithoutExtension(artifactJsonFile) + ".dbg.json");

            var dbgJson = ReadJsonFile(dbgFilePath, HardhatJsonSchemaDbg);

            var buildJsonRelPath = dbgJson.Value<string>("buildInfo");
            if (string.IsNullOrEmpty(buildJsonRelPath))
            {
                throw new MissingFieldException();
            }
            
            // find build info json
            var buildJsonPath = Path.Combine(
                Path.GetDirectoryName(artifactJsonFile) ?? ".",
                buildJsonRelPath);
            if (!File.Exists(buildJsonPath))
            {
                throw new FileNotFoundException(buildJsonPath);
            }

            return Path.GetFullPath(buildJsonPath);
        }

        private Dictionary<string, JObject> ParseArtifactsDir()
        {
            var buildInfos = new Dictionary<string, JObject>();

            if (!Directory.Exists(Path.Combine(_projectDir, ArtifactsSubdir)))
            {
                return buildInfos;
            }
            
            foreach (var dpath in Directory.EnumerateDirectories(
                         Path.Combine(_projectDir, ArtifactsSubdir), 
                         "*",
                         SearchOption.AllDirectories))
            {
                foreach (var fpath in Directory.EnumerateFiles(dpath, "*.json"))
                {
                    try
                    {
                        var buildJsonFilePath = GetBuildJsonFile(fpath);
                        if (buildInfos.ContainsKey(buildJsonFilePath))
                        {
                            continue;
                        }

                        var buildJson = ReadJsonFile(buildJsonFilePath, HardhatJsonSchemaBuildInfo);
                        GetSolcSourcesFromBuildJson(buildJson);
                        var solcOutput = GetSolcOutputFromBuildJson(buildJson);
                        buildInfos[buildJsonFilePath] = solcOutput;
                    }
                    catch (Exception ex)
                    {
                        // PluginLoader.Default.Svc.Log(ex.Message);
                    }
                }
            }

            return buildInfos;
        }

        private Dictionary<string, JObject> ParseArtifactsDirCached()
        {
            var buildInfos = new Dictionary<string, JObject>();
            
            foreach (var buildJsonFilePath in Directory.EnumerateFiles(
                         Path.Combine(_projectDir, ArtifactsSubdir, "build-info"), 
                         "*.json"))
            {
                try
                {
                    var cacheKey = $"{buildJsonFilePath}:{GetFileHash(buildJsonFilePath)}";
                    if (SdbgCache.Contains(cacheKey))
                    {
                        buildInfos[buildJsonFilePath] = SdbgCache.Get(cacheKey);
                    }
                    else
                    {
                        var buildJson = ReadJsonFile(buildJsonFilePath, HardhatJsonSchemaBuildInfo);
                        GetSolcSourcesFromBuildJson(buildJson);
                        var solcOutput = GetSolcOutputFromBuildJson(buildJson);
                        buildInfos[buildJsonFilePath] = solcOutput;
                        SdbgCache.Set(cacheKey, solcOutput);
                    }
                }
                catch (Exception ex)
                {
                    PluginLoader.Default.Svc.Log(ex.Message);
                }
            }

            return buildInfos;
        }
        
        
        public HardhatProject(string dpath)
        {
            _projectDir = dpath;
        }

        public void GetSolcOutput(
            out JObject solcOutput,
            out Dictionary<string, string> sourceCodes
            )
        {
            solcOutput = new JObject();

            var jsonMergeSettings = new JsonMergeSettings()
            {
                MergeArrayHandling = MergeArrayHandling.Union
            };

            // include compiled hardhat contracts
            foreach (var singleSolcOutput in ParseArtifactsDirCached().Values)
            {
                solcOutput.Merge(singleSolcOutput, jsonMergeSettings);
            }

            sourceCodes = _compiledSources;
        }
    }
}
