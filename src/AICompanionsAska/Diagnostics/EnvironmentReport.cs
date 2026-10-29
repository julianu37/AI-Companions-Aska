using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace AICompanionsAska.Diagnostics;

/// <summary>
/// Relatório de boot: versões, caminhos e presença das interop assemblies.
/// Ajuda a diagnosticar instalação/versão antes de qualquer feature.
/// </summary>
internal static class EnvironmentReport
{
    internal static void Log()
    {
        var log = Plugin.LogSource;

        Safe(log, "mod", () => $"{PluginInfo.Name} {PluginInfo.Version}");
        Safe(log, "Unity", () => $"{Application.unityVersion} | produto {Application.productName} {Application.version}");
        Safe(log, "BepInEx", () => $"{Paths.BepInExVersion} | root {Paths.BepInExRootPath}");
        Safe(log, "jogo", () => Paths.GameRootPath);
        Safe(log, "plugin", () => Plugin.PluginDirectory);

        string bepInExRoot = SafeValue(() => Paths.BepInExRootPath, string.Empty);
        string interop = string.IsNullOrEmpty(bepInExRoot)
            ? string.Empty
            : Path.Combine(bepInExRoot, "interop");
        Safe(log, "interop", () => DescribeInterop(interop));

        try
        {
            log.LogInfo($"[AIC] SO {SystemInfo.operatingSystem} | CPU {SystemInfo.processorType} | GPU {SystemInfo.graphicsDeviceName}");
        }
        catch (Exception ex)
        {
            log.LogWarning($"[AIC] SystemInfo indisponivel: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string DescribeInterop(string interop)
    {
        if (string.IsNullOrEmpty(interop))
            return "AUSENTE (BepInExRootPath vazio)";

        if (!Directory.Exists(interop))
            return $"AUSENTE: {interop}";

        string asm = Path.Combine(interop, "Assembly-CSharp.dll");
        string mscorlib = Path.Combine(interop, "Il2Cppmscorlib.dll");
        bool hasAsm = File.Exists(asm);
        bool hasMscorlib = File.Exists(mscorlib);

        return hasAsm && hasMscorlib
            ? $"ok: {interop}"
            : $"INCOMPLETO: {interop} (Assembly-CSharp={hasAsm}, Il2Cppmscorlib={hasMscorlib})";
    }

    private static void Safe(BepInEx.Logging.ManualLogSource log, string area, Func<string> value)
    {
        try
        {
            log.LogInfo($"[AIC] {area} {value()}");
        }
        catch (Exception ex)
        {
            log.LogWarning($"[AIC] {area} indisponivel: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string SafeValue(Func<string> value, string fallback)
    {
        try
        {
            return value() ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }
}
