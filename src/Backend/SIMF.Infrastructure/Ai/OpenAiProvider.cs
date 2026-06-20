using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Ai.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Ai;

/// <summary>D-176 (gap doc G12) — OpenAI chat-completions provider.
/// Reads <c>ApiKey</c>, <c>BaseUrl</c>, <c>DefaultModel</c> from
/// <see cref="AiOptions"/>. If the API key is empty, throws
/// <c>AI_PROVIDER_NOT_CONFIGURED</c> immediately — the service layer
/// surfaces that as 503 to the caller. Network / parsing failures
/// raise <c>AI_PROVIDER_FAILED</c>; the service catches and persists
/// the error code on the invocation row.</summary>
internal sealed class OpenAiProvider(
    HttpClient httpClient,
    IOptions<AiOptions> options,
    ILogger<OpenAiProvider> logger) : IAiProvider
{
    public AiProvider Tag => AiProvider.OpenAi;

    public async Task<AiProviderResponse> CallAsync(
        AiProviderCall call, CancellationToken cancellationToken = default)
    {
        var opts = options.Value.OpenAi;
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            throw new ApiException(
                ErrorCodes.AiProviderNotConfigured, 503,
                "OpenAI provider is not configured.",
                "موفّر OpenAI غير مُهيّأ.");
        }

        var model = string.IsNullOrWhiteSpace(call.Model) ? opts.DefaultModel : call.Model;
        var url = opts.BaseUrl.TrimEnd('/') + "/chat/completions";
        var payload = new
        {
            model,
            temperature = call.Temperature,
            max_tokens = call.MaxOutputTokens,
            messages = new object[]
            {
                new { role = "system", content = call.SystemPrompt },
                new { role = "user", content = call.UserPrompt },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                // L5 (security) — never log the raw provider error body verbatim;
                // it can echo request content / secrets. Redact known secret/PII
                // patterns and cap the length.
                var safeBody = AiAuditDetail.RedactValue(body);
                if (safeBody.Length > 500)
                {
                    safeBody = safeBody[..500] + "…";
                }

                logger.LogWarning(
                    "OpenAI call failed: {Status} {Body}", response.StatusCode, safeBody);
                throw new ApiException(
                    ErrorCodes.AiProviderFailed,
                    (int)response.StatusCode,
                    "AI provider call failed.",
                    "فشل استدعاء موفّر الذكاء الاصطناعي.");
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;
            var content = root.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            int? tokensIn = null, tokensOut = null;
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var pin)
                    && pin.TryGetInt32(out var pinVal)) tokensIn = pinVal;
                if (usage.TryGetProperty("completion_tokens", out var pout)
                    && pout.TryGetInt32(out var poutVal)) tokensOut = poutVal;
            }
            return new AiProviderResponse(content, tokensIn, tokensOut);
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OpenAI call threw");
            throw new ApiException(
                ErrorCodes.AiProviderFailed, 502,
                "AI provider call failed.",
                "فشل استدعاء موفّر الذكاء الاصطناعي.");
        }
    }
}
