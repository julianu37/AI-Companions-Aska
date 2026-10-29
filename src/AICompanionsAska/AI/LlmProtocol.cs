using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AICompanionsAska.AI;

internal enum LlmResponseMode
{
    JsonSchema,
    JsonObject,
    None,
}

/// <summary>
/// Peças puras do protocolo OpenAI-compatible: montagem de URL, schema e
/// payload, e o predicado de "o provedor recusou o formato". Sem BepInEx/Unity
/// para poder ser testado no harness de console.
/// </summary>
internal static class LlmProtocol
{
    internal static string BuildUrl(string baseUrl)
    {
        string url = (baseUrl ?? string.Empty).Trim();
        if (url.Length == 0)
            throw new InvalidOperationException("BaseUrl vazia");

        if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            return url;

        string trimmed = url.TrimEnd('/');
        var uri = new Uri(trimmed);

        if (uri.AbsolutePath is "" or "/")
            return trimmed + "/v1/chat/completions";

        return trimmed + "/chat/completions";
    }

    internal static string BuildSchemaJson(IReadOnlyList<string> actions)
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                action = new { type = "string", @enum = actions.ToArray() },
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

    internal static JsonObject BuildRequestBody(
        string model,
        float temperature,
        int maxTokens,
        string systemPrompt,
        string userText,
        LlmResponseMode mode,
        IReadOnlyList<string> actions)
    {
        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
            new JsonObject { ["role"] = "user", ["content"] = userText },
        };

        var body = new JsonObject
        {
            ["model"] = model,
            ["messages"] = messages,
            ["temperature"] = temperature,
            ["max_tokens"] = maxTokens,
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
                    ["schema"] = JsonNode.Parse(BuildSchemaJson(actions)),
                },
            };
        }
        else if (mode == LlmResponseMode.JsonObject)
        {
            body["response_format"] = new JsonObject { ["type"] = "json_object" };
        }

        return body;
    }

    /// <summary>
    /// O provedor só deve cair para o próximo formato quando a recusa cita o
    /// campo de formato. Um 400 de "model not found" ou um 500 não disparam a
    /// cadeia.
    /// </summary>
    internal static bool IsFormatRejection(int statusCode, string? body)
    {
        if (statusCode is not (400 or 422) || string.IsNullOrEmpty(body))
            return false;

        if (body.Contains("response_format", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("json_schema", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("json_object", StringComparison.OrdinalIgnoreCase))
            return true;

        return body.Contains("format", StringComparison.OrdinalIgnoreCase) &&
               body.Contains("json", StringComparison.OrdinalIgnoreCase);
    }
}
