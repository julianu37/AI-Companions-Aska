using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using AICompanionsAska.Chat;
using AICompanionsAska.Core;
using HarmonyLib;
using UnityEngine;

namespace AICompanionsAska.Companion;

/// <summary>
/// Probe read-only da <c>SSSGame.PlayerParty</c>. No <c>Spawned</c>/<c>Start</c>
/// a party costuma estar vazia (os membros vêm da desserialização); por isso
/// guardamos as instâncias e fazemos polling de 1 Hz no <c>Update</c> até os
/// nomes aparecerem. Registra os nomes no <see cref="CompanionRegistry"/>.
/// <para>
/// Não move ninguém; é o primeiro passo de <c>feat/recruit-follow</c>.
/// </para>
/// </summary>
internal static class CompanionPartyProbe
{
    private const string HarmonyId = PluginInfo.Guid + ".party";
    private const string PlayerPartyTypeName = "SSSGame.PlayerParty";
    private const float PollIntervalSeconds = 1f;
    private const float MaxPollSeconds = 90f;

    private static readonly object Sync = new();
    private static readonly List<object> Parties = new();
    private static readonly Dictionary<int, string> LastSignature = new();
    private static readonly Dictionary<int, float> FirstSeen = new();
    private static Harmony? _harmony;
    private static float _nextPoll;

    internal static bool Applied { get; private set; }

