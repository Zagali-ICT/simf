// Tests: SIMF.Api.Tests/DelegationMeetingRequestsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Programme;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Notifications;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.MeetingRequests;

/// <summary>D-478 (#11, Group G phase 2) — delegation↔delegation (G2G) meeting
/// requests. A delegate requests their delegation meets another invited country's;
/// the team Accepts/Rejects and the requester is notified (+ emailed on accept via
/// the dispatcher, since the requester is a SimfUser). Mirrors the speaker flow.</summary>
internal sealed class DelegationMeetingRequestService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    INotificationDispatcher notifications,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<DelegationMeetingRequestService> logger) : IDelegationMeetingRequestService
{
    private const int MaxAttendees = 100;

    public async Task<DelegationMeetingRequestSubmitted> SubmitAsync(
        Guid requesterUserId, SubmitDelegationMeetingRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var subject = (request.Subject ?? string.Empty).Trim();
        if (subject.Length is < 1 or > 1000)
        {
            throw new ApiException(ErrorCodes.DelegationMeetingRequestInvalid, 400,
                "Subject must be between 1 and 1000 characters.",
                "يجب أن يتراوح طول الموضوع بين 1 و1000 حرف.");
        }
        if (request.AttendeeCount is < 1 or > MaxAttendees)
        {
            throw new ApiException(ErrorCodes.DelegationMeetingRequestInvalid, 400,
                $"Attendee count must be between 1 and {MaxAttendees}.",
                $"يجب أن يتراوح عدد الحضور بين 1 و{MaxAttendees}.");
        }

        // A1 — validate the optional slot pair (mirror the speaker flow): if either
        // end is supplied, require both and end > start, so an invalid pair cannot
        // be persisted silently.
        DateTimeOffset? slotStart = null;
        DateTimeOffset? slotEnd = null;
        if (request.SlotStartUtc is not null || request.SlotEndUtc is not null)
        {
            if (request.SlotStartUtc is not { } pickedStart
                || request.SlotEndUtc is not { } pickedEnd
                || pickedEnd <= pickedStart)
            {
                throw new ApiException(ErrorCodes.DelegationMeetingRequestInvalid, 400,
                    "A valid meeting slot (start and end) is required.",
                    "يلزم اختيار فترة اجتماع صحيحة (بداية ونهاية).");
            }
            slotStart = pickedStart;
            slotEnd = pickedEnd;
        }

        // The requester must be a delegate; their nationality is the requesting country.
        var profile = await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == requesterUserId)
            .Select(p => new { p.IsDelegate, p.NationalityId })
            .SingleOrDefaultAsync(cancellationToken);
        if (profile is null || !profile.IsDelegate)
        {
            throw new ApiException(ErrorCodes.Forbidden, 403,
                "Delegation meetings are available to delegation members only.",
                "اجتماعات الوفود متاحة لأعضاء الوفود فقط.");
        }
        var requestingCountry = await appDbContext.Countries.AsNoTracking()
            .Where(c => c.Id == profile.NationalityId && c.IsActive)
            .Select(c => new { c.Id, c.IsInvited })
            .SingleOrDefaultAsync(cancellationToken);
        if (requestingCountry is null || !requestingCountry.IsInvited)
        {
            throw new ApiException(ErrorCodes.DelegateCountryNotInvited, 400,
                "Your country is not an invited delegation.",
                "دولتك ليست من الوفود المدعوّة.");
        }

        var targetCode = (request.TargetCountryCode ?? string.Empty).Trim().ToUpperInvariant();
        var targetCountry = await appDbContext.Countries.AsNoTracking()
            .Where(c => c.Code == targetCode && c.IsActive)
            .Select(c => new { c.Id, c.IsInvited })
            .SingleOrDefaultAsync(cancellationToken);
        if (targetCountry is null || !targetCountry.IsInvited)
        {
            throw new ApiException(ErrorCodes.DelegateCountryNotInvited, 400,
                "The target country is not an invited delegation.",
                "الدولة المستهدفة ليست من الوفود المدعوّة.");
        }
        if (targetCountry.Id == requestingCountry.Id)
        {
            throw new ApiException(ErrorCodes.DelegationMeetingRequestInvalid, 400,
                "A delegation cannot request a meeting with itself.",
                "لا يمكن للوفد طلب اجتماع مع نفسه.");
        }

