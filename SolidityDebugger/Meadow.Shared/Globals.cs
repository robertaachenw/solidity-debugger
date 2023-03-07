using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;

namespace Meadow.Shared
{
    public static class Globals
    {
        public static SdbgProject CurrentProject;
        public static string CurrentContractName;
        public static SdbgContractJson CurrentContractJson => CurrentProject.GetContractJson(CurrentContractName);

        public static bool IsDebuggerAttached = true;
        
        // obsolete
        public static string ProjectContractsDir = string.Empty;

        public static readonly BlockingCollection<string> MessageLog = new BlockingCollection<string>(); 

        /// <summary>
        /// Used by cheat codes EvmSetForkUrl(), EvmSetForkBlockNumber(), EvmStartFork(), EvmStopFork()
        /// </summary>
        public static bool ForkEnabled;
        public static string ForkUrl;
        public static ulong ForkBlockNumber;
        public static HashSet<string> ForkAccessedContracts = new HashSet<string>();
        
        /// <summary>
        /// Used by cheat code EvmSetCreateAddress()
        /// </summary>
        public static BigInteger NextCreateAddress = BigInteger.Zero;

    }
}
