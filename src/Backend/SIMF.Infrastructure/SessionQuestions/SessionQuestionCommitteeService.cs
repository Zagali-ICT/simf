// Tests: SIMF.Api.Tests/SessionQuestionCommitteeTests.cs
// Tests: SIMF.Api.Tests/ModeratorDeskStateTests.cs (a Committee rejection
//        restores to Pending, never onto the moderator desk)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.SessionQuestions.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Sessions;
using SIMF.Domain.SessionQuestions;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.SessionQuestions;

/// <summary>
/// The Scientific-Committee central
/// Q&amp;A queue (stage 2). Trusts the caller is authorized — the endpoint layer
/// gates with Questions.View / Moderate / Escalate. Cross-DB submitter display
/// names are resolved against the Identity DB.
/// </summary>
internal sealed class SessionQuestionCommitteeService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<SessionQuestionCommitteeService> logger) : ISessionQuestionCommitteeService
{
    /// <summary>The filter key that carries the queue's bucket. Named as a
    /// constant because the default-scope guard below has to ask whether the
    /// request supplied it, and a literal in two places is a rename away from a
    /// silent behaviour change.</summary>
    private const string StatusFilterKey = "status";

    /// <summary>
    /// The grid contract for /admin/questions: one entry per key
    /// QuestionQueueList.razor can send, as both its filter and its sort. A key
    /// not declared here is a 400, not a silently ignored request.
    ///
    /// <para>The submitter's display name and email live in the Identity database
    /// and are resolved only after the page has been chosen, so the submitter is
    /// neither sortable nor filterable — the two databases are separate by design
    /// and no cross-database JOIN exists that could make it either.</para>
    ///
    /// <para><c>status</c> and <c>sessionId</c> are declared columns rather than
    /// method parameters: they were the two filters the old endpoint took by
    /// hand, and the grid already has one way of expressing a filter.</para>
    /// </summary>
    private static readonly GridColumns<SessionQuestion> Columns =
        new GridColumns<SessionQuestion>()
            // The session is reached through the question's REQUIRED navigation
            // (SessionId is a non-nullable real FK), so this is the same inner
            // join the hand-written projection already emitted.
            .Add("session", question => question.Session!.Title, searchable: true)
            .Add("question", question => question.QuestionText, searchable: true)
            .Add("phase", question => question.Phase)
            .Add("ai", question => question.AiFilterVerdict, searchable: true)
            .Add("status", question => question.Status)
            .Add("sessionId", question => question.SessionId)
            .Add("createdAt", question => question.CreatedAt)
            // Oldest first: the Committee works the head of the queue. Paired with
            // the default Pending scope this is exactly the (Status, CreatedAt)
            // index on SessionQuestions.
            .DefaultOrder("createdAt")
            .PageSize(fallback: 25, max: 200);

    public async Task<GridPage<SessionQuestionQueueRow>> ListQueueAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        // The queue's default bucket is Pending — what the CP page reads and what
        // the old status-less call returned. It is a SCOPE, so it composes onto
        // the source before the grid, and only when the request names no status of
        // its own; an explicit status filter still selects exactly that bucket.
        // Without this guard an empty GridQuery would mean "no constraint" and the
        // page would silently start listing every already-actioned question.
        //
        // The test is a status with a VALUE, not merely a status key: the grid
        // validates a blank-valued key and then builds no predicate for it, so
        // keying on presence alone would let `status=""` fall through both the
        // default scope and the filter and return every bucket at once — a shape
        // the old `status ?? Pending` parameter could not produce.
        IQueryable<SessionQuestion> source = appDbContext.SessionQuestions;
        if (!query.Filters.Any(HasStatusValue))
        {
            source = source.Where(question => question.Status == QuestionStatus.Pending);
        }

        var page = await source.ToGridPageAsync(
            query, Columns, question => question.Id,
            question => new
            {
                question.Id,
                question.SessionId,
                SessionTitle = question.Session!.Title,
                question.SubmittedByUserId,
                question.QuestionText,
                question.Recipient,
                question.Phase,
                question.Status,
                question.AiFilterVerdict,
                question.AssignedToRole,
                question.CreatedAt,
            },
            cancellationToken);

        var pageRows = page.Items;
        if (pageRows.Count == 0)
        {
            return GridPage<SessionQuestionQueueRow>.Of(
                Array.Empty<SessionQuestionQueueRow>(), page.Total, page.Skip, page.Top);
        }

        // Cross-DB submitter resolution, for the CHOSEN PAGE only — one Identity
        // read of at most a page's worth of ids instead of the whole queue's.
        var userIds = pageRows.Select(r => r.SubmittedByUserId).Distinct().ToList();
        var users = await identityDbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var items = pageRows.Select(r =>
        {
            users.TryGetValue(r.SubmittedByUserId, out var user);
            return new SessionQuestionQueueRow(
                r.Id,
                r.SessionId,
                r.SessionTitle,
                r.SubmittedByUserId,
                user?.DisplayName ?? string.Empty,
                user?.Email,
                r.QuestionText,
                r.Recipient,
                r.Phase,
                r.Status,
                r.AiFilterVerdict,
                r.AssignedToRole,
                r.CreatedAt);
        }).ToList();

