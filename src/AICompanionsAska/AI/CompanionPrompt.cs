using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using AICompanionsAska.Core;
using UnityEngine;

namespace AICompanionsAska.AI;

internal enum LlmResponseMode
{
    JsonSchema,
    JsonObject,
    None,
}

/// <summary>
/// Prompt e schema do contrato. O prompt é montado por mensagem (persona +
/// contrato), com o contrato no fim — é a última coisa que o modelo lê.
/// <para>
/// A persona e o idioma são resolvidos na <b>main thread</b> (o chamador passa o
/// idioma); o resto do request é montado no worker.
/// </para>
/// </summary>
internal static class CompanionPrompt
{
    internal static string ResolveLanguage()
    {
        try
        {
            string configured = ModConfig.Language is { Value: not null }
                ? ModConfig.Language.Value.Trim()
                : "auto";

            if (configured.Equals("pt-BR", StringComparison.OrdinalIgnoreCase) ||
                configured.Equals("pt", StringComparison.OrdinalIgnoreCase))
                return "pt-BR";

            if (configured.Equals("en", StringComparison.OrdinalIgnoreCase) ||
                configured.Equals("en-US", StringComparison.OrdinalIgnoreCase))
                return "en";

            string system = Application.systemLanguage.ToString();
            return system.StartsWith("Portuguese", StringComparison.OrdinalIgnoreCase) ? "pt-BR" : "en";
        }
        catch (Exception)
        {
            return "en";
        }
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

    internal static string BuildSchemaJson()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                action = new { type = "string", @enum = LlmIntent.ValidActions },
                reply = new { type = "string" },
                target = new { type = new[] { "string", "null" } },
                resource = new { type = new[] { "string", "null" } },
                amount = new { type = new[] { "integer", "null" } },
            },
            required = new[] { "action", "reply", "target", "resource", "amount" },
            additionalProperties = false,
        };

        return JsonSerializer.Serialize(schema);
    }

    internal static JsonObject BuildRequestBody(string systemPrompt, string userText, LlmResponseMode mode)
    {
        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
            new JsonObject { ["role"] = "user", ["content"] = userText },
        };

        var body = new JsonObject
        {
            ["model"] = ModConfig.Model.Value,
            ["messages"] = messages,
            ["temperature"] = ModConfig.Temperature.Value,
            ["max_tokens"] = ModConfig.MaxTokens.Value,
        };

        if (mode == LlmResponseMode.JsonSchema)
        {
            body["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject
                {
                    ["name"] = "companion_intent",
                    ["strict"] = true,
                    ["schema"] = JsonNode.Parse(BuildSchemaJson()),
                },
            };
        }
        else if (mode == LlmResponseMode.JsonObject)
        {
            body["response_format"] = new JsonObject { ["type"] = "json_object" };
        }

        return body;
    }
}
