using System;
using AICompanionsAska.Core;

namespace AICompanionsAska.AI;

/// <summary>
/// Prompt do companheiro: persona + contrato, no idioma configurado. O schema e
/// o payload ficam em <see cref="LlmProtocol"/> (puros e testáveis).
/// <para>
/// O contrato é a última coisa do system prompt: é o que o modelo lê por
/// último antes de responder.
/// </para>
/// </summary>
internal static class CompanionPrompt
{
    internal static string ResolveLanguage()
    {
        string configured = ModConfig.Language is { Value: not null }
            ? ModConfig.Language.Value.Trim()
            : "pt-BR";

        if (configured.Equals("en", StringComparison.OrdinalIgnoreCase) ||
            configured.Equals("en-US", StringComparison.OrdinalIgnoreCase))
            return "en";

        // "auto" e "pt-BR" caem em pt-BR nesta fase. O idioma real do jogo
        // (não o do sistema operacional) entra na fase de localização; ver
        // docs/re/llm-intent.md.
        return "pt-BR";
    }

    internal static string BuildSystemPrompt(string language)
    {
        bool pt = language.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
        string persona = ModConfig.Personality is { Value: not null }
            ? ModConfig.Personality.Value.Trim()
            : string.Empty;

        if (persona.Length == 0)
            persona = pt ? "um companheiro leal, direto e prestativo" : "a loyal, direct and helpful companion";

        string actions = string.Join(", ", LlmIntent.ValidActions);

        if (pt)
        {
            return string.Join("\n", new[]
            {
                $"Você é {persona} em ASKA.",
                "Responda SEMPRE com um único objeto JSON, sem texto fora do JSON.",
                "Contrato: {\"action\":\"...\",\"reply\":\"...\",\"target\":null,\"resource\":null,\"amount\":null}",
                $"Ações permitidas: {actions}.",
                "- Em gather, \"amount\" é o total desejado (ex.: 10 madeiras).",
                "- Em attack/equip/drop, preencha \"target\" com o nome citado; se não houver, use null.",
                "- Em conversa, action = \"none\" e reply com uma frase curta (até 15 palavras).",
                "- Nunca invente itens, criaturas ou alvos.",
                "- O campo \"reply\" é falado em voz alta: uma frase curta.",
            });
        }

        return string.Join("\n", new[]
        {
            $"You are {persona} in ASKA.",
            "ALWAYS answer with a single JSON object, with no text outside the JSON.",
            "Contract: {\"action\":\"...\",\"reply\":\"...\",\"target\":null,\"resource\":null,\"amount\":null}",
            $"Allowed actions: {actions}.",
            "- In gather, \"amount\" is the total wanted (e.g. 10 wood).",
            "- In attack/equip/drop, fill \"target\" with the name the player said; use null if absent.",
            "- In conversation, action = \"none\" and reply with one short sentence (up to 15 words).",
            "- Never invent items, creatures or targets.",
            "- The \"reply\" field is spoken aloud: one short sentence.",
        });
    }
}
