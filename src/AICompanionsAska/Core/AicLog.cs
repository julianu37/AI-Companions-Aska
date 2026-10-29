namespace AICompanionsAska.Core;

/// <summary>
/// Logging com o prefixo do projeto. Use os helpers em vez de interpolar
/// structs do Unity diretamente (Vector3/Vector4 podem lancar
/// VerificationException no handler do BepInEx).
/// </summary>
internal static class AicLog
{
    internal static void Info(string message) => Plugin.LogSource.LogInfo($"[AIC] {message}");

    internal static void Warn(string message) => Plugin.LogSource.LogWarning($"[AIC] {message}");

    internal static void Error(string message) => Plugin.LogSource.LogError($"[AIC] {message}");

    internal static void Debug(string message)
    {
        if (ModConfig.VerboseLog is { Value: true })
            Plugin.LogSource.LogDebug($"[AIC] {message}");
    }

    internal static void Flow(string area, string message) =>
        Plugin.LogSource.LogInfo($"[AIC][{area}] {message}");
}
