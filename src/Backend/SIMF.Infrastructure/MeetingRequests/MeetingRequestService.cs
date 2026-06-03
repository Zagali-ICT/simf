// Tests: SIMF.Api.Tests/MeetingRequestsTests.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Sessions;
using SIMF.Domain.BusinessMeetings;
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
    private static readonly JsonSerializerOptions DetailJsonOptions = new()
    {
        WriteIndented = false,
    };

    private static string DetailJson(object value) =>
        JsonSerializer.Serialize(value, DetailJsonOptions);

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

        // D-185: structured JSON so SIEM field-extracts instead of
        // regex-parsing free text; matches the canonical Detail shape
        // documented in docs/soc/siem-rules/README.md.
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MeetingRequestSubmitted,
            Outcome = AuditOutcome.Success,
            ActorUserId = requesterUserId,
            Detail = DetailJson(new
            {
                meetingRequestId = req.Id,
                sessionId,
            }),
        }, cancellationToken);

        logger.LogInformation(
            "Meeting request {Id} submitted on session {Code} by {Actor}",
            req.Id, session.Code, requesterUserId);

        return new MeetingRequestSubmitted(
            req.Id, sessionId, req.Status, req.CreatedAt);
    }

    public async Task<GridPage<AdminMeetingRequestRow>> ListAllAsync(
        Guid actorUserId, GridQuery query,
        CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = appDbContext.MeetingRequests.AsNoTracking().AsQueryable();
        var statusFilter = string.Empty;
        if (query.Filters.TryGetValue("status", out var statusRaw)
            && Enum.TryParse<MeetingRequestStatus>(statusRaw, ignoreCase: true,
                out var status))
        {
            rows = rows.Where(r => r.Status == status);
            statusFilter = status.ToString();
        }
        var sessionFilter = string.Empty;
        if (query.Filters.TryGetValue("sessionId", out var sidRaw)
            && Guid.TryParse(sidRaw, out var sessionIdFilter))
        {
            rows = rows.Where(r => r.SessionId == sessionIdFilter);
            sessionFilter = sessionIdFilter.ToString();
        }

        // D-256 (SimfDataGrid migration) — CP grid per-column text filters
        // over the entity's own columns. The session code/title columns are
        // joined AFTER paging below, so they stay non-server-filterable; the
        // status column keeps its enum-parse filter above. Unknown columns
        // are ignored.
        if (query.Filters.TryGetValue("requesterName", out var nameRaw)
            && !string.IsNullOrWhiteSpace(nameRaw))
        {
            var v = nameRaw.Trim();
            rows = rows.Where(r => r.RequesterName.Contains(v));
        }
        if (query.Filters.TryGetValue("subject", out var subjectRaw)
            && !string.IsNullOrWhiteSpace(subjectRaw))
        {
            var v = subjectRaw.Trim();
            rows = rows.Where(r => r.Subject.Contains(v));
        }

        // D-256 — CP grid sortable columns. Default preserves the original
        // newest-first order (CreatedAt descending).
        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("requestername", false) => rows.OrderBy(r => r.RequesterName),
            ("requestername", true) => rows.OrderByDescending(r => r.RequesterName),
            ("subject", false) => rows.OrderBy(r => r.Subject),
            ("subject", true) => rows.OrderByDescending(r => r.Subject),
            ("status", false) => rows.OrderBy(r => r.Status),
            ("status", true) => rows.OrderByDescending(r => r.Status),
            ("createdat", false) => rows.OrderBy(r => r.CreatedAt),
            ("respondedat", false) => rows.OrderBy(r => r.RespondedAt),
            ("respondedat", true) => rows.OrderByDescending(r => r.RespondedAt),
            _ => rows.OrderByDescending(r => r.CreatedAt),
        };

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

        // D-185: every list call is auditable. SOC needs the off-hours
        // bulk-pull signal — see SIEM rule scaffolding doc. Detail
        // carries count + applied filters so the rule can distinguish
        // a routine top-25 status-filtered view from a top-200
        // unfiltered scrape. Filter values are sourced from already-
        // validated enum + Guid parses above (so no free-text rides
        // the audit row — explicit allowlist, no dictionary echo).
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminMeetingRequestsListed,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = DetailJson(new
            {
                count = pageRows.Count,
                total,
                top,
                skip,
                statusFilter,
                sessionFilter,
            }),
        }, cancellationToken);

        var items = pageRows.Select(r => new AdminMeetingRequestRow(
            r.Id, r.SessionId, r.Code, r.Title,
            r.RequestedByUserId, r.RequesterName,
            r.Subject, r.Status, r.ResponseNote, r.CreatedAt, r.RespondedAt))
            .ToList();
        return GridPage<AdminMeetingRequestRow>.Of(items, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminMeetingRequestDetail> GetAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default)
    {
        // D-185: single-record fetch carries the requester email so the
        // admin can reach out off-modal. Audit every read.
        var req = await appDbContext.MeetingRequests.AsNoTracking()
            .Where(r => r.Id == id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.MeetingRequestNotFound, 404,
                "Meeting request not found.",
                "لم يتم العثور على طلب المقابلة.");

        var session = await appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == req.SessionId)
            .Select(s => new { s.Code, s.Title })
            .SingleAsync(cancellationToken);

        var email = await identityDbContext.Users.AsNoTracking()
            .Where(u => u.Id == req.RequestedByUserId)
            .Select(u => u.Email)
            .SingleOrDefaultAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminMeetingRequestViewed,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = DetailJson(new
            {
                meetingRequestId = req.Id,
            }),
        }, cancellationToken);

        return new AdminMeetingRequestDetail(
            req.Id, req.SessionId, session.Code, session.Title,
            req.RequestedByUserId, req.RequesterName, email,
            req.Subject, req.Status, req.ResponseNote,
            req.CreatedAt, req.RespondedAt);
    }

    public async Task<AdminMeetingRequestDetail> RespondAsync(
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

        // D-185: structured JSON Detail so SIEM rule S-002 can
        // field-extract `Detail.status` instead of regex-parsing.
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MeetingRequestResponded,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = DetailJson(new
            {
                meetingRequestId = req.Id,
                status = req.Status.ToString(),
            }),
        }, cancellationToken);

        // D-185 (security review-pass): the respond path returns the
        // requester email, so SOC must see one Viewed event for every
        // email disclosure regardless of which endpoint disclosed it.
        // Without this row, a scripted client skipping the GET would
        // exfiltrate email + leave no Viewed audit trail.
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminMeetingRequestViewed,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = DetailJson(new
            {
                meetingRequestId = req.Id,
            }),
        }, cancellationToken);

        var session = await appDbContext.Sessions.AsNoTracking()
            .Where(s => s.Id == req.SessionId)
            .Select(s => new { s.Code, s.Title })
            .SingleAsync(cancellationToken);
        var email = await identityDbContext.Users.AsNoTracking()
            .Where(u => u.Id == req.RequestedByUserId)
            .Select(u => u.Email)
            .SingleOrDefaultAsync(cancellationToken);

        return new AdminMeetingRequestDetail(
            req.Id, req.SessionId, session.Code, session.Title,
            req.RequestedByUserId, req.RequesterName, email,
            req.Subject, req.Status, req.ResponseNote,
            req.CreatedAt, req.RespondedAt);
    }
}