        return GridPage<SessionQuestionQueueRow>.Of(
            items, page.Total, page.Skip, page.Top);
    }

    public async Task<SessionQuestionQueueRow> ApproveAsync(
        Guid actorUserId, Guid questionId, CancellationToken cancellationToken = default)
    {
        var question = await LoadAsync(questionId, cancellationToken);
        if (question.Status != QuestionStatus.Approved)
        {
            question.Status = QuestionStatus.Approved;
            // An explicit approval is a new decision, so the "put it back
            // where it was" memory taken at hide time is spent.
            question.StatusBeforeHidden = null;
            await appDbContext.SaveChangesAsync(cancellationToken);
            await AuditAsync(AuditEvents.SessionQuestionApproved, actorUserId, question, cancellationToken);
            logger.LogInformation(
                "Committee {Actor} approved question {QuestionId}", actorUserId, questionId);
        }
        return await ToRowAsync(question, cancellationToken);
    }

    public async Task<SessionQuestionQueueRow> HideAsync(
        Guid actorUserId, Guid questionId, CancellationToken cancellationToken = default)
    {
        var question = await LoadAsync(questionId, cancellationToken);
        if (question.Status != QuestionStatus.Hidden)
        {
            // Status is the single source of truth for visibility; the desk's
            // IsHidden marker is derived from it at projection time, so there is
            // no separate flag to keep in sync.
            // Record where the row came from (mirrors
            // SessionModerationService.SetHiddenAsync). A Committee rejection of a
            // PENDING question must not become an Approved question the moment a
            // per-session moderator restores it from the rejected tab: it goes back
            // to Pending, i.e. back to this queue, never onto the stage.
            question.StatusBeforeHidden = question.Status;
            question.Status = QuestionStatus.Hidden;
            // S-8 — a hidden question must not stay "pushed to the speaker": clear
            // the pushed marker (mirrors SessionModerationService.SetHiddenAsync) so
            // a pushed-then-committee-hidden question drops off the on-stage queue
            // and does not resurface on stage if the Committee later re-approves it.
            // (Re-approval does not re-push — a fresh push is an explicit action.)
            if (question.IsPushed)
            {
                question.IsPushed = false;
                question.PushedAt = null;
            }
            await appDbContext.SaveChangesAsync(cancellationToken);
            await AuditAsync(AuditEvents.SessionQuestionHidden, actorUserId, question, cancellationToken);
            logger.LogInformation(
                "Committee {Actor} hid question {QuestionId}", actorUserId, questionId);
        }
        return await ToRowAsync(question, cancellationToken);
    }

    public async Task<SessionQuestionQueueRow> EscalateAsync(
        Guid actorUserId, Guid questionId, string role,
        CancellationToken cancellationToken = default)
    {
        var trimmedRole = (role ?? string.Empty).Trim();
        if (trimmedRole.Length is < 1 or > 64)
        {
            throw new ApiException(
                ErrorCodes.SessionQuestionInvalid, 400,
                "The escalation role must be between 1 and 64 characters.",
                "يجب أن يتراوح طول دور التصعيد بين 1 و 64 حرفاً.");
        }

        var question = await LoadAsync(questionId, cancellationToken);
        question.AssignedToRole = trimmedRole;
        question.EscalatedByUserId = actorUserId;
        question.EscalatedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await AuditAsync(
            AuditEvents.SessionQuestionEscalated, actorUserId, question,
            cancellationToken, detailSuffix: $"; role={trimmedRole}");
        logger.LogInformation(
            "Committee {Actor} escalated question {QuestionId} to role {Role}",
            actorUserId, questionId, trimmedRole);

        return await ToRowAsync(question, cancellationToken);
    }

    // -- helpers ---------------------------------------------------------------

    // True when the request actually constrains the status. Grid keys are matched
    // case-insensitively, so the default-scope guard has to match the same way the
    // column lookup will; a blank value is no constraint, exactly as the grid
    // itself treats it.
    private static bool HasStatusValue(KeyValuePair<string, string> filter) =>
        string.Equals(filter.Key, StatusFilterKey, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(filter.Value);

    // No Include(q => q.Session): we mutate only the question and read just the
    // title — fetched as a scalar in ToRowAsync — so the wide Session row
    // (Description/recording columns) is never loaded or tracked.
    private async Task<SessionQuestion> LoadAsync(Guid questionId, CancellationToken cancellationToken) =>
        await appDbContext.SessionQuestions
            .SingleOrDefaultAsync(q => q.Id == questionId, cancellationToken)
        ?? throw new ApiException(
            ErrorCodes.SessionQuestionNotFound, 404,
            "The question was not found.",
            "لم يتم العثور على السؤال.");

    private Task AuditAsync(
        string eventType, Guid actorUserId, SessionQuestion question,
        CancellationToken cancellationToken, string detailSuffix = "") =>
        auditLog.WriteAsync(new AuditEntry
        {
            EventType = eventType,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            // The affected submitter is the audit subject (mirrors AdminSessionModeratorService).
            SubjectUserId = question.SubmittedByUserId,
            Detail = $"questionId={question.Id}; sessionId={question.SessionId}{detailSuffix}",
        }, cancellationToken);

    // Mirror of SessionModerationService.ToRowAsync — submitter resolved from
    // the Identity DB (no cross-DB JOIN); title is a separate scalar read.
    private async Task<SessionQuestionQueueRow> ToRowAsync(
        SessionQuestion question, CancellationToken cancellationToken)
    {
        var title = await appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == question.SessionId)
            .Select(s => s.Title)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
        var user = await identityDbContext.Users.AsNoTracking()
            .Where(u => u.Id == question.SubmittedByUserId)
            .Select(u => new { u.Email, u.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);
        return new SessionQuestionQueueRow(
            question.Id,
            question.SessionId,
            title,
            question.SubmittedByUserId,
            user?.DisplayName ?? string.Empty,
            user?.Email,
            question.QuestionText,
            question.Recipient,
            question.Phase,
            question.Status,
            question.AiFilterVerdict,
            question.AssignedToRole,
            question.CreatedAt);
    }
}
