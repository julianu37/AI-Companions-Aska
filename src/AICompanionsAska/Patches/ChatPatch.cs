using System;
using System.Collections.Generic;
using System.Reflection;
using AICompanionsAska.Chat;
using AICompanionsAska.Core;
using HarmonyLib;

namespace AICompanionsAska.Patches;

/// <summary>
/// Harmony no envio de chat do jogador local:
/// <c>SSSGame.UI.ChatMenu.SendPlayerMessage(string)</c>.
/// <para>
/// Por que este alvo: é público, tem assinatura simples (sem by-ref, sem tipos
/// de inventário) e é o ponto por onde passa a mensagem que o jogador digitou.
/// <c>_ReceivePlayerMessage(PlayerRef, string, bool)</c> é o caminho de
/// <b>recebimento</b> (outros jogadores) e <b>não</b> é patchado.
/// </para>
/// <para>
/// Sem <c>Harmony.PatchAll()</c> e sem atributos <c>[HarmonyPatch(typeof(jogo)...)]</c>:
/// o tipo é resolvido por nome dentro de <c>try/catch</c> (ver
/// <c>docs/re/dead-ends.md</c>, item 8). O prefix só captura e degrada em
/// silêncio em qualquer erro.
/// </para>
/// </summary>
internal static class ChatPatch
{
    private const string HarmonyId = PluginInfo.Guid + ".chat";
    private const string ChatMenuTypeName = "SSSGame.UI.ChatMenu";
    private const string TargetMethodName = "SendPlayerMessage";

    private static Harmony? _harmony;
    private static bool _firedOnce;

    /// <summary>Verdadeiro enquanto o prefix estiver anexado.</summary>
    internal static bool Applied { get; private set; }

    /// <summary>
    /// Resolve o alvo e anexa o prefix. Idempotente: chamar duas vezes não
    /// duplica o patch. Falha de resolução vira log de erro, nunca exceção.
    /// </summary>
    internal static void Apply()
    {
        if (Applied)
        {
            AicLog.Flow("PATCH", "chat hook already applied; skipping.");
            return;
        }

        try
        {
            Type? chatMenu = ResolveChatMenu();
            if (chatMenu is null)
            {
                AicLog.Error($"[PATCH] type not found: {ChatMenuTypeName}. Chat hook disabled; update the mod for this game build.");
                return;
            }

            MethodInfo? target = ResolveTarget(chatMenu);
            if (target is null)
            {
                AicLog.Error($"[PATCH] method not found: {ChatMenuTypeName}.{TargetMethodName}(string). Chat hook disabled.");
                return;
            }

            MethodInfo? prefix = typeof(ChatPatch).GetMethod(
                nameof(Prefix),
                BindingFlags.Static | BindingFlags.NonPublic);
            if (prefix is null)
            {
                AicLog.Error("[PATCH] internal error: ChatPatch.Prefix(string) not found. Chat hook disabled.");
                return;
            }

            var harmony = new Harmony(HarmonyId);
            harmony.Patch(target, prefix: new HarmonyMethod(prefix));

            _harmony = harmony;
            _firedOnce = false;
            Applied = true;

            bool verified = Harmony.HasAnyPatches(HarmonyId);
            AicLog.Flow(
                "PATCH",
                $"applied chat hook: {ChatMenuTypeName}.{TargetMethodName}(string) | id={HarmonyId} | verified={verified} | prefix=\"{ChatAddressing.GetPrefix()}\" | enabled={IsEnabled()}");

            if (!verified)
                AicLog.Warn("[PATCH] Harmony reported no patches for the chat hook id; capture may never fire.");
        }
        catch (Exception ex)
        {
            Applied = false;
            _harmony = null;
            AicLog.Error($"[PATCH] failed to apply chat hook: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Remove o patch. Idempotente e seguro depois de um <see cref="Apply"/>
    /// que falhou.
    /// </summary>
    internal static void Unapply()
    {
        Harmony? harmony = _harmony;
        if (harmony is null)
        {
            Applied = false;
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
        }
    }

    /// <summary>
    /// Prefix curto: não altera o argumento, não cancela o original
    /// (<c>void</c>, sem <c>__runOriginal</c>) — a mensagem continua indo para o
    /// chat do jogo. Fire-verify na primeira execução.
    /// </summary>
    private static void Prefix(string __0)
    {
        try
        {
            if (!IsEnabled())
                return;

            if (!_firedOnce)
            {
                _firedOnce = true;
                AicLog.Flow("PATCH", $"{ChatMenuTypeName}.{TargetMethodName} fired (first call; string arg received).");
            }

            if (ChatAddressing.TryParse(__0, out ChatCommand command))
            {
                ChatCapture.Submit(command);
                return;
            }

            // Mensagem não endereçada: não processamos e NÃO logamos o conteúdo.
            if (ModConfig.VerboseLog is { Value: true })
                AicLog.Flow("CHAT", $"ignored ({__0?.Length ?? 0} chars)");
        }
        catch (Exception ex)
        {
            // O prefix vive dentro do trampoline do Harmony: nada pode escapar.
            AicLog.Error($"[CHAT] prefix failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool IsEnabled() => ModConfig.Enabled is { Value: true };

    /// <summary>
    /// Resolve o tipo do chat sem <c>AccessTools.TypeByName</c>: aquele caminho
    /// varre <c>Assembly.GetTypes()</c> de todas as assemblies e, nesta build,
    /// o stub de <c>UnityEngine.CoreModule</c> gera um
    /// <c>ReflectionTypeLoadException</c> barulhento (mas inofensivo) no
    /// HarmonyX. <c>Type.GetType</c> + <c>Assembly.GetType</c> são pontuais.
    /// </summary>
    private static Type? ResolveChatMenu()
    {
        try
        {
            Type? direct = Type.GetType($"{ChatMenuTypeName}, Assembly-CSharp", throwOnError: false);
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
                Type? type = assembly.GetType(ChatMenuTypeName, throwOnError: false);
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
    /// Resolve <c>SendPlayerMessage(string)</c>. Caminho normal:
    /// <see cref="AccessTools.Method(Type, string, Type[], Type[])"/>; se o
    /// interop mudar o tipo do parâmetro, cai para uma varredura de métodos com
    /// um único <c>string</c> e loga o que achou.
    /// </summary>
    private static MethodInfo? ResolveTarget(Type chatMenu)
    {
        MethodInfo? exact = AccessTools.Method(chatMenu, TargetMethodName, new[] { typeof(string) });
        if (exact is not null)
            return exact;

        var candidates = new List<string>();
        foreach (MethodInfo method in chatMenu.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!string.Equals(method.Name, TargetMethodName, StringComparison.Ordinal))
                continue;

            ParameterInfo[] parameters = method.GetParameters();
            candidates.Add($"{method.Name}({parameters.Length} params)");
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
            {
                AicLog.Warn($"[PATCH] exact signature miss; using fallback {method.Name}(string).");
                return method;
            }
        }

        AicLog.Error($"[PATCH] no single-string overload of {ChatMenuTypeName}.{TargetMethodName}. Candidates: {(candidates.Count == 0 ? "none" : string.Join(", ", candidates))}");
        return null;
    }
}