        // A1 — one open request per (requester, target delegation): reject a
        // duplicate Pending submission rather than flooding the review queue.
        var hasOpenRequest = await appDbContext.DelegationMeetingRequests.AsNoTracking()
            .AnyAsync(r => r.RequestedByUserId == requesterUserId
                && r.TargetCountryId == targetCountry.Id
                && r.Status == MeetingRequestStatus.Pending, cancellationToken);
        if (hasOpenRequest)
        {
            throw new ApiException(ErrorCodes.AppRequestDuplicatePending, 409,
                "You already have a pending meeting request for this delegation.",
                "لديك بالفعل طلب اجتماع قيد المراجعة لهذا الوفد.");
        }

        var now = timeProvider.GetUtcNow();
        var req = new DelegationMeetingRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = requesterUserId,
            RequestingCountryId = requestingCountry.Id,
            TargetCountryId = targetCountry.Id,
            AttendeeCount = request.AttendeeCount,
            Subject = subject,
            SlotStartUtc = slotStart,
            SlotEndUtc = slotEnd,
            Status = MeetingRequestStatus.Pending,
            CreatedAt = now,
        };
        appDbContext.DelegationMeetingRequests.Add(req);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.DelegationMeetingRequestSubmitted,
            Outcome = AuditOutcome.Success,
            ActorUserId = requesterUserId,
            Detail = $"requestId={req.Id}; target={targetCode}",
        }, cancellationToken);

        logger.LogInformation(
            "Delegation meeting request {Id} submitted by {Actor} (target {Target})",
            req.Id, requesterUserId, targetCode);

        return new DelegationMeetingRequestSubmitted(req.Id, req.Status, req.CreatedAt);
    }

    public async Task<GridPage<AdminDelegationMeetingRequestRow>> ListAllAsync(
        Guid actorUserId, GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = appDbContext.DelegationMeetingRequests.AsNoTracking().AsQueryable();
        if (query.Filters.TryGetValue("status", out var statusRaw)
            && Enum.TryParse<MeetingRequestStatus>(statusRaw, ignoreCase: true, out var status))
        {
            rows = rows.Where(r => r.Status == status);
        }
        // Newest first — the review desk works the pending queue top-down.
        rows = rows.OrderByDescending(r => r.CreatedAt);

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows.Skip(skip).Take(top)
            .Select(r => new AdminDelegationMeetingRequestRow(
                r.Id,
                r.RequestingCountryId,
                r.RequestingCountry!.Name,
                r.TargetCountryId,
                r.TargetCountry!.Name,
                r.RequestedByUserId,
                r.AttendeeCount,
                r.Subject,
                r.Status,
                r.SlotStartUtc,
                r.ResponseNote,
                r.CreatedAt,
                r.RespondedAt))
            .ToListAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminDelegationMeetingRequestsListed,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"count={page.Count}; total={total}",
        }, cancellationToken);

        return GridPage<AdminDelegationMeetingRequestRow>.Of(
            page, total, new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminDelegationMeetingRequestDetail> GetAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var detail = await LoadDetailAsync(id, cancellationToken);
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminDelegationMeetingRequestViewed,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"requestId={id}",
        }, cancellationToken);
        return detail;
    }

    public async Task<AdminDelegationMeetingRequestDetail> RespondAsync(
        Guid actorUserId, Guid id, RespondToDelegationMeetingRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Status is not (MeetingRequestStatus.Accepted or MeetingRequestStatus.Rejected))
        {
            throw new ApiException(ErrorCodes.DelegationMeetingRequestInvalid, 400,
                "Response status must be Accepted or Rejected.",
                "يجب أن تكون حالة الردّ مقبولة أو مرفوضة.");
        }
        var req = await appDbContext.DelegationMeetingRequests
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.DelegationMeetingRequestNotFound, 404,
                "Delegation meeting request not found.",
                "لم يتم العثور على طلب اجتماع الوفد.");

        // A1 — only a Pending request may be decided (guards double-response and
        // overwriting a prior decision).
        if (req.Status != MeetingRequestStatus.Pending)
        {
            throw new ApiException(ErrorCodes.AppRequestAlreadyResponded, 409,
                "This meeting request has already been responded to.",
                "تمت معالجة طلب المقابلة هذا بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        req.Status = request.Status;
        req.ResponseNote = string.IsNullOrWhiteSpace(request.ResponseNote)
            ? null : request.ResponseNote.Trim();
        req.RespondedAt = now;
        req.RespondedByUserId = actorUserId;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.DelegationMeetingRequestResponded,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"requestId={req.Id}; status={req.Status}",
        }, cancellationToken);

        // The detail (returned below) carries the requester email and the target
        // country name — load it once and reuse the name for the notification body
        // (no separate target-country round-trip).
        var detail = await LoadDetailAsync(id, cancellationToken);

        // D-185: the respond response discloses the requester email, so SOC must see
        // one Viewed event per disclosure regardless of which endpoint emitted it.
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminDelegationMeetingRequestViewed,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"requestId={req.Id}",
        }, cancellationToken);

        // Notify (and on accept email) the requesting delegate — they are a SimfUser,
        // so the dispatcher's email path applies. Best-effort.
        var accepted = req.Status == MeetingRequestStatus.Accepted;
        await notifications.TryDispatchAsync(new NotificationRequest
        {
            UserId = req.RequestedByUserId,
            Kind = accepted ? NotificationKind.MeetingScheduled : NotificationKind.MeetingCancelled,
            Title = accepted ? "Delegation meeting accepted" : "Delegation meeting declined",
            TitleArabic = accepted ? "تم قبول اجتماع الوفد" : "تم رفض اجتماع الوفد",
            Body = accepted
                ? $"Your delegation meeting with {detail.TargetCountry} was accepted."
                : $"Your delegation meeting with {detail.TargetCountry} was declined.",
            BodyArabic = accepted
                ? $"تم قبول اجتماع وفدك مع {detail.TargetCountry}."
                : $"تم رفض اجتماع وفدك مع {detail.TargetCountry}.",
            Severity = NotificationSeverity.Info,
            RelatedEntityType = nameof(DelegationMeetingRequest),
            RelatedEntityId = req.Id,
            SendEmail = accepted,
        }, logger, cancellationToken);

        return detail;
    }

    private async Task<AdminDelegationMeetingRequestDetail> LoadDetailAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var r = await appDbContext.DelegationMeetingRequests.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, x.RequestedByUserId, x.AttendeeCount, x.Subject, x.Status,
                x.SlotStartUtc, x.SlotEndUtc, x.ResponseNote, x.CreatedAt, x.RespondedAt,
                RequestingCountry = x.RequestingCountry!.Name,
                TargetCountry = x.TargetCountry!.Name,
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(ErrorCodes.DelegationMeetingRequestNotFound, 404,
                "Delegation meeting request not found.",
                "لم يتم العثور على طلب اجتماع الوفد.");

        var email = await identityDbContext.Users.AsNoTracking()
            .Where(u => u.Id == r.RequestedByUserId)
            .Select(u => u.Email)
            .SingleOrDefaultAsync(cancellationToken);

        return new AdminDelegationMeetingRequestDetail(
            r.Id, r.RequestingCountry, r.TargetCountry, r.RequestedByUserId, email,
            r.AttendeeCount, r.Subject, r.Status, r.SlotStartUtc, r.SlotEndUtc,
            r.ResponseNote, r.CreatedAt, r.RespondedAt);
    }
}
