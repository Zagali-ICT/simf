// Tests: SIMF.Api.Tests/MeetingRequestsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Sessions;
using SIMF.Domain.MeetingRequests;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.MeetingRequests;

/// <summary>D-174 (gap doc G11, Mockup page 27) — meeting/interview
/// request service. Public submission validates the session is live;
/// admin response sets RespondedAt + RespondedByUserId.</summary>
internal sealed class MeetingRequestService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<MeetingRequestService> logger) : IMeetingRequestService
{
    public async Task<MeetingRequestSubmitted> SubmitAsync(
        Guid sessionId, Guid requesterUserId,
        SubmitMeetingRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = (request.RequesterName ?? string.Empty).Trim();
        var subject = (request.Subject ?? string.Empty).Trim();
        if (name.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.MeetingRequestInvalid, 400,
                "Requester name must be between 1 and 128 characters.",
                "يجب أن يتراوح طول اسم مقدّم الطلب بين 1 و 128 حرفاً.");
        }
        if (subject.Length is < 1 or > 1000)
        {
            throw new ApiException(
                ErrorCodes.MeetingRequestInvalid, 400,
                "Subject must be between 1 and 1000 characters.",
                "يجب أن يتراوح طول الموضوع بين 1 و 1000 حرف.");
        }

        var session = await appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new { s.Id, s.IsActive, s.Code })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.MeetingRequestSessionNotFound, 400,
                "The session was not found.",
                "لم يتم العثور على الجلسة.");
        if (!session.IsActive)
        {
            throw new ApiException(
                ErrorCodes.MeetingRequestSessionNotFound, 400,
                "The session is not active.",
                "الجلسة غير مفعّلة.");
        }

        var now = timeProvider.GetUtcNow();
        var req = new MeetingRequest
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            RequestedByUserId = requesterUserId,
            RequesterName = name,
            Subject = subject,
            Status = MeetingRequestStatus.Pending,
            CreatedAt = now,
        };
        appDbContext.MeetingRequests.Add(req);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MeetingRequestSubmitted,
            Outcome = AuditOutcome.Success,
            ActorUserId = requesterUserId,
            Detail = $"meetingRequestId={req.Id}; sessionId={sessionId}",
        }, cancellationToken);

        logger.LogInformation(
            "Meeting request {Id} submitted on session {Code} by {Actor}",
            req.Id, session.Code, requesterUserId);

        return new MeetingRequestSubmitted(
            req.Id, sessionId, req.Status, req.CreatedAt);
    }

    public async Task<GridPage<AdminMeetingRequestRow>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = appDbContext.MeetingRequests.AsNoTracking().AsQueryable();
        if (query.Filters.TryGetValue("status", out var statusRaw)
            && Enum.TryParse<MeetingRequestStatus>(statusRaw, ignoreCase: true,
                out var status))
        {
            rows = rows.Where(r => r.Status == status);
        }
        if (query.Filters.TryGetValue("sessionId", out var sidRaw)
            && Guid.TryParse(sidRaw, out var sessionIdFilter))
        {
            rows = rows.Where(r => r.SessionId == sessionIdFilter);
        }
        rows = rows.OrderByDescending(r => r.CreatedAt);

        var total = await rows.CountAsync(cancellationToken);
        var pageRows = await rows
            .Skip(skip).Take(top)
            .Join(appDbContext.Sessions,
                r => r.SessionId, s => s.Id,
                (r, s) => new
                {
                    r.Id, r.SessionId, s.Code, s.Title,
                    r.RequestedByUserId, r.RequesterName, r.Subject,
                    r.Status, r.ResponseNote, r.CreatedAt, r.RespondedAt,
                })
            .ToListAsync(cancellationToken);

        if (pageRows.Count == 0)
        {
            return GridPage<AdminMeetingRequestRow>.Of(
                Array.Empty<AdminMeetingRequestRow>(), total,
                new GridQuery { Skip = skip, Top = top });
        }

        var userIds = pageRows.Select(r => r.RequestedByUserId).Distinct().ToList();
        var emails = await identityDbContext.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        var items = pageRows.Select(r => new AdminMeetingRequestRow(
            r.Id, r.SessionId, r.Code, r.Title,
            r.RequestedByUserId, r.RequesterName,
            emails.TryGetValue(r.RequestedByUserId, out var email) ? email : null,
            r.Subject, r.Status, r.ResponseNote, r.CreatedAt, r.RespondedAt))
            .ToList();
        return GridPage<AdminMeetingRequestRow>.Of(items, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminMeetingRequestRow> RespondAsync(
        Guid actorUserId, Guid id,
        RespondToMeetingRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Status == MeetingRequestStatus.Pending)
        {
            throw new ApiException(
                ErrorCodes.MeetingRequestStatusInvalid, 400,
                "Response status must be Accepted or Rejected.",
                "يجب أن تكون حالة الردّ مقبولة أو مرفوضة.");
        }
        var req = await appDbContext.MeetingRequests
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.MeetingRequestNotFound, 404,
                "Meeting request not found.",
                "لم يتم العثور على طلب المقابلة.");

        var now = timeProvider.GetUtcNow();
        req.Status = request.Status;
        req.ResponseNote = string.IsNullOrWhiteSpace(request.ResponseNote)
            ? null : request.ResponseNote.Trim();
        req.RespondedAt = now;
        req.RespondedByUserId = actorUserId;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MeetingRequestResponded,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"meetingRequestId={req.Id}; status={req.Status}",
        }, cancellationToken);

        var session = await appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == req.SessionId)
            .Select(s => new { s.Code, s.Title })
            .SingleAsync(cancellationToken);
        var email = await identityDbContext.Users.AsNoTracking()
            .Where(u => u.Id == req.RequestedByUserId)
            .Select(u => u.Email)
            .SingleOrDefaultAsync(cancellationToken);

        return new AdminMeetingRequestRow(
            req.Id, req.SessionId, session.Code, session.Title,
            req.RequestedByUserId, req.RequesterName, email,
            req.Subject, req.Status, req.ResponseNote,
            req.CreatedAt, req.RespondedAt);
    }
}
