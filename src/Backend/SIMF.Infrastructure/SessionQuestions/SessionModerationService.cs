// Tests: SIMF.Api.Tests/SessionQuestionsTests.cs
// Tests: SIMF.Api.Tests/SessionQuestionCommitteeTests.cs (P3.3 — D-234 desk = Approved set)
// Tests: SIMF.Api.Tests/ModeratorDeskStateTests.cs (DEF-MOD-001/002 — answered + rejected recovery;
//        D-772 — the Hidden tab excludes Committee rejections)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.SessionQuestions.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Sessions;
using SIMF.Domain.SessionQuestions;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.SessionQuestions;

/// <summary>
/// D-169 (gap doc G6) — moderator surface. Trusts the caller is
/// already authorized; the endpoint layer handles the role check.
/// </summary>
internal sealed class SessionModerationService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<SessionModerationService> logger) : ISessionModerationService
{
    public async Task<IReadOnlyList<SessionQuestionModeratorRow>> ListAsync(
        Guid sessionId,
        QuestionStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        // P3.3 — D-212: the desk works the Committee-approved set (stage 3);
        // Pending questions still await the Committee (stage 2).
        // DEF-MOD-001 — an Answered row stays on the working desk (its own tab)
        // instead of dying in a screen-local Set.
        // DEF-MOD-002 — an explicit status returns exactly that bucket, so the
        // desk can pull back its own rejected (Hidden) rows and restore one; a
        // mis-click is no longer permanent from the app. The endpoint has already
        // proved the caller moderates THIS session (or is an Administrator), so a
        // hidden row is never exposed to an attendee.
        // The tab is an ALLOW-LIST, not a pass-through: a per-session moderator
        // works stage 3, so Pending text — still gated by the Scientific Committee
        // at stage 2 (D-212) — must not be readable from this desk, and an
        // unsupported value is refused rather than silently ignored.
        if (status is { } requested && !IsDeskTab(requested))
        {
            throw new ApiException(
                ErrorCodes.SessionQuestionInvalid, 400,
                "The moderator desk lists approved, answered or hidden questions only.",
                "يعرض مكتب المنسّق الأسئلة المعتمدة أو التي تمت الإجابة عنها أو المخفية فقط.");
        }
        // Built conditionally (not with an inline ternary) so each shape emits a
        // plain predicate the (SessionId, Status, Order) index can serve.
        var query = appDbContext.SessionQuestions
            .AsNoTracking()
            .Where(q => q.SessionId == sessionId);
        if (status == QuestionStatus.Hidden)
        {
            // D-772 — the rejected tab is the DESK's recovery tray, not a window
            // into the Committee's bin. Allowing ?status=Hidden through verbatim
            // still shipped the full QuestionText of every question the Scientific
            // Committee rejected while it was PENDING — text that never cleared the
            // stage-2 gate (D-212) and that the same r2 allow-list was added to keep
            // off this desk. StatusBeforeHidden (D-771) records where each hidden
            // row came from, so the tab now returns only rows hidden FROM the desk
            // (prior status Approved or Answered).
            //
            // NULL provenance = hidden before D-771 existed = unknown, and unknown
            // is treated as Committee, NOT desk. That is the safe side: being wrong
            // here costs one legacy row the desk cannot self-serve (an Administrator
            // still recovers it from the Committee queue), whereas the other way it
            // costs the leak this fix exists to close. SQL's three-valued logic
            // excludes NULL from both equality tests already.
            query = query.Where(q => q.Status == QuestionStatus.Hidden
                && (q.StatusBeforeHidden == QuestionStatus.Approved
                    || q.StatusBeforeHidden == QuestionStatus.Answered));
        }
        else if (status is { } wanted)
        {
            query = query.Where(q => q.Status == wanted);
        }
        else
        {
            query = query.Where(q => q.Status == QuestionStatus.Approved
                || q.Status == QuestionStatus.Answered);
        }

        var rows = await query
            .OrderBy(q => q.Order).ThenBy(q => q.CreatedAt)
            .Select(q => new
            {
                q.Id,
                q.SessionId,
                q.SubmittedByUserId,
                q.QuestionText,
                q.Recipient,
                q.Order,
                q.IsPushed,
                q.PushedAt,
                q.CreatedAt,
                q.Phase,
                q.Status,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<SessionQuestionModeratorRow>();
        }

        var userIds = rows.Select(r => r.SubmittedByUserId).Distinct().ToList();
        // A9 (D-185) — the submitter email is PII and is NOT shipped to the
        // moderator queue (a single grid render must never broadcast bulk PII).
        // Only the display name is projected from the cross-DB Identity lookup.
        var users = await identityDbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        return rows.Select(r =>
        {
            users.TryGetValue(r.SubmittedByUserId, out var user);
            return new SessionQuestionModeratorRow(
                r.Id,
                r.SessionId,
                r.SubmittedByUserId,
                user?.DisplayName ?? string.Empty,
                // A9 (D-185) — email redacted; the nullable DTO field stays for
                // wire-compat (D-219) but is always null on the moderator queue.
                null,
                r.QuestionText,
                r.Recipient,
                r.Order,
                r.Status == QuestionStatus.Hidden,
                r.IsPushed,
                r.PushedAt,
                r.CreatedAt,
                r.Phase,
                r.Status);
        }).ToList();
    }

    public async Task<SessionQuestionModeratorRow> SetHiddenAsync(
        Guid actorUserId,
        Guid sessionId,
        Guid questionId,
        bool isHidden,
        CancellationToken cancellationToken = default)
    {
        var question = await LoadQuestionAsync(sessionId, questionId, cancellationToken);

        // P3.3 — D-212: Status is the single source of truth for visibility; the
        // row's IsHidden marker is derived from it. (The persisted IsHidden column
        // is no longer written.)
        // D-771 — un-hiding RESTORES the status the row held before it was hidden
        // instead of promoting everything to Approved: an Answered question keeps
        // its answered mark (the durability DEF-MOD-001 exists for), and a
        // Committee-rejected question goes back to Pending — back into the
        // Committee queue, not onto the desk, and still un-pushable. Rows hidden
        // before this column existed have no memory, so they keep the old
        // fall-back of Approved.
        var currentlyHidden = question.Status == QuestionStatus.Hidden;
        if (currentlyHidden == isHidden)
        {
            return await ToRowAsync(question, cancellationToken); // idempotent
        }
        if (isHidden)
        {
            question.StatusBeforeHidden = question.Status;
            question.Status = QuestionStatus.Hidden;
        }
        else
        {
            question.Status = question.StatusBeforeHidden ?? QuestionStatus.Approved;
            question.StatusBeforeHidden = null;
        }
        // S-8 — a hidden question must not stay "pushed to the speaker": clear the
        // pushed marker so a pushed-then-hidden question drops off the on-stage
        // queue. (Un-hiding does not re-push — a fresh push is an explicit action.)
        if (isHidden && question.IsPushed)
        {
            question.IsPushed = false;
            question.PushedAt = null;
        }
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            isHidden
                ? AuditEvents.SessionQuestionHidden
                : AuditEvents.SessionQuestionUnhidden,
            actorUserId,
            $"sessionId={sessionId}; questionId={questionId}",
            cancellationToken);

        logger.LogInformation(
            "Moderator {Actor} {Action} question {QuestionId} on session {SessionId}",
            actorUserId, isHidden ? "hid" : "unhid", questionId, sessionId);

        return await ToRowAsync(question, cancellationToken);
    }

