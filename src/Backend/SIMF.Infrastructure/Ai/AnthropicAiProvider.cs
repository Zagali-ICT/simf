// Tests: SIMF.Api.Tests/AnthropicAiProviderTests.cs
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Ai.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Ai;

/// <summary>D-484 — Anthropic (Claude) Messages-API provider. Reads
/// <c>ApiKey</c> / <c>BaseUrl</c> / <c>DefaultModel</c> / <c>AnthropicVersion</c>
/// / <c>DefaultMaxTokens</c> from <see cref="AiOptions.Anthropic"/>. An empty key
/// throws <c>AI_PROVIDER_NOT_CONFIGURED</c> (503). Mirrors
/// <see cref="OpenAiProvider"/>'s error handling + secret-safe logging, but speaks
/// Anthropic's wire format: <c>x-api-key</c> + <c>anthropic-version</c> headers, a
/// top-level <c>system</c>, and a <c>content[]</c> response.</summary>
internal sealed class AnthropicAiProvider(
    HttpClient httpClient,
    IOptions<AiOptions> options,
    ILogger<AnthropicAiProvider> logger) : IAiProvider
{
    public AiProvider Tag => AiProvider.Anthropic;

    public async Task<AiProviderResponse> CallAsync(
        AiProviderCall call, CancellationToken cancellationToken = default)
    {
        var opts = options.Value.Anthropic;
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            throw new ApiException(
                ErrorCodes.AiProviderNotConfigured, 503,
                "Anthropic provider is not configured.",
                "موفّر Anthropic غير مُهيّأ.");
        }

        var model = string.IsNullOrWhiteSpace(call.Model) ? opts.DefaultModel : call.Model;
        // Anthropic requires max_tokens on every request; fall back to the option.
        var maxTokens = call.MaxOutputTokens > 0 ? call.MaxOutputTokens : opts.DefaultMaxTokens;
        var url = opts.BaseUrl.TrimEnd('/') + "/v1/messages";
        var payload = new
        {
            model,
            max_tokens = maxTokens,
            temperature = call.Temperature,
            system = call.SystemPrompt,
            messages = new object[]
            {
                new { role = "user", content = call.UserPrompt },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("x-api-key", opts.ApiKey);
        request.Headers.Add("anthropic-version", opts.AnthropicVersion);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                // L5 (security) — never log the raw provider error body verbatim;
                // it can echo request content / secrets. Redact + cap (as OpenAi).
                var safeBody = AiAuditDetail.RedactValue(body);
                if (safeBody.Length > 500)
                {
                    safeBody = safeBody[..500] + "…";
                }

                logger.LogWarning(
                    "Anthropic call failed: {Status} {Body}", response.StatusCode, safeBody);
                throw new ApiException(
                    ErrorCodes.AiProviderFailed,
                    (int)response.StatusCode,
                    "AI provider call failed.",
                    "فشل استدعاء موفّر الذكاء الاصطناعي.");
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;
            // Anthropic response: { content: [ { type:"text", text:"…" } ],
            //                       usage: { input_tokens, output_tokens } }.
            var text = new StringBuilder();
            if (root.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var block in content.EnumerateArray())
                {
                    if (block.TryGetProperty("type", out var type)
                        && type.GetString() == "text"
                        && block.TryGetProperty("text", out var txt))
                    {
                        text.Append(txt.GetString());
                    }
                }
            }
            int? tokensIn = null, tokensOut = null;
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("input_tokens", out var pin)
                    && pin.TryGetInt32(out var pinVal)) tokensIn = pinVal;
                if (usage.TryGetProperty("output_tokens", out var pout)
                    && pout.TryGetInt32(out var poutVal)) tokensOut = poutVal;
            }
            return new AiProviderResponse(text.ToString(), tokensIn, tokensOut);
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Anthropic call threw");
            throw new ApiException(
                ErrorCodes.AiProviderFailed, 502,
                "AI provider call failed.",
                "فشل استدعاء موفّر الذكاء الاصطناعي.");
        }
    }
}
