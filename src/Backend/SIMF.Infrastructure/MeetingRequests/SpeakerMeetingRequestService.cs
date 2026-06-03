// Tests: SIMF.Api.Tests/SpeakerMeetingRequestsTests.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Programme;
using SIMF.Domain.MeetingRequests;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.MeetingRequests;

/// <summary>D-269 (Mockup page 20 "Speaker profile") — speaker meeting-request
/// service. Submission validates the speaker is active and opted in
/// (<c>AllowsMeetingRequests</c>); admin response sets RespondedAt +
/// RespondedByUserId. Mirrors <see cref="MeetingRequestService"/> (audit-only,
/// no notification — consistent with the session-scoped flow).</summary>
internal sealed class SpeakerMeetingRequestService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<SpeakerMeetingRequestService> logger) : ISpeakerMeetingRequestService
{
    private static readonly JsonSerializerOptions DetailJsonOptions = new()
    {
        WriteIndented = false,
    };

    private static string DetailJson(object value) =>
        JsonSerializer.Serialize(value, DetailJsonOptions);

    public async Task<SpeakerMeetingRequestSubmitted> SubmitAsync(
        Guid speakerId, Guid requesterUserId,
        SubmitSpeakerMeetingRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = (request.RequesterName ?? string.Empty).Trim();
        var subject = (request.Subject ?? string.Empty).Trim();
        if (name.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.SpeakerMeetingRequestInvalid, 400,
                "Requester name must be between 1 and 128 characters.",
                "يجب أن يتراوح طول اسم مقدّم الطلب بين 1 و 128 حرفاً.");
        }
        if (subject.Length is < 1 or > 1000)
        {
            throw new ApiException(
                ErrorCodes.SpeakerMeetingRequestInvalid, 400,
                "Subject must be between 1 and 1000 characters.",
                "يجب أن يتراوح طول الموضوع بين 1 و 1000 حرف.");
        }