    public async Task<SessionQuestionModeratorRow> SetAnsweredAsync(
        Guid actorUserId,
        Guid sessionId,
        Guid questionId,
        bool isAnswered,
        CancellationToken cancellationToken = default)
    {
        var question = await LoadQuestionAsync(sessionId, questionId, cancellationToken);

        // DEF-MOD-001 — "تمت الإجابة" is now a persisted status, not a screen-local
        // Set: the mark survives a screen exit, an app restart and a co-moderator
        // on another device. Mirrors SetHiddenAsync: idempotent, Status is the
        // single source of truth, and the only legal transition is
        // Approved <-> Answered. A Pending question has not cleared the Committee
        // and a Hidden one was rejected — neither is on the working desk, so
        // neither can be "answered on stage".
        var currentlyAnswered = question.Status == QuestionStatus.Answered;
        if (currentlyAnswered == isAnswered)
        {
            return await ToRowAsync(question, cancellationToken); // idempotent
        }
        if (isAnswered && question.Status != QuestionStatus.Approved)
        {
            throw new ApiException(
                ErrorCodes.SessionQuestionInvalid, 400,
                "Only an approved question can be marked answered.",
                "لا يمكن وضع علامة \"تمت الإجابة\" إلا على سؤال معتمد.");
        }
        question.Status = isAnswered ? QuestionStatus.Answered : QuestionStatus.Approved;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            isAnswered
                ? AuditEvents.SessionQuestionAnswered
                : AuditEvents.SessionQuestionUnanswered,
            actorUserId,
            $"sessionId={sessionId}; questionId={questionId}",
            cancellationToken);

