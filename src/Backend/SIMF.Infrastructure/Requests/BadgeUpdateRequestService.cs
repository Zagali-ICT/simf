// Tests: SIMF.Api.Tests/BadgeUpdateRequestsTests.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Application.Requests.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Requests;
using SIMF.Domain.Requests;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Requests;

/// <summary>The badge job-title update request service
/// (الطلبات "طلب تحديث البادج"). Submission snapshots the requester's current job title; on
/// Accept the service applies the requested title to the requester's
/// <c>UserProfile.JobTitle</c> (same App DB — no cross-DB write). Requester name
/// is resolved from the profile, email on read from the Identity DB.
/// Mirrors <c>SpeakerMeetingRequestService</c>.</summary>
internal sealed class BadgeUpdateRequestService(
    SimfAppDbContext appDbContext,
    IIdentityUserDirectory userDirectory,
    INotificationDispatcher notifications,
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

        // One open badge-update request per requester (mirrors the speaker-meeting
        // dup guard): a second Pending submission just floods the review desk.
        var hasOpenRequest = await appDbContext.BadgeUpdateRequests.AsNoTracking()
            .AnyAsync(r => r.RequestedByUserId == requesterUserId
                && r.Status == MeetingRequestStatus.Pending, cancellationToken);
        if (hasOpenRequest)
        {
            throw new ApiException(
                ErrorCodes.AppRequestDuplicatePending, 409,
                "You already have a pending badge update request.",
                "لديك بالفعل طلب تحديث بادج قيد المراجعة.");
        }

        var now = timeProvider.SimfNow();
        var badgeRequest = new BadgeUpdateRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = requesterUserId,
            RequestedJobTitle = title,
            CurrentJobTitle = currentTitle,
            Note = note,
            Status = MeetingRequestStatus.Pending,
            CreatedAt = now,
        };
        appDbContext.BadgeUpdateRequests.Add(badgeRequest);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.BadgeUpdateRequestSubmitted,
            requesterUserId,
            JsonSerializer.Serialize(new { badgeUpdateRequestId = badgeRequest.Id }),
            cancellationToken);

        logger.LogInformation(
            "Badge update request {Id} submitted by {Actor}", badgeRequest.Id, requesterUserId);

        return new BadgeUpdateRequestSubmitted(
            badgeRequest.Id, badgeRequest.Status, badgeRequest.CreatedAt);
    }

    /// <summary>
    /// The grid contract for /admin/badge-requests: one entry per key
    /// BadgeRequestsList.razor can send, as a sort and as a filter. A key not
    /// declared here is a 400, not a silently ignored request.
    /// </summary>
    private static readonly GridColumns<BadgeUpdateRequest> Columns =
        new GridColumns<BadgeUpdateRequest>()
            .Add("jobTitle", request => request.RequestedJobTitle)
            .Add("status", request => request.Status)
            .Add("createdAt", request => request.CreatedAt)
            .Add("respondedAt", request => request.RespondedAt)
            .DefaultOrder("createdAt", descending: true)
            .PageSize(fallback: 25, max: 200);

    public async Task<GridPage<AdminBadgeUpdateRequestRow>> ListAllAsync(
        Guid actorUserId, GridQuery query,
        CancellationToken cancellationToken = default)
    {
        // The rows are paged as entities rather than projected, because the
        // requester's display name is a second App-DB read the SELECT cannot carry.
        var page = await appDbContext.BadgeUpdateRequests.ToGridPageAsync(
            query, Columns, request => request.Id, request => request, cancellationToken);

        var names = await ResolveRequesterNamesAsync(
            page.Items.Select(request => request.RequestedByUserId), cancellationToken);

        // The audit keeps recording the CANONICAL status name rather than the raw
        // filter text, so entries written before and after the grid conversion read
        // the same. An unparseable value never reaches here — the grid 400s first.
        var statusFilter =
            query.Filters.TryGetValue("status", out var statusRaw)
            && Enum.TryParse<MeetingRequestStatus>(statusRaw, ignoreCase: true, out var status)
                ? status.ToString()
                : string.Empty;

        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminBadgeUpdateRequestsListed,
            actorUserId,
            JsonSerializer.Serialize(new
            {
                count = page.Items.Count,
                total = page.Total,
                top = page.Top,
                skip = page.Skip,
                statusFilter,
            }),
            cancellationToken);

        var items = page.Items.Select(request => new AdminBadgeUpdateRequestRow(
            request.Id, request.RequestedByUserId,
            names.GetValueOrDefault(request.RequestedByUserId),
            request.RequestedJobTitle, request.CurrentJobTitle, request.Status,
            request.ResponseNote, request.CreatedAt, request.RespondedAt))
            .ToList();
        return GridPage<AdminBadgeUpdateRequestRow>.Of(
            items, page.Total, page.Skip, page.Top);
    }

    public async Task<AdminBadgeUpdateRequestDetail> GetAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var detail = await LoadDetailAsync(id, cancellationToken);
        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminBadgeUpdateRequestViewed,
            actorUserId,
            JsonSerializer.Serialize(new { badgeUpdateRequestId = id }),
            cancellationToken);
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
        var badgeRequest = await appDbContext.BadgeUpdateRequests
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.BadgeUpdateRequestNotFound, 404,
                "Badge update request not found.",
                "لم يتم العثور على طلب تحديث البادج.");

        // A1 — only a Pending request may be decided. Without this guard an Accept
        // on an already-Cancelled/decided request would replay the JobTitle side
        // effect below and overwrite the profile.
        if (badgeRequest.Status != MeetingRequestStatus.Pending)
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

        badgeRequest.Status = request.Status;
        badgeRequest.ResponseNote = responseNote;
        badgeRequest.RespondedAt = timeProvider.SimfNow();
        badgeRequest.RespondedByUserId = actorUserId;

        // On Accept apply the requested title to the requester's profile (same App
        // DB — resolve the row and update JobTitle in the same unit of work).
        if (badgeRequest.Status == MeetingRequestStatus.Accepted)
        {
            var profile = await appDbContext.UserProfiles
                .SingleOrDefaultAsync(p => p.UserId == badgeRequest.RequestedByUserId, cancellationToken);
            if (profile is not null)
            {
                profile.JobTitle = badgeRequest.RequestedJobTitle;
            }
        }

        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.BadgeUpdateRequestResponded,
            actorUserId,
            JsonSerializer.Serialize(new
            {
                badgeUpdateRequestId = badgeRequest.Id,
                status = badgeRequest.Status.ToString(),
            }),
            cancellationToken);
        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminBadgeUpdateRequestViewed,
            actorUserId,
            JsonSerializer.Serialize(new { badgeUpdateRequestId = badgeRequest.Id }),
            cancellationToken);

        // Notify the requester of the decision. On Accept the badge job title was
        // applied above; this makes that side effect visible instead of silent.
        // Best-effort: a dispatch failure never undoes the committed response.
        var accepted = badgeRequest.Status == MeetingRequestStatus.Accepted;
        await notifications.TryDispatchAsync(new NotificationRequest
        {
            UserId = badgeRequest.RequestedByUserId,
            Kind = NotificationKind.BadgeUpdateDecided,
            Title = accepted ? "Badge update accepted" : "Badge update rejected",
            TitleArabic = accepted ? "تم قبول تحديث البادج" : "تم رفض تحديث البادج",
            Body = accepted
                ? $"Your badge job title was updated to \"{badgeRequest.RequestedJobTitle}\"."
                : "Your badge update request was rejected.",
            BodyArabic = accepted
                ? $"تم تحديث المسمى الوظيفي في بادجك إلى \"{badgeRequest.RequestedJobTitle}\"."
                : "تم رفض طلب تحديث البادج الخاص بك.",
            Severity = NotificationSeverity.Info,
            RelatedEntityType = nameof(BadgeUpdateRequest),
            RelatedEntityId = badgeRequest.Id,
            SendEmail = false,
        }, logger, cancellationToken);

        return await LoadDetailAsync(id, cancellationToken);
    }

    private async Task<AdminBadgeUpdateRequestDetail> LoadDetailAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var badgeRequest = await appDbContext.BadgeUpdateRequests.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.BadgeUpdateRequestNotFound, 404,
                "Badge update request not found.",
                "لم يتم العثور على طلب تحديث البادج.");

        var name = await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == badgeRequest.RequestedByUserId)
            .Select(p => p.Name)
            .SingleOrDefaultAsync(cancellationToken);
        var email = await userDirectory.GetEmailAsync(
            badgeRequest.RequestedByUserId, cancellationToken);

        return new AdminBadgeUpdateRequestDetail(
            badgeRequest.Id, badgeRequest.RequestedByUserId, name, email,
            badgeRequest.RequestedJobTitle, badgeRequest.CurrentJobTitle, badgeRequest.Note,
            badgeRequest.Status, badgeRequest.ResponseNote,
            badgeRequest.CreatedAt, badgeRequest.RespondedAt);
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
            .Where(p => p.UserId != null && ids.Contains(p.UserId.Value))
            .Select(p => new { UserId = p.UserId!.Value, p.Name })
            .ToDictionaryAsync(p => p.UserId, p => p.Name, cancellationToken);
    }
}
