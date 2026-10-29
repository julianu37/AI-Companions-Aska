using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AICompanionsAska.Core;

namespace AICompanionsAska.AI;

internal sealed class LlmResult
{
    private LlmResult(bool success, LlmIntent? intent, string error, long elapsedMs, LlmResponseMode mode)
    {
        Success = success;
        Intent = intent;
        Error = error;
        ElapsedMs = elapsedMs;
        Mode = mode;
    }

    internal bool Success { get; }

    internal LlmIntent? Intent { get; }

    internal string Error { get; }

    internal long ElapsedMs { get; }

    internal LlmResponseMode Mode { get; }

    internal static LlmResult Ok(LlmIntent intent, long elapsedMs, LlmResponseMode mode) =>
        new(true, intent, string.Empty, elapsedMs, mode);

    internal static LlmResult Fail(string error, long elapsedMs = 0, LlmResponseMode mode = LlmResponseMode.None) =>
        new(false, null, error, elapsedMs, mode);
}

/// <summary>
/// Cliente OpenAI-compatible (LM Studio por padrão). Roda em background; nunca
/// toca em objetos do Unity. A cadeia de formato tenta
/// <c>json_schema</c> -> <c>json_object</c> -> sem formato, uma vez cada, somente
/// quando o provedor recusa o formato (HTTP 400/422 citando o campo).
/// </summary>
internal static class LlmClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

    internal static async Task<LlmResult> CompleteAsync(
        string userText,
        string language,
        CancellationToken cancellationToken)
    {
        if (ModConfig.AiEnabled is { Value: false })
            return LlmResult.Fail("AI.Enabled=false");

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
            try
            {
                url = BuildUrl(ModConfig.BaseUrl.Value);
            }
            catch (Exception ex)
            {
                return LlmResult.Fail($"BaseUrl invalida: {ex.Message}");
            }

            string bodyJson = CompanionPrompt.BuildRequestBody(systemPrompt, userText, mode).ToJsonString();

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
            timeout.CancelAfter(TimeSpan.FromSeconds(seconds));

            var stopwatch = Stopwatch.StartNew();
            try
            {
                using HttpResponseMessage response = await Http.SendAsync(request, timeout.Token).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                stopwatch.Stop();
                lastElapsed = stopwatch.ElapsedMilliseconds;

                if (!response.IsSuccessStatusCode)
                {
                    lastError = $"HTTP {(int)response.StatusCode} ({response.StatusCode}): {Truncate(responseBody, 400)}";
                    lastMode = mode;

                    if (mode != LlmResponseMode.None && IsFormatRejection(response.StatusCode, responseBody))
                    {
                        AicLog.Flow("LLM", $"formato {mode} recusado (HTTP {(int)response.StatusCode}); tentando o proximo.");
                        continue;
                    }

                    return LlmResult.Fail(lastError, lastElapsed, mode);
                }

                if (!TryExtractContent(responseBody, out string content, out string extractError))
                {
                    lastError = extractError;
                    lastMode = mode;
                    return LlmResult.Fail(lastError, lastElapsed, mode);
                }

                if (!LlmIntent.TryParse(content, out LlmIntent intent, out string parseError))
                {
                    AicLog.Flow("LLM", $"conteudo nao parseavel (mode={mode}): {Truncate(content, 400)}");
                    lastError = $"parse: {parseError}";
                    lastMode = mode;
                    return LlmResult.Fail(lastError, lastElapsed, mode);
                }

                AicLog.Flow("LLM", $"HTTP 200 em {lastElapsed} ms | mode={mode} | {intent}");
                return LlmResult.Ok(intent, lastElapsed, mode);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return LlmResult.Fail("cancelado", lastElapsed, mode);
            }
            catch (OperationCanceledException)
            {
                lastError = $"timeout apos {seconds}s";
                lastMode = mode;
                return LlmResult.Fail(lastError, lastElapsed, mode);
            }
            catch (HttpRequestException ex)
            {
                return LlmResult.Fail($"rede: {ex.Message}", lastElapsed, mode);
            }
            catch (Exception ex)
            {
                return LlmResult.Fail($"{ex.GetType().Name}: {ex.Message}", lastElapsed, mode);
            }
        }

        return LlmResult.Fail(lastError, lastElapsed, lastMode);
    }

    internal static string BuildUrl(string baseUrl)
    {
        string url = (baseUrl ?? string.Empty).Trim();
        if (url.Length == 0)
            throw new InvalidOperationException("BaseUrl vazia");

        if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            return url;

        string trimmed = url.TrimEnd('/');
        if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("/v1/", StringComparison.OrdinalIgnoreCase))
            return trimmed + "/chat/completions";

        var uri = new Uri(trimmed);
        if (uri.AbsolutePath is "" or "/")
            return trimmed + "/v1/chat/completions";

        return trimmed + "/chat/completions";
    }

    private static bool IsFormatRejection(HttpStatusCode status, string body)
    {
        if (status is not (HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity))
            return false;

        if (string.IsNullOrEmpty(body))
            return false;

        return body.Contains("response_format", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("json_schema", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("json_object", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("format", StringComparison.OrdinalIgnoreCase);
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
