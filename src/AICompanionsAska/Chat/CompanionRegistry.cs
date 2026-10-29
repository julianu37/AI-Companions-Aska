using System;
using System.Collections.Generic;
using System.Linq;
using AICompanionsAska.Core;

namespace AICompanionsAska.Chat;

/// <summary>
/// Lista de nomes de companheiros disponíveis para o chat. Nesta fase os nomes
/// vêm de <c>AI.CompanionNames</c> (default "Thorin"); quando o recrutamento
/// via <c>PlayerParty</c> entrar, os villagers recrutados serão registrados
/// aqui. O primeiro nome é o destino padrão de <c>/c ...</c>.
/// </summary>
internal static class CompanionRegistry
{
    private static readonly object Sync = new();
    private static string[] _names = { "Thorin" };
    private static string _lastConfig = string.Empty;

    internal static IReadOnlyList<string> Names
    {
        get
        {
            Refresh();
            lock (Sync)
                return _names;
        }
    }

    internal static string DefaultName
    {
        get
        {
            Refresh();
            lock (Sync)
                return _names[0];
        }
    }

    /// <summary>
    /// Recalcula a lista a partir da config. Barato (string curta), chamado por
    /// mensagem para aceitar mudança no `.cfg` sem reiniciar.
    /// </summary>
    internal static void Refresh()
    {
        string raw = ModConfig.CompanionNames is { Value: not null }
            ? ModConfig.CompanionNames.Value
            : "Thorin";

        lock (Sync)
        {
            if (string.Equals(raw, _lastConfig, StringComparison.Ordinal))
                return;

            _lastConfig = raw;
            string[] parsed = raw
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            _names = parsed.Length > 0 ? parsed : new[] { "Thorin" };
        }
    }

    internal static bool TryResolve(string? text, out string name, out string rest)
    {
        Refresh();
        string[] snapshot;
        lock (Sync)
            snapshot = _names;

        return NameAddressing.TryStrip(text, snapshot, out name, out rest);
    }

    /// <summary>Chamado pelo recrutamento no futuro; mantém o nome na lista.</summary>
    internal static void Register(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        lock (Sync)
        {
            string trimmed = name.Trim();
            if (_names.Any(n => string.Equals(n, trimmed, StringComparison.OrdinalIgnoreCase)))
                return;

            var list = _names.ToList();
            list.Add(trimmed);
            _names = list.ToArray();
        }
    }
}
