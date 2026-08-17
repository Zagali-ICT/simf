// Tests: SIMF.Api.Tests/AiAdminTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Ai.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Ai;
using SIMF.Domain.Ai;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Ai;

/// <summary>Admin CRUD over <see cref="AiPrompt"/>
/// + read-only invocations log. Writes bump <see cref="AiPrompt.Version"/>
/// and audit; deactivate is soft (<see cref="AiPrompt.IsActive"/> = false).</summary>
internal sealed class AdminAiPromptService(
    SimfAppDbContext appDbContext,
    IAiService aiService,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminAiPromptService> logger) : IAdminAiPromptService
{
    /// <summary>
    /// The grid contract for /admin/ai/prompts: one entry per key
    /// AiPromptsList.razor can send, plus the columns the page shows but does not
    /// yet sort on. The Arabic display name is declared so the free-text search
    /// still spans both languages the way the hand-written search did.
    /// </summary>
    private static readonly GridColumns<AiPrompt> PromptColumns = new GridColumns<AiPrompt>()
        .Add("key", prompt => prompt.Key, searchable: true)
        .Add("feature", prompt => prompt.Feature)
        .Add("displayName", prompt => prompt.DisplayName, searchable: true)
        .Add("displayNameArabic", prompt => prompt.DisplayNameArabic, searchable: true)
        .Add("provider", prompt => prompt.Provider)
        .Add("model", prompt => prompt.Model)
        .Add("version", prompt => prompt.Version)
        .Add("isActive", prompt => prompt.IsActive)
        .DefaultOrder("feature")
        .DefaultOrder("key")
        .PageSize(fallback: 25, max: 200);

    private static readonly Expression<Func<AiPrompt, AdminAiPromptSummary>> ToPromptSummary =
        prompt => new AdminAiPromptSummary(
            prompt.Id, prompt.Key, prompt.Feature, prompt.DisplayName, prompt.DisplayNameArabic,
            prompt.Provider, prompt.Model, prompt.Temperature, prompt.MaxOutputTokens,
            prompt.IsActive, prompt.Version, prompt.CreatedAt, prompt.UpdatedAt);

    public Task<GridPage<AdminAiPromptSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        appDbContext.AiPrompts.ToGridPageAsync(
            query, PromptColumns, prompt => prompt.Id, ToPromptSummary, cancellationToken);

    public async Task<AdminAiPromptDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await appDbContext.AiPrompts.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new AdminAiPromptDetail(
                p.Id, p.Key, p.Feature, p.DisplayName, p.DisplayNameArabic,
                p.Description, p.DescriptionArabic, p.Provider, p.Model,
                p.SystemPrompt, p.UserPromptTemplate,
                p.Temperature, p.MaxOutputTokens,
                p.IsActive, p.Version, p.CreatedAt, p.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AdminAiPromptDetail> CreateAsync(
        Guid actorUserId, CreateAiPromptRequest request,
        CancellationToken cancellationToken = default)
    {
        var validated = ValidateCreate(request);
        var existing = await appDbContext.AiPrompts.AsNoTracking()
            .AnyAsync(p => p.Key == validated.Key, cancellationToken);
        if (existing)
        {
            throw new ApiException(
                ErrorCodes.AiPromptKeyDuplicate, 409,
                $"AI prompt key '{validated.Key}' is already in use.",
                $"مفتاح المحفّز '{validated.Key}' مستخدم بالفعل.");
        }

        var prompt = new AiPrompt
        {
            Id = Guid.NewGuid(),
            Key = validated.Key,
            Feature = validated.Feature,
            DisplayName = validated.DisplayName,
            DisplayNameArabic = validated.DisplayNameArabic,
            Description = validated.Description,
            DescriptionArabic = validated.DescriptionArabic,
            Provider = validated.Provider,
            Model = validated.Model,
            SystemPrompt = validated.SystemPrompt,
            UserPromptTemplate = validated.UserPromptTemplate,
            Temperature = validated.Temperature,
            MaxOutputTokens = validated.MaxOutputTokens,
            IsActive = true,
            Version = 1,
            CreatedAt = timeProvider.SimfNow(),
            UpdatedByUserId = actorUserId,
        };
        appDbContext.AiPrompts.Add(prompt);
        await appDbContext.SaveChangesAsync(cancellationToken);

        // Structured JSON detail with prompt-content hash so
        // SOC can detect prompt drift across edits without storing
        // the raw text (may contain prompt-injection payloads).
        await auditLog.WriteSuccessAsync(
            AuditEvents.AiPromptCreated,
            actorUserId,
            AiAuditDetail.ToJson(new
            {
                promptId = prompt.Id,
                promptKey = prompt.Key,
                feature = prompt.Feature.ToString(),
                provider = prompt.Provider.ToString(),
                model = prompt.Model,
                version = prompt.Version,
                contentHash = AiAuditDetail.PromptContentHash(
                    prompt.SystemPrompt, prompt.UserPromptTemplate),
            }),
            cancellationToken);
        logger.LogInformation(
            "AI prompt {Key} created by {Actor}", prompt.Key, actorUserId);
        return ToDetail(prompt);
    }

    public async Task<AdminAiPromptDetail> UpdateAsync(
        Guid actorUserId, Guid id, UpdateAiPromptRequest request,
        CancellationToken cancellationToken = default)
    {
        var validated = ValidateUpdate(request);
        var prompt = await appDbContext.AiPrompts
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AiPromptNotFound, 404,
                "AI prompt not found.",
                "لم يتم العثور على محفّز الذكاء الاصطناعي.");

        // Capture old hash BEFORE mutation so the audit row
        // carries both old + new content hashes; SOC can flag any
        // prompt-text drift even without raw text access.
        var oldHash = AiAuditDetail.PromptContentHash(
            prompt.SystemPrompt, prompt.UserPromptTemplate);

        // Snapshot the PRE-mutation state into AiPromptHistory
        // so SOC + the CP history tab can reconstruct any prior
        // version after a drift detection. Captured BEFORE the
        // version bump; uses the current prompt.Version so the
        // snapshot's Version equals the live version that's about to
        // be replaced. The hash matches the `contentHashOld` value
        // the audit row will carry — SOC can correlate the audit
        // row and the history snapshot by hash.
        var now = timeProvider.SimfNow();
        appDbContext.AiPromptHistory.Add(new AiPromptHistory
        {
            Id = Guid.NewGuid(),
            AiPromptId = prompt.Id,
            Version = prompt.Version,
            SystemPrompt = prompt.SystemPrompt,
            UserPromptTemplate = prompt.UserPromptTemplate,
            Provider = prompt.Provider,
            Model = prompt.Model,
            Temperature = prompt.Temperature,
            MaxOutputTokens = prompt.MaxOutputTokens,
            IsActive = prompt.IsActive,
            ContentHash = oldHash,
            CapturedFromUpdatedAt = prompt.UpdatedAt ?? prompt.CreatedAt,
            UpdatedByUserId = prompt.UpdatedByUserId,
            CapturedAt = now,
        });

        prompt.Feature = validated.Feature;
        prompt.DisplayName = validated.DisplayName;
        prompt.DisplayNameArabic = validated.DisplayNameArabic;
        prompt.Description = validated.Description;
        prompt.DescriptionArabic = validated.DescriptionArabic;
        prompt.Provider = validated.Provider;
        prompt.Model = validated.Model;
        prompt.SystemPrompt = validated.SystemPrompt;
        prompt.UserPromptTemplate = validated.UserPromptTemplate;
        prompt.Temperature = validated.Temperature;
        prompt.MaxOutputTokens = validated.MaxOutputTokens;
        prompt.IsActive = request.IsActive;
        prompt.Version++;
        prompt.UpdatedAt = now;
        prompt.UpdatedByUserId = actorUserId;
        // SaveChangesAsync writes both the snapshot AND the
        // live-row update in one transaction. EF SaveChanges is
        // atomic per call; if the snapshot insert fails (e.g. unique
        // index violation on a duplicate version retry), the live
        // mutation is not persisted either.
        await appDbContext.SaveChangesAsync(cancellationToken);

        var newHash = AiAuditDetail.PromptContentHash(
            prompt.SystemPrompt, prompt.UserPromptTemplate);
        await auditLog.WriteSuccessAsync(
            AuditEvents.AiPromptUpdated,
            actorUserId,
            AiAuditDetail.ToJson(new
            {
                promptId = prompt.Id,
                promptKey = prompt.Key,
                version = prompt.Version,
                contentHashOld = oldHash,
                contentHashNew = newHash,
                contentChanged = !string.Equals(oldHash, newHash, StringComparison.Ordinal),
                isActive = prompt.IsActive,
            }),
            cancellationToken);
        return ToDetail(prompt);
    }

    public async Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default)
    {
        var prompt = await appDbContext.AiPrompts
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AiPromptNotFound, 404,
                "AI prompt not found.",
                "لم يتم العثور على محفّز الذكاء الاصطناعي.");
        if (!prompt.IsActive) return;
        prompt.IsActive = false;
        prompt.UpdatedAt = timeProvider.SimfNow();
        prompt.UpdatedByUserId = actorUserId;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.AiPromptDeactivated,
            actorUserId,
            AiAuditDetail.ToJson(new
            {
                promptId = prompt.Id,
                promptKey = prompt.Key,
            }),
            cancellationToken);
    }

    public async Task<AiCallResult> TestAsync(
        Guid actorUserId, Guid id, TestAiPromptRequest request,
        CancellationToken cancellationToken = default)
    {
        // Input caps live in AiService.InvokeAsync
        // so every caller (admin Test + public feature endpoints) goes
        // through the same chokepoint. No duplicate validation here.
        var prompt = await appDbContext.AiPrompts.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AiPromptNotFound, 404,
                "AI prompt not found.",
                "لم يتم العثور على محفّز الذكاء الاصطناعي.");
        return await aiService.InvokeAsync(
            prompt.Key, request.Inputs ?? new(),
            new AiCallerContext(actorUserId, "Admin"),
            cancellationToken);
    }

    public async Task<AdminAiInvocationDetail?> GetInvocationAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await appDbContext.AiInvocations.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new AdminAiInvocationDetail(
                i.Id, i.PromptKey, i.Feature, i.Provider, i.Model,
                i.CallerKind, i.CallerUserId,
                i.TokensInput, i.TokensOutput, i.LatencyMs,
                i.ErrorCode, i.InputJson, i.OutputText, i.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// The grid contract for /admin/ai/invocations. <c>errorOnly</c> is the
    /// page-level toggle rather than a column, so it is hand-written: it reads a
    /// nullable error code as a boolean, which no column declaration can infer.
    /// </summary>
    private static readonly GridColumns<AiInvocation> InvocationColumns = new GridColumns<AiInvocation>()
        .Add("createdAt", invocation => invocation.CreatedAt)
        .Add("promptKey", invocation => invocation.PromptKey)
        .Add("feature", invocation => invocation.Feature)
        .Add("provider", invocation => invocation.Provider)
        .Add("callerKind", invocation => invocation.CallerKind)
        .Add("latencyMs", invocation => invocation.LatencyMs)
        .AddFilter("errorOnly", ErrorOnly)
        .DefaultOrder("createdAt", descending: true)
        .PageSize(fallback: 50, max: 500);

    private static readonly Expression<Func<AiInvocation, AdminAiInvocationRow>> ToInvocationRow =
        invocation => new AdminAiInvocationRow(
            invocation.Id, invocation.PromptKey, invocation.Feature, invocation.Provider,
            invocation.Model, invocation.CallerKind, invocation.CallerUserId,
            invocation.TokensInput, invocation.TokensOutput, invocation.LatencyMs,
            invocation.ErrorCode, invocation.CreatedAt);

    public Task<GridPage<AdminAiInvocationRow>> ListInvocationsAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        appDbContext.AiInvocations.ToGridPageAsync(
            query, InvocationColumns, invocation => invocation.Id, ToInvocationRow,
            cancellationToken);

    private static Expression<Func<AiInvocation, bool>> ErrorOnly(string raw)
    {
        if (!bool.TryParse(raw, out var errorsOnly))
        {
            throw GridFilters.ValueInvalid("errorOnly", raw, "true or false");
        }

        if (errorsOnly)
        {
            return invocation => invocation.ErrorCode != null;
        }

        return invocation => invocation.ErrorCode == null;
    }

    public async Task<AdminAiDashboard> GetDashboardAsync(
        int windowHours = 24, CancellationToken cancellationToken = default)
    {
        var hours = Math.Clamp(windowHours, 1, 24 * 30);
        var since = timeProvider.SimfNow().AddHours(-hours);

        // Per-service invocation aggregates over the window (one GROUP BY).
        var perFeature = await appDbContext.AiInvocations.AsNoTracking()
            .Where(i => i.CreatedAt >= since)
            .GroupBy(i => i.Feature)
            .Select(g => new
            {
                Feature = g.Key,
                Calls = g.Count(),
                Errors = g.Count(x => x.ErrorCode != null),
                AvgLatency = g.Average(x => (double?)x.LatencyMs) ?? 0,
                Tokens = g.Sum(x => (long?)((x.TokensInput ?? 0) + (x.TokensOutput ?? 0))) ?? 0L,
            })
            .ToListAsync(cancellationToken);

        var services = perFeature
            .OrderBy(f => f.Feature)
            .Select(f => new AdminAiServiceStat(
                f.Feature,
                f.Calls,
                f.Errors,
                f.Calls > 0 ? (double)f.Errors / f.Calls : 0,
                (int)Math.Round(f.AvgLatency),
                f.Tokens))
            .ToList();

        var totalCalls = perFeature.Sum(f => f.Calls);
        var errorCount = perFeature.Sum(f => f.Errors);
        var totalTokens = perFeature.Sum(f => f.Tokens);
        // Call-weighted average latency across services (not an average of averages).
        var avgLatency = totalCalls > 0
            ? (int)Math.Round(perFeature.Sum(f => f.AvgLatency * f.Calls) / totalCalls)
            : 0;

        // Service-configuration counts from the prompt catalogue (independent of
        // the call window — a service with no calls is still a configured service).
        var promptFeatures = await appDbContext.AiPrompts.AsNoTracking()
            .GroupBy(p => p.Feature)
            .Select(g => new { Feature = g.Key, AnyActive = g.Any(p => p.IsActive) })
            .ToListAsync(cancellationToken);

        return new AdminAiDashboard(
            WindowHours: hours,
            TotalCalls: totalCalls,
            ErrorCount: errorCount,
            ErrorRate: totalCalls > 0 ? (double)errorCount / totalCalls : 0,
            AvgLatencyMs: avgLatency,
            TotalTokens: totalTokens,
            ActiveServices: promptFeatures.Count(f => f.AnyActive),
            TotalServices: promptFeatures.Count,
            Services: services);
    }

    // -- Validation helpers --

    private sealed record ValidatedCreate(
        string Key, AiFeature Feature, string DisplayName, string DisplayNameArabic,
        string? Description, string? DescriptionArabic,
        AiProvider Provider, string Model,
        string SystemPrompt, string UserPromptTemplate,
        double Temperature, int MaxOutputTokens);

    private static ValidatedCreate ValidateCreate(CreateAiPromptRequest r)
    {
        var key = (r.Key ?? string.Empty).Trim().ToLowerInvariant();
        if (key.Length is < 2 or > 64 || !IsKebab(key))
        {
            throw new ApiException(
                ErrorCodes.AiPromptInvalid, 400,
                "Key must be 2–64 chars, kebab-case (a-z, 0-9, -).",
                "يجب أن يكون المفتاح بين 2 و 64 محرفاً، بصيغة kebab.");
        }
        return new ValidatedCreate(
            key, r.Feature,
            ValidateText(r.DisplayName, 1, 128, "DisplayName"),
            ValidateText(r.DisplayNameArabic, 1, 128, "DisplayNameArabic"),
            string.IsNullOrWhiteSpace(r.Description) ? null
                : ValidateText(r.Description, 1, 512, "Description"),
            string.IsNullOrWhiteSpace(r.DescriptionArabic) ? null
                : ValidateText(r.DescriptionArabic, 1, 512, "DescriptionArabic"),
            r.Provider,
            ValidateText(r.Model, 1, 64, "Model"),
            ValidateText(r.SystemPrompt, 1, 8000, "SystemPrompt"),
            ValidateText(r.UserPromptTemplate, 1, 8000, "UserPromptTemplate"),
            ClampTemperature(r.Temperature),
            ClampMaxTokens(r.MaxOutputTokens));
    }

    private static ValidatedCreate ValidateUpdate(UpdateAiPromptRequest r)
    {
        return new ValidatedCreate(
            string.Empty, r.Feature,
            ValidateText(r.DisplayName, 1, 128, "DisplayName"),
            ValidateText(r.DisplayNameArabic, 1, 128, "DisplayNameArabic"),
            string.IsNullOrWhiteSpace(r.Description) ? null
                : ValidateText(r.Description, 1, 512, "Description"),
            string.IsNullOrWhiteSpace(r.DescriptionArabic) ? null
                : ValidateText(r.DescriptionArabic, 1, 512, "DescriptionArabic"),
            r.Provider,
            ValidateText(r.Model, 1, 64, "Model"),
            ValidateText(r.SystemPrompt, 1, 8000, "SystemPrompt"),
            ValidateText(r.UserPromptTemplate, 1, 8000, "UserPromptTemplate"),
            ClampTemperature(r.Temperature),
            ClampMaxTokens(r.MaxOutputTokens));
    }

    private static string ValidateText(string? value, int min, int max, string field)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length < min || trimmed.Length > max)
        {
            throw new ApiException(
                ErrorCodes.AiPromptInvalid, 400,
                $"{field} must be between {min} and {max} characters.",
                $"يجب أن يتراوح طول {field} بين {min} و {max} محرفاً.");
        }
        return trimmed;
    }

    private static double ClampTemperature(double value)
    {
        if (double.IsNaN(value) || value < 0 || value > 2)
        {
            throw new ApiException(
                ErrorCodes.AiPromptInvalid, 400,
                "Temperature must be between 0 and 2.",
                "يجب أن تكون درجة الحرارة بين 0 و 2.");
        }
        return value;
    }

    private static int ClampMaxTokens(int value)
    {
        if (value is < 1 or > 8000)
        {
            throw new ApiException(
                ErrorCodes.AiPromptInvalid, 400,
                "MaxOutputTokens must be between 1 and 8000.",
                "يجب أن يتراوح الحدّ الأقصى للمخرجات بين 1 و 8000.");
        }
        return value;
    }

    private static bool IsKebab(string s)
    {
        foreach (var c in s)
        {
            if (!(c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
                return false;
        }
        return s[0] != '-' && s[^1] != '-';
    }

    private static AdminAiPromptDetail ToDetail(AiPrompt p) => new(
        p.Id, p.Key, p.Feature, p.DisplayName, p.DisplayNameArabic,
        p.Description, p.DescriptionArabic, p.Provider, p.Model,
        p.SystemPrompt, p.UserPromptTemplate, p.Temperature, p.MaxOutputTokens,
        p.IsActive, p.Version, p.CreatedAt, p.UpdatedAt);

    /// <summary>Read the append-only edit history for one
    /// prompt. Newest first. Capped at the natural ceiling of how
    /// many edits a single prompt accumulates over an event
    /// lifetime (single-digit hundreds at most); no paging needed
    /// today.</summary>
    public async Task<IReadOnlyList<AdminAiPromptHistoryEntry>> GetHistoryAsync(
        Guid promptId, CancellationToken cancellationToken = default)
    {
        return await appDbContext.AiPromptHistory
            .AsNoTracking()
            .Where(h => h.AiPromptId == promptId)
            .OrderByDescending(h => h.Version)
            .Select(h => new AdminAiPromptHistoryEntry(
                h.Id,
                h.AiPromptId,
                h.Version,
                h.Provider,
                h.Model,
                h.SystemPrompt,
                h.UserPromptTemplate,
                h.Temperature,
                h.MaxOutputTokens,
                h.IsActive,
                h.ContentHash,
                h.CapturedFromUpdatedAt,
                h.UpdatedByUserId,
                h.CapturedAt))
            .ToListAsync(cancellationToken);
    }
}
