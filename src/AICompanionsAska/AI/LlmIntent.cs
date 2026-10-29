using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace AICompanionsAska.AI;

/// <summary>
/// Intenção devolvida pelo LLM. Puro C# (sem BepInEx/Unity) para poder ser
/// testado num harness de console e reutilizado pelo resto do mod.
/// <para>
/// O parser é deliberadamente tolerante: aceita JSON em code fence, texto ao
/// redor, nomes de propriedade em qualquer caixa, aliases em PT-BR/EN e
/// <c>amount</c> como número ou string. Ele <b>não</b> confia no modelo: ação
/// desconhecida vira erro e o chamador decide (hoje, não executa nada).
/// </para>
/// </summary>
internal sealed class LlmIntent
{
    /// <summary>Lista fechada de ações. Fonte única do enum do JSON schema.</summary>
    internal static readonly string[] ValidActions =
    {
        "follow", "stop", "come", "attack", "gather", "equip", "drop", "inventory", "none",
    };

    private static readonly Dictionary<string, string> ActionAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["follow"] = "follow",
            ["seguir"] = "follow",
            ["siga"] = "follow",
            ["acompanhar"] = "follow",
            ["stop"] = "stop",
            ["parar"] = "stop",
            ["pare"] = "stop",
            ["para"] = "stop",
            ["come"] = "come",
            ["vir"] = "come",
            ["venha"] = "come",
            ["vem"] = "come",
            ["attack"] = "attack",
            ["atacar"] = "attack",
            ["ataque"] = "attack",
            ["matar"] = "attack",
            ["kill"] = "attack",
            ["gather"] = "gather",
            ["coletar"] = "gather",
            ["colectar"] = "gather",
            ["colete"] = "gather",
            ["minerar"] = "gather",
            ["mine"] = "gather",
            ["chop"] = "gather",
            ["cortar"] = "gather",
            ["harvest"] = "gather",
            ["equip"] = "equip",
            ["equipar"] = "equip",
            ["equipe"] = "equip",
            ["pegar"] = "equip",
            ["take"] = "equip",
            ["drop"] = "drop",
            ["largar"] = "drop",
            ["largue"] = "drop",
            ["jogar"] = "drop",
            ["throw"] = "drop",
            ["descartar"] = "drop",
            ["inventory"] = "inventory",
            ["inventario"] = "inventory",
            ["inventário"] = "inventory",
            ["mochila"] = "inventory",
            ["bolsa"] = "inventory",
            ["bag"] = "inventory",
            ["none"] = "none",
            ["nenhum"] = "none",
            ["nenhuma"] = "none",
            ["conversa"] = "none",
            ["chat"] = "none",
            ["null"] = "none",
        };

    private static readonly Dictionary<string, string> ResourceAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["wood"] = "wood",
            ["madeira"] = "wood",
            ["lenha"] = "wood",
            ["tronco"] = "wood",
            ["log"] = "wood",
            ["stone"] = "stone",
            ["pedra"] = "stone",
            ["rocha"] = "stone",
            ["rock"] = "stone",
            ["ore"] = "ore",
            ["minerio"] = "ore",
            ["minério"] = "ore",
            ["iron"] = "ore",
            ["ferro"] = "ore",
            ["copper"] = "ore",
            ["cobre"] = "ore",
            ["tin"] = "ore",
            ["estanho"] = "ore",
            ["forage"] = "forage",
            ["forragear"] = "forage",
            ["forragem"] = "forage",
            ["food"] = "forage",
            ["comida"] = "forage",
            ["berry"] = "forage",
            ["fruta"] = "forage",
            ["mushroom"] = "forage",
            ["cogumelo"] = "forage",
        };

    private LlmIntent()
    {
    }

    internal string Action { get; private init; } = "none";

    internal string Reply { get; private init; } = string.Empty;

    internal string? Target { get; private init; }

    internal string? Resource { get; private init; }

    internal int? Amount { get; private init; }

    internal string RawJson { get; private init; } = string.Empty;

    internal static bool TryParse(string? content, out LlmIntent intent, out string error)
    {
        intent = new LlmIntent();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(content))
        {
            error = "conteudo vazio";
            return false;
        }

        string json = ExtractJson(content);
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });

            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "raiz do JSON nao e um objeto";
                return false;
            }

            string actionRaw = (ReadString(root, "action") ?? "none").Trim();
            string? reply = ReadString(root, "reply");
            string? target = Clean(ReadString(root, "target"));
            string? resource = Clean(ReadString(root, "resource"));
            int? amount = ReadAmount(root, "amount");

            string? action = NormalizeAction(actionRaw);

            // Modelos pequenos às vezes devolvem o recurso no campo action
            // (ex.: {"action":"wood","resource":"stone"}). O recurso específico
            // manda: vira gather e o resource é preservado.
            if (action is null && TryNormalizeResource(actionRaw, out string resourceFromAction))
            {
                action = "gather";
                resource ??= resourceFromAction;
            }

            if (action is null)
            {
                error = $"acao desconhecida: '{actionRaw}'";
                return false;
            }

            if (resource is not null)
                resource = NormalizeResource(resource);

            intent = new LlmIntent
            {
                Action = action,
                Reply = (reply ?? string.Empty).Trim(),
                Target = target,
                Resource = resource,
                Amount = amount,
                RawJson = json,
            };
            return true;
        }
        catch (JsonException ex)
        {
            error = $"JSON invalido: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    /// <summary>Resumo sem o texto falado (para log com VerboseLog=false).</summary>
    internal string Summary =>
        $"action={Action} target={Flat(Target)} resource={Flat(Resource)} " +
        $"amount={(Amount?.ToString(CultureInfo.InvariantCulture) ?? "-")}";

    /// <summary>Resumo seguro para log (sem quebras de linha em nenhum campo).</summary>
    public override string ToString()
    {
        string reply = Flat(Reply);
        if (reply.Length > 200)
            reply = reply.Substring(0, 200) + "...";

        return $"{Summary} reply=\"{reply}\"";
    }

    private static string Flat(string? value) =>
        string.IsNullOrEmpty(value)
            ? "-"
            : value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');

    private static string ExtractJson(string content)
    {
        string text = content.Trim();

        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0)
                text = text[(firstNewline + 1)..];

            int lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0)
                text = text[..lastFence];
        }

        // Recorta o primeiro objeto balanceado, ignorando chaves dentro de
        // strings (um texto depois do JSON não pode estragar o parse).
        int start = text.IndexOf('{');
        if (start < 0)
            return text.Trim();

        int depth = 0;
        bool inString = false;
        bool escaped = false;

        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return text[start..(i + 1)].Trim();
            }
        }

        return text[start..].Trim();
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (!TryGetProperty(root, name, out JsonElement property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => property.GetRawText(),
        };
    }

    private static int? ReadAmount(JsonElement root, string name)
    {
        if (!TryGetProperty(root, name, out JsonElement property))
            return null;

        double value;
        if (property.ValueKind == JsonValueKind.Number)
        {
            if (!property.TryGetDouble(out value))
                return null;
        }
        else if (property.ValueKind == JsonValueKind.String)
        {
            if (!double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return null;
        }
        else
        {
            return null;
        }

        if (value <= 0 || value > int.MaxValue)
            return null;

        return (int)Math.Round(value);
    }

    private static bool TryGetProperty(JsonElement root, string name, out JsonElement value)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string trimmed = value.Trim();
        if (string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase))
            return null;

        return trimmed;
    }

    private static string? NormalizeAction(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "none";

        string key = raw.Trim();
        return ActionAliases.TryGetValue(key, out string? action) ? action : null;
    }

    private static string NormalizeResource(string raw)
    {
        string key = raw.Trim();
        return ResourceAliases.TryGetValue(key, out string? resource)
            ? resource
            : key.ToLowerInvariant();
    }

    private static bool TryNormalizeResource(string raw, out string resource)
    {
        resource = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (!ResourceAliases.TryGetValue(raw.Trim(), out string? normalized))
            return false;

        resource = normalized;
        return true;
    }
}
