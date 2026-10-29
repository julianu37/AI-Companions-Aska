using System;
using AICompanionsAska.Core;

namespace AICompanionsAska.Chat;

/// <summary>
/// Filtro de endereçamento do chat. Duas formas, nesta ordem:
/// <list type="number">
/// <item><c>AI.ChatPrefix</c> (default <c>/c</c>) — vai para o companheiro padrão
/// (o primeiro de <c>AI.CompanionNames</c>);</item>
/// <item>nome do companheiro no início (<c>Thorin, ...</c> / <c>Thorin: ...</c>) —
/// só se <c>AI.AddressByName</c> estiver ligado e o nome existir no registro.</item>
/// </list>
/// Sem prefixo nem nome, a mensagem é ignorada (o chat do jogo não é sequestrado).
/// </summary>
internal static class ChatAddressing
{
    internal static bool TryParse(string? raw, out ChatCommand command)
    {
        command = default;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string message = raw!.Trim();

        string prefix = GetPrefix();
        if (prefix.Length > 0 &&
            message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            (message.Length == prefix.Length || char.IsWhiteSpace(message[prefix.Length])))
        {
            string text = message.Substring(prefix.Length).Trim();
            if (text.Length == 0)
                return false;

            command = ChatCommand.FromChat(raw!, text, CompanionRegistry.DefaultName);
            return true;
        }

        if (ModConfig.AddressByName is { Value: true } &&
            CompanionRegistry.TryResolve(message, out string name, out string body))
        {
            command = ChatCommand.FromChat(raw!, body, name);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Prefixo configurado, trimado. Config ainda não bindada (ou valor nulo)
    /// resulta em string vazia, isto é, captura por prefixo desligada — o
    /// caminho seguro.
    /// </summary>
    internal static string GetPrefix()
    {
        try
        {
            return ModConfig.ChatPrefix?.Value?.Trim() ?? string.Empty;
        }
        catch (Exception)
        {
            // ConfigEntry.Value não deve lançar; se lançar, ficamos desligados
            // em vez de derrubar o callback do chat.
            return string.Empty;
        }
    }
}
