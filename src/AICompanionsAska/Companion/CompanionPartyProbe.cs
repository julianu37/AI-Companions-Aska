using System;
using System.Collections.Generic;
using AICompanionsAska.Chat;
using AICompanionsAska.Core;
using SSSGame;
using SSSGame.AI;
using UnityEngine;

namespace AICompanionsAska.Companion;

/// <summary>
/// Probe read-only da <c>SSSGame.PlayerParty</c>, 100% poll tipado.
/// <para>
/// Não usa Harmony: <c>Spawned</c>/<c>Start</c> podem ser thunks compartilhados
/// (como <c>PlayerHouse._OnActivate</c>, com 29 aliases) e a guarda
/// <c>is PlayerParty</c> não protege contra o wrapper de tipo declarado. O
/// <c>Tick</c> procura a party com <c>FindAnyObjectByType&lt;PlayerParty&gt;()</c>
/// (singular), lê as propriedades tipadas e registra os nomes no
/// <see cref="CompanionRegistry"/>.
/// </para>
/// <para>
/// No interop, <c>_pc</c>, <c>_partyMembers</c>, <c>_partyPets</c>,
/// <c>_hasAuthority</c> e <c>_followQuest</c> são <b>propriedades</b>, não campos.
/// O probe antigo usava <c>Type.GetField</c> e lia sempre nulo.
/// </para>
/// </summary>
internal static class CompanionPartyProbe
{
    private const float PollIntervalSeconds = 1f;

    private static readonly object Sync = new();
    private static readonly Dictionary<int, string> LastSignature = new();
    private static readonly HashSet<string> Warned = new();

    private static float _nextPoll;
    private static bool _loggedNoParty;

    internal static bool Applied { get; private set; }

