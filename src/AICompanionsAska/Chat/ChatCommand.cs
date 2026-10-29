using System;

namespace AICompanionsAska.Chat;

/// <summary>
/// De onde o texto veio. O hook de chat nasce com <see cref="Chat"/>; o
/// push-to-talk (Whisper) vai produzir <see cref="Voice"/> na fase de voz.
/// </summary>
internal enum ChatSource
{
    Chat,
    Voice,
}

/// <summary>
/// Uma entrada de texto do jogador depois do endereçamento.
/// <para>
/// <see cref="Raw"/> é o texto original (para diagnóstico),
/// <see cref="Text"/> é o texto sem o prefixo e já trimado (o que a próxima
/// fase vai enviar ao LLM). <see cref="Addressed"/> só é verdadeiro quando a
/// mensagem passou pelo filtro de endereçamento.
/// </para>
/// Imutável e barato de copiar (struct). Não guarda referência ao jogo.
/// </summary>
internal readonly struct ChatCommand
{
    internal ChatCommand(string raw, string text, bool addressed, ChatSource source, DateTime timestampUtc)
    {
        Raw = raw;
        Text = text;
        Addressed = addressed;
        Source = source;
        TimestampUtc = timestampUtc;
    }

    /// <summary>Texto como o jogo entregou (pode conter o prefixo).</summary>
    internal string Raw { get; }

    /// <summary>Texto sem o prefixo, já trimado.</summary>
    internal string Text { get; }

    /// <summary>Indica que a mensagem foi endereçada ao companheiro.</summary>
    internal bool Addressed { get; }

    /// <summary>Canal de origem (chat ou voz).</summary>
    internal ChatSource Source { get; }

    /// <summary>Momento da captura, em UTC (nunca local, para não depender do fuso).</summary>
    internal DateTime TimestampUtc { get; }

    /// <summary>Fábrica usada pelo endereçamento por prefixo do chat.</summary>
    internal static ChatCommand FromChat(string raw, string text) =>
        new(raw, text, addressed: true, ChatSource.Chat, DateTime.UtcNow);

    /// <summary>
    /// Resumo seguro para log: **não** inclui o texto (pode conter quebras de
    /// linha ou conversa privada). Use <c>ChatCapture</c> para o texto
    /// sanitizado e só em modo verbose. Chamado só em pontos de fluxo.
    /// </summary>
    public override string ToString() =>
        $"{(Addressed ? "addressed" : "plain")}/{Source} ({Text.Length} chars)";
}
