// Tests: SIMF.Api.Tests/SessionQuestionsTests.cs
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
/// The geofence guard (§G7) is **deferred** — a placeholder check
/// goes here once G-OI-2 resolves. Until then submission requires
/// only that the session is active and currently within (or near)
/// its time window.
/// </summary>
internal sealed class SessionQuestionService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
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

        // D-171 (gap doc G7, PDF §2.10) — venue self-assert toggle. Owner-
        // default resolution of G-OI-2: the lightest input source that
        // does not require GPS permissions or a maintained venue-WiFi list.
        // Hardening to lat/lon polygon or SSID can be added later as a
        // strictly more restrictive layer on top of this gate.
        if (!request.IsAtVenue)
        {
            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.SessionQuestionRejectedNotAtVenue,
                Outcome = AuditOutcome.Failure,
                ActorUserId = submittedByUserId,
                ErrorCode = ErrorCodes.NotAtVenue,
                Detail = $"sessionId={sessionId}",
            }, cancellationToken);
            throw new ApiException(
                ErrorCodes.NotAtVenue, 403,
                "You must be at the venue to ask a question.",
                "يجب أن تكون في مكان الفعالية لطرح سؤال.");
        }

        var session = await appDbContext.Sessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new { s.Id, s.IsActive, s.StartUtc, s.EndUtc, s.Code })
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

        // New arrivals all carry Order=0; the moderator queue sort is
        // (Order ASC, CreatedAt ASC), so two unmoderated rows tie at
        // Order=0 and the older CreatedAt wins — natural FIFO without
        // the racy "max+1 then insert" pattern that an earlier draft
        // shipped. A moderator who reorders writes explicit Order
        // values to override the natural order.
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
