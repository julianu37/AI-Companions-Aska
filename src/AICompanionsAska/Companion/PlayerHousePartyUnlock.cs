using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using AICompanionsAska.Core;
using HarmonyLib;
using SSSGame;

namespace AICompanionsAska.Companion;

/// <summary>
/// Desbloqueio de desenvolvimento da party: a Cabana do Chefe nível 2
/// ("Cabana do Chefe Orgulhoso") é quem liga
/// <c>SSSGame.PlayerHouse.allowPartyManagement</c>. Com
/// <c>AI.DebugUnlockParty=true</c>, este probe força o bool para <c>true</c>
/// em toda <c>PlayerHouse</c>, permitindo criar a party cedo para testar as
/// ordens.
/// <para>
/// É opt-in e reversível: sem a config, nenhum patch é aplicado. Não mexe em
/// nível, receita, custo nem save; só no bool que a UI consulta.
/// </para>
/// </summary>
internal static class PlayerHousePartyUnlock
{
    private const string HarmonyId = PluginInfo.Guid + ".party-unlock";
    private const string PlayerHouseTypeName = "SSSGame.PlayerHouse";
    private const string FlagFieldName = "allowPartyManagement";

    private static readonly object Sync = new();
    private static readonly HashSet<int> Logged = new();
    private static Harmony? _harmony;

    internal static bool Applied { get; private set; }

    internal static void Apply()
    {
        if (ModConfig.DebugUnlockParty is not { Value: true })
            return;

        if (Applied)
            return;

        try
        {
            Type? type = ResolveGameType(PlayerHouseTypeName);
            if (type is null)
            {
                AicLog.Warn($"[PARTY] {PlayerHouseTypeName} not found; DebugUnlockParty ignored.");
                return;
            }

            MethodInfo? postfix = typeof(PlayerHousePartyUnlock).GetMethod(
                nameof(Postfix),
                BindingFlags.Static | BindingFlags.NonPublic);
            if (postfix is null)
            {
                AicLog.Error("[PARTY] internal error: PlayerHousePartyUnlock.Postfix not found.");
                return;
            }

            var harmony = new Harmony(HarmonyId);
            var applied = new List<string>();

            foreach (string methodName in new[] { "Initialize", "_OnActivate" })
            {
                MethodInfo? target = ResolveMethod(type, methodName);
                if (target is null)
                    continue;

                try
                {
                    harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                    applied.Add($"{PlayerHouseTypeName}.{methodName}");
                }
                catch (Exception ex)
                {
                    AicLog.Warn($"[PARTY] unlock patch {methodName} failed: {ex.GetType().Name}: {ex.Message}");
                }
            }

            if (applied.Count == 0)
            {
                AicLog.Warn("[PARTY] DebugUnlockParty: no PlayerHouse lifecycle method patched.");
                return;
            }

            _harmony = harmony;
            Applied = true;
            lock (Sync)
                Logged.Clear();

            AicLog.Flow("PARTY", $"DebugUnlockParty applied: {string.Join(", ", applied)}");
        }
        catch (Exception ex)
        {
            Applied = false;
            _harmony = null;
            AicLog.Error($"[PARTY] DebugUnlockParty failed: {ex.GetType().Name}: {ex.Message}");
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
            AicLog.Warn($"[PARTY] unlock unpatch failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _harmony = null;
            Applied = false;
            lock (Sync)
                Logged.Clear();
        }
    }

    private static void Postfix(object __instance)
    {
        try
        {
            if (__instance is null)
                return;

            bool oldValue;
            if (__instance is PlayerHouse house)
            {
                oldValue = house.allowPartyManagement;
                if (!oldValue)
                    house.allowPartyManagement = true;
            }
            else
            {
                // Fallback para um wrapper derivado: sobe a hierarquia.
                FieldInfo? field = FindField(__instance.GetType());
                if (field is null)
                {
                    AicLog.Warn(
                        $"[PARTY] {PlayerHouseTypeName}.{FlagFieldName} not found on " +
                        $"{__instance.GetType().FullName}; unlock ignored.");
                    return;
                }

                oldValue = (bool)(field.GetValue(__instance) ?? false);
                if (!oldValue)
                    field.SetValue(__instance, true);
            }

            int key = RuntimeHelpers.GetHashCode(__instance);
            bool first;
            lock (Sync)
                first = Logged.Add(key);

            if (first || ModConfig.VerboseLog is { Value: true })
            {
                AicLog.Flow(
                    "PARTY",
                    $"PlayerHouse allowPartyManagement: {oldValue} -> true (debug unlock, id={key})");
            }
        }
        catch (Exception ex)
        {
            AicLog.Warn($"[PARTY] unlock postfix failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static FieldInfo? FindField(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            FieldInfo? field = current.GetField(
                FlagFieldName,
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field is not null && field.FieldType == typeof(bool))
                return field;
        }

        return null;
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
}
