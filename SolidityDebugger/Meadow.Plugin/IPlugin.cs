using System;

namespace Meadow.Plugin
{
    public interface IPlugin
    {
        void OnEvmStateInit(PluginServices svc, ICheatCodeRegistry cheatCodeRegistry);
        void OnExecutionTraceAnalysis(PluginServices svc, IPatchExecutionTraceAnalysis e);
    }
}