    /// <summary>Chamado do <c>Plugin.Load</c>. Só inicializa estado; não patcheia nada.</summary>
    internal static void Apply()
    {
        if (Applied)
            return;

        try
        {
            if (ModConfig.Enabled is { Value: false })
            {
                AicLog.Flow("PARTY", "probe not armed (General/Enabled=false).");
                return;
            }

            lock (Sync)
            {
                LastSignature.Clear();
                Warned.Clear();
            }

            _nextPoll = 0f;
            _loggedNoParty = false;
            Applied = true;

            AicLog.Flow("PARTY", "probe armed (poll-only).");
        }
        catch (Exception ex)
        {
            Applied = false;
            AicLog.Error($"[PARTY] probe failed to arm: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static void Unapply()
    {
        bool wasApplied = Applied;
        Applied = false;

        lock (Sync)
        {
            LastSignature.Clear();
            Warned.Clear();
        }

        _loggedNoParty = false;

        if (wasApplied)
            AicLog.Flow("PARTY", "probe disarmed (poll-only).");
    }

    /// <summary>Chamado pelo <see cref="CompanionBrainRunner.Update"/> — main thread.</summary>
    internal static void Tick()
    {
        if (!Applied)
            return;

        float now = Time.unscaledTime;
        if (now < _nextPoll)
            return;
        _nextPoll = now + PollIntervalSeconds;

        PlayerParty? party;
        try
        {
            party = UnityEngine.Object.FindAnyObjectByType<PlayerParty>();
        }
        catch (Exception ex)
        {
            Disarm($"FindAnyObjectByType<PlayerParty>() failed: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        if (party == null)
        {
            if (!_loggedNoParty)
            {
                _loggedNoParty = true;
                AicLog.Flow("PARTY", "state=NoParty (poll-only); waiting for PlayerParty.");
            }

            return;
        }

        _loggedNoParty = false;

        int key;
        try
        {
            key = party.GetInstanceID();
        }
        catch (Exception ex)
        {
            Disarm($"PlayerParty.GetInstanceID() failed: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        try
        {
            LogParty(party, key);
        }
        catch (Exception ex)
        {
            AicLog.Warn($"[PARTY] poll failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void Disarm(string reason)
    {
        if (!Applied)
            return;

        Applied = false;

        lock (Sync)
        {
            LastSignature.Clear();
            Warned.Clear();
        }

        _loggedNoParty = false;

        AicLog.Warn($"[PARTY] probe disarmed (poll-only): {reason}");
    }

    private static void LogParty(PlayerParty party, int key)
    {
        // Cada leitura é independente: uma propriedade problemática não pode
        // esconder as outras nem inventar "false/0" no log.
        PlayerCharacter? pc = null;
        bool pcOk = false;
        try
        {
            pc = party._pc;
            pcOk = true;
        }
        catch (Exception ex)
        {
            WarnOnce(key, "_pc", ex);
        }

        string pcName = !pcOk
            ? "?"
            : (pc is null ? "null" : ReadName(() => pc!.GetName(), key, "_pc.GetName"));

        bool authority = false;
        bool authorityOk = false;
        try
        {
            authority = party._hasAuthority;
            authorityOk = true;
        }
        catch (Exception ex)
        {
            WarnOnce(key, "_hasAuthority", ex);
        }

        PartyFollowQuest? followQuest = null;
        bool followOk = false;
        try
        {
            followQuest = party._followQuest;
            followOk = true;
        }
        catch (Exception ex)
        {
            WarnOnce(key, "_followQuest", ex);
        }

        Il2CppSystem.Collections.Generic.List<Villager>? members = null;
        bool membersOk = false;
        try
        {
            members = party._partyMembers;
            membersOk = true;
        }
        catch (Exception ex)
        {
            WarnOnce(key, "_partyMembers", ex);
        }

        Il2CppSystem.Collections.Generic.List<Pet>? pets = null;
        bool petsOk = false;
        try
        {
            pets = party._partyPets;
            petsOk = true;
        }
        catch (Exception ex)
        {
            WarnOnce(key, "_partyPets", ex);
        }

        int memberCount = 0;
        bool memberCountOk = false;
        string membersText;
        List<string> memberNames;
        if (!membersOk)
        {
            membersText = "?";
            memberNames = new List<string> { "?" };
        }
        else if (members is null)
        {
            membersText = "null";
            memberNames = new List<string>();
        }
        else
        {
            memberNames = ReadVillagerNames(members, key, out memberCount, out memberCountOk);
            membersText = memberCountOk ? memberCount.ToString() : "?";
        }

        int petCount = 0;
        bool petCountOk = false;
        string petsText;
        List<string> petNames;
        if (!petsOk)
        {
            petsText = "?";
            petNames = new List<string> { "?" };
        }
        else if (pets is null)
        {
            petsText = "null";
            petNames = new List<string>();
        }
        else
        {
            petNames = ReadPetNames(pets, key, out petCount, out petCountOk);
            petsText = petCountOk ? petCount.ToString() : "?";
        }

        bool hasMembers = membersOk && memberCountOk && memberCount > 0;
        bool hasPets = petsOk && petCountOk && petCount > 0;
        if (hasMembers || hasPets)
        {
            // Impede que o primeiro Refresh (com _lastConfig vazio) apague os
            // nomes que o probe acabou de registrar.
            try
            {
                CompanionRegistry.Refresh();
            }
            catch (Exception ex)
            {
                WarnOnce(key, "CompanionRegistry.Refresh", ex);
            }

            if (hasMembers)
            {
                foreach (string name in memberNames)
                    RegisterName(name);
            }

            if (hasPets)
            {
                foreach (string name in petNames)
                    RegisterName(name);
            }
        }

        string signature =
            $"pc={pcName}|authority={(authorityOk ? authority.ToString() : "?")}|" +
            $"followQuest={(followOk ? (followQuest is not null).ToString() : "?")}|" +
            $"members={membersText}|names={string.Join(",", memberNames)}|" +
            $"pets={petsText}|petNames={string.Join(",", petNames)}";

        bool changed;
        lock (Sync)
        {
            changed = !LastSignature.TryGetValue(key, out string? previous) ||
                      !string.Equals(previous, signature, StringComparison.Ordinal);
            if (changed)
                LastSignature[key] = signature;
        }

        if (changed)
            AicLog.Flow("PARTY", $"id={key} {signature}");
    }

    private static List<string> ReadVillagerNames(
        Il2CppSystem.Collections.Generic.List<Villager> list,
        int key,
        out int count,
        out bool countOk)
    {
        var names = new List<string>();
        count = 0;
        countOk = false;

        try
        {
            count = list.Count;
            countOk = true;
        }
        catch (Exception ex)
        {
            WarnOnce(key, "_partyMembers.Count", ex);
            return names;
        }

        for (int i = 0; i < count; i++)
        {
            Villager? villager = null;
            try
            {
                villager = list[i];
            }
            catch (Exception ex)
            {
                WarnOnce(key, $"_partyMembers[{i}]", ex);
                names.Add("?");
                continue;
            }

            names.Add(villager is null
                ? "null"
                : ReadName(() => villager!.GetName(), key, $"_partyMembers[{i}].GetName"));
        }

        return names;
    }

    private static List<string> ReadPetNames(
        Il2CppSystem.Collections.Generic.List<Pet> list,
        int key,
        out int count,
        out bool countOk)
    {
        var names = new List<string>();
        count = 0;
        countOk = false;

        try
        {
            count = list.Count;
            countOk = true;
        }
        catch (Exception ex)
        {
            WarnOnce(key, "_partyPets.Count", ex);
            return names;
        }

        for (int i = 0; i < count; i++)
        {
            Pet? pet = null;
            try
            {
                pet = list[i];
            }
            catch (Exception ex)
            {
                WarnOnce(key, $"_partyPets[{i}]", ex);
                names.Add("?");
                continue;
            }

            names.Add(pet is null
                ? "null"
                : ReadName(() => pet!.GetName(), key, $"_partyPets[{i}].GetName"));
        }

        return names;
    }

    private static string ReadName(Func<string?> getName, int key, string label)
    {
        try
        {
            string? name = getName();
            if (string.IsNullOrWhiteSpace(name))
                return "?";

            return Sanitize(name!);
        }
        catch (Exception ex)
        {
            WarnOnce(key, label, ex);
            return "?";
        }
    }

    private static void RegisterName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        if (name == "?" || string.Equals(name, "null", StringComparison.OrdinalIgnoreCase))
            return;

        CompanionRegistry.Register(name);
    }

    private static string Sanitize(string value)
    {
        string flat = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ')
            .Replace('|', '/')
            .Trim();

        return flat.Length == 0 ? "?" : flat;
    }

    private static void WarnOnce(int key, string member, Exception ex)
    {
        string warningKey = $"{key}:{member}";
        bool first;
        lock (Sync)
            first = Warned.Add(warningKey);

        if (first)
        {
            AicLog.Warn(
                $"[PARTY] read {member} failed for id={key}: " +
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
