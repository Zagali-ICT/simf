// Tests: SIMF.Api.Tests/SessionQuestionsTests.cs
// Tests: SIMF.Api.Tests/QuestionArrivalGatingTests.cs
// Tests: SIMF.Api.Tests/SessionQuestionVerdictClampTests.cs
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
/// Public submission service for audience questions.
/// The acceptance window is **phase-based**: a FUTURE session
/// (before start) takes questions from any approved user with **no** venue gate
/// (asking ahead of time); once **LIVE** (<c>now &gt;= Start</c>) the attendee
/// must be at the hall; after <c>End</c> the session is done (a recording, not
/// a live broadcast) and no question is taken. The LIVE venue gate is this:
/// when the session's hall has a geofence the attendee must
/// have a <c>HallAttendance</c> arrival record. When the hall has
/// no arrival mechanism presence cannot be verified, so the question is accepted
/// (remote Q&amp;A works); the client-sent <c>isAtVenue</c> flag is no longer
/// trusted as a gate.
/// </summary>
internal sealed class SessionQuestionService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    IQuestionAiFilter questionAiFilter,
    ILogger<SessionQuestionService> logger) : ISessionQuestionService
{
    /// <summary>Questions CLOSE at the end of the session: zero
    /// grace after <c>End</c>. After that the session view is a recording /
    /// archive, not a live broadcast, so no asking once the session is done.
    /// There is deliberately **no** lower bound now: a FUTURE (active,
    /// not-yet-ended) session accepts questions from any approved user — see the
    /// window check + the phase-gated venue check below.</summary>
    private static readonly TimeSpan PostEndWindow = TimeSpan.Zero;

    /// <summary>Mirrors SessionQuestionConfiguration's HasMaxLength on
    /// <c>AiFilterVerdict</c>. The filter is ADVISORY — it must never block a
    /// submit — but the verdict was persisted verbatim, so a filter that answered
    /// at length failed the INSERT and took the audience member's question with it.
    /// An over-long verdict loses its tail instead.</summary>
    private const int MaxAiFilterVerdictLength = 256;

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
                s.Id, s.IsActive, s.Start, s.End, s.Code,
                // Does this session's hall have a geofence?
                // If so, hall arrival is the authoritative gate; if not,
                // presence cannot be verified and the question is accepted.
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

        var now = timeProvider.SimfNow();
        // A FUTURE session (before start) accepts questions from any
        // approved user (asking ahead of time); questions only CLOSE once the
        // session is over. No lower bound — the whole pre-start slice is open.
        // The venue (check-in) gate below then applies only once the session is LIVE.
        if (now > session.End + PostEndWindow)
        {
            throw new ApiException(
                ErrorCodes.SessionNotLiveForQuestions, 400,
                "The session is over and no longer accepting questions.",
                "انتهت الجلسة ولم تعد تستقبل الأسئلة.");
        }

        // Questions are gated by hall arrival. When the
        // hall has a geofence the authoritative gate is a HallAttendance
        // arrival record; when it has none the question is accepted (remote
        // Q&A works, the client self-assert is not trusted). The session-end
        // close is the time-window check above.
        //
        // Intentionally NO `Leave == null` filter: the rule is
        // "has a HallAttendance enter record for the session" — i.e. arrived at
        // any point this session, not "currently inside". A visitor who briefly
        // stepped out (closing their row) keeps the right to ask within the
        // window, and the future QR-door-scan path's closed rows also satisfy
        // the gate. (This is a deliberate divergence from the present-tense
        // `HallAttendanceStatus.Arrived`, which reports current presence.)
        // The LIVE venue gate is REAL hall arrival, never a client
        // self-assert. It applies only once the session is LIVE (now >= Start);
        // before start any approved user may ask (the `!isLive` short-circuit
        // skips the arrival query). When the hall has an arrival mechanism (a
        // geofence; a hall-door gate feeds the SAME HallAttendance record)
        // the authoritative signal is a HallAttendance row for this session. When
        // the hall has NO arrival mechanism presence cannot be verified, so — per
        // owner — remote Q&A still works and the question is accepted (the earlier
        // `request.IsAtVenue` self-assert was hardcoded `true` by the app and gated
        // nothing; it is no longer trusted).
        var isLive = now >= session.Start;
        // Attendance is keyed by the attendee PROFILE, so the signed-in account is
        // translated before the presence check; no profile means no arrival to find.
        var submitterProfileId = isLive && session.HasGeofence
            ? await appDbContext.ProfileIdForAccountAsync(submittedByUserId, cancellationToken)
            : null;
        var atVenue = !isLive
            || !session.HasGeofence
            || await appDbContext.HallAttendances.AnyAsync(
                a => a.SessionId == sessionId && a.UserProfileId == submitterProfileId,
                cancellationToken);
        if (!atVenue)
        {
            await auditLog.WriteFailureAsync(
                AuditEvents.SessionQuestionRejectedNotAtVenue,
                submittedByUserId,
                errorCode: ErrorCodes.NotAtVenue,
                detail: $"sessionId={sessionId}; gate=hall-arrival",
                cancellationToken: cancellationToken);
            throw new ApiException(
                ErrorCodes.NotAtVenue, 403,
                "You must have arrived at the hall to ask a question.",
                "يجب أن تكون قد وصلت إلى القاعة لطرح سؤال.");
        }

        // New arrivals all carry Order=0; the moderator queue sort is
        // (Order ASC, CreatedAt ASC), so two unmoderated rows tie at
        // Order=0 and the older CreatedAt wins — natural FIFO without
        // the racy "max+1 then insert" pattern that an earlier draft
        // shipped. A moderator who reorders writes explicit Order
        // values to override the natural order.
        // Two-path Q&A: the phase is pre vs
        // live relative to the session start, and it now ROUTES the question —
        // it is no longer just a display label.
        //
        //  • PRE (asked before the session goes live): stage 1 is the AI filter.
        //    It tags a verdict for the Committee but never blocks a
        //    submit, then the row lands Pending for the
        //    Scientific Committee (stage 2) → the moderator desk (stage 3).
        //  • LIVE (asked once the session has started): owner directive — NO AI,
        //    NO committee. A live question goes STRAIGHT to the per-session
        //    moderator desk (lands Approved) for accept (push) / reject (hide);
        //    the moderator is the human gate for live Q&A.
        string? aiFilterVerdict = null;
        if (!isLive)
        {
            var verdict = await questionAiFilter.ScreenAsync(
                sessionId, submittedByUserId, text, cancellationToken);
            aiFilterVerdict = ClampVerdict(verdict.Verdict);
        }

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
            Phase = isLive ? QuestionPhase.Live : QuestionPhase.Pre,
            Status = isLive ? QuestionStatus.Approved : QuestionStatus.Pending,
            AiFilterVerdict = aiFilterVerdict,
        };
        appDbContext.SessionQuestions.Add(question);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SessionQuestionSubmitted,
            submittedByUserId,
            $"sessionId={sessionId}; questionId={question.Id}",
            cancellationToken);

        logger.LogInformation(
            "Audience question {QuestionId} submitted on session {SessionId} ({Code}) by {UserId}",
            question.Id, sessionId, session.Code, submittedByUserId);

        return new SessionQuestionSubmitted(
            question.Id, sessionId, question.Order, question.CreatedAt);
    }

    /// <summary>Trim a verdict to what the column holds. Null stays null — an
    /// absent verdict and an empty one mean different things to the Committee
    /// queue that reads this.</summary>
    private static string? ClampVerdict(string? verdict) =>
        verdict is not null && verdict.Length > MaxAiFilterVerdictLength
            ? verdict[..MaxAiFilterVerdictLength]
            : verdict;
}
