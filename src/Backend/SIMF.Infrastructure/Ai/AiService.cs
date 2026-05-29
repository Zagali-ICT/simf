// Tests: SIMF.Api.Tests/AiServiceTests.cs
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

        if (!providers.TryGetValue(prompt.Provider, out var provider))
        {
            await PersistFailureAsync(prompt, inputs, caller,
                ErrorCodes.AiProviderNotConfigured, latencyMs: 0,
                cancellationToken);
            throw new ApiException(
                ErrorCodes.AiProviderNotConfigured, 503,
                $"AI provider '{prompt.Provider}' is not registered.",
                $"موفّر الذكاء الاصطناعي '{prompt.Provider}' غير مسجَّل.");
        }

        var stopwatch = Stopwatch.StartNew();
        AiProviderResponse providerResponse;
        try
        {
            providerResponse = await provider.CallAsync(new AiProviderCall(
                Model: prompt.Model,
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

        var invocation = new AiInvocation
        {
            Id = Guid.NewGuid(),
            PromptKey = prompt.Key,
            Feature = prompt.Feature,
            Provider = prompt.Provider,
            Model = prompt.Model,
            InputJson = SerialiseInputs(inputs),
            OutputText = providerResponse.OutputText,
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

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AiInvocationSucceeded,
            Outcome = AuditOutcome.Success,
            ActorUserId = caller.UserId ?? Guid.Empty,
            Detail = $"promptKey={prompt.Key}; feature={prompt.Feature}; "
                + $"provider={prompt.Provider}; latencyMs={latencyMs}",
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
            InputJson = SerialiseInputs(inputs),
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
            Detail = $"promptKey={prompt.Key}; feature={prompt.Feature}; "
                + $"provider={prompt.Provider}; errorCode={errorCode}",
        }, cancellationToken);
    }

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

    private static string SerialiseInputs(IReadOnlyDictionary<string, string> inputs)
    {
        try
        {
            return JsonSerializer.Serialize(inputs);
        }
        catch
        {
            return "{}";
        }
    }
}
