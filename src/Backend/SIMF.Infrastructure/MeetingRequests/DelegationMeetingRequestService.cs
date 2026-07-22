// Tests: SIMF.Api.Tests/DelegationMeetingRequestsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
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
    IIdentityUserDirectory userDirectory,
    INotificationDispatcher notifications,
    IHallAvailabilityService hallAvailability,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<DelegationMeetingRequestService> logger) : IDelegationMeetingRequestService
{
    private static readonly SIMF.Common.Enums.HallPurpose[] MeetingHallPurposes =
        { SIMF.Common.Enums.HallPurpose.Meeting, SIMF.Common.Enums.HallPurpose.General };

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

        // Bi-Meeting rework — the requester must hold the per-user
        // AllowsDelegationMeeting flag (admin-assigned; replaces the former IsDelegate
        // requester-gate). Their nationality is the requesting country; it must still
        // be an invited delegation (Country.IsInvited, checked below).
        var profile = await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == requesterUserId)
            .Select(p => new { p.AllowsDelegationMeeting, p.NationalityId })
            .SingleOrDefaultAsync(cancellationToken);
        if (profile is null || !profile.AllowsDelegationMeeting)
        {
            throw new ApiException(ErrorCodes.Forbidden, 403,
                "Requesting a delegation meeting is not enabled for your account.",
                "طلب اجتماع وفد غير مُفعَّل لحسابك.");
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
        var (skip, top) = query.ClampPage(25, 200);

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
            page, total, skip, top);
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

        var now = timeProvider.GetUtcNow();

        // Bi-Meeting rework — unified 3-button model. Status=Rejected is CANCEL (with a
        // justification note). Status=Accepted with a bound HallId is APPROVE
        // (VerbalConfirmed=false → AwaitingSpeaker, awaiting the other party's confirm,
        // wired in P4) or CONFIRM (true → Accepted, the admin has the verbal confirmation).
        // A legacy accept without a hall keeps the requester-proposed-slot behaviour.
        // This is admin-brokered + low-concurrency; the DB (HallId, SlotStartUtc)
        // filtered-unique index is the equal-start hall double-book backstop.
        var cancel = request.Status == MeetingRequestStatus.Rejected;
        var bindHall = request.Status == MeetingRequestStatus.Accepted && request.HallId is not null;
        var confirmVerbal = bindHall && request.VerbalConfirmed;

        if (cancel)
        {
            // Cancel/Decline is allowed from any non-terminal state: a Pending decline
            // → Rejected; a post-approval cancel releases the held slot → Cancelled.
            if (req.Status is MeetingRequestStatus.Rejected or MeetingRequestStatus.Cancelled
                or MeetingRequestStatus.Done)
            {
                throw new ApiException(ErrorCodes.AppRequestAlreadyResponded, 409,
                    "This meeting request has already been responded to.",
                    "تمت معالجة طلب المقابلة هذا بالفعل.");
            }
            var wasPending = req.Status == MeetingRequestStatus.Pending;
            req.Status = wasPending ? MeetingRequestStatus.Rejected : MeetingRequestStatus.Cancelled;
            // Release any held hall slot so it frees up for another meeting.
            req.HallId = null;
            req.MeetingTableId = null;
        }
        else
        {
            // Approve is valid only from Pending; Confirm may also finalise a previously
            // Approved (AwaitingSpeaker) request.
            var allowed = confirmVerbal
                ? req.Status is MeetingRequestStatus.Pending or MeetingRequestStatus.AwaitingSpeaker
                : req.Status == MeetingRequestStatus.Pending;
            if (!allowed)
            {
                throw new ApiException(ErrorCodes.AppRequestAlreadyResponded, 409,
                    "This meeting request has already been responded to.",
                    "تمت معالجة طلب المقابلة هذا بالفعل.");
            }

            if (bindHall)
            {
                await BindDelegationHallSlotAsync(req, request, now, cancellationToken);
            }
            else if (req.SlotStartUtc is { } sStart && req.SlotEndUtc is { } sEnd)
            {
                // Legacy accept-without-hall — honour the requester-proposed slot with the
                // past + cross-country overlap guard.
                if (sStart < now)
                {
                    throw new ApiException(ErrorCodes.DelegationMeetingRequestInvalid, 400,
                        "The proposed meeting slot is in the past.",
                        "فترة الاجتماع المقترحة في الماضي.");
                }
                await GuardDelegationOverlapAsync(req, sStart, sEnd, cancellationToken);
            }

            if (confirmVerbal)
            {
                req.Status = MeetingRequestStatus.Accepted;
                req.ConfirmedAt = now;
                req.ConfirmedByUserId = actorUserId;
            }
            else
            {
                // Approve with a hall → AwaitingSpeaker; legacy accept-without-hall → Accepted.
                req.Status = bindHall ? MeetingRequestStatus.AwaitingSpeaker : MeetingRequestStatus.Accepted;
            }
        }

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
        // Notify the requester of the outcome. Confirmed (Accepted) and Approved
        // (AwaitingSpeaker) email + in-app; a decline is in-app + email too so the
        // requester always learns the result. The rich other-party 2-email + app-tap
        // confirm flow is added in P4.
        var (title, titleAr, body, bodyAr, kind, email) = req.Status switch
        {
            MeetingRequestStatus.Accepted => (
                "Delegation meeting confirmed", "تم تأكيد اجتماع الوفد",
                $"Your delegation meeting with {detail.TargetCountry} is confirmed.",
                $"تم تأكيد اجتماع وفدك مع {detail.TargetCountry}.",
                NotificationKind.MeetingScheduled, true),
            MeetingRequestStatus.AwaitingSpeaker => (
                "Delegation meeting approved", "تمت الموافقة على اجتماع الوفد",
                $"Your delegation meeting with {detail.TargetCountry} was approved and is awaiting confirmation.",
                $"تمت الموافقة على اجتماع وفدك مع {detail.TargetCountry} وهو بانتظار التأكيد.",
                NotificationKind.MeetingScheduled, true),
            _ => (
                "Delegation meeting declined", "تم رفض اجتماع الوفد",
                $"Your delegation meeting with {detail.TargetCountry} was declined.",
                $"تم رفض اجتماع وفدك مع {detail.TargetCountry}.",
                NotificationKind.MeetingCancelled, false),
        };
        await notifications.TryDispatchAsync(new NotificationRequest
        {
            UserId = req.RequestedByUserId,
            Kind = kind,
            Title = title, TitleArabic = titleAr,
            Body = body, BodyArabic = bodyAr,
            Severity = NotificationSeverity.Info,
            RelatedEntityType = nameof(DelegationMeetingRequest),
            RelatedEntityId = req.Id,
            SendEmail = email,
        }, logger, cancellationToken);

        // Bi-Meeting rework — on Approve (AwaitingSpeaker) notify the OTHER PARTY (each
        // eligible target-delegation member) by app + email so they can confirm on tap
        // (or by the email link, P4c). A verbal Confirm skips this (already Accepted).
        if (req.Status == MeetingRequestStatus.AwaitingSpeaker)
        {
            await NotifyTargetMembersAsync(req, detail.RequestingCountry, cancellationToken);
        }

        return detail;
    }

    public async Task<AdminDelegationMeetingRequestDetail> ConfirmByOtherPartyAsync(
        Guid callerUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var req = await appDbContext.DelegationMeetingRequests.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.DelegationMeetingRequestNotFound, 404,
                "Delegation meeting request not found.",
                "لم يتم العثور على طلب اجتماع الوفد.");

        // Only an Approved (AwaitingSpeaker) request can be confirmed by the other party.
        if (req.Status != MeetingRequestStatus.AwaitingSpeaker)
        {
            throw new ApiException(ErrorCodes.AppRequestAlreadyResponded, 409,
                "This meeting is not awaiting confirmation.",
                "هذا الاجتماع ليس بانتظار التأكيد.");
        }

        // The caller must be an eligible member of the TARGET delegation (their profile
        // country is the target country and they hold the delegation-meeting flag).
        var isTargetMember = await appDbContext.UserProfiles.AsNoTracking()
            .AnyAsync(p => p.UserId == callerUserId
                && p.NationalityId == req.TargetCountryId
                && p.AllowsDelegationMeeting, cancellationToken);
        if (!isTargetMember)
        {
            throw new ApiException(ErrorCodes.Forbidden, 403,
                "You are not permitted to confirm this meeting.",
                "غير مسموح لك بتأكيد هذا الاجتماع.");
        }

        var now = timeProvider.GetUtcNow();

        // Race-safe conditional flip AwaitingSpeaker → Accepted (any one eligible member's
        // tap confirms; a concurrent second confirm matches 0 rows and 409s cleanly).
        var affected = await appDbContext.DelegationMeetingRequests
            .Where(r => r.Id == id && r.Status == MeetingRequestStatus.AwaitingSpeaker)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, MeetingRequestStatus.Accepted)
                .SetProperty(r => r.ConfirmedAt, now)
                .SetProperty(r => r.ConfirmedByUserId, callerUserId), cancellationToken);
        if (affected == 0)
        {
            throw new ApiException(ErrorCodes.AppRequestAlreadyResponded, 409,
                "This meeting is not awaiting confirmation.",
                "هذا الاجتماع ليس بانتظار التأكيد.");
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.DelegationMeetingRequestResponded,
            Outcome = AuditOutcome.Success,
            ActorUserId = callerUserId,
            Detail = $"requestId={id}; status=Accepted; confirmedByOtherParty=true",
        }, cancellationToken);

        var detail = await LoadDetailAsync(id, cancellationToken);

        // Notify the requester their meeting is now confirmed.
        await notifications.TryDispatchAsync(new NotificationRequest
        {
            UserId = req.RequestedByUserId,
            Kind = NotificationKind.MeetingRequestConfirmed,
            Title = "Delegation meeting confirmed",
            TitleArabic = "تم تأكيد اجتماع الوفد",
            Body = $"Your delegation meeting with {detail.TargetCountry} is confirmed.",
            BodyArabic = $"تم تأكيد اجتماع وفدك مع {detail.TargetCountry}.",
            Severity = NotificationSeverity.Info,
            RelatedEntityType = nameof(DelegationMeetingRequest),
            RelatedEntityId = id,
            SendEmail = true,
        }, logger, cancellationToken);

        return detail;
    }

    public async Task<AdminDelegationMeetingRequestDetail> CheckInAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var req = await appDbContext.DelegationMeetingRequests
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.DelegationMeetingRequestNotFound, 404,
                "Delegation meeting request not found.",
                "لم يتم العثور على طلب اجتماع الوفد.");
        if (req.Status != MeetingRequestStatus.Accepted)
        {
            throw new ApiException(ErrorCodes.AppRequestAlreadyResponded, 409,
                "Only a confirmed meeting can be checked in.",
                "لا يمكن تسجيل الحضور إلا لاجتماع مؤكَّد.");
        }
        var now = timeProvider.GetUtcNow();
        req.Status = MeetingRequestStatus.Done;
        req.CheckedInAt = now;
        req.CheckedInByUserId = actorUserId;
        await appDbContext.SaveChangesAsync(cancellationToken);
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.DelegationMeetingRequestCheckedIn,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"requestId={id}",
        }, cancellationToken);
        return await LoadDetailAsync(id, cancellationToken);
    }

    // Bi-Meeting rework — dispatch the other-party request-to-confirm notification to
    // every eligible member of the target delegation (profile country == target country
    // AND AllowsDelegationMeeting). App row (confirm-on-tap deep-link from
    // NotificationKindCatalog) + email. Members are resolved on the App DB; their emails
    // are resolved by the dispatcher (Identity) — no cross-DB JOIN.
    private async Task NotifyTargetMembersAsync(
        DelegationMeetingRequest req, string requestingCountry, CancellationToken cancellationToken)
    {
        var memberIds = await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.NationalityId == req.TargetCountryId && p.AllowsDelegationMeeting)
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);
        foreach (var userId in memberIds)
        {
            await notifications.TryDispatchAsync(new NotificationRequest
            {
                UserId = userId,
                Kind = NotificationKind.MeetingRequested,
                Title = "Delegation meeting request",
                TitleArabic = "طلب اجتماع وفد",
                Body = $"A meeting request from {requestingCountry} is awaiting your confirmation.",
                BodyArabic = $"طلب اجتماع من {requestingCountry} بانتظار تأكيدك.",
                Severity = NotificationSeverity.Info,
                RelatedEntityType = nameof(DelegationMeetingRequest),
                RelatedEntityId = req.Id,
                SendEmail = true,
            }, logger, cancellationToken);
        }
    }

    // Bi-Meeting rework — bind the meeting to a free hall slot on Approve/Confirm,
    // mirroring SpeakerMeetingRequestService.BindHallSlotAsync. Validates the hall
    // hosts meetings, the picked slot is currently free (hall availability already
    // subtracts bound meetings), neither delegation overlaps, and the optional table
    // belongs to the hall. The (HallId, SlotStartUtc) filtered-unique index is the race
    // backstop.
    private async Task BindDelegationHallSlotAsync(
        DelegationMeetingRequest req,
        RespondToDelegationMeetingRequestRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var hallId = request.HallId!.Value;
        if (request.SlotStartUtc is not { } start
            || request.SlotEndUtc is not { } end || end <= start)
        {
            throw new ApiException(ErrorCodes.DelegationMeetingRequestInvalid, 400,
                "A valid hall slot (start and end) is required to bind a hall.",
                "يلزم اختيار فترة قاعة صحيحة (بداية ونهاية) لربط القاعة.");
        }
        if (start < now)
        {
            throw new ApiException(ErrorCodes.DelegationMeetingRequestInvalid, 400,
                "The meeting slot is in the past.",
                "فترة الاجتماع في الماضي.");
        }
        var hall = await appDbContext.Halls.AsNoTracking()
            .Where(h => h.Id == hallId)
            .Select(h => new { h.IsActive, h.Purpose })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(ErrorCodes.HallNotFound, 404,
                "The hall was not found.", "لم يتم العثور على القاعة.");
        if (!hall.IsActive || !MeetingHallPurposes.Contains(hall.Purpose))
        {
            throw new ApiException(ErrorCodes.HallNotFound, 404,
                "The hall does not host meetings.",
                "هذه القاعة لا تستضيف الاجتماعات.");
        }

        var slots = await hallAvailability.GetAvailableSlotsAsync(hallId, cancellationToken);
        if (!slots.Any(s => s.StartUtc == start && s.EndUtc == end))
        {
            throw new ApiException(ErrorCodes.DelegationMeetingRequestInvalid, 409,
                "That hall slot is no longer available.",
                "لم تعد فترة القاعة هذه متاحة.");
        }

        await GuardDelegationOverlapAsync(req, start, end, cancellationToken);

        if (request.MeetingTableId is { } tableId)
        {
            var tableOk = await appDbContext.MeetingTables.AsNoTracking()
                .AnyAsync(t => t.Id == tableId && t.HallId == hallId && t.IsActive, cancellationToken);
            if (!tableOk)
            {
                throw new ApiException(ErrorCodes.DelegationMeetingRequestInvalid, 400,
                    "The meeting table was not found in this hall.",
                    "لم يتم العثور على طاولة الاجتماع في هذه القاعة.");
            }
            req.MeetingTableId = tableId;
        }
        req.HallId = hallId;
        req.SlotStartUtc = start;
        req.SlotEndUtc = end;
    }

    // Neither delegation (as requester OR target) may already hold a LIVE meeting
    // (`MeetingRequestStatuses.SlotHolding`) overlapping [start, end) — the cross-country
    // double-book guard. Read-then-write, acceptable for this admin-brokered,
    // low-concurrency G2G table (the DB hall index is the equal-start backstop).
    private async Task GuardDelegationOverlapAsync(
        DelegationMeetingRequest req, DateTimeOffset start, DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var reqId = req.Id;
        var homeCountryId = req.RequestingCountryId;
        var targetCountryId = req.TargetCountryId;
        var overlaps = await appDbContext.DelegationMeetingRequests.AsNoTracking()
            .Where(r => r.Id != reqId
                && MeetingRequestStatuses.SlotHolding.Contains(r.Status)
                && r.SlotStartUtc != null && r.SlotEndUtc != null
                && (r.RequestingCountryId == homeCountryId || r.TargetCountryId == homeCountryId
                    || r.RequestingCountryId == targetCountryId || r.TargetCountryId == targetCountryId))
            .AnyAsync(r => r.SlotStartUtc < end && start < r.SlotEndUtc, cancellationToken);
        if (overlaps)
        {
            throw new ApiException(ErrorCodes.DelegationMeetingRequestInvalid, 409,
                "One of the delegations already has a meeting at that time.",
                "لدى أحد الوفدين اجتماع بالفعل في ذلك الوقت.");
        }
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

        var email = await userDirectory.GetEmailAsync(
            r.RequestedByUserId, cancellationToken);

        return new AdminDelegationMeetingRequestDetail(
            r.Id, r.RequestingCountry, r.TargetCountry, r.RequestedByUserId, email,
            r.AttendeeCount, r.Subject, r.Status, r.SlotStartUtc, r.SlotEndUtc,
            r.ResponseNote, r.CreatedAt, r.RespondedAt);
    }
}
