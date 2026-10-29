using System;
using AICompanionsAska.Core;

namespace AICompanionsAska.Chat;

/// <summary>
/// Ponto único de entrada das mensagens já endereçadas. Nesta branch só loga e
/// guarda a última mensagem; a próxima fase (<c>feat/llm-intent</c>) consome
/// <see cref="TryGetLast"/> e enfileira o prompt.
/// <para>
/// Chamado da main thread (prefix Harmony do chat). Não bloqueia, não faz I/O
/// além do log e não toca em objetos do jogo. Como o patch instrumenta mais de
/// um candidato do mesmo submit, uma mensagem idêntica vinda de outro alvo
/// dentro de <see cref="DuplicateWindowMs"/> é descartada (evita captura dupla).
/// </para>
/// </summary>
internal static class ChatCapture
{
    /// <summary>Limite de caracteres exibidos no log (não trunca o <see cref="ChatCommand"/>).</summary>
    private const int MaxLoggedChars = 300;

    /// <summary>Janela para considerar dois alvos do mesmo submit como duplicata.</summary>
    private const double DuplicateWindowMs = 750;

    private static readonly object Sync = new();
    private static ChatCommand _last;
    private static bool _hasLast;
    private static long _count;
    private static long _duplicates;

    /// <summary>
    /// Disparado na main thread quando uma mensagem endereçada é aceita (não
    /// duplicada). O brain do LLM assina em <c>Plugin.Load</c>. É um evento
    /// gerenciado nosso, não um <c>Action</c> do jogo.
    /// </summary>
    internal static event Action<ChatCommand>? Captured;

    /// <summary>Quantas mensagens endereçadas já foram capturadas nesta sessão.</summary>
    internal static long Count
    {
        get
        {
            lock (Sync)
                return _count;
        }
    }

    /// <summary>Quantas capturas foram descartadas como duplicata do mesmo submit.</summary>
    internal static long Duplicates
    {
        get
        {
            lock (Sync)
                return _duplicates;
        }
    }

    /// <summary>
    /// Registra um comando endereçado. Log obrigatório (é o critério de aceite
    /// do probe) e guarda como <see cref="TryGetLast"/>.
    /// </summary>
    /// <param name="command">Comando já filtrado.</param>
    /// <param name="source">Alvo Harmony que disparou (para diagnóstico).</param>
    internal static void Submit(ChatCommand command, string source)
    {
        try
        {
            bool duplicate;
            lock (Sync)
            {
                duplicate =
                    _hasLast &&
                    string.Equals(_last.Text, command.Text, StringComparison.Ordinal) &&
                    (command.TimestampUtc - _last.TimestampUtc).TotalMilliseconds < DuplicateWindowMs;

                if (duplicate)
                {
                    _duplicates++;
                }
                else
                {
                    _last = command;
                    _hasLast = true;
                    _count++;
                }
            }

            if (duplicate)
            {
                AicLog.Flow("CHAT", $"duplicate from {source} ignored ({command.Text.Length} chars).");
                return;
            }

            // O texto do jogador só vai para o log em modo verbose; no release
            // apenas o comprimento, para não vazar conversa em bug reports.
            if (ModConfig.VerboseLog is { Value: true })
                AicLog.Flow("CHAT", $"addressed from {source} ({command.Text.Length} chars): \"{ForLog(command.Text)}\"");
            else
                AicLog.Flow("CHAT", $"addressed from {source} ({command.Text.Length} chars)");

            try
            {
                Captured?.Invoke(command);
            }
            catch (Exception ex)
            {
                // Um assinante com defeito não pode derrubar o trampoline do chat.
                AicLog.Error($"[CHAT] Captured subscriber failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            // Nunca propagar para o trampoline do Harmony.
            AicLog.Error($"[CHAT] Submit failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Devolve a última mensagem endereçada, ou <c>null</c> se ainda não houve
    /// nenhuma. Evita a armadilha de um <c>out</c> com <c>Text</c> nulo no
    /// consumidor da próxima fase.
    /// </summary>
    internal static ChatCommand? TryGetLast()
    {
        lock (Sync)
            return _hasLast ? _last : null;
    }

    /// <summary>Limpa o estado (saída do mundo / unload). Idempotente.</summary>
    internal static void Clear()
    {
        lock (Sync)
        {
            _last = default;
            _hasLast = false;
            _count = 0;
            _duplicates = 0;
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
