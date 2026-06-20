// Tests: SIMF.Api.Tests/SessionSummaryCommitteeTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Ai.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>
/// P4.1 — D-238 (Completion Programme §6.4.1, Mockup screen 34): the
/// Scientific-Committee session-summary / محضر desk. AI drafting routes through
/// the central <see cref="IAiService"/> seam (the <c>session-summary</c>
/// prompt) — the shipped provider is the deterministic Echo stub; the real
/// provider plugs in by editing the prompt's provider in the CP, no code
/// change. The محضر is advisory until the Committee publishes it; the public
/// read (<see cref="ProgrammeSessionService.GetSessionSummaryAsync"/>) gates on
/// the publish stamp.
/// </summary>
internal sealed class AdminSessionSummaryService(
    SimfAppDbContext appDbContext,
    IAiService aiService,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminSessionSummaryService> logger) : IAdminSessionSummaryService
{
    // The §7 source-of-truth lengths — must match SessionSummaryConfiguration.
    private const int SectionMax = 4000;
    private const int SpeakersMax = 1000;
    private const int FullTextMax = 8000;

    /// <summary>The seeded prompt key the AI draft routes through (D-238).</summary>
    private const string SummaryPromptKey = "session-summary";

    public async Task<IReadOnlyList<AdminSessionSummaryRow>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        // One row per active session with its summary state (correlated
        // sub-select — no separate round-trip, summary is 1:1).
        var rows = await appDbContext.Sessions
            .AsNoTracking()
            .Where(session => session.IsActive)
            .OrderByDescending(session => session.StartUtc)
            .Select(session => new
            {
                session.Id,
                session.Code,
                session.Title,
                session.TitleArabic,
                session.StartUtc,
                Summary = appDbContext.SessionSummaries
                    .Where(s => s.SessionId == session.Id && s.IsActive)
                    .Select(s => new
                    {
                        s.AiModel,
                        s.PublishedAt,
                        s.UpdatedAt,
                        s.ReviewSubmittedAt,
                        s.ApprovedAt,
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new AdminSessionSummaryRow(
            row.Id,
            row.Code,
            row.Title,
            row.TitleArabic,
            row.StartUtc,
            HasSummary: row.Summary is not null,
            GeneratedByAi: row.Summary?.AiModel is not null,
            IsPublished: row.Summary?.PublishedAt is not null,
            PublishedAt: row.Summary?.PublishedAt,
            UpdatedAt: row.Summary?.UpdatedAt,
            IsInReview: row.Summary?.ReviewSubmittedAt is not null && row.Summary?.ApprovedAt is null,
            IsApproved: row.Summary?.ApprovedAt is not null,
            ApprovedAt: row.Summary?.ApprovedAt)).ToList();
    }

    public async Task<AdminSessionSummaryDetail?> GetAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await appDbContext.Sessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId && s.IsActive)
            .Select(s => new { s.Id, s.Code, s.Title, s.TitleArabic })
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null)
        {
            return null;
        }

        var summary = await appDbContext.SessionSummaries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                s => s.SessionId == sessionId && s.IsActive, cancellationToken);
        return summary is null ? null : ToDetail(session.Code, session.Title, session.TitleArabic, summary);
    }

    public async Task<AdminSessionSummaryDetail> GenerateAsync(
        Guid actorUserId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionForDraftAsync(sessionId, cancellationToken);

        // Build the prompt inputs from the session metadata. The Echo provider
        // echoes them back; a real provider drafts the minutes from them.
        var speakers = await appDbContext.SessionSpeakers
            .AsNoTracking()
            .Where(link => link.SessionId == sessionId && link.Speaker!.IsActive)
            .OrderBy(link => link.DisplayOrder)
            .Select(link => link.Speaker!.Name)
            .ToListAsync(cancellationToken);

        var inputs = new Dictionary<string, string>
        {
            ["sessionTitle"] = session.Title,
            ["speakers"] = speakers.Count > 0 ? string.Join(", ", speakers) : "—",
            ["sessionAbstract"] = session.Description ?? string.Empty,
        };

        var result = await aiService.InvokeAsync(
            SummaryPromptKey, inputs, new AiCallerContext(actorUserId, "Admin"), cancellationToken);

        var summary = await appDbContext.SessionSummaries
            .SingleOrDefaultAsync(s => s.SessionId == sessionId && s.IsActive, cancellationToken);
        var now = timeProvider.GetUtcNow();
        // The seeded `session-summary` prompt produces ARABIC minutes, so the
        // draft lands in the Arabic full-text column only — the English column
        // stays for the Committee to fill (or the app falls back to Arabic per
        // the bilingual contract). Writing one language into both columns would
        // surface the wrong language once a real Arabic provider replaces Echo.
        var draft = Truncate(result.OutputText, FullTextMax);

        if (summary is null)
        {
            // First draft — the English column + curated sections start empty
            // for the Committee.
            summary = new SessionSummary
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                FullTextArabic = draft,
                AiModel = result.Model,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                UpdatedByUserId = actorUserId,
            };
            appDbContext.SessionSummaries.Add(summary);
        }
        else
        {
            // Re-generate replaces the Arabic AI draft but preserves the
            // Committee's English text, curated sections, and publish state.
            summary.FullTextArabic = draft;
            summary.AiModel = result.Model;
            summary.IsActive = true;
            summary.UpdatedAt = now;
            summary.UpdatedByUserId = actorUserId;
        }
        // A (re)generated draft changes the content, so it returns to the review
        // workflow's Draft state — any prior submit/approval is cleared (D-472).
        ResetReviewState(summary);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            AuditEvents.SessionSummaryGenerated, actorUserId, sessionId,
            $"summaryId={summary.Id}; model={result.Model}; invocation={result.InvocationId}",
            cancellationToken);
        logger.LogInformation(
            "Session summary {SummaryId} AI-drafted for session {SessionId} by {UserId} (model {Model}).",
            summary.Id, sessionId, actorUserId, result.Model);

        return ToDetail(session.Code, session.Title, session.TitleArabic, summary);
    }

    public async Task<AdminSessionSummaryDetail> SaveAsync(
        Guid actorUserId, Guid sessionId, SaveSessionSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionForDraftAsync(sessionId, cancellationToken);

        var keyPoints = Clean(request.KeyPoints, SectionMax, "key points");
        var keyPointsAr = Clean(request.KeyPointsArabic, SectionMax, "key points (Arabic)");
        var recommendations = Clean(request.Recommendations, SectionMax, "recommendations");
        var recommendationsAr = Clean(request.RecommendationsArabic, SectionMax, "recommendations (Arabic)");
        var speakers = Clean(request.Speakers, SpeakersMax, "speakers");
        var speakersAr = Clean(request.SpeakersArabic, SpeakersMax, "speakers (Arabic)");
        var fullText = Clean(request.FullText, FullTextMax, "full text");
        var fullTextAr = Clean(request.FullTextArabic, FullTextMax, "full text (Arabic)");

        var summary = await appDbContext.SessionSummaries
            .SingleOrDefaultAsync(s => s.SessionId == sessionId && s.IsActive, cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (summary is null)
        {
            // Hand-written draft — AiModel stays null (no model ran).
            summary = new SessionSummary
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                IsActive = true,
                CreatedAt = now,
            };
            appDbContext.SessionSummaries.Add(summary);
        }

        summary.KeyPoints = keyPoints;
        summary.KeyPointsArabic = keyPointsAr;
        summary.Recommendations = recommendations;
        summary.RecommendationsArabic = recommendationsAr;
        summary.Speakers = speakers;
        summary.SpeakersArabic = speakersAr;
        summary.FullText = fullText;
        summary.FullTextArabic = fullTextAr;
        summary.IsActive = true;
        summary.UpdatedAt = now;
        summary.UpdatedByUserId = actorUserId;
        // An edit invalidates any prior review/approval — back to Draft (D-472).
        ResetReviewState(summary);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            AuditEvents.SessionSummarySaved, actorUserId, sessionId,
            $"summaryId={summary.Id}", cancellationToken);

        return ToDetail(session.Code, session.Title, session.TitleArabic, summary);
    }

    public Task<AdminSessionSummaryDetail> PublishAsync(
        Guid actorUserId, Guid sessionId, CancellationToken cancellationToken = default) =>
        SetPublishedAsync(actorUserId, sessionId, publish: true, cancellationToken);

    public Task<AdminSessionSummaryDetail> UnpublishAsync(
        Guid actorUserId, Guid sessionId, CancellationToken cancellationToken = default) =>
        SetPublishedAsync(actorUserId, sessionId, publish: false, cancellationToken);

    private async Task<AdminSessionSummaryDetail> SetPublishedAsync(
        Guid actorUserId, Guid sessionId, bool publish, CancellationToken cancellationToken)
    {
        var session = await LoadSessionForDraftAsync(sessionId, cancellationToken);
        var summary = await LoadSummaryAsync(sessionId, cancellationToken);

        var now = timeProvider.GetUtcNow();
        summary.PublishedAt = publish ? now : null;
        summary.PublishedByUserId = publish ? actorUserId : null;
        summary.UpdatedAt = now;
        summary.UpdatedByUserId = actorUserId;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            publish ? AuditEvents.SessionSummaryPublished : AuditEvents.SessionSummaryUnpublished,
            actorUserId, sessionId, $"summaryId={summary.Id}", cancellationToken);

        return ToDetail(session.Code, session.Title, session.TitleArabic, summary);
    }

    public async Task<AdminSessionSummaryDetail> SubmitForReviewAsync(
        Guid actorUserId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionForDraftAsync(sessionId, cancellationToken);
        var summary = await LoadSummaryAsync(sessionId, cancellationToken);

        if (summary.ApprovedAt is not null)
        {
            throw new ApiException(
                ErrorCodes.SessionSummaryInvalid, 400,
                "This summary is already approved — return it to draft before resubmitting.",
                "تمت الموافقة على هذا الملخّص بالفعل — أعده إلى المسودة قبل إعادة الإرسال.");
        }

        var now = timeProvider.GetUtcNow();
        summary.ReviewSubmittedAt = now;
        summary.ReviewSubmittedByUserId = actorUserId;
        summary.UpdatedAt = now;
        summary.UpdatedByUserId = actorUserId;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            AuditEvents.SessionSummarySubmittedForReview, actorUserId, sessionId,
            $"summaryId={summary.Id}", cancellationToken);

        return ToDetail(session.Code, session.Title, session.TitleArabic, summary);
    }

    public async Task<AdminSessionSummaryDetail> ApproveAsync(
        Guid actorUserId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionForDraftAsync(sessionId, cancellationToken);
        var summary = await LoadSummaryAsync(sessionId, cancellationToken);

        if (summary.ReviewSubmittedAt is null)
        {
            throw new ApiException(
                ErrorCodes.SessionSummaryInvalid, 400,
                "Submit the summary for review before approving it.",
                "أرسل الملخّص للمراجعة قبل الموافقة عليه.");
        }

        var now = timeProvider.GetUtcNow();
        summary.ApprovedAt = now;
        summary.ApprovedByUserId = actorUserId;
        summary.UpdatedAt = now;
        summary.UpdatedByUserId = actorUserId;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            AuditEvents.SessionSummaryApproved, actorUserId, sessionId,
            $"summaryId={summary.Id}", cancellationToken);

        return ToDetail(session.Code, session.Title, session.TitleArabic, summary);
    }

    public async Task<AdminSessionSummaryDetail> ReturnToDraftAsync(
        Guid actorUserId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionForDraftAsync(sessionId, cancellationToken);
        var summary = await LoadSummaryAsync(sessionId, cancellationToken);

        var now = timeProvider.GetUtcNow();
        ResetReviewState(summary);
        summary.UpdatedAt = now;
        summary.UpdatedByUserId = actorUserId;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            AuditEvents.SessionSummaryReturnedToDraft, actorUserId, sessionId,
            $"summaryId={summary.Id}", cancellationToken);

        return ToDetail(session.Code, session.Title, session.TitleArabic, summary);
    }

    private async Task<SessionSummary> LoadSummaryAsync(
        Guid sessionId, CancellationToken cancellationToken) =>
        await appDbContext.SessionSummaries
            .SingleOrDefaultAsync(
                s => s.SessionId == sessionId && s.IsActive, cancellationToken)
        ?? throw new ApiException(
            ErrorCodes.SessionSummaryNotFound, 404,
            "No summary exists for this session yet.",
            "لا يوجد ملخّص لهذه الجلسة بعد.");

    /// <summary>Clears the review + approval stamps (back to Draft). Called on
    /// every content edit and by the explicit return-to-draft (D-472).</summary>
    private static void ResetReviewState(SessionSummary summary)
    {
        summary.ReviewSubmittedAt = null;
        summary.ReviewSubmittedByUserId = null;
        summary.ApprovedAt = null;
        summary.ApprovedByUserId = null;
    }

    private async Task<Session> LoadSessionForDraftAsync(
        Guid sessionId, CancellationToken cancellationToken) =>
        await appDbContext.Sessions
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == sessionId && s.IsActive, cancellationToken)
        ?? throw new ApiException(
            ErrorCodes.SessionNotFound, 404,
            "The session was not found.",
            "لم يتم العثور على الجلسة.");

    private async Task WriteAuditAsync(
        string eventType, Guid actorUserId, Guid sessionId, string detail,
        CancellationToken cancellationToken) =>
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = eventType,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"sessionId={sessionId}; {detail}",
        }, cancellationToken);

    private static string Clean(string? value, int max, string field)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length > max)
        {
            throw new ApiException(
                ErrorCodes.SessionSummaryInvalid, 400,
                $"The {field} must be {max} characters or fewer.",
                $"يجب ألا يتجاوز هذا الحقل {max} حرفاً.");
        }
        return trimmed;
    }

    private static string Truncate(string value, int max) =>
        value.Length > max ? value[..max] : value;

    private static AdminSessionSummaryDetail ToDetail(
        string code, string title, string titleArabic, SessionSummary s) =>
        new(
            s.SessionId,
            code,
            title,
            titleArabic,
            s.KeyPoints,
            s.KeyPointsArabic,
            s.Recommendations,
            s.RecommendationsArabic,
            s.Speakers,
            s.SpeakersArabic,
            s.FullText,
            s.FullTextArabic,
            s.AiModel,
            IsPublished: s.PublishedAt is not null,
            s.PublishedAt,
            s.CreatedAt,
            s.UpdatedAt,
            IsInReview: s.ReviewSubmittedAt is not null && s.ApprovedAt is null,
            IsApproved: s.ApprovedAt is not null,
            s.ApprovedAt);
}
