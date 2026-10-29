using System.IO;
using AICompanionsAska.Chat;
using AICompanionsAska.Core;
using AICompanionsAska.Diagnostics;
using AICompanionsAska.Patches;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace AICompanionsAska;

/// <summary>
/// Entrada do plugin. Em 0.1.x ele carrega, registra o ambiente e — desde a
/// branch <c>feat/chat-hook</c> — captura a mensagem endereçada do chat
/// (<c>ChatMenu.SendPlayerMessage</c>). Ainda não há LLM, resposta nem voz.
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

        // Patch mínimo, aplicado por nome dentro de try/catch (sem PatchAll).
        // ChatPatch registra o próprio sucesso/falha; isto não lança.
        ChatPatch.Apply();

        LogSource.LogInfo(
            $"[AIC] Enabled={ModConfig.Enabled.Value} | chat hook active (no LLM/voice yet) | " +
            $"ChatPrefix=\"{ChatAddressing.GetPrefix()}\" | AddressByName={ModConfig.AddressByName.Value} (reserved, not applied) | " +
            $"hook applied={ChatPatch.Applied}.");
        LogSource.LogInfo($"[AIC] {PluginInfo.Name} loaded successfully.");
    }

    public override bool Unload()
    {
        LogSource.LogInfo($"[AIC] {PluginInfo.Name} unloading.");

        ChatPatch.Unapply();
        ChatCapture.Clear();

        return true;
    }
}
