using System;
using System.IO;
using AICompanionsAska.AI;
using AICompanionsAska.Chat;
using AICompanionsAska.Core;
using AICompanionsAska.Diagnostics;
using AICompanionsAska.Patches;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

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

    private static GameObject? _brainObject;
    private static bool _brainReady;

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

        // A ponte com o Update do Unity é necessária para drenar o LLM na main
        // thread. Se o ClassInjector falhar, o chat continua; o LLM fica off.
        try
        {
            ClassInjector.RegisterTypeInIl2Cpp<CompanionBrainRunner>();
            _brainObject = new GameObject("AIC_CompanionBrain");
            UnityEngine.Object.DontDestroyOnLoad(_brainObject);
            _brainObject.AddComponent<CompanionBrainRunner>();
            CompanionBrain.Start();
            _brainReady = true;
        }
        catch (Exception ex)
        {
            _brainReady = false;
            LogSource.LogError($"[AIC] LLM brain runner failed: {ex.GetType().Name}: {ex.Message}");
        }

        if (_brainReady &&
            (ModConfig.SelfTest is { Value: true } ||
             string.Equals(Environment.GetEnvironmentVariable("AIC_LLM_SELFTEST"), "1", StringComparison.Ordinal)))
            CompanionBrain.SelfTest();

        LogSource.LogInfo(
            $"[AIC] Enabled={ModConfig.Enabled.Value} | VerboseLog={ModConfig.VerboseLog.Value} | chat hook applied={ChatPatch.Applied} | " +
            $"ChatPrefix=\"{ChatAddressing.GetPrefix()}\" | AddressByName={ModConfig.AddressByName.Value} | " +
            $"CompanionNames=\"{ModConfig.CompanionNames.Value}\" | ReplyInChat={ModConfig.ReplyInChat.Value} | " +
            $"AI.Enabled={ModConfig.AiEnabled.Value} | model={ModConfig.Model.Value} | " +
            $"url={ModConfig.BaseUrl.Value} | structured={ModConfig.StructuredOutput.Value} | " +
            $"brain runner={(_brainReady ? "ok" : "failed")}.");
        LogSource.LogInfo($"[AIC] {PluginInfo.Name} loaded successfully.");
    }

    public override bool Unload()
    {
        LogSource.LogInfo($"[AIC] {PluginInfo.Name} unloading.");

        CompanionBrain.Stop();
        _brainReady = false;

        if (_brainObject != null)
        {
            UnityEngine.Object.Destroy(_brainObject);
            _brainObject = null;
        }

        ChatPatch.Unapply();
        ChatCapture.Clear();

        return true;
    }
}
