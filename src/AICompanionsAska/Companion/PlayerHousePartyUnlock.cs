using System;
using System.Collections.Generic;
using AICompanionsAska.Core;
using SSSGame;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AICompanionsAska.Companion;

/// <summary>
/// Desbloqueio de desenvolvimento da party. A Cabana do Chefe nível 2
/// ("Cabana do Chefe Orgulhoso") é quem liga
/// <c>SSSGame.PlayerHouse.allowPartyManagement</c>; com
/// <c>General/DebugUnlockParty=true</c> este arquivo arma um poll de main thread
/// que força o bool para <c>true</c> em um <c>PlayerHouse</c> já instanciado na
/// cena carregada — <b>depois</b> do load, nunca durante.
/// </summary>
/// <remarks>
/// Histórico (2026-10-29): a primeira versão patcheava
/// <c>PlayerHouse.Initialize</c> + <c>PlayerHouse._OnActivate</c> com Harmony e
/// derrubou o processo com <c>0xC0000005</c> em <c>GameAssembly.dll</c>
/// (RVA 0xF72955, dentro de <c>WeatherManager::Instance_OnWeatherUpdate</c>).
/// <para>
/// Causa raiz (RE, 2026-10-29): <c>_OnActivate</c> é um <b>thunk de 7 bytes
/// compartilhado por 29 métodos</b> (<c>WeatherManager.Awake</c>,
/// <c>TaskManager.Awake</c>, ...). O Harmony patcheia o endereço e entra nos 29;
/// como o detour materializa <c>this</c> sem validar a classe nativa,
/// <c>__instance is PlayerHouse</c> é sempre verdadeiro e a escrita caía no
/// offset do campo dentro de objetos de outras classes — corrupção de memória.
/// Reduzir o patch a <c>_OnActivate</c> não muda nada, e filtro de cena não
/// ajuda: o problema é tipo, não cena.
/// </para>
/// <para>
/// A versão atual não usa Harmony: (1) nenhum trampoline nativo para dar errado,
/// (2) <c>FindAnyObjectByType</c> só enxerga objetos de cena — prefab/template
/// fica de fora por construção, (3) a escrita espera o mundo estabilizar e é
/// idempotente, e (4) qualquer falha de interop desarma o poll com log, sem
/// derrubar o jogo. É opt-in; sem a config nada roda.
/// </para>
/// </remarks>
internal static class PlayerHousePartyUnlock
{
    /// <summary>Só para log: o tipo é referenciado direto, então um rename quebra no build.</summary>
    private const string PlayerHouseTypeName = "SSSGame.PlayerHouse";

    /// <summary>Espera desde o boot do plugin antes da primeira varredura (main thread).</summary>
    private const float MinSecondsAfterBoot = 5f;

    /// <summary>Tempo que a mesma casa precisa continuar "pronta" antes da escrita.</summary>
    private const float SettleSeconds = 5f;

    /// <summary>Poll apertado no começo: é onde o load de save termina.</summary>
    private const float PollIntervalSeconds = 1f;

    /// <summary>Depois disso o poll desacelera; a feature é dev-only e não pode custar caro.</summary>
    private const float SlowPollAfterSeconds = 60f;

    private const float SlowPollIntervalSeconds = 5f;

    private const int MaxTrackedHouses = 64;

    /// <summary>Motivo de o poll não ter escrito. Serve só para log de mudança de estado.</summary>
    private enum Reason
    {
        None = 0,
        NoHouse,
        Destroyed,
        NotInitialized,
        SceneNotReady,
        Settling,
        AlreadyUnlocked,
        AlreadyTrue,
    }

    private static readonly object Sync = new();
    private static readonly Dictionary<int, float> ReadySince = new();
    private static readonly HashSet<int> Unlocked = new();
    private static readonly HashSet<string> Warned = new();

    private static bool _enabled;
    private static float _bootTime = -1f;
    private static float _nextPoll;
    private static int _writes;

    // Só tocados na main thread (Tick/Attempt): sem lock e sem alloc por frame.
    private static int _diagId = -1;
    private static Reason _diagReason = Reason.None;

    /// <summary>Verdadeiro quando o poll está armado por esta sessão.</summary>
    internal static bool Applied { get; private set; }

