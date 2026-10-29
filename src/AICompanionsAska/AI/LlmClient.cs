using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AICompanionsAska.Chat;
using AICompanionsAska.Core;

namespace AICompanionsAska.AI;

internal sealed class LlmResult
{
    private LlmResult(bool success, LlmIntent? intent, string error, long elapsedMs,
        LlmResponseMode mode, ChatCommand command)
    {
        Success = success;
        Intent = intent;
        Error = error;
        ElapsedMs = elapsedMs;
        Mode = mode;
        Command = command;
    }

    internal bool Success { get; }

    internal LlmIntent? Intent { get; }

    internal string Error { get; }

    internal long ElapsedMs { get; }

    internal LlmResponseMode Mode { get; }

    /// <summary>Comando original, para a próxima fase validar staleness/target.</summary>
    internal ChatCommand Command { get; }

    internal static LlmResult Ok(LlmIntent intent, ChatCommand command, long elapsedMs, LlmResponseMode mode) =>
        new(true, intent, string.Empty, elapsedMs, mode, command);

    internal static LlmResult Fail(string error, ChatCommand command, long elapsedMs = 0,
        LlmResponseMode mode = LlmResponseMode.None) =>
        new(false, null, error, elapsedMs, mode, command);
}

/// <summary>
/// Cliente OpenAI-compatible (LM Studio por padrão). Roda em background e
/// <b>não loga</b>: o <see cref="CompanionBrain.Drain"/> é quem escreve na main
/// thread (o DiskLogListener do BepInEx não é thread-safe). A cadeia de formato
/// tenta <c>json_schema</c> -> <c>json_object</c> -> sem formato, uma vez cada,
/// somente quando o provedor recusa o formato (HTTP 400/422 citando o campo).
/// </summary>
internal static class LlmClient
{
    private static readonly HttpClient DirectHttp = CreateClient(useProxy: false);
    private static readonly HttpClient ProxyHttp = CreateClient(useProxy: true);

    internal static async Task<LlmResult> CompleteAsync(
        ChatCommand command,
        string language,
        CancellationToken cancellationToken)
    {
        if (ModConfig.AiEnabled is { Value: false })
            return LlmResult.Fail("AI.Enabled=false", command);

        string systemPrompt = CompanionPrompt.BuildSystemPrompt(language);
        LlmResponseMode[] modes = ModConfig.StructuredOutput is { Value: true }
            ? new[] { LlmResponseMode.JsonSchema, LlmResponseMode.JsonObject, LlmResponseMode.None }
            : new[] { LlmResponseMode.None };

        string lastError = "sem tentativa";
        long lastElapsed = 0;
        LlmResponseMode lastMode = LlmResponseMode.None;

        foreach (LlmResponseMode mode in modes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string url;
            Uri uri;
            try
            {
                url = LlmProtocol.BuildUrl(ModConfig.BaseUrl.Value);
                uri = new Uri(url);
            }
            catch (Exception ex)
            {
                return LlmResult.Fail($"BaseUrl invalida: {ex.Message}", command);
            }

            string bodyJson = LlmProtocol.BuildRequestBody(
                ModConfig.Model.Value,
                ModConfig.Temperature.Value,
                ModConfig.MaxTokens.Value,
                systemPrompt,
                command.Text,
                mode,
                LlmIntent.ValidActions).ToJsonString();

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(bodyJson, Encoding.UTF8, "application/json"),
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            string apiKey = ModConfig.ApiKey?.Value ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(apiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            int seconds = Math.Max(5, ModConfig.TimeoutSeconds?.Value ?? 30);
            if (mode != LlmResponseMode.JsonSchema)
                seconds = Math.Min(seconds, 12); // fallback de formato deve ser rápido
            timeout.CancelAfter(TimeSpan.FromSeconds(seconds));

            var stopwatch = Stopwatch.StartNew();
            try
            {
                HttpClient client = uri.IsLoopback ? DirectHttp : ProxyHttp;
                using HttpResponseMessage response = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                stopwatch.Stop();
                lastElapsed = stopwatch.ElapsedMilliseconds;

                if (!response.IsSuccessStatusCode)
                {
                    lastError = $"HTTP {(int)response.StatusCode} ({response.StatusCode}): {Truncate(responseBody, 400)}";
                    lastMode = mode;

                    if (mode != LlmResponseMode.None && LlmProtocol.IsFormatRejection((int)response.StatusCode, responseBody))
                        continue;

                    return LlmResult.Fail(lastError, command, lastElapsed, mode);
                }

                if (!TryExtractContent(responseBody, out string content, out string extractError))
                {
                    lastError = extractError;
                    lastMode = mode;
                    return LlmResult.Fail(lastError, command, lastElapsed, mode);
                }

                if (!LlmIntent.TryParse(content, out LlmIntent intent, out string parseError))
                {
                    lastError = $"parse: {parseError} | content={Truncate(content, 400)}";
                    lastMode = mode;
                    return LlmResult.Fail(lastError, command, lastElapsed, mode);
                }

                return LlmResult.Ok(intent, command, lastElapsed, mode);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return LlmResult.Fail("cancelado", command, lastElapsed, mode);
            }
            catch (OperationCanceledException)
            {
                lastError = $"timeout apos {seconds}s";
                lastMode = mode;
                return LlmResult.Fail(lastError, command, lastElapsed, mode);
            }
            catch (HttpRequestException ex)
            {
                return LlmResult.Fail($"rede: {ex.Message}", command, lastElapsed, mode);
            }
            catch (Exception ex)
            {
                return LlmResult.Fail($"{ex.GetType().Name}: {ex.Message}", command, lastElapsed, mode);
            }
        }

        return LlmResult.Fail(lastError, command, lastElapsed, lastMode);
    }

    private static HttpClient CreateClient(bool useProxy)
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = useProxy,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

        return new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    private static bool TryExtractContent(string responseBody, out string content, out string error)
    {
        content = string.Empty;
        error = string.Empty;

        try
        {
            using JsonDocument document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("choices", out JsonElement choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                error = $"resposta sem choices: {Truncate(responseBody, 300)}";
                return false;
            }

            JsonElement first = choices[0];
            if (!first.TryGetProperty("message", out JsonElement message))
            {
                error = $"resposta sem message: {Truncate(responseBody, 300)}";
                return false;
            }

            if (message.TryGetProperty("refusal", out JsonElement refusal) &&
                refusal.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(refusal.GetString()))
            {
                error = $"modelo recusou: {Truncate(refusal.GetString()!, 300)}";
                return false;
            }

            if (!message.TryGetProperty("content", out JsonElement contentElement) ||
                contentElement.ValueKind != JsonValueKind.String)
            {
                error = $"resposta sem content string: {Truncate(responseBody, 300)}";
                return false;
            }

            content = contentElement.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content))
            {
                error = "content vazio";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"JSON da resposta invalido: {ex.Message}";
            return false;
        }
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
            return value ?? string.Empty;

        return value.Substring(0, max) + "...";
    }
}
