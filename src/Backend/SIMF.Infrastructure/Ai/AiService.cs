// Tests: SIMF.Api.Tests/AiServiceTests.cs, SIMF.Api.Tests/AiHardeningTests.cs
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Ai.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Ai;
using SIMF.Domain.Ai;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Ai;

/// <summary>D-176 (gap doc G12) — centralised AI orchestrator. The
/// one path every feature endpoint goes through. Steps:
/// <list type="number">
/// <item>Look up the prompt by key (404 + 503 if missing / inactive).</item>
/// <item>Substitute inputs into both template fields.</item>
/// <item>Resolve the provider by <see cref="AiPrompt.Provider"/>.</item>
/// <item>Call the provider with timing.</item>
/// <item>Persist an <c>AiInvocation</c> row (success or failure both
/// land — failure mode includes the error code).</item>
/// <item>Write an audit event so SOC sees the call class.</item>
/// </list></summary>
internal sealed class AiService(
    SimfAppDbContext appDbContext,
    IReadOnlyDictionary<AiProvider, IAiProvider> providers,
    IOptions<AiOptions> aiOptions,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AiService> logger) : IAiService
{
    public async Task<AiCallResult> InvokeAsync(
        string promptKey,
        IReadOnlyDictionary<string, string> inputs,
        AiCallerContext caller,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(promptKey))
        {
            throw new ApiException(
                ErrorCodes.AiInputInvalid, 400,
                "Prompt key is required.",
                "مفتاح المحفّز مطلوب.");
        }
        // D-179 (review-pass): caps live at the IAiService chokepoint, not
        // only at AdminAiPromptService.TestAsync. The public feature endpoints
        // (FAQ, Assistance, Translate, live-translation, live-sign-language)
        // call InvokeAsync directly with raw user text — without this cap an
        // approved visitor can paste megabyte payloads or arbitrary-cardinality
        // dictionaries into the LLM call. Bounds: MaxInputsCount = 16 keys,
        // MaxInputKeyLength = 64 chars, MaxInputValueLength = 4000 chars.
        if (inputs.Count > MaxInputsCount)
        {
            throw new ApiException(
                ErrorCodes.AiInputInvalid, 400,
                $"Too many inputs (max {MaxInputsCount}).",
                $"عدد المدخلات يتجاوز الحدّ الأقصى ({MaxInputsCount}).");
        }
        foreach (var (key, value) in inputs)
        {
            if (key.Length > MaxInputKeyLength
                || (value?.Length ?? 0) > MaxInputValueLength)
            {
                throw new ApiException(
                    ErrorCodes.AiInputInvalid, 400,
                    $"Input key/value exceeds size limit "
                        + $"(key {MaxInputKeyLength}, value {MaxInputValueLength}).",
                    $"حجم المفتاح/القيمة يتجاوز الحدّ المسموح به.");
            }
        }

        var prompt = await appDbContext.AiPrompts.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Key == promptKey, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AiPromptNotFound, 404,
                $"AI prompt '{promptKey}' not found.",
                $"لم يتم العثور على محفّز الذكاء الاصطناعي '{promptKey}'.");
        if (!prompt.IsActive)
        {
            throw new ApiException(
                ErrorCodes.AiFeatureDisabled, 503,
                $"AI prompt '{promptKey}' is disabled.",
                $"محفّز الذكاء الاصطناعي '{promptKey}' معطّل.");
        }

        var systemPrompt = Substitute(prompt.SystemPrompt, inputs);
        var userPrompt = Substitute(prompt.UserPromptTemplate, inputs);

        // D-484 — an Echo-default prompt is redirected to the operator's
        // configured DefaultProvider (e.g. Anthropic) when one is set; a prompt
        // pinned to a concrete provider is honoured as-is.
        var effectiveProvider = AiProviderRouting.Effective(
            prompt.Provider, aiOptions.Value.DefaultProvider);
        if (!providers.TryGetValue(effectiveProvider, out var provider))
        {
            await PersistFailureAsync(prompt, inputs, caller,
                ErrorCodes.AiProviderNotConfigured, latencyMs: 0,
                cancellationToken);
            throw new ApiException(
                ErrorCodes.AiProviderNotConfigured, 503,
                $"AI provider '{effectiveProvider}' is not registered.",
                $"موفّر الذكاء الاصطناعي '{effectiveProvider}' غير مسجَّل.");
        }

        var modelForCall = ResolveModelForCall(prompt.Model, effectiveProvider);

        var stopwatch = Stopwatch.StartNew();
        AiProviderResponse providerResponse;
        try
        {
            providerResponse = await provider.CallAsync(new AiProviderCall(
                Model: modelForCall,
                SystemPrompt: systemPrompt,
                UserPrompt: userPrompt,
                Temperature: prompt.Temperature,
                MaxOutputTokens: prompt.MaxOutputTokens),
                cancellationToken);
        }
        catch (ApiException apiEx)
        {
            stopwatch.Stop();
            await PersistFailureAsync(prompt, inputs, caller,
                apiEx.Code, (int)stopwatch.ElapsedMilliseconds,
                cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex,
                "AI provider {Provider} for prompt {Key} threw unhandled",
                prompt.Provider, prompt.Key);
            await PersistFailureAsync(prompt, inputs, caller,
                ErrorCodes.AiProviderFailed, (int)stopwatch.ElapsedMilliseconds,
                cancellationToken);
            throw new ApiException(
                ErrorCodes.AiProviderFailed, 502,
                "AI provider call failed.",
                "فشل استدعاء موفّر الذكاء الاصطناعي.");
        }
        stopwatch.Stop();
        var latencyMs = (int)stopwatch.ElapsedMilliseconds;

        // D-185 — single-call redact+serialise+summarise so the audit
        // Detail carries the SIEM-canonical redactionKinds + count +
        // inputPreview (SIEM rules AI-005/007/008/009 depend on these).
        var redactedOutput = AiAuditDetail.RedactValue(providerResponse.OutputText);
        var redacted = AiAuditDetail.SerialiseAndRedactWithSummary(
            inputs, extraRedactedText: redactedOutput);

        var invocation = new AiInvocation
        {
            Id = Guid.NewGuid(),
            PromptKey = prompt.Key,
            Feature = prompt.Feature,
            Provider = prompt.Provider,
            Model = prompt.Model,
            // D-179 — redact common secret / PII patterns before
            // persistence so PII/keys never land raw in the DB
            // (was an unfulfilled promise — comment in AiInvocation.cs:20).
            // D-179 (review-pass) — also redact OutputText: an LLM that
            // echoes a user-pasted secret (or names a person verbatim
            // from RAG context) would otherwise persist it.
            InputJson = redacted.InputJson,
            OutputText = redactedOutput,
            TokensInput = providerResponse.TokensInput,
            TokensOutput = providerResponse.TokensOutput,
            LatencyMs = latencyMs,
            ErrorCode = null,
            CallerUserId = caller.UserId,
            CallerKind = caller.CallerKind,
            CreatedAt = timeProvider.GetUtcNow(),
        };
        appDbContext.AiInvocations.Add(invocation);
        await appDbContext.SaveChangesAsync(cancellationToken);

        // D-179 — JSON-shape the audit Detail so SIEM field-extracts
        // instead of regex-parsing free text. D-185 — added
        // redactionKinds/redactionCount/inputPreview so SIEM rules
        // AI-005/007/008/009 can field-extract instead of joining the
        // OperationLog row back to the AiInvocation row.
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AiInvocationSucceeded,
            Outcome = AuditOutcome.Success,
            ActorUserId = caller.UserId ?? Guid.Empty,
            Detail = AiAuditDetail.ToJson(new
            {
                promptKey = prompt.Key,
                feature = prompt.Feature.ToString(),
                provider = prompt.Provider.ToString(),
                model = prompt.Model,
                callerKind = caller.CallerKind,
                latencyMs,
                tokensInput = providerResponse.TokensInput,
                tokensOutput = providerResponse.TokensOutput,
                invocationId = invocation.Id,
                redactionKinds = redacted.RedactionKinds,
                redactionCount = redacted.RedactionCount,
                inputPreview = redacted.InputPreview,
            }),
        }, cancellationToken);

        return new AiCallResult(
            invocation.Id, prompt.Key, prompt.Feature, prompt.Provider,
            prompt.Model, providerResponse.OutputText,
            providerResponse.TokensInput, providerResponse.TokensOutput,
            latencyMs);
    }

    private async Task PersistFailureAsync(
        AiPrompt prompt,
        IReadOnlyDictionary<string, string> inputs,
        AiCallerContext caller, string errorCode, int latencyMs,
        CancellationToken cancellationToken)
    {
        var invocation = new AiInvocation
        {
            Id = Guid.NewGuid(),
            PromptKey = prompt.Key,
            Feature = prompt.Feature,
            Provider = prompt.Provider,
            Model = prompt.Model,
            // D-179 — redacted serialise on failure path too.
            InputJson = AiAuditDetail.SerialiseAndRedact(inputs),
            OutputText = null,
            LatencyMs = latencyMs,
            ErrorCode = errorCode,
            CallerUserId = caller.UserId,
            CallerKind = caller.CallerKind,
            CreatedAt = timeProvider.GetUtcNow(),
        };
        appDbContext.AiInvocations.Add(invocation);
        await appDbContext.SaveChangesAsync(cancellationToken);
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AiInvocationFailed,
            Outcome = AuditOutcome.Failure,
            ActorUserId = caller.UserId ?? Guid.Empty,
            Detail = AiAuditDetail.ToJson(new
            {
                promptKey = prompt.Key,
                feature = prompt.Feature.ToString(),
                provider = prompt.Provider.ToString(),
                model = prompt.Model,
                callerKind = caller.CallerKind,
                latencyMs,
                errorCode,
                invocationId = invocation.Id,
            }),
        }, cancellationToken);
    }

    // D-179 (review-pass) — single source of truth for AI input caps. The
    // numbers live in the shared SIMF.Contracts.Ai.AiInputLimits so producers
    // outside this assembly (the CP grounding builder) reference the same values;
    // these aliases keep the in-assembly references and the boundary tests
    // pointing at that one place.
    public const int MaxInputsCount = AiInputLimits.MaxInputsCount;
    public const int MaxInputKeyLength = AiInputLimits.MaxInputKeyLength;
    public const int MaxInputValueLength = AiInputLimits.MaxInputValueLength;

    // D-484 follow-up — every prompt is seeded with the sentinel Model="echo".
    // When routing redirects an Echo-default prompt to a REAL provider (D-484),
    // that literal would be sent to the vendor API as a model name and 404. Blank
    // it so the real provider substitutes its configured DefaultModel; an Echo call
    // keeps "echo" so its "[echo:echo]" label is unchanged. This is what lets a
    // go-live flip DefaultProvider + set a key without editing each prompt's Model.
    // (internal for the unit test in AiServiceModelResolutionTests.)
    internal static string ResolveModelForCall(string model, AiProvider effectiveProvider) =>
        effectiveProvider != AiProvider.Echo && IsEchoOrBlank(model)
            ? string.Empty
            : model;

    // True for the offline sentinel model (EchoAiProvider.ModelName) or an unset
    // model — the two cases where a real provider should fall back to its own
    // configured DefaultModel.
    private static bool IsEchoOrBlank(string? model) =>
        string.IsNullOrWhiteSpace(model)
        || string.Equals(model, EchoAiProvider.ModelName, StringComparison.OrdinalIgnoreCase);

    private static string Substitute(
        string template, IReadOnlyDictionary<string, string> inputs)
    {
        if (string.IsNullOrEmpty(template) || inputs.Count == 0)
        {
            return template ?? string.Empty;
        }
        var result = template;
        foreach (var (key, value) in inputs)
        {
            result = result.Replace("{" + key + "}", value ?? string.Empty,
                StringComparison.Ordinal);
        }
        return result;
    }

}
