using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Meadow.Plugin.Builtin.Hardhat;

namespace Meadow.Plugin;

public class PluginLoader : IPlugin
{
    private const string PluginDllPattern = "*Plugin.*.dll";
    public static PluginLoader Default = new();
    private readonly List<IPlugin> _plugins = new();

    public PluginLoader()
    {
        try
        {
            Init();
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            throw;
        }
    }

    public PluginServices Svc { get; } = new();

    public void OnEvmStateInit(PluginServices notUsed, ICheatCodeRegistry cheatCodeRegistry)
    {
        foreach (var plugin in _plugins)
            try
            {
                plugin.OnEvmStateInit(Svc, cheatCodeRegistry);
            }
            catch (NotImplementedException)
            {
                // ignored
            }
            catch (Exception ex)
            {
                Default.Svc.Log(ex.ToString());
            }
    }

    public void OnExecutionTraceAnalysis(PluginServices notUsed, IPatchExecutionTraceAnalysis e)
    {
        foreach (var plugin in _plugins)
            try
            {
                plugin.OnExecutionTraceAnalysis(Svc, e);
            }
            catch (NotImplementedException)
            {
                // ignored
            }
            catch (Exception ex)
            {
                Default.Svc.Log(ex.ToString());
            }
    }

    private void Init()
    {
        _plugins.Add(new HardhatPlugin());

        var dpath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        foreach (var dllPath in Directory.GetFiles(dpath ?? ".", PluginDllPattern))
            try
            {
                var asm = Assembly.LoadFile(dllPath);

                foreach (var exportedType in asm.GetExportedTypes())
                    if (exportedType.GetInterface("IPlugin") != null)
                    {
                        var plugin = (IPlugin)Activator.CreateInstance(exportedType, Array.Empty<object>());
                        _plugins.Add(plugin);
                        Trace.WriteLine($"Plugin loaded: {dllPath}");
                    }
            }
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
            }
    }
}