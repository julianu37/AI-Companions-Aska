using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AICompanionsAska.Chat;
using AICompanionsAska.Core;
using HarmonyLib;

namespace AICompanionsAska.Patches;

/// <summary>
/// Harmony no envio de chat do jogador local.
/// <para>
/// O alvo original (<c>SSSGame.UI.ChatMenu.SendPlayerMessage(string)</c>) foi
/// aplicado e verificado em jogo, mas <b>não disparou</b> no teste de
/// 2026-10-28: nessa build o submit do input parece passar por outro método
/// (ou o stub foi inlinado). Para não apostar num único ponto, esta versão
/// instrumenta três candidatos e deduplica a captura:
/// </para>
/// <list type="number">
/// <item><c>ChatMenu._OnTextInputSubmit(string)</c> — submit do campo de texto
/// (protegido; o caminho mais provável do Enter);</item>
/// <item><c>ChatMenu.SendPlayerMessage(string)</c> — wrapper público (mantido);</item>
/// <item><c>NetworkChatSession.SendInGameChatMessage(string)</c> — envio final
/// (fallback; pode ser chamado por mensagens do sistema, mas o filtro de prefixo
/// descarta o que não é comando).</item>
/// </list>
/// <para>
/// Sem <c>Harmony.PatchAll()</c> e sem atributos <c>[HarmonyPatch(typeof(jogo)...)]</c>:
/// cada tipo é resolvido por nome dentro de <c>try/catch</c> (ver
/// <c>docs/re/dead-ends.md</c>, item 8). O prefix só captura e degrada em
/// silêncio em qualquer erro; a mensagem original continua no chat do jogo.
/// </para>
/// </summary>
internal static class ChatPatch
{
    private const string HarmonyId = PluginInfo.Guid + ".chat";
    private const string ChatMenuTypeName = "SSSGame.UI.ChatMenu";
    private const string ChatSessionTypeName = "SSSGame.Network.NetworkChatSession";

    private static readonly (string TypeName, string MethodName)[] Targets =
    {
        (ChatMenuTypeName, "_OnTextInputSubmit"),
        (ChatMenuTypeName, "SendPlayerMessage"),
        (ChatSessionTypeName, "SendInGameChatMessage"),
    };

    // Só guarda a instância, sem processar texto. OnSetup costuma rodar quando
    // a UI do chat é montada; é o capture mais cedo possível.
    private static readonly (string TypeName, string MethodName)[] CaptureTargets =
    {
        (ChatMenuTypeName, "OnSetup"),
    };

    private static readonly object FiredLock = new();
    private static readonly HashSet<string> FiredTargets = new(StringComparer.Ordinal);
    private static bool _menuCapturedLogged;

    private static Harmony? _harmony;

    /// <summary>Verdadeiro enquanto ao menos um dos prefixes estiver anexado.</summary>
    internal static bool Applied { get; private set; }