    /// <summary>
    /// Lê a config e arma o poll. Chamado do <c>Plugin.Load</c> (main thread).
    /// Nada aqui toca no jogo: a primeira escrita só acontece em <see cref="Tick"/>.
    /// </summary>
    internal static void Apply()
    {
        try
        {
            if (Applied)
                return;

            if (ModConfig.DebugUnlockParty is not { Value: true })
                return;

            Reset();
            _enabled = true;
            Applied = true;

            AicLog.Flow(
                "PARTY",
                "DebugUnlockParty armed (no Harmony patch): " +
                "PlayerHouse.allowPartyManagement is forced only after the world is loaded.");
        }
        catch (Exception ex)
        {
            _enabled = false;
            Applied = false;
            AicLog.Error($"[PARTY] DebugUnlockParty failed to arm: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Desarma o poll (unload do plugin). Não desfaz o que já foi escrito: a
    /// segunda escrita seria outro toque em estado do jogo só para "limpar".
    /// </summary>
    internal static void Unapply()
    {
        if (!_enabled && !Applied)
            return;

        int writes = _writes;

        _enabled = false;
        Applied = false;
        Reset();

        AicLog.Flow(
            "PARTY",
            $"DebugUnlockParty disarmed ({writes} unlock(s) this session; " +
            "the bool keeps the value until the save/world reloads).");
    }

    /// <summary>
    /// Chamado pelo <see cref="AI.CompanionBrainRunner.Update"/> — main thread,
    /// uma vez por frame. Sai em O(1) enquanto não é hora do próximo poll.
    /// </summary>
    internal static void Tick()
    {
        if (!_enabled)
            return;

        try
        {
            // Se o jogador desligar a config (e o BepInEx recarregar), o mod para
            // de escrever sem precisar de restart.
            if (ModConfig.DebugUnlockParty is not { Value: true })
            {
                Disarm("General/DebugUnlockParty is no longer true.");
                return;
            }

            float now = Time.unscaledTime;
            if (_bootTime < 0f)
            {
                _bootTime = now;
                return;
            }

            float elapsed = now - _bootTime;
            if (elapsed < MinSecondsAfterBoot || now < _nextPoll)
                return;

            _nextPoll = now + (elapsed < SlowPollAfterSeconds
                ? PollIntervalSeconds
                : SlowPollIntervalSeconds);

            Attempt(force: false);
        }
        catch (Exception ex)
        {
            Disarm($"poll failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Tentativa manual (main thread), para um futuro comando de chat
    /// (<c>/c unlockparty</c>) e para teste. Ignora a espera inicial e o
    /// settle, mas mantém todos os gates de segurança. Devolve <c>true</c> se
    /// escreveu o bool agora.
    /// </summary>
    internal static bool TryUnlockNow()
    {
        if (!_enabled)
            return false;

        try
        {
            return Attempt(force: true);
        }
        catch (Exception ex)
        {
            Disarm($"manual unlock failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static bool Attempt(bool force)
    {
        PlayerHouse? house;
        try
        {
            // Só objetos de cena carregada: prefabs/templates nunca aparecem aqui,
            // ao contrário de um patch em Initialize. A variante singular é a que
            // casa com o interop — o overload plural muda o tipo de retorno
            // (T[] vs Il2CppArrayBase<T>) e estoura MissingMethodException.
            house = UnityEngine.Object.FindAnyObjectByType<PlayerHouse>();
        }
        catch (Exception ex)
        {
            Disarm($"FindAnyObjectByType<{PlayerHouseTypeName}>() failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }

        if (house is null)
        {
            Diagnose(0, Reason.NoHouse);
            return false;
        }

        // `PlayerHouse` deriva do MonoBehaviour real (unity-libs), então não há
        // `WasCollected`: o operador == do Unity é o que detecta objeto destruído.
        PlayerHouse target = house;
        if (target == null)
        {
            Diagnose(0, Reason.Destroyed);
            return false;
        }

        int id;
        try
        {
            id = target.GetInstanceID();
        }
        catch (Exception ex)
        {
            WarnOnce("instanceid", $"PlayerHouse.GetInstanceID failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }

        if (IsUnlocked(id))
        {
            Diagnose(id, Reason.AlreadyUnlocked);
            return false;
        }

        Structure? owner;
        try
        {
            owner = target.OwnerStructure;
        }
        catch (Exception ex)
        {
            WarnOnce("owner", $"PlayerHouse.OwnerStructure failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }

        if (owner is null)
        {
            // Initialize(Structure) ainda não rodou: a casa existe, mas não está pronta.
            Diagnose(id, Reason.NotInitialized);
            return false;
        }

        if (!TryGetLoadedScene(target, out Scene scene))
        {
            Diagnose(id, Reason.SceneNotReady);
            return false;
        }

        if (!force && !Settled(id, Time.unscaledTime))
        {
            Diagnose(id, Reason.Settling);
            return false;
        }

        bool previous;
        try
        {
            previous = target.allowPartyManagement;
        }
        catch (Exception ex)
        {
            WarnOnce("read", $"PlayerHouse.allowPartyManagement read failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }

        if (previous)
        {
            MarkUnlocked(id);
            Diagnose(id, Reason.AlreadyTrue);
            return false;
        }

        try
        {
            target.allowPartyManagement = true;
        }
        catch (Exception ex)
        {
            WarnOnce("write", $"PlayerHouse.allowPartyManagement write failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }

        MarkUnlocked(id);
        _writes++;
        _diagId = -1; // próxima leitura pode voltar a logar

        AicLog.Flow(
            "PARTY",
            $"PlayerHouse allowPartyManagement: False -> true " +
            $"(debug unlock, id={id}, scene=\"{SafeSceneName(scene)}\", " +
            $"trigger={(force ? "manual" : "post-load")}).");

        return true;
    }

    private static bool TryGetLoadedScene(PlayerHouse house, out Scene scene)
    {
        scene = default;

        try
        {
            GameObject? gameObject = house.gameObject;
            if (gameObject is null)
                return false;

            scene = gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }
        catch (Exception ex)
        {
            WarnOnce("scene", $"PlayerHouse scene check failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static bool Settled(int id, float now)
    {
        float since;
        lock (Sync)
        {
            if (!ReadySince.TryGetValue(id, out since))
            {
                if (ReadySince.Count >= MaxTrackedHouses)
                    ReadySince.Clear();

                ReadySince[id] = since = now;
            }
        }

        return now - since >= SettleSeconds;
    }

    private static bool IsUnlocked(int id)
    {
        lock (Sync)
            return Unlocked.Contains(id);
    }

    private static void MarkUnlocked(int id)
    {
        lock (Sync)
        {
            if (Unlocked.Count >= MaxTrackedHouses)
                Unlocked.Clear();

            Unlocked.Add(id);
        }
    }

    /// <summary>
    /// Log de mudança de estado (nada repetido por poll). É o que permite
    /// descobrir em jogo por que o unlock não aplicou.
    /// </summary>
    private static void Diagnose(int id, Reason reason)
    {
        if (id == _diagId && reason == _diagReason)
            return;

        _diagId = id;
        _diagReason = reason;

        AicLog.Debug($"[PARTY] unlock poll: id={id} state={reason}");
    }

    private static void WarnOnce(string code, string message)
    {
        lock (Sync)
        {
            if (!Warned.Add(code))
                return;
        }

        AicLog.Warn($"[PARTY] {message}");
    }

    /// <summary>
    /// Degradação: desarma e loga uma vez. No pior caso o unlock não acontece —
    /// o jogo segue normal.
    /// </summary>
    private static void Disarm(string reason)
    {
        if (!_enabled)
            return;

        _enabled = false;
        Applied = false;

        AicLog.Warn($"[PARTY] DebugUnlockParty disabled: {reason}");

        lock (Sync)
        {
            ReadySince.Clear();
            Unlocked.Clear();
        }
    }

    private static void Reset()
    {
        lock (Sync)
        {
            ReadySince.Clear();
            Unlocked.Clear();
            Warned.Clear();
        }

        _bootTime = -1f;
        _nextPoll = 0f;
        _writes = 0;
        _diagId = -1;
        _diagReason = Reason.None;
    }

    private static string SafeSceneName(Scene scene)
    {
        try
        {
            return scene.name ?? "?";
        }
        catch (Exception)
        {
            return "?";
        }
    }
}
