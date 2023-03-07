using System.Collections.Generic;
using Meadow.Contract;

namespace Meadow.Plugin
{
    public interface IPatchExecutionTraceAnalysis
    {
        List<SolcBytecodeInfo> SolcBytecodeInfos { get; }
        List<SolcSourceInfo> SolcSourceInfos { get; }
        
        bool Reload { get; set; }
    }
}