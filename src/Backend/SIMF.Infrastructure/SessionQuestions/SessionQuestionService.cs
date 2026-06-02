// Tests: SIMF.Api.Tests/SessionQuestionsTests.cs
// Tests: SIMF.Api.Tests/QuestionArrivalGatingTests.cs (P5.1c — D-242 FR-704)
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
/// D-169 (gap doc G6, PDF §2.7.2 + §2.10) — public submission service.
/// Submission requires the session to be active and within (or near) its time
/// window, and the attendee to be at the hall. P5.1 — D-242 (FR-704) resolved
/// the §G7 / G-OI-2 venue gate: when the session's hall has a geofence (D-240)
/// the attendee must have a <c>HallAttendance</c> arrival record (D-241); when
/// it has none, the gate falls back to the D-171 self-assert toggle.
/// </summary>
internal sealed class SessionQuestionService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    IQuestionAiFilter questionAiFilter,
    ILogger<SessionQuestionService> logger) : ISessionQuestionService
{
    /// <summary>How long after a session ends to keep accepting
    /// questions. PDF §2.10 doesn't pin this; one hour matches the
    /// typical Q&amp;A window after a talk.</summary>
    private static readonly TimeSpan PostEndWindow = TimeSpan.FromHours(1);

    /// <summary>How long before a session starts to begin accepting
    /// questions (audience pre-submits while seating).</summary>
    private static readonly TimeSpan PreStartWindow = TimeSpan.FromMinutes(15);

    public async Task<SessionQuestionSubmitted> SubmitAsync(
        Guid sessionId,
        Guid submittedByUserId,
        SubmitSessionQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        var text = (request.QuestionText ?? string.Empty).Trim();
        if (text.Length is < 1 or > 1000)
        {
            throw new ApiException(
                ErrorCodes.SessionQuestionInvalid, 400,
                "Question text must be between 1 and 1000 characters.",
                "يجب أن يتراوح طول نص السؤال بين 1 و 1000 حرف.");
        }

        var session = await appDbContext.Sessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new
            {
                s.Id, s.IsActive, s.StartUtc, s.EndUtc, s.Code,
                // P5.1 — D-242 (FR-704): does this session's hall have a geofence
                // (D-240)? If so, hall arrival is the authoritative gate; if not,
                // we fall back to the D-171 self-assert toggle.
                HasGeofence = s.Hall!.GeofenceRadiusMeters != null,
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SessionNotFound, 404,
                "The session was not found.",
                "لم يتم العثور على الجلسة.");

        if (!session.IsActive)
        {
            throw new ApiException(
                ErrorCodes.SessionNotLiveForQuestions, 400,
                "The session is not active.",
                "الجلسة غير مفعّلة.");
        }

        var now = timeProvider.GetUtcNow();
        if (now < session.StartUtc - PreStartWindow
            || now > session.EndUtc + PostEndWindow)
        {
            throw new ApiException(
                ErrorCodes.SessionNotLiveForQuestions, 400,
                "The session is not currently accepting questions.",
                "الجلسة لا تستقبل الأسئلة في الوقت الحالي.");
        }

        // P5.1 — D-242 (FR-704): questions are gated by hall arrival. When the
        // hall has a geofence (D-240) the authoritative gate is a HallAttendance
        // arrival record (D-241); when it has none (QR-only / coordinates not yet
        // seeded), fall back to the D-171 self-assert toggle so nothing breaks
        // pre-seed. The session-end close is the time-window check above (FR-704).
        //
        // Intentionally NO `LeaveUtc == null` filter: FDS-007 FR-704 gates on
        // "has a HallAttendance enter record for the session" — i.e. arrived at
        // any point this session, not "currently inside". A visitor who briefly
        // stepped out (closing their row) keeps the right to ask within the
        // window, and the future QR-door-scan path's closed rows also satisfy
        // the gate. (This is a deliberate divergence from the present-tense
        // `HallAttendanceStatus.Arrived`, which reports current presence.)
        var atVenue = session.HasGeofence
            ? await appDbContext.HallAttendances.AnyAsync(
                a => a.SessionId == sessionId && a.UserId == submittedByUserId, cancellationToken)
            : request.IsAtVenue;
        if (!atVenue)
        {
            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.SessionQuestionRejectedNotAtVenue,
                Outcome = AuditOutcome.Failure,
                ActorUserId = submittedByUserId,
                ErrorCode = ErrorCodes.NotAtVenue,
                Detail = $"sessionId={sessionId}; gate={(session.HasGeofence ? "geofence" : "self-assert")}",
            }, cancellationToken);
            throw new ApiException(
                ErrorCodes.NotAtVenue, 403,
                session.HasGeofence
                    ? "You must have arrived at the hall to ask a question."
                    : "You must be at the venue to ask a question.",
                session.HasGeofence
                    ? "يجب أن تكون قد وصلت إلى القاعة لطرح سؤال."
                    : "يجب أن تكون في مكان الفعالية لطرح سؤال.");
        }

        // New arrivals all carry Order=0; the moderator queue sort is
        // (Order ASC, CreatedAt ASC), so two unmoderated rows tie at
        // Order=0 and the older CreatedAt wins — natural FIFO without
        // the racy "max+1 then insert" pattern that an earlier draft
        // shipped. A moderator who reorders writes explicit Order
        // values to override the natural order.
        // P4.2 — D-236: stage 1 of the pipeline. The AI filter is ADVISORY — it
        // tags a verdict for the Committee but never changes the status; the
        // question still lands Pending (stage 2). The shipped impl is a stub.
        var verdict = await questionAiFilter.ScreenAsync(
            sessionId, submittedByUserId, text, cancellationToken);

        // P3.3 — D-212: phase is pre vs live relative to the session start; the
        // question lands Pending for the Scientific Committee (stage 2).
        var question = new SessionQuestion
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            SubmittedByUserId = submittedByUserId,
            QuestionText = text,
            Recipient = request.Recipient,
            Order = 0,
            IsHidden = false,
            IsPushed = false,
            CreatedAt = now,
            Phase = now < session.StartUtc ? QuestionPhase.Pre : QuestionPhase.Live,
            Status = QuestionStatus.Pending,
            AiFilterVerdict = verdict.Verdict,
        };
        appDbContext.SessionQuestions.Add(question);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SessionQuestionSubmitted,
            Outcome = AuditOutcome.Success,
            ActorUserId = submittedByUserId,
            Detail = $"sessionId={sessionId}; questionId={question.Id}",
        }, cancellationToken);

        logger.LogInformation(
            "Audience question {QuestionId} submitted on session {SessionId} ({Code}) by {UserId}",
            question.Id, sessionId, session.Code, submittedByUserId);

        return new SessionQuestionSubmitted(
            question.Id, sessionId, question.Order, question.CreatedAt);
    }
}
