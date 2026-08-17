// Tests: SIMF.Api.Tests/ParticipationDocumentRequestsTests.cs
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

/// <summary>The participation-document request service
/// (الطلبات "طلب وثيقة المشاركة"). Submission is request-only (no document is
/// generated yet); an admin Accepts/Rejects with an optional note. Requester name is
/// resolved from the App-DB profile; the email is resolved on read from the
/// Identity DB (no cross-DB JOIN). Mirrors
/// <c>SpeakerMeetingRequestService</c>.</summary>
internal sealed class ParticipationDocumentRequestService(
    SimfAppDbContext appDbContext,
    IIdentityUserDirectory userDirectory,
    INotificationDispatcher notifications,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<ParticipationDocumentRequestService> logger)
    : IParticipationDocumentRequestService
{
    public async Task<ParticipationDocumentRequestSubmitted> SubmitAsync(
        Guid requesterUserId, SubmitParticipationDocumentRequestBody request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.DocumentType))
        {
            throw new ApiException(
                ErrorCodes.ParticipationDocumentRequestInvalid, 400,
                "Unknown document type.",
                "نوع وثيقة غير معروف.");
        }
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > 1000 })
        {
            throw new ApiException(
                ErrorCodes.ParticipationDocumentRequestInvalid, 400,
                "Note must be 1000 characters or fewer.",
                "يجب ألا يتجاوز طول الملاحظة 1000 حرف.");
        }

        // One open request per (requester, document type): a duplicate Pending
        // submission floods the review desk (mirrors the speaker-meeting dup guard).
        var hasOpenRequest = await appDbContext.ParticipationDocumentRequests.AsNoTracking()
            .AnyAsync(r => r.RequestedByUserId == requesterUserId
                && r.DocumentType == request.DocumentType
                && r.Status == MeetingRequestStatus.Pending, cancellationToken);
        if (hasOpenRequest)
        {
            throw new ApiException(
                ErrorCodes.AppRequestDuplicatePending, 409,
                "You already have a pending request for this document.",
                "لديك بالفعل طلب قيد المراجعة لهذه الوثيقة.");
        }

        var now = timeProvider.SimfNow();
        var req = new ParticipationDocumentRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = requesterUserId,
            DocumentType = request.DocumentType,
            Note = note,
            Status = MeetingRequestStatus.Pending,
            CreatedAt = now,
        };
        appDbContext.ParticipationDocumentRequests.Add(req);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ParticipationDocumentRequestSubmitted,
            requesterUserId,
            JsonSerializer.Serialize(new
            {
                participationDocumentRequestId = req.Id,
                documentType = req.DocumentType.ToString(),
            }),
            cancellationToken);

        logger.LogInformation(
            "Participation document request {Id} ({Type}) submitted by {Actor}",
            req.Id, req.DocumentType, requesterUserId);

        return new ParticipationDocumentRequestSubmitted(
            req.Id, req.DocumentType, req.Status, req.CreatedAt);
    }

    /// <summary>
    /// The grid contract for /admin/document-requests. It covers the two keys
    /// DocumentRequestsList.razor can send (status, createdAt / respondedAt sorts)
    /// plus the documentType filter the API has always accepted. A key not declared
    /// here is a 400, not a silently ignored request.
    /// </summary>
    private static readonly GridColumns<ParticipationDocumentRequest> Columns =
        new GridColumns<ParticipationDocumentRequest>()
            .Add("documentType", request => request.DocumentType)
            .Add("status", request => request.Status)
            .Add("createdAt", request => request.CreatedAt)
            .Add("respondedAt", request => request.RespondedAt)
            .DefaultOrder("createdAt", descending: true)
            .PageSize(fallback: 25, max: 200);

    public async Task<GridPage<AdminParticipationDocumentRequestRow>> ListAllAsync(
        Guid actorUserId, GridQuery query,
        CancellationToken cancellationToken = default)
    {
        // The rows are paged as entities rather than projected, because the
        // requester's display name is a second App-DB read the SELECT cannot carry.
        var page = await appDbContext.ParticipationDocumentRequests.ToGridPageAsync(
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
            AuditEvents.AdminParticipationDocumentRequestsListed,
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

        var items = page.Items.Select(request => new AdminParticipationDocumentRequestRow(
            request.Id, request.RequestedByUserId,
            names.GetValueOrDefault(request.RequestedByUserId),
            request.DocumentType, request.Note, request.Status, request.ResponseNote,
            request.CreatedAt, request.RespondedAt))
            .ToList();
        return GridPage<AdminParticipationDocumentRequestRow>.Of(
            items, page.Total, page.Skip, page.Top);
    }

    public async Task<AdminParticipationDocumentRequestDetail> GetAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var detail = await LoadDetailAsync(id, cancellationToken);
        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminParticipationDocumentRequestViewed,
            actorUserId,
            JsonSerializer.Serialize(new { participationDocumentRequestId = id }),
            cancellationToken);
        return detail;
    }

    public async Task<AdminParticipationDocumentRequestDetail> RespondAsync(
        Guid actorUserId, Guid id,
        RespondToParticipationDocumentRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Status is not (MeetingRequestStatus.Accepted or MeetingRequestStatus.Rejected))
        {
            throw new ApiException(
                ErrorCodes.ParticipationDocumentRequestStatusInvalid, 400,
                "Response status must be Accepted or Rejected.",
                "يجب أن تكون حالة الردّ مقبولة أو مرفوضة.");
        }
        var req = await appDbContext.ParticipationDocumentRequests
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.ParticipationDocumentRequestNotFound, 404,
                "Participation document request not found.",
                "لم يتم العثور على طلب وثيقة المشاركة.");

        // A1 — only a Pending request may be decided (guards double-response and
        // re-deciding a Cancelled request).
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
                ErrorCodes.ParticipationDocumentRequestInvalid, 400,
                "Response note must be 2000 characters or fewer.",
                "يجب ألا يتجاوز طول ملاحظة الردّ 2000 حرف.");
        }

        req.Status = request.Status;
        req.ResponseNote = responseNote;
        req.RespondedAt = timeProvider.SimfNow();
        req.RespondedByUserId = actorUserId;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ParticipationDocumentRequestResponded,
            actorUserId,
            JsonSerializer.Serialize(new { participationDocumentRequestId = req.Id, status = req.Status.ToString() }),
            cancellationToken);
        // One Viewed event for the email disclosure the respond detail returns.
        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminParticipationDocumentRequestViewed,
            actorUserId,
            JsonSerializer.Serialize(new { participationDocumentRequestId = req.Id }),
            cancellationToken);

        // Notify the requester of the decision (mirrors the speaker/booking flows).
        // Best-effort: a dispatch failure never undoes the committed response.
        var accepted = req.Status == MeetingRequestStatus.Accepted;
        await notifications.TryDispatchAsync(new NotificationRequest
        {
            UserId = req.RequestedByUserId,
            Kind = NotificationKind.ParticipationDocumentDecided,
            Title = accepted ? "Document request accepted" : "Document request rejected",
            TitleArabic = accepted ? "تم قبول طلب الوثيقة" : "تم رفض طلب الوثيقة",
            Body = accepted
                ? "Your participation-document request was accepted."
                : "Your participation-document request was rejected.",
            BodyArabic = accepted
                ? "تم قبول طلب وثيقة المشاركة الخاص بك."
                : "تم رفض طلب وثيقة المشاركة الخاص بك.",
            Severity = NotificationSeverity.Info,
            RelatedEntityType = nameof(ParticipationDocumentRequest),
            RelatedEntityId = req.Id,
            SendEmail = false,
        }, logger, cancellationToken);

        return await LoadDetailAsync(id, cancellationToken);
    }

    private async Task<AdminParticipationDocumentRequestDetail> LoadDetailAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var req = await appDbContext.ParticipationDocumentRequests.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.ParticipationDocumentRequestNotFound, 404,
                "Participation document request not found.",
                "لم يتم العثور على طلب وثيقة المشاركة.");

        var name = await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == req.RequestedByUserId)
            .Select(p => p.Name)
            .SingleOrDefaultAsync(cancellationToken);
        var email = await userDirectory.GetEmailAsync(
            req.RequestedByUserId, cancellationToken);

        return new AdminParticipationDocumentRequestDetail(
            req.Id, req.RequestedByUserId, name, email,
            req.DocumentType, req.Note, req.Status, req.ResponseNote,
            req.CreatedAt, req.RespondedAt);
    }

    // Batch-resolve display names for a page of requesters from the App-DB
    // profile (no email — that stays on the detail, the bulk-PII pattern).
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
