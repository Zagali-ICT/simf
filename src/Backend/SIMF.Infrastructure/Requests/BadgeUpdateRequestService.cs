// Tests: SIMF.Api.Tests/BadgeUpdateRequestsTests.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Requests.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Requests;
using SIMF.Domain.Requests;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Requests;

/// <summary>D-500 (Wave 5, الطلبات "طلب تحديث البادج") — badge job-title update
/// request service. Submission snapshots the requester's current job title; on
/// Accept the service applies the requested title to the requester's
/// <c>UserProfile.JobTitle</c> (same App DB — no cross-DB write). Requester name
/// is resolved from the profile, email on read from the Identity DB (D-157).
/// Mirrors <c>SpeakerMeetingRequestService</c>.</summary>
internal sealed class BadgeUpdateRequestService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<BadgeUpdateRequestService> logger) : IBadgeUpdateRequestService
{
    public async Task<BadgeUpdateRequestSubmitted> SubmitAsync(
        Guid requesterUserId, SubmitBadgeUpdateRequestBody request,
        CancellationToken cancellationToken = default)
    {
        var title = (request.RequestedJobTitle ?? string.Empty).Trim();
        if (title.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.BadgeUpdateRequestInvalid, 400,
                "Requested job title must be between 1 and 128 characters.",
                "يجب أن يتراوح طول المسمى الوظيفي المطلوب بين 1 و 128 حرفاً.");
        }
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > 1000 })
        {
            throw new ApiException(
                ErrorCodes.BadgeUpdateRequestInvalid, 400,
                "Note must be 1000 characters or fewer.",
                "يجب ألا يتجاوز طول الملاحظة 1000 حرف.");
        }

        var currentTitle = await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == requesterUserId)
            .Select(p => p.JobTitle)
            .SingleOrDefaultAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var req = new BadgeUpdateRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = requesterUserId,
            RequestedJobTitle = title,
            CurrentJobTitle = currentTitle,
            Note = note,
            Status = MeetingRequestStatus.Pending,
            CreatedAt = now,
        };
        appDbContext.BadgeUpdateRequests.Add(req);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BadgeUpdateRequestSubmitted,
            Outcome = AuditOutcome.Success,
            ActorUserId = requesterUserId,
            Detail = JsonSerializer.Serialize(new { badgeUpdateRequestId = req.Id }),
        }, cancellationToken);

        logger.LogInformation(
            "Badge update request {Id} submitted by {Actor}", req.Id, requesterUserId);

        return new BadgeUpdateRequestSubmitted(req.Id, req.Status, req.CreatedAt);
    }

    public async Task<GridPage<AdminBadgeUpdateRequestRow>> ListAllAsync(
        Guid actorUserId, GridQuery query,
        CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(25, 200);

        var rows = appDbContext.BadgeUpdateRequests.AsNoTracking().AsQueryable();
        var statusFilter = string.Empty;
        if (query.Filters.TryGetValue("status", out var statusRaw)
            && Enum.TryParse<MeetingRequestStatus>(statusRaw, ignoreCase: true, out var status))
        {
            rows = rows.Where(r => r.Status == status);
            statusFilter = status.ToString();
        }
        if (query.Filters.TryGetValue("jobTitle", out var jtRaw) && !string.IsNullOrWhiteSpace(jtRaw))
        {
            var v = jtRaw.Trim();
            rows = rows.Where(r => r.RequestedJobTitle.Contains(v));
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("jobtitle", false) => rows.OrderBy(r => r.RequestedJobTitle),
            ("jobtitle", true) => rows.OrderByDescending(r => r.RequestedJobTitle),
            ("status", false) => rows.OrderBy(r => r.Status),
            ("status", true) => rows.OrderByDescending(r => r.Status),
            ("createdat", false) => rows.OrderBy(r => r.CreatedAt),
            ("respondedat", false) => rows.OrderBy(r => r.RespondedAt),
            ("respondedat", true) => rows.OrderByDescending(r => r.RespondedAt),
            _ => rows.OrderByDescending(r => r.CreatedAt),
        };

        var total = await rows.CountAsync(cancellationToken);
        var pageRows = await rows.Skip(skip).Take(top).ToListAsync(cancellationToken);

        var names = await ResolveRequesterNamesAsync(
            pageRows.Select(r => r.RequestedByUserId), cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminBadgeUpdateRequestsListed,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = JsonSerializer.Serialize(new { count = pageRows.Count, total, top, skip, statusFilter }),
        }, cancellationToken);

        var items = pageRows.Select(r => new AdminBadgeUpdateRequestRow(
            r.Id, r.RequestedByUserId, names.GetValueOrDefault(r.RequestedByUserId),
            r.RequestedJobTitle, r.CurrentJobTitle, r.Status, r.ResponseNote,
            r.CreatedAt, r.RespondedAt))
            .ToList();
        return GridPage<AdminBadgeUpdateRequestRow>.Of(items, total,
            skip, top);
    }

    public async Task<AdminBadgeUpdateRequestDetail> GetAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var detail = await LoadDetailAsync(id, cancellationToken);
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminBadgeUpdateRequestViewed,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = JsonSerializer.Serialize(new { badgeUpdateRequestId = id }),
        }, cancellationToken);
        return detail;
    }

    public async Task<AdminBadgeUpdateRequestDetail> RespondAsync(
        Guid actorUserId, Guid id,
        RespondToBadgeUpdateRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Status is not (MeetingRequestStatus.Accepted or MeetingRequestStatus.Rejected))
        {
            throw new ApiException(
                ErrorCodes.BadgeUpdateRequestStatusInvalid, 400,
                "Response status must be Accepted or Rejected.",
                "يجب أن تكون حالة الردّ مقبولة أو مرفوضة.");
        }
        var req = await appDbContext.BadgeUpdateRequests
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.BadgeUpdateRequestNotFound, 404,
                "Badge update request not found.",
                "لم يتم العثور على طلب تحديث البادج.");

        // A1 — only a Pending request may be decided. Without this guard an Accept
        // on an already-Cancelled/decided request would replay the JobTitle side
        // effect below and overwrite the profile.
        if (req.Status != MeetingRequestStatus.Pending)
        {
            throw new ApiException(
                ErrorCodes.AppRequestAlreadyResponded, 409,
                "This request has already been responded to.",
                "تمت معالجة هذا الطلب بالفعل.");
        }

        var responseNote = string.IsNullOrWhiteSpace(request.ResponseNote)
            ? null : request.ResponseNote.Trim();
        if (responseNote is { Length: > 2000 })
        {
            throw new ApiException(
                ErrorCodes.BadgeUpdateRequestInvalid, 400,
                "Response note must be 2000 characters or fewer.",
                "يجب ألا يتجاوز طول ملاحظة الردّ 2000 حرف.");
        }

        req.Status = request.Status;
        req.ResponseNote = responseNote;
        req.RespondedAt = timeProvider.GetUtcNow();
        req.RespondedByUserId = actorUserId;

        // On Accept apply the requested title to the requester's profile (same App
        // DB — resolve the row and update JobTitle in the same unit of work).
        if (req.Status == MeetingRequestStatus.Accepted)
        {
            var profile = await appDbContext.UserProfiles
                .SingleOrDefaultAsync(p => p.UserId == req.RequestedByUserId, cancellationToken);
            if (profile is not null)
            {
                profile.JobTitle = req.RequestedJobTitle;
            }
        }

        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BadgeUpdateRequestResponded,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = JsonSerializer.Serialize(new { badgeUpdateRequestId = req.Id, status = req.Status.ToString() }),
        }, cancellationToken);
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminBadgeUpdateRequestViewed,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = JsonSerializer.Serialize(new { badgeUpdateRequestId = req.Id }),
        }, cancellationToken);

        return await LoadDetailAsync(id, cancellationToken);
    }

    private async Task<AdminBadgeUpdateRequestDetail> LoadDetailAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var req = await appDbContext.BadgeUpdateRequests.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.BadgeUpdateRequestNotFound, 404,
                "Badge update request not found.",
                "لم يتم العثور على طلب تحديث البادج.");

        var name = await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == req.RequestedByUserId)
            .Select(p => p.Name)
            .SingleOrDefaultAsync(cancellationToken);
        var email = await identityDbContext.Users.AsNoTracking()
            .Where(u => u.Id == req.RequestedByUserId)
            .Select(u => u.Email)
            .SingleOrDefaultAsync(cancellationToken);

        return new AdminBadgeUpdateRequestDetail(
            req.Id, req.RequestedByUserId, name, email,
            req.RequestedJobTitle, req.CurrentJobTitle, req.Note,
            req.Status, req.ResponseNote, req.CreatedAt, req.RespondedAt);
    }

    private async Task<Dictionary<Guid, string>> ResolveRequesterNamesAsync(
        IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }
        return await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => ids.Contains(p.UserId))
            .Select(p => new { p.UserId, p.Name })
            .ToDictionaryAsync(p => p.UserId, p => p.Name, cancellationToken);
    }
}
