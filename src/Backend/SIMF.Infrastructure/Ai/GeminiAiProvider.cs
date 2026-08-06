// Tests: SIMF.Api.Tests/GeminiAiProviderTests.cs
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Ai.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Ai;

/// <summary>Google Gemini provider, speaking the
/// Generative Language API (<c>{BaseUrl}/v1beta/models/{model}:generateContent</c>).
/// Reads <c>ApiKey</c> / <c>BaseUrl</c> / <c>DefaultModel</c> / <c>DefaultMaxTokens</c>
/// from <see cref="AiOptions.Gemini"/>. An empty key throws
/// <c>AI_PROVIDER_NOT_CONFIGURED</c> (503). Mirrors <see cref="AnthropicAiProvider"/>
/// and <see cref="OpenAiProvider"/> for error handling + secret-safe logging, but
/// speaks Gemini's wire format: the key rides the <c>x-goog-api-key</c> header (kept
/// out of the URL so it never lands in logs), a top-level <c>system_instruction</c>,
/// a <c>contents[]</c> request and a <c>candidates[].content.parts[].text</c>
/// response. Under the hybrid policy this serves non-sensitive features in the
/// cloud; sensitive defense content stays on-prem via <see cref="OpenAiProvider"/>
/// pointed at a local <c>BaseUrl</c>.</summary>
internal sealed class GeminiAiProvider(
    HttpClient httpClient,
    IOptions<AiOptions> options,
    ILogger<GeminiAiProvider> logger) : IAiProvider
{
    public AiProvider Tag => AiProvider.Gemini;

    public async Task<AiProviderResponse> CallAsync(
        AiProviderCall call, CancellationToken cancellationToken = default)
    {
        var opts = options.Value.Gemini;
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            throw new ApiException(
                ErrorCodes.AiProviderNotConfigured, 503,
                "Gemini provider is not configured.",
                "موفّر Gemini غير مُهيّأ.");
        }

        var model = string.IsNullOrWhiteSpace(call.Model) ? opts.DefaultModel : call.Model;
        // Gemini wants the cap under generationConfig.maxOutputTokens; fall back to
        // the option when the prompt/call does not specify one.
        var maxTokens = call.MaxOutputTokens > 0 ? call.MaxOutputTokens : opts.DefaultMaxTokens;
        var url = $"{opts.BaseUrl.TrimEnd('/')}/v1beta/models/{model}:generateContent";
        var payload = new
        {
            system_instruction = new
            {
                parts = new object[] { new { text = call.SystemPrompt } },
            },
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[] { new { text = call.UserPrompt } },
                },
            },
            generationConfig = new
            {
                temperature = call.Temperature,
                maxOutputTokens = maxTokens,
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };
        // The key rides a header, never the query string — keeps it out of any
        // request-line logging on intermediaries / the singleton HttpClient.
        request.Headers.Add("x-goog-api-key", opts.ApiKey);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                // L5 (security) — never log the raw provider error body verbatim;
                // it can echo request content / secrets. Redact + cap (as OpenAi/Anthropic).
                var safeBody = AiAuditDetail.RedactValue(body);
                if (safeBody.Length > 500)
                {
                    safeBody = safeBody[..500] + "…";
                }

                logger.LogWarning(
                    "Gemini call failed: {Status} {Body}", response.StatusCode, safeBody);
                throw new ApiException(
                    ErrorCodes.AiProviderFailed,
                    (int)response.StatusCode,
                    "AI provider call failed.",
                    "فشل استدعاء موفّر الذكاء الاصطناعي.");
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;
            // Gemini response: { candidates:[ { content:{ parts:[ {text:"…"} ] } } ],
            //                    usageMetadata:{ promptTokenCount, candidatesTokenCount } }.
            var text = new StringBuilder();
            if (root.TryGetProperty("candidates", out var candidates)
                && candidates.ValueKind == JsonValueKind.Array
                && candidates.GetArrayLength() > 0)
            {
                var first = candidates[0];
                if (first.TryGetProperty("content", out var content)
                    && content.TryGetProperty("parts", out var parts)
                    && parts.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var txt))
                        {
                            text.Append(txt.GetString());
                        }
                    }
                }
            }
            int? tokensIn = null, tokensOut = null;
            if (root.TryGetProperty("usageMetadata", out var usage))
            {
                if (usage.TryGetProperty("promptTokenCount", out var pin)
                    && pin.TryGetInt32(out var pinVal)) tokensIn = pinVal;
                if (usage.TryGetProperty("candidatesTokenCount", out var pout)
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
            logger.LogError(ex, "Gemini call threw");
            throw new ApiException(
                ErrorCodes.AiProviderFailed, 502,
                "AI provider call failed.",
                "فشل استدعاء موفّر الذكاء الاصطناعي.");
        }
    }
}
