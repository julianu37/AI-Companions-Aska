using System;
using System.Reflection;
using AICompanionsAska.Core;
using HarmonyLib;
using UnityEngine;

namespace AICompanionsAska.Chat;

/// <summary>
/// Adiciona a fala do companheiro no chat do jogo, com o nome dele como autor.
/// Usa <c>ChatMenu._AddNewTextLine(userName, message, color, newMessage, isMe)</c>
/// por reflexão (sem referência compile-time ao tipo do jogo) na instância
/// capturada pelo <see cref="ChatMenuCapture"/>.
/// <para>
/// É uma linha **local** (o <c>isMe=false</c> apenas escolhe o lado); em co-op
/// ela não é replicada para os outros jogadores. O texto também vai para o log,
/// respeitando <c>VerboseLog</c>.
/// </para>
/// </summary>
internal static class CompanionChatReply
{
    private static readonly Color ReplyColor = new(0.86f, 0.78f, 0.55f, 1f);

    private static MethodInfo? _addLine;
    private static Type? _addLineType;

    internal static bool Say(string speaker, string text)
    {
        if (string.IsNullOrWhiteSpace(speaker) || string.IsNullOrWhiteSpace(text))
            return false;

        if (ModConfig.ReplyInChat is { Value: false })
            return false;

        object? menu = ChatMenuCapture.Instance;
        if (menu is null)
        {
            AicLog.Flow("REPLY", $"(sem ChatMenu) {speaker}: {LogText(text)}");
            return false;
        }

        // Se o mundo foi descarregado, o wrapper pode apontar para um objeto
        // destruído; o operador do Unity responde null.
        if (menu is UnityEngine.Object unityObject && unityObject == null)
        {
            ChatMenuCapture.Clear();
            AicLog.Flow("REPLY", $"(ChatMenu destruido) {speaker}: {LogText(text)}");
            return false;
        }

        try
        {
            MethodInfo? method = Resolve(menu.GetType());
            if (method is null)
            {
                AicLog.Warn("[REPLY] ChatMenu._AddNewTextLine(string,string,Color,bool,bool) not found.");
                return false;
            }

            method.Invoke(menu, new object?[] { speaker, text, ReplyColor, true, false });
            AicLog.Flow("REPLY", $"{speaker}: {LogText(text)}");
            return true;
        }
        catch (Exception ex)
        {
            AicLog.Error($"[REPLY] failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    internal static void Reset()
    {
        _addLine = null;
        _addLineType = null;
    }

    private static MethodInfo? Resolve(Type chatMenuType)
    {
        if (_addLine is not null && _addLineType == chatMenuType)
            return _addLine;

        _addLineType = chatMenuType;
        _addLine = AccessTools.Method(
            chatMenuType,
            "_AddNewTextLine",
            new[] { typeof(string), typeof(string), typeof(Color), typeof(bool), typeof(bool) });

        return _addLine;
    }

    private static string LogText(string text)
    {
        if (ModConfig.VerboseLog is { Value: true })
            return text;

        return $"({text.Length} chars)";
    }
}
