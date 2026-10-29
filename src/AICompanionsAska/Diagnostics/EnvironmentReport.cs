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
        try
        {
            var log = Plugin.LogSource;
            string gameRoot = Paths.GameRootPath ?? "(desconhecido)";
            string bepInExRoot = Paths.BepInExRootPath ?? "(desconhecido)";
            string interop = Path.Combine(bepInExRoot, "interop");
            string pluginDir = Plugin.PluginDirectory;

            log.LogInfo($"[AIC] {PluginInfo.Name} {PluginInfo.Version}");
            log.LogInfo($"[AIC] Unity {Application.unityVersion} | produto {Application.productName} {Application.version}");
            log.LogInfo($"[AIC] BepInEx {Paths.BepInExVersion} | root {bepInExRoot}");
            log.LogInfo($"[AIC] jogo {gameRoot}");
            log.LogInfo($"[AIC] plugin {pluginDir}");
            log.LogInfo($"[AIC] interop {(Directory.Exists(interop) ? "ok" : "AUSENTE")}: {interop}");

            try
            {
                log.LogInfo($"[AIC] SO {SystemInfo.operatingSystem} | CPU {SystemInfo.processorType} | GPU {SystemInfo.graphicsDeviceName}");
            }
            catch (Exception ex)
            {
                log.LogWarning($"[AIC] SystemInfo indisponivel: {ex.GetType().Name}: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"[AIC] falha no relatorio de ambiente: {ex}");
        }
    }
}