        logger.LogInformation(
            "Moderator {Actor} {Action} question {QuestionId} on session {SessionId}",
            actorUserId, isAnswered ? "answered" : "un-answered", questionId, sessionId);

        return await ToRowAsync(question, cancellationToken);
    }

    public async Task<SessionQuestionModeratorRow> PushAsync(
        Guid actorUserId,
        Guid sessionId,
        Guid questionId,
        CancellationToken cancellationToken = default)
    {
        var question = await LoadQuestionAsync(sessionId, questionId, cancellationToken);

        // S-8 — only an APPROVED (desk-visible) question can be pushed to the
        // speaker. A Pending question has not cleared the Committee, and a Hidden
        // one was rejected; neither appears on the moderator desk, so pushing it
        // would surface a suppressed question on stage.
        if (question.Status != QuestionStatus.Approved)
        {
            throw new ApiException(
                ErrorCodes.SessionQuestionInvalid, 400,
                "Only an approved question can be pushed to the speaker.",
                "لا يمكن دفع سؤال غير معتمد إلى المتحدث.");
        }

        if (question.IsPushed)
        {
            return await ToRowAsync(question, cancellationToken); // idempotent
        }
        question.IsPushed = true;
        question.PushedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SessionQuestionPushed,
            actorUserId,
            $"sessionId={sessionId}; questionId={questionId}",
            cancellationToken);

        return await ToRowAsync(question, cancellationToken);
    }

    public async Task ReorderAsync(
        Guid actorUserId,
        Guid sessionId,
        IReadOnlyList<Guid> orderedQuestionIds,
        CancellationToken cancellationToken = default)
    {
        if (orderedQuestionIds.Count == 0)
        {
            return;
        }
        var distinctIds = orderedQuestionIds.Distinct().ToList();
        if (distinctIds.Count != orderedQuestionIds.Count)
        {
            throw new ApiException(
                ErrorCodes.SessionQuestionInvalid, 400,
                "Reorder list contains duplicate ids.",
                "تحتوي قائمة الترتيب على معرّفات مكررة.");
        }

        // S-8 — reorder operates on the moderator DESK. Validate + renumber against
        // exactly the set ListAsync returns with no status filter: a desk can only
        // ever supply the ids it is holding, so requiring SetEquals over EVERY row
        // (Pending / Hidden included) made the endpoint unsatisfiable (a latent
        // 400). DEF-MOD-001 put Answered rows on that working desk too, so the
        // Approved-only predicate had become a strict subset of what the desk
        // sends — the same latent 400, one session-with-an-answered-question later.
        // Pending/Hidden rows stay off the desk and keep their Order (they sort by
        // CreatedAt when later approved).
        var deskQuestions = await appDbContext.SessionQuestions
            .Where(q => q.SessionId == sessionId
                && (q.Status == QuestionStatus.Approved
                    || q.Status == QuestionStatus.Answered))
            .ToListAsync(cancellationToken);

        var deskIds = deskQuestions.Select(q => q.Id).ToHashSet();
        var supplied = distinctIds.ToHashSet();
        if (!supplied.SetEquals(deskIds))
        {
            throw new ApiException(
                ErrorCodes.SessionQuestionInvalid, 400,
                "Reorder list must contain every question on the moderator desk exactly once.",
                "يجب أن تشمل قائمة الترتيب جميع أسئلة مكتب المنسّق بالضبط مرة واحدة.");
        }

        var trackedById = deskQuestions.ToDictionary(q => q.Id);
        for (var i = 0; i < distinctIds.Count; i++)
        {
            trackedById[distinctIds[i]].Order = i;
        }
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SessionQuestionReordered,
            actorUserId,
            $"sessionId={sessionId}; count={distinctIds.Count}",
            cancellationToken);
    }

    public async Task<IReadOnlyList<ModeratedSessionRow>> ListMySessionsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        // FR-MOD-001 — the moderator's own grant list. Served by the existing
        // SessionModerators(UserId) index, joined to its session inside the SAME
        // context (both live on the App DB, so this is not a cross-DB join —
        // D-157 only forbids reaching across to Identity).
        //
        // Inactive (soft-deleted) sessions drop out: a grant on a session that no
        // longer exists for the audience is not a desk the moderator can work.
        return await appDbContext.SessionModerators
            .AsNoTracking()
            .Where(m => m.UserId == userId
                && m.Session != null
                && m.Session.IsActive)
            .OrderBy(m => m.Session!.Start)
            .Select(m => new ModeratedSessionRow(
                m.SessionId,
                m.Session!.Title,
                m.Session.TitleArabic,
                m.Session.Hall == null ? string.Empty : m.Session.Hall.Name,
                m.Session.Hall == null ? string.Empty : m.Session.Hall.NameArabic,
                m.Session.Start,
                m.Session.End,
                m.AssignedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> IsModeratorAsync(
        Guid sessionId, Guid userId, CancellationToken cancellationToken = default) =>
        appDbContext.SessionModerators
            .AsNoTracking()
            .AnyAsync(m => m.SessionId == sessionId && m.UserId == userId,
                cancellationToken);

    // -- helpers ---------------------------------------------------------------

    /// <summary>DEF-MOD-002 — the three buckets the moderator desk renders as
    /// tabs. Pending is deliberately absent: those questions are still inside the
    /// Scientific Committee's stage-2 gate (D-212) and the desk must not be able
    /// to read them. Any other value (including an int outside the enum) is not a
    /// tab either.</summary>
    private static bool IsDeskTab(QuestionStatus status) =>
        status is QuestionStatus.Approved
            or QuestionStatus.Answered
            or QuestionStatus.Hidden;

    private async Task<SessionQuestion> LoadQuestionAsync(
        Guid sessionId, Guid questionId, CancellationToken cancellationToken)
    {
        var question = await appDbContext.SessionQuestions
            .SingleOrDefaultAsync(q => q.SessionId == sessionId && q.Id == questionId,
                cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SessionQuestionNotFound, 404,
                "The question was not found on this session.",
                "لم يتم العثور على السؤال على هذه الجلسة.");
        return question;
    }

    private async Task<SessionQuestionModeratorRow> ToRowAsync(
        SessionQuestion question, CancellationToken cancellationToken)
    {
        // A9 (D-185) — submitter email is PII and is NOT shipped to the moderator
        // desk (parity with ListAsync); only the display name is projected.
        var user = await identityDbContext.Users.AsNoTracking()
            .Where(u => u.Id == question.SubmittedByUserId)
            .Select(u => new { u.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);
        return new SessionQuestionModeratorRow(
            question.Id,
            question.SessionId,
            question.SubmittedByUserId,
            user?.DisplayName ?? string.Empty,
            // A9 (D-185) — email redacted; field kept null for wire-compat (D-219).
            null,
            question.QuestionText,
            question.Recipient,
            question.Order,
            question.Status == QuestionStatus.Hidden,
            question.IsPushed,
            question.PushedAt,
            question.CreatedAt,
            question.Phase,
            question.Status);
    }
}
