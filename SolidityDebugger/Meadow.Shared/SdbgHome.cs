using System;
using System.IO;

namespace Meadow.Shared
{
    public static class SdbgHome
    {
        public static string AppHome { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
            ".solidity-ide");

        public static string AppHomeSolc { get; } = Path.Combine(AppHome, "solc");
        public static string AppHomeConfig { get; } = Path.Combine(AppHome, "config");
        public static string AppHomeEngine { get; } = Path.Combine(AppHome, "engine");
        public static string AppHomeDotNet { get; } = Path.Combine(AppHome, "dotnet");
        public static string AppHomeDownloadCache { get; } = Path.Combine(AppHome, "dlcache");
        public static string AppHomeEngineCache { get; } = Path.Combine(AppHome, "cache");

        public static string SolcJsPath(Version solcVersion)
        {
            return Path.Combine(AppHomeSolc, SolcVersion.ToString(solcVersion), "solc.js");
        }

        public static string SolcJsPath(string solcVersion)
        {
            return SolcJsPath(SolcVersion.FromString(solcVersion));
        }
    }
}