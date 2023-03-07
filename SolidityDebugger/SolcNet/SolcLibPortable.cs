using System.Text;
using Microsoft.ClearScript.V8;
using SolcNet.CompileErrors;
using SolcNet.DataDescription.Input;
using SolcNet.DataDescription.Output;

namespace SolcNet
{
    public class SolcLibPortable
    {
        private string _sourceDir;
        private string _solcJs;

        public string VersionDescription { get; }
        
        public Version Version => ParseVersionString(VersionDescription);

        private static Version ParseVersionString(string versionString)
        {
            return Version.Parse(versionString.Split(new[] { '-', '+' }, 2, StringSplitOptions.RemoveEmptyEntries)[0]);
        }
        
        
        /// <param name="solcWasmPath">soljson-v0.8.9+commit.e5eed63a.js</param>
        /// <param name="sourceDir"></param>
        public SolcLibPortable(string solcWasmPath, string sourceDir)
        {
            _sourceDir = sourceDir;
            _solcJs = File.ReadAllText(solcWasmPath);
            
            using var engine = new V8ScriptEngine();
            engine.Execute(_solcJs);
            VersionDescription = $"{engine.Evaluate("UTF8ToString(_solidity_version())")}";
        }
        
        private OutputDescription CompileInputDescriptionJson(
            string jsonInput,
            CompileErrorHandling errorHandling = CompileErrorHandling.ThrowOnError,
            Dictionary<string, string> soliditySourceFileContent = null)
        {
            using var engine = new V8ScriptEngine();
            engine.AddHostType("Console", typeof(Console));
            
            engine.Execute(_solcJs);
            
            var jsonInputEscaped = Encoding.UTF8.GetBytes(jsonInput).Aggregate("", (current, val) => current + $"\\x{val:x2}");
            engine.Execute($@"
                jsonInputStr = '{jsonInputEscaped}';
                jsonInputPtr = _solidity_alloc(lengthBytesUTF8(jsonInputStr) + 1);
                stringToUTF8(jsonInputStr, jsonInputPtr, lengthBytesUTF8(jsonInputStr) + 1);
            ");
            
            var sourceResolver = new SourceFileResolver(_sourceDir, soliditySourceFileContent);
            engine.AddHostObject(
                "CSharpCallback",
                (string kind, string data) =>
                {
                    var contents = string.Empty;
                    var error = string.Empty;
                    sourceResolver.ReadFileDelegate(data, ref contents, ref error);
                    return !string.IsNullOrEmpty(contents) ? contents : string.Empty;
                }
            );
            
            engine.Execute(@"
                function cb(context, kind, data, contents, error) {
                    var strKind = UTF8ToString(kind);
                    var strData = UTF8ToString(data);

                    var fileContent = CSharpCallback(strKind, strData);

                    var ptr;
                    if (fileContent)
                    {
                        ptr = _solidity_alloc(lengthBytesUTF8(fileContent) + 1);
                        stringToUTF8(fileContent, ptr, lengthBytesUTF8(fileContent) + 1);
                        setValue(contents, ptr, '*');
                    }
                    else
                    {
                        ptr = _solidity_alloc(lengthBytesUTF8(data) + 1);
                        stringToUTF8(data, ptr, lengthBytesUTF8(data) + 1);
                        setValue(error, ptr, '*');
                    }
                }

                cbPtr = addFunction(cb, 'viiiii');
                outputPtr = _solidity_compile(jsonInputPtr, cbPtr, 0);
                outputStr = UTF8ToString(outputPtr);
            ");

            var res = $"{engine.Evaluate("outputStr")}";
                
            var output = OutputDescription.FromJsonString(res);

            var compilerException = CompilerException.GetCompilerExceptions(output.Errors, errorHandling);
            if (compilerException != null)
            {
                throw compilerException;
            }
                
            return output;
        }
        
        public OutputDescription Compile(
            InputDescription input,
            CompileErrorHandling errorHandling = CompileErrorHandling.ThrowOnError,
            Dictionary<string, string> soliditySourceFileContent = null)
        {
            using var sourceResolver = new SourceFileResolver(_sourceDir, soliditySourceFileContent);
            
            var jsonStr = input.ToJsonString();
            
            return CompileInputDescriptionJson(
                jsonStr, 
                errorHandling, 
                soliditySourceFileContent);
        }
        
        public OutputDescription Compile(
            string[] contractFilePaths,
            OutputType[] outputSelection,
            Optimizer? optimizer = null,
            CompileErrorHandling errorHandling = CompileErrorHandling.ThrowOnError,
            Dictionary<string, string> soliditySourceFileContent = null)
        {
            var inputDesc = new InputDescription();
            inputDesc.Settings.OutputSelection["*"] = new Dictionary<string, OutputType[]>
            {
                ["*"] = outputSelection,
                [""] = outputSelection
            };

            if (optimizer != null)
            {
                inputDesc.Settings.Optimizer = optimizer;
            }

            foreach (var filePath in contractFilePaths)
            {
                var normalizedPath = filePath.Replace('\\', '/');
                var source = new Source { Urls = new List<string> { normalizedPath } };
                inputDesc.Sources[normalizedPath] = source;
            }

            return Compile(inputDesc, errorHandling, soliditySourceFileContent);
        }
    }
}