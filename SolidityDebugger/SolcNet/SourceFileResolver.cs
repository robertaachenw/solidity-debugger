using System.Text;
using Meadow.Shared;

namespace SolcNet
{
    public delegate void ReadFileCallback(string path, ref string contents, ref string error);
    
    public class SourceFileResolver : IDisposable
    {
        string _sourceDir;
        string _lastSourceDir;

        public readonly ReadFileCallback ReadFileDelegate;

        Dictionary<string, string> _fileContents = new Dictionary<string, string>();

        private const string NodeModules = "node_modules";
        private const int NodeModulesDepth = 10;
        
        public SourceFileResolver(string sourceDir, Dictionary<string, string> fileContents)
        {
            _sourceDir = sourceDir;
            _fileContents = fileContents ?? new Dictionary<string, string>();
            ReadFileDelegate = ReadSolSourceFileManaged;
        }

        public bool ReadSolSourceFileFromNode(string importPath, out string resolvedFilePath, out string resolvedFileContent)
        {
            resolvedFilePath = null;
            resolvedFileContent = null;
            
            for (var i = 0; i < 10; ++i)
            {
                var rootDir = Path.Combine(_sourceDir, string.Concat(Enumerable.Repeat("../", i)));
                var nodeModules = Path.Combine(rootDir, "node_modules");
                var filePath = Path.Combine(nodeModules, importPath);

                if (File.Exists(filePath))
                {
                    resolvedFilePath = filePath;
                    resolvedFileContent = File.ReadAllText(filePath);
                    return true;
                }
            }

            return false;
        }
        
        public void ReadSolSourceFileManaged(string importPath, ref string contents, ref string error)
        {
            // try cache first
            if (_fileContents.ContainsKey(importPath))
            {
                contents = _fileContents[importPath];
                return;
            }
            
            // collect paths from sdbg json files
            var sourceDirs = new HashSet<string> { Path.GetFullPath(_sourceDir) };

            if (Globals.CurrentProject != null && !string.IsNullOrEmpty(Globals.CurrentContractName))
            {
                sourceDirs.Add(Globals.CurrentProject.Path);
                
                if (Globals.CurrentProject.ProjectJson.SourceDirs != null)
                {
                    foreach (var relPath in Globals.CurrentProject.ProjectJson.SourceDirs)
                    {
                        sourceDirs.Add(Path.GetFullPath(Path.Combine(Globals.CurrentProject.Path, relPath)));
                    }
                }

                if (Globals.CurrentContractJson.SourceDirs != null)
                {
                    foreach (var relPath in Globals.CurrentContractJson.SourceDirs)
                    {
                        sourceDirs.Add(Path.GetFullPath(Path.Combine(Globals.CurrentProject.GetContractPath(Globals.CurrentContractName), relPath)));
                    }
                }
            }
            
            // expand node_modules
            var nodeModules = new HashSet<string>();
            foreach (var dpath in sourceDirs)
            {
                if (dpath.EndsWith(NodeModules, StringComparison.Ordinal)) continue;

                for (var i = 0; i < NodeModulesDepth; ++i)
                {
                    var dir = Path.Combine(dpath, string.Concat(Enumerable.Repeat("../", i)), NodeModules);
                    if (Directory.Exists(dir)) nodeModules.Add(Path.GetFullPath(dir));
                }
            }
            sourceDirs.UnionWith(nodeModules);
            
            // look for source file
            var wrongPaths = new HashSet<string>();
            
            foreach (var dpath in sourceDirs)
            {
                var fpath = Path.Combine(dpath, importPath);

                if (File.Exists(fpath))
                {
                    var src = File.ReadAllText(fpath).Replace("\r\n", "\n");
                    _fileContents[importPath] = src;
                    contents = src;
                    return;
                }
                
                wrongPaths.Add(fpath);
            }
            
            // failed
            error = $"File not found: {string.Join(',', wrongPaths)}";

            if (!Globals.IsDebuggerAttached)
            {
                Console.WriteLine($"Source '{importPath}' not found. Search paths:");
                foreach (var fpath in wrongPaths)
                {
                    Console.WriteLine($"\t{fpath}");
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
