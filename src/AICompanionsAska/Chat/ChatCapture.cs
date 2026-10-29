using System;
using AICompanionsAska.Core;

namespace AICompanionsAska.Chat;

/// <summary>
/// Ponto único de entrada das mensagens já endereçadas. Nesta branch só loga e
/// guarda a última mensagem; a próxima fase (<c>feat/llm-intent</c>) consome
/// <see cref="TryGetLast"/> e enfileira o prompt.
/// <para>
/// Chamado da main thread (prefix Harmony do chat). Não bloqueia, não faz I/O
/// além do log e não toca em objetos do jogo.
/// </para>
/// </summary>
internal static class ChatCapture
{
    /// <summary>Limite de caracteres exibidos no log (não trunca o <see cref="ChatCommand"/>).</summary>
    private const int MaxLoggedChars = 300;

    private static readonly object Sync = new();
    private static ChatCommand _last;
    private static bool _hasLast;
    private static long _count;

    /// <summary>Quantas mensagens endereçadas já foram capturadas nesta sessão.</summary>
    internal static long Count
    {
        get
        {
            lock (Sync)
                return _count;
        }
    }

    /// <summary>
    /// Registra um comando endereçado. Log obrigatório (é o critério de aceite
    /// do probe) e guarda como <see cref="TryGetLast"/>.
    /// </summary>
    internal static void Submit(ChatCommand command)
    {
        try
        {
            lock (Sync)
            {
                _last = command;
                _hasLast = true;
                _count++;
            }

            AicLog.Flow("CHAT", $"addressed ({command.Text.Length} chars): \"{ForLog(command.Text)}\"");
        }
        catch (Exception ex)
        {
            // Nunca propagar para o trampoline do Harmony.
            AicLog.Error($"[CHAT] Submit failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Copia a última mensagem endereçada, se houver.</summary>
    internal static bool TryGetLast(out ChatCommand command)
    {
        lock (Sync)
        {
            command = _last;
            return _hasLast;
        }
    }

    /// <summary>Limpa o estado (saída do mundo / unload). Idempotente.</summary>
    internal static void Clear()
    {
        lock (Sync)
        {
            _last = default;
            _hasLast = false;
            _count = 0;
        }
    }

    /// <summary>
    /// Higiene de log: uma mensagem de chat não deve quebrar a linha do
    /// LogOutput.log nem inundá-lo. Mantém o comprimento contado em
    /// <see cref="ChatCommand.Text"/>, que é o valor do critério de aceite.
    /// </summary>
    private static string ForLog(string text)
    {
        string flat = text
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');

        return flat.Length <= MaxLoggedChars
            ? flat
            : flat.Substring(0, MaxLoggedChars) + "...";
    }
}