    /// <summary>
    /// Resolve os alvos e anexa o prefix em cada um. Idempotente: chamar duas
    /// vezes não duplica o patch. Falha de um alvo não impede os outros; falha
    /// de todos vira log de erro, nunca exceção.
    /// </summary>
    internal static void Apply()
    {
        if (Applied)
        {
            AicLog.Flow("PATCH", "chat hook already applied; skipping.");
            return;
        }

        Harmony? harmony = null;
        try
        {
            MethodInfo? prefix = typeof(ChatPatch).GetMethod(
                nameof(Prefix),
                BindingFlags.Static | BindingFlags.NonPublic);
            if (prefix is null)
            {
                AicLog.Error("[PATCH] internal error: ChatPatch.Prefix(object, string, MethodBase) not found. Chat hook disabled.");
                return;
            }

            MethodInfo? capturePrefix = typeof(ChatPatch).GetMethod(
                nameof(CapturePrefix),
                BindingFlags.Static | BindingFlags.NonPublic);

            harmony = new Harmony(HarmonyId);
            var applied = new List<string>();
            var missing = new List<string>();

            foreach ((string typeName, string methodName) in Targets)
            {
                Type? type = ResolveGameType(typeName);
                if (type is null)
                {
                    missing.Add($"{typeName} (type)");
                    continue;
                }

                MethodInfo? target = ResolveMethod(type, methodName);
                if (target is null)
                {
                    missing.Add($"{typeName}.{methodName}(string)");
                    continue;
                }

                try
                {
                    harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                    applied.Add(DescribeSignature(target));
                }
                catch (Exception ex)
                {
                    missing.Add($"{typeName}.{methodName} ({ex.GetType().Name}: {ex.Message})");
                }
            }

            if (capturePrefix is not null)
            {
                foreach ((string typeName, string methodName) in CaptureTargets)
                {
                    Type? type = ResolveGameType(typeName);
                    if (type is null)
                    {
                        missing.Add($"{typeName} (type)");
                        continue;
                    }

                    MethodInfo? target = ResolveMethodAny(type, methodName);
                    if (target is null)
                    {
                        missing.Add($"{typeName}.{methodName}");
                        continue;
                    }

                    try
                    {
                        harmony.Patch(target, prefix: new HarmonyMethod(capturePrefix));
                        applied.Add(DescribeSignature(target));
                    }
                    catch (Exception ex)
                    {
                        missing.Add($"{typeName}.{methodName} ({ex.GetType().Name}: {ex.Message})");
                    }
                }
            }

            if (applied.Count == 0)
            {
                AicLog.Error($"[PATCH] no chat target could be patched. Missing: {string.Join("; ", missing)}. Chat hook disabled.");
                return;
            }

            _harmony = harmony;
            Applied = true;
            _menuCapturedLogged = false;
            lock (FiredLock)
                FiredTargets.Clear();

            bool registered = Harmony.HasAnyPatches(HarmonyId);
            AicLog.Flow(
                "PATCH",
                $"applied chat hook(s): {string.Join(", ", applied)} | id={HarmonyId} | harmony-registered={registered} | prefix=\"{ChatAddressing.GetPrefix()}\" | enabled={IsEnabled()}");

            if (missing.Count > 0)
                AicLog.Warn($"[PATCH] chat targets not found (ignored): {string.Join("; ", missing)}");

            if (!registered)
                AicLog.Warn("[PATCH] Harmony reported no patches for the chat hook id; capture may never fire.");

            AicLog.Flow("PATCH", "harmony-registered=True is NOT proof of firing; wait for a '... fired' line after a real chat submit.");
        }
        catch (Exception ex)
        {
            try
            {
                // Se o Patch() falhou no meio, remove o que chegou a ser anexado
                // antes de perder a referência (evita prefix duplicado num retry).
                harmony?.UnpatchSelf();
            }
            catch
            {
                // melhor um patch parcial do que mascarar a exceção original
            }

            Applied = false;
            _harmony = null;
            AicLog.Error($"[PATCH] failed to apply chat hook: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Remove os patches. Idempotente e seguro depois de um <see cref="Apply"/>
    /// que falhou.
    /// </summary>
    internal static void Unapply()
    {
        Harmony? harmony = _harmony;
        if (harmony is null)
        {
            Applied = false;
            ChatMenuCapture.Clear();
            CompanionChatReply.Reset();
            AicLog.Debug("[PATCH] chat hook was not applied; nothing to remove.");
            return;
        }

        try
        {
            harmony.UnpatchSelf();
            AicLog.Flow("PATCH", $"chat hook removed (UnpatchSelf, id={HarmonyId}).");
        }
        catch (Exception ex)
        {
            AicLog.Error($"[PATCH] failed to remove chat hook: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _harmony = null;
            Applied = false;
            _menuCapturedLogged = false;
            ChatMenuCapture.Clear();
            CompanionChatReply.Reset();
        }
    }

    /// <summary>
    /// Prefix curto: não altera o argumento, não cancela o original
    /// (<c>void</c>, sem <c>__runOriginal</c>) — a mensagem continua indo para o
    /// chat do jogo. Fire-verify por alvo na primeira execução; a captura é
    /// deduplicada por <see cref="ChatCapture"/> quando mais de um alvo dispara
    /// para o mesmo submit.
    /// </summary>
    private static void Prefix(object __instance, string __0, MethodBase __originalMethod)
    {
        try
        {
            string target = Describe(__originalMethod);

            // Guarda a instância viva do ChatMenu para o companheiro responder
            // no chat. Só os alvos declarados em ChatMenu têm essa instância.
            if (__instance is not null &&
                string.Equals(__originalMethod?.DeclaringType?.FullName, ChatMenuTypeName, StringComparison.Ordinal))
            {
                CapturePrefix(__instance);
            }

            // Fire-verify antes do gate: se o jogador desligou o mod no .cfg,
            // ainda queremos saber que o Harmony chegou ao método (e não que o
            // AOT inlinou o alvo).
            if (MarkFired(target))
                AicLog.Flow("PATCH", $"{target}(string) fired (first call; string arg received).");

            if (!IsEnabled())
                return;

            if (ChatAddressing.TryParse(__0, out ChatCommand command))
            {
                ChatCapture.Submit(command, target);
                return;
            }

            // Mensagem não endereçada: não processamos e NÃO logamos o conteúdo.
            if (ModConfig.VerboseLog is { Value: true })
                AicLog.Flow("CHAT", $"ignored on {target} ({__0?.Length ?? 0} chars)");
        }
        catch (Exception ex)
        {
            // O prefix vive dentro do trampoline do Harmony: nada pode escapar.
            AicLog.Error($"[CHAT] prefix failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool IsEnabled() => ModConfig.Enabled is { Value: true };

    /// <summary>Prefix mínimo para guardar a instância do ChatMenu (sem ler texto).</summary>
    private static void CapturePrefix(object __instance)
    {
        if (__instance is null)
            return;

        ChatMenuCapture.Set(__instance);
        if (!_menuCapturedLogged)
        {
            _menuCapturedLogged = true;
            AicLog.Flow("CHAT", "ChatMenu instance captured (reply target ready).");
        }
    }

    /// <summary>Assinatura resolvida, para o log de apply não mentir sobre o alvo.</summary>
    private static string DescribeSignature(MethodInfo method)
    {
        string parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
        return $"{method.DeclaringType?.FullName ?? "?"}.{method.Name}({parameters})";
    }

    private static string Describe(MethodBase? method)
    {
        if (method is null)
            return "unknown";

        string? type = method.DeclaringType?.Name;
        return string.IsNullOrEmpty(type) ? method.Name : $"{type}.{method.Name}";
    }

    private static bool MarkFired(string target)
    {
        lock (FiredLock)
            return FiredTargets.Add(target);
    }

    /// <summary>
    /// Resolve um tipo do jogo por nome, sem <c>AccessTools.TypeByName</c>: aquele
    /// caminho varre <c>Assembly.GetTypes()</c> de todas as assemblies e, nesta
    /// build, o stub de <c>UnityEngine.CoreModule</c> gera um
    /// <c>ReflectionTypeLoadException</c> barulhento (mas inofensivo) no HarmonyX.
    /// <c>Type.GetType</c> + <c>Assembly.GetType</c> são pontuais.
    /// </summary>
    private static Type? ResolveGameType(string fullName)
    {
        try
        {
            Type? direct = Type.GetType($"{fullName}, Assembly-CSharp", throwOnError: false);
            if (direct is not null)
                return direct;
        }
        catch (Exception)
        {
            // segue para a varredura pontual
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
                continue;

            try
            {
                Type? type = assembly.GetType(fullName, throwOnError: false);
                if (type is not null)
                    return type;
            }
            catch (Exception)
            {
                // assemblies com tipos inválidos (stubs do Unity) são ignoradas
            }
        }

        return null;
    }

    /// <summary>
    /// Resolve um método por nome sem exigir assinatura (usado no <c>OnSetup</c>,
    /// que recebe <c>MenuParameters</c>). Evita <c>typeof</c> de tipo do jogo.
    /// </summary>
    private static MethodInfo? ResolveMethodAny(Type type, string methodName)
    {
        try
        {
            MethodInfo? method = AccessTools.Method(type, methodName);
            if (method is not null)
                return method;
        }
        catch (Exception)
        {
            // ambíguo; cai para a varredura
        }

        return type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .FirstOrDefault(m => string.Equals(m.Name, methodName, StringComparison.Ordinal));
    }

    /// <summary>
    /// Resolve um método com um único <c>string</c>. Caminho normal:
    /// <see cref="AccessTools.Method(Type, string, Type[], Type[])"/>; se o
    /// interop mudar o tipo do parâmetro, cai para uma varredura de métodos
    /// declarados com um único <c>string</c>.
    /// </summary>
    private static MethodInfo? ResolveMethod(Type type, string methodName)
    {
        MethodInfo? exact = AccessTools.Method(type, methodName, new[] { typeof(string) });
        if (exact is not null)
            return exact;

        foreach (MethodInfo method in type.GetMethods(
                     BindingFlags.Public | BindingFlags.NonPublic |
                     BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                continue;

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
            {
                AicLog.Warn($"[PATCH] exact signature miss; using fallback {type.Name}.{method.Name}(string).");
                return method;
            }
        }

        return null;
    }
}
