using System;
using AICompanionsAska.Core;

namespace AICompanionsAska.Chat;

/// <summary>
/// Filtro de endereçamento do chat: só deixa passar a mensagem que o jogador
/// dirigiu explicitamente ao companheiro.
/// <para>
/// Nesta branch (<c>feat/chat-hook</c>) a única regra ativa é o
/// <c>AI.ChatPrefix</c> (default <c>/c</c>). O endereçamento por nome
/// (<c>AI.AddressByName</c>) é <b>reservado</b> e <b>não</b> captura nada ainda
/// — ver o TODO em <see cref="TryParse"/>.
/// </para>
/// Puro C#: não toca em tipos do jogo, não aloca por frame (só por mensagem) e
/// nunca lança em caso de config estranha.
/// </summary>
internal static class ChatAddressing
{
    /// <summary>
    /// Tenta transformar o texto bruto do chat em um comando endereçado.
    /// Regras, nesta ordem:
    /// <list type="number">
    /// <item>texto nulo/vazio (após trim) -> <c>false</c>;</item>
    /// <item>prefixo configurado vazio -> <c>false</c> (hook desligado);</item>
    /// <item>prefixo ausente (case-insensitive) -> <c>false</c>;</item>
    /// <item>o prefixo precisa terminar a string ou ser seguido de espaço
    /// (impede <c>/casa</c> virar comando);</item>
    /// <item>texto restante vazio -> <c>false</c>.</item>
    /// </list>
    /// </summary>
    /// <param name="raw">Texto recebido do jogo (pode ser nulo).</param>
    /// <param name="command">Comando pronto quando o retorno é <c>true</c>.</param>
    internal static bool TryParse(string? raw, out ChatCommand command)
    {
        command = default;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string message = raw!.Trim();

        string prefix = GetPrefix();
        if (prefix.Length == 0)
            return false; // AI.ChatPrefix vazio = captura desligada (intencional).

        if (!message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        // "/casa" não é comando; "/c casa" e "/c" (sozinho -> vira vazio abaixo) são.
        if (message.Length > prefix.Length && !char.IsWhiteSpace(message[prefix.Length]))
            return false;

        string text = message.Substring(prefix.Length).Trim();
        if (text.Length == 0)
            return false;

        // TODO(feat/chat-address-by-name): AddressByName está reservado.
        // Quando for implementado, comparar com o nome do companheiro de forma
        // conservadora (nome completo no início + separador) e só depois de
        // existir uma lista de companheiros recrutados. NÃO capture todas as
        // mensagens por nome enquanto isso: em co-op o chat é dos jogadores.

        command = ChatCommand.FromChat(raw!, text);
        return true;
    }

    /// <summary>
    /// Prefixo configurado, trimado. Config ainda não bindada (ou valor nulo)
    /// resulta em string vazia, isto é, captura desligada — o caminho seguro.
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
