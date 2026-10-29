using System.IO;
using AICompanionsAska.Core;
using AICompanionsAska.Diagnostics;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace AICompanionsAska;

/// <summary>
/// Entrada do plugin. Nesta versão (0.1.0) o objetivo é apenas carregar,
/// registrar o ambiente e provar que o toolchain BepInEx 6 IL2CPP + net6.0
/// está correto.
/// </summary>
[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
public sealed class Plugin : BasePlugin
{
    internal static Plugin Instance { get; private set; } = null!;

    internal static ManualLogSource LogSource { get; private set; } = null!;

    internal static string PluginDirectory { get; private set; } = string.Empty;

    public override void Load()
    {
        Instance = this;
        LogSource = base.Log;
        PluginDirectory = Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? string.Empty;

        ModConfig.Bind(Config);
        EnvironmentReport.Log();

        LogSource.LogInfo($"[AIC] {PluginInfo.Name} loaded successfully.");
    }

    public override bool Unload()
    {
        LogSource.LogInfo($"[AIC] {PluginInfo.Name} unloading.");
        return true;
    }
}