    internal static void Apply()
    {
        if (Applied)
            return;

        try
        {
            Type? partyType = ResolveGameType(PlayerPartyTypeName);
            if (partyType is null)
            {
                AicLog.Warn($"[PARTY] type not found: {PlayerPartyTypeName}; names will not be auto-registered.");
                return;
            }

            MethodInfo? postfix = typeof(CompanionPartyProbe).GetMethod(
                nameof(Postfix),
                BindingFlags.Static | BindingFlags.NonPublic);
            if (postfix is null)
            {
                AicLog.Error("[PARTY] internal error: CompanionPartyProbe.Postfix not found.");
                return;
            }

            var harmony = new Harmony(HarmonyId);
            var applied = new List<string>();

            foreach (string methodName in new[] { "Spawned", "Start" })
            {
                MethodInfo? target = ResolveMethod(partyType, methodName);
                if (target is null)
                    continue;

                try
                {
                    harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                    applied.Add($"{PlayerPartyTypeName}.{methodName}()");
                }
                catch (Exception ex)
                {
                    AicLog.Warn($"[PARTY] failed to patch {methodName}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            if (applied.Count == 0)
            {
                AicLog.Warn("[PARTY] no PlayerParty lifecycle method patched; names will not be auto-registered.");
                return;
            }

            _harmony = harmony;
            Applied = true;
            lock (Sync)
            {
                Parties.Clear();
                LastSignature.Clear();
                FirstSeen.Clear();
            }
            _nextPoll = 0f;

            AicLog.Flow("PARTY", $"probe applied: {string.Join(", ", applied)}");
        }
        catch (Exception ex)
        {
            Applied = false;
            _harmony = null;
            AicLog.Error($"[PARTY] probe failed to apply: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static void Unapply()
    {
        try
        {
            _harmony?.UnpatchSelf();
        }
        catch (Exception ex)
        {
            AicLog.Warn($"[PARTY] unpatch failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _harmony = null;
            Applied = false;
            lock (Sync)
            {
                Parties.Clear();
                LastSignature.Clear();
                FirstSeen.Clear();
            }
        }
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

        object[] snapshot;
        lock (Sync)
            snapshot = Parties.ToArray();

        foreach (object party in snapshot)
        {
            try
            {
                LogParty(party, force: false);
            }
            catch (Exception ex)
            {
                AicLog.Warn($"[PARTY] poll failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Para de pollar depois de um tempo; o probe já cumpriu o papel.
        lock (Sync)
        {
            foreach (int key in new List<int>(FirstSeen.Keys))
            {
                if (now - FirstSeen[key] > MaxPollSeconds)
                {
                    FirstSeen.Remove(key);
                    LastSignature.Remove(key);
                    Parties.RemoveAll(p => RuntimeHelpers.GetHashCode(p) == key);
                }
            }
        }
    }

    private static void Postfix(object __instance)
    {
        if (__instance is null)
            return;

        int key = RuntimeHelpers.GetHashCode(__instance);
        lock (Sync)
        {
            if (!FirstSeen.ContainsKey(key))
                FirstSeen[key] = Time.unscaledTime;

            bool known = false;
            foreach (object party in Parties)
            {
                if (RuntimeHelpers.GetHashCode(party) == key)
                {
                    known = true;
                    break;
                }
            }

            if (!known)
                Parties.Add(__instance);
        }

        try
        {
            LogParty(__instance, force: true);
        }
        catch (Exception ex)
        {
            AicLog.Warn($"[PARTY] probe failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void LogParty(object party, bool force)
    {
        int key = RuntimeHelpers.GetHashCode(party);
        object? pc = GetField(party, "_pc");
        string pcName = GetMemberName(pc) ?? "?";
        bool authority = ReadBool(party, "_hasAuthority");
        bool followQuest = GetField(party, "_followQuest") is not null;
        object? members = GetField(party, "_partyMembers");
        object? pets = GetField(party, "_partyPets");
        List<string> memberNames = GetListNames(members);
        List<string> petNames = GetListNames(pets);

        foreach (string name in memberNames)
            CompanionRegistry.Register(name);
        foreach (string name in petNames)
            CompanionRegistry.Register(name);

        string signature =
            $"pc={pcName}|authority={authority}|followQuest={followQuest}|" +
            $"members={memberNames.Count}|names={string.Join(",", memberNames)}|" +
            $"pets={petNames.Count}|petNames={string.Join(",", petNames)}";

        bool changed;
        lock (Sync)
        {
            changed = !LastSignature.TryGetValue(key, out string? previous) ||
                      !string.Equals(previous, signature, StringComparison.Ordinal);
            if (changed)
                LastSignature[key] = signature;
        }

        if (force || changed)
        {
            AicLog.Flow(
                "PARTY",
                $"id={key} {signature}");
        }
    }

    private static List<string> GetListNames(object? list)
    {
        var names = new List<string>();
        int count = GetCount(list);
        for (int i = 0; i < count; i++)
        {
            string? name = GetMemberName(GetItem(list, i));
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name.Trim());
        }

        return names;
    }

    private static Type? ResolveGameType(string fullName)
    {
        try
        {
            Type? direct = Type.GetType($"{fullName}, Assembly-CSharp", throwOnError: false);
            if (direct is not null)
                return direct;
        }
        catch (Exception)
        {
            // segue para a varredura pontual
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
                continue;

            try
            {
                Type? type = assembly.GetType(fullName, throwOnError: false);
                if (type is not null)
                    return type;
            }
            catch (Exception)
            {
                // stubs do Unity com tipos inválidos
            }
        }

        return null;
    }

    private static MethodInfo? ResolveMethod(Type type, string methodName)
    {
        try
        {
            MethodInfo? method = AccessTools.Method(type, methodName);
            if (method is not null)
                return method;
        }
        catch (Exception)
        {
            // ambíguo; cai para a varredura
        }

        return type.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }

    private static object? GetField(object instance, string name) =>
        instance.GetType()
            .GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(instance);

    private static bool ReadBool(object instance, string name)
    {
        object? value = GetField(instance, name);
        return value is bool flag && flag;
    }

    private static int GetCount(object? list)
    {
        if (list is null)
            return 0;

        try
        {
            object? count = list.GetType().GetProperty("Count")?.GetValue(list);
            if (count is int value)
                return value;

            object? viaMethod = list.GetType().GetMethod("get_Count")?.Invoke(list, null);
            return viaMethod is int methodValue ? methodValue : 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static object? GetItem(object? list, int index)
    {
        if (list is null)
            return null;

        try
        {
            PropertyInfo? item = list.GetType().GetProperty("Item");
            if (item is not null)
                return item.GetValue(list, new object[] { index });

            MethodInfo? getItem = list.GetType().GetMethod("get_Item", new[] { typeof(int) });
            return getItem?.Invoke(list, new object[] { index });
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? GetMemberName(object? member)
    {
        if (member is null)
            return null;

        try
        {
            MethodInfo? getName = member.GetType().GetMethod(
                "GetName",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            if (getName?.Invoke(member, null) is string name && !string.IsNullOrWhiteSpace(name))
                return name;

            object? fallback = member.GetType()
                .GetProperty("name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(member);

            return fallback as string;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
