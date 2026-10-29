using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AICompanionsAska.Chat;
using AICompanionsAska.Core;
using UnityEngine;

namespace AICompanionsAska.AI;

/// <summary>
/// Cérebro do companheiro: recebe as mensagens endereçadas do chat, chama o LLM
/// em background (uma por vez) e drena o resultado na main thread.
/// <para>
/// Nesta fase o resultado é apenas logado; a execução das ordens entra em
/// <c>feat/recruit-follow</c>. O <see cref="CompanionBrainRunner"/> é quem
/// chama <see cref="Drain"/> e <see cref="Pump"/> no <c>Update</c>.
/// </para>
/// </summary>
internal static class CompanionBrain
{
    private static readonly ConcurrentQueue<ChatCommand> Incoming = new();
    private static readonly ConcurrentQueue<LlmResult> Results = new();

    private static int _busy;
    private static bool _started;
    private static CancellationTokenSource? _cts;
    private static float _lastErrorLogAt;
    private static string _lastError = string.Empty;

    internal static void Start()
    {
        if (_started)
            return;

        _started = true;
        ChatCapture.Captured += OnCaptured;

        AicLog.Flow(
            "LLM",
            $"brain started | AI.Enabled={ModConfig.AiEnabled.Value} | model={ModConfig.Model.Value} | " +
            $"url={ModConfig.BaseUrl.Value} | structured={ModConfig.StructuredOutput.Value}");
    }

    internal static void Stop()
    {
        if (!_started)
            return;

        _started = false;
        ChatCapture.Captured -= OnCaptured;

        try
        {
            _cts?.Cancel();
        }
        catch (Exception)
        {
            // shutdown nao pode lancar
        }

        _cts = null;
        Interlocked.Exchange(ref _busy, 0);

        while (Incoming.TryDequeue(out _))
        {
        }

        while (Results.TryDequeue(out _))
        {
        }

        AicLog.Flow("LLM", "brain stopped.");
    }

    private static void OnCaptured(ChatCommand command)
    {
        if (ModConfig.AiEnabled is { Value: false })
            return;

        if (command.Source != ChatSource.Chat)
            return;

        Incoming.Enqueue(command);

        // Fila de chat nao deve crescer sem limite se o modelo estiver lento.
        while (Incoming.Count > 4)
            Incoming.TryDequeue(out _);
    }

    /// <summary>Chamado na main thread. Inicia no maximo uma requisicao por vez.</summary>
    internal static void Pump()
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            return;

        if (!Incoming.TryDequeue(out ChatCommand command))
        {
            Interlocked.Exchange(ref _busy, 0);
            return;
        }

        // Resolve o idioma na main thread (Application.systemLanguage) e passa
        // pronto para o worker.
        string language = CompanionPrompt.ResolveLanguage();
        var cts = new CancellationTokenSource();
        _cts = cts;

        _ = Task.Run(async () =>
        {
            LlmResult result;
            try
            {
                result = await LlmClient.CompleteAsync(command.Text, language, cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result = LlmResult.Fail($"{ex.GetType().Name}: {ex.Message}");
            }

            Results.Enqueue(result);
            Interlocked.Exchange(ref _busy, 0);
        });
    }

    /// <summary>Chamado na main thread. Loga (e, no futuro, executa) os resultados.</summary>
    internal static void Drain()
    {
        while (Results.TryDequeue(out LlmResult? result))
        {
            if (result is null)
                continue;

            if (result.Success && result.Intent is not null)
            {
                AicLog.Flow("LLM", $"intent em {result.ElapsedMs} ms (mode={result.Mode}): {result.Intent}");
                // Proxima fase: CompanionOrders.Run(result.Intent, command.Text)
                continue;
            }

            float now = Time.unscaledTime;
            if (!string.Equals(_lastError, result.Error, StringComparison.Ordinal) || now - _lastErrorLogAt > 10f)
            {
                _lastError = result.Error;
                _lastErrorLogAt = now;
                AicLog.Warn($"[LLM] falhou: {result.Error}");
            }
            else
            {
                AicLog.Debug($"[LLM] falhou (repetido): {result.Error}");
            }
        }
    }

    /// <summary>
    /// Teste fora do fluxo do chat, habilitado por <c>AI.SelfTest</c> ou
    /// <c>AIC_LLM_SELFTEST=1</c>. Injeta uma mensagem sintética no
    /// <see cref="ChatCapture"/>, exercitando a cadeia
    /// captura -> brain -> LLM -> drenagem (tudo menos o Harmony do chat).
    /// </summary>
    internal static void SelfTest()
    {
        if (ModConfig.AiEnabled is { Value: false })
        {
            AicLog.Flow("LLM-SELFTEST", "ignorado: AI.Enabled=false");
            return;
        }

        AicLog.Flow(
            "LLM-SELFTEST",
            $"enfileirando 'segue' via ChatCapture (language={CompanionPrompt.ResolveLanguage()}, model={ModConfig.Model.Value})");
        ChatCapture.Submit(ChatCommand.FromChat("/c segue", "segue"), "selftest");
    }
}