        var speaker = await appDbContext.Speakers.AsNoTracking()
            .Where(s => s.Id == speakerId)
            .Select(s => new { s.Id, s.IsActive, s.AllowsMeetingRequests, s.Code })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SpeakerNotFound, 404,
                "The speaker was not found.",
                "لم يتم العثور على المتحدّث.");
        if (!speaker.IsActive)
        {
            throw new ApiException(
                ErrorCodes.SpeakerNotFound, 404,
                "The speaker was not found.",
                "لم يتم العثور على المتحدّث.");
        }
        if (!speaker.AllowsMeetingRequests)
        {
            throw new ApiException(
                ErrorCodes.SpeakerMeetingRequestsNotAllowed, 409,
                "This speaker does not accept meeting requests.",
                "هذا المتحدّث لا يقبل طلبات المقابلة.");
        }

        var now = timeProvider.GetUtcNow();
        var req = new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speakerId,
            RequestedByUserId = requesterUserId,
            RequesterName = name,
            Subject = subject,
            Status = MeetingRequestStatus.Pending,
            CreatedAt = now,
        };
        appDbContext.SpeakerMeetingRequests.Add(req);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SpeakerMeetingRequestSubmitted,
            Outcome = AuditOutcome.Success,
            ActorUserId = requesterUserId,
            Detail = DetailJson(new
            {
                speakerMeetingRequestId = req.Id,
                speakerId,
            }),
        }, cancellationToken);

        logger.LogInformation(
            "Speaker meeting request {Id} submitted for speaker {Code} by {Actor}",
            req.Id, speaker.Code, requesterUserId);

        return new SpeakerMeetingRequestSubmitted(
            req.Id, speakerId, req.Status, req.CreatedAt);
    }

    public async Task<GridPage<AdminSpeakerMeetingRequestRow>> ListAllAsync(
        Guid actorUserId, GridQuery query,
        CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = appDbContext.SpeakerMeetingRequests.AsNoTracking().AsQueryable();
        var statusFilter = string.Empty;
        if (query.Filters.TryGetValue("status", out var statusRaw)
            && Enum.TryParse<MeetingRequestStatus>(statusRaw, ignoreCase: true,
                out var status))
        {
            rows = rows.Where(r => r.Status == status);
            statusFilter = status.ToString();
        }
        var speakerFilter = string.Empty;
        if (query.Filters.TryGetValue("speakerId", out var sidRaw)
            && Guid.TryParse(sidRaw, out var speakerIdFilter))
        {
            rows = rows.Where(r => r.SpeakerId == speakerIdFilter);
            speakerFilter = speakerIdFilter.ToString();
        }
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
            .Join(appDbContext.Speakers,
                r => r.SpeakerId, s => s.Id,
                (r, s) => new
                {
                    r.Id, r.SpeakerId, SpeakerName = s.Name, SpeakerNameArabic = s.NameArabic,
                    r.RequestedByUserId, r.RequesterName, r.Subject,
                    r.Status, r.ResponseNote, r.CreatedAt, r.RespondedAt,
                })
            .ToListAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminSpeakerMeetingRequestsListed,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = DetailJson(new
            {
                count = pageRows.Count,
                total,
                top,
                skip,
                statusFilter,
                speakerFilter,
            }),
        }, cancellationToken);

        var items = pageRows.Select(r => new AdminSpeakerMeetingRequestRow(
            r.Id, r.SpeakerId, r.SpeakerName, r.SpeakerNameArabic,
            r.RequestedByUserId, r.RequesterName,
            r.Subject, r.Status, r.ResponseNote, r.CreatedAt, r.RespondedAt))
            .ToList();
        return GridPage<AdminSpeakerMeetingRequestRow>.Of(items, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminSpeakerMeetingRequestDetail> GetAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default)
    {
        var detail = await LoadDetailAsync(id, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminSpeakerMeetingRequestViewed,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = DetailJson(new { speakerMeetingRequestId = id }),
        }, cancellationToken);

        return detail;
    }

    public async Task<AdminSpeakerMeetingRequestDetail> RespondAsync(
        Guid actorUserId, Guid id,
        RespondToSpeakerMeetingRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Status == MeetingRequestStatus.Pending)
        {
            throw new ApiException(
                ErrorCodes.SpeakerMeetingRequestStatusInvalid, 400,
                "Response status must be Accepted or Rejected.",
                "يجب أن تكون حالة الردّ مقبولة أو مرفوضة.");
        }
        var req = await appDbContext.SpeakerMeetingRequests
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SpeakerMeetingRequestNotFound, 404,
                "Speaker meeting request not found.",
                "لم يتم العثور على طلب مقابلة المتحدّث.");

        var now = timeProvider.GetUtcNow();
        req.Status = request.Status;
        req.ResponseNote = string.IsNullOrWhiteSpace(request.ResponseNote)
            ? null : request.ResponseNote.Trim();
        req.RespondedAt = now;
        req.RespondedByUserId = actorUserId;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SpeakerMeetingRequestResponded,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = DetailJson(new
            {
                speakerMeetingRequestId = req.Id,
                status = req.Status.ToString(),
            }),
        }, cancellationToken);

        // Mirrors the session flow (D-185): the respond path returns the
        // requester email, so SOC must see one Viewed event for every email
        // disclosure regardless of which endpoint disclosed it.
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminSpeakerMeetingRequestViewed,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = DetailJson(new { speakerMeetingRequestId = req.Id }),
        }, cancellationToken);

        return await LoadDetailAsync(id, cancellationToken);
    }

    // Loads the admin detail (speaker name from the App DB + requester email
    // resolved on read from the Identity DB — no cross-DB JOIN, D-157).
    private async Task<AdminSpeakerMeetingRequestDetail> LoadDetailAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var req = await appDbContext.SpeakerMeetingRequests.AsNoTracking()
            .Where(r => r.Id == id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SpeakerMeetingRequestNotFound, 404,
                "Speaker meeting request not found.",
                "لم يتم العثور على طلب مقابلة المتحدّث.");

        var speaker = await appDbContext.Speakers.AsNoTracking()
            .Where(s => s.Id == req.SpeakerId)
            .Select(s => new { s.Name, s.NameArabic })
            .SingleAsync(cancellationToken);

        var email = await identityDbContext.Users.AsNoTracking()
            .Where(u => u.Id == req.RequestedByUserId)
            .Select(u => u.Email)
            .SingleOrDefaultAsync(cancellationToken);

        return new AdminSpeakerMeetingRequestDetail(
            req.Id, req.SpeakerId, speaker.Name, speaker.NameArabic,
            req.RequestedByUserId, req.RequesterName, email,
            req.Subject, req.Status, req.ResponseNote,
            req.CreatedAt, req.RespondedAt);
    }
}
