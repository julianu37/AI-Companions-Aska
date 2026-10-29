using System;
using System.Collections.Generic;
using System.Linq;

namespace AICompanionsAska.Chat;

/// <summary>
/// Reconhece uma mensagem que começa com o nome de um companheiro seguido de um
/// separador (<c>Thorin, ...</c>, <c>Thorin: ...</c>, <c>Thorin ...</c>).
/// Puro C# e conservador: o nome tem que ser **completo** e o resto não pode
/// ser vazio. Sem lista de nomes, nada é capturado.
/// </summary>
internal static class NameAddressing
{
    internal static bool TryStrip(
        string? text,
        IReadOnlyList<string> names,
        out string name,
        out string rest)
    {
        name = string.Empty;
        rest = string.Empty;

        if (string.IsNullOrWhiteSpace(text) || names.Count == 0)
            return false;

        string trimmed = text.Trim();

        // Nomes mais longos primeiro: "Thorin" vence "Thor".
        foreach (string candidate in names
                     .Where(n => !string.IsNullOrWhiteSpace(n))
                     .OrderByDescending(n => n.Trim().Length))
        {
            string wanted = candidate.Trim();
            if (wanted.Length == 0 || trimmed.Length <= wanted.Length)
                continue;

            if (!trimmed.StartsWith(wanted, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!IsSeparator(trimmed[wanted.Length]))
                continue;

            string body = trimmed[wanted.Length..]
                .TrimStart(' ', '\t', ',', ':', '-', ';', '.', '!', '?')
                .Trim();

            if (body.Length == 0)
                continue;

            name = wanted;
            rest = body;
            return true;
        }

        return false;
    }

    private static bool IsSeparator(char value) =>
        char.IsWhiteSpace(value) || value is ',' or ':' or '-' or ';' or '.' or '!' or '?';
}
