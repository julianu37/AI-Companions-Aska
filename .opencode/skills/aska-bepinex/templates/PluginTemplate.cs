using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace AICompanionsAska;

/// <summary>
/// Template mínimo de plugin BepInEx 6 IL2CPP para ASKA.
/// Copie para src/AICompanionsAska/Plugin.cs e ajuste.
/// </summary>
[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Log = null!;
    internal static Plugin Instance = null!;

    public override void Load()
    {
        Instance = this;
        Log = base.Log;

        ModConfig.Bind(Config);

        Log.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} carregando...");
        Log.LogInfo($"Unity {Application.unityVersion} | produto {Application.productName}");
        Log.LogInfo($"BepInEx: {BepInEx.Paths.BepInExRootPath}");
        Log.LogInfo($"{PluginInfo.Name} loaded successfully.");
    }

    public override bool Unload()
    {
        // Pare threads/filas, libere modelos e remova patches Harmony aqui.
        return true;
    }
}

internal static class PluginInfo
{
    public const string Guid = "julianu37.AICompanionsAska";
    public const string Name = "AI Companions - ASKA";
    public const string Version = "0.1.0";
}

internal static class ModConfig
{
    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<bool> VerboseLog = null!;
    internal static ConfigEntry<string> BaseUrl = null!;
    internal static ConfigEntry<string> Model = null!;
    internal static ConfigEntry<string> ApiKey = null!;

    internal static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("General", "Enabled", true,
            "Liga/desliga o mod.");
        VerboseLog = config.Bind("General", "VerboseLog", true,
            "Log detalhado (prompt/JSON). Desligue no release.");

        BaseUrl = config.Bind("AI", "BaseUrl", "http://127.0.0.1:1234/v1",
            "Endpoint OpenAI-compatible (LM Studio).");
        Model = config.Bind("AI", "Model", "qwen2.5-3b-instruct",
            "Modelo carregado no LM Studio.");
        ApiKey = config.Bind("AI", "ApiKey", "",
            "Chave do provedor. Vazia no LM Studio. Nunca commite.");
    }
}
