// Tests: SIMF.Api.Tests/DelegationMeetingRequestsTests.cs
// Tests: SIMF.Api.Tests/DelegationMeetingQaFixesTests.cs
// Tests: SIMF.Api.Tests/MeetingNoAvailabilityTests.cs
// Tests: SIMF.Api.Tests/DelegationMeetingHallSerializationTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Programme;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Notifications;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.MeetingRequests;

/// <summary>Delegation↔delegation (G2G) meeting
/// requests. A delegate requests their delegation meets another invited country's;
/// the team Accepts/Rejects and the requester is notified (+ emailed on accept via
/// the dispatcher, since the requester is a SimfUser). Mirrors the speaker flow.</summary>
internal sealed class DelegationMeetingRequestService(
    SimfAppDbContext appDbContext,
    IIdentityUserDirectory userDirectory,
    INotificationDispatcher notifications,
    IDelegationAvailabilityService delegationAvailability,
    IHallAvailabilityService hallAvailability,
    IMeetingActionTokenService meetingActionTokens,
    IEmailQueue emailQueue,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<DelegationMeetingRequestService> logger) : IDelegationMeetingRequestService
{
    private static readonly SIMF.Common.Enums.HallPurpose[] MeetingHallPurposes =
        { SIMF.Common.Enums.HallPurpose.Meeting, SIMF.Common.Enums.HallPurpose.General };

    private const int MaxAttendees = 100;

    /// <summary>The nvarchar length DelegationMeetingRequestConfiguration gives
    /// ResponseNote. Named here so the respond guard and the column cannot drift.</summary>
    private const int MaxResponseNoteLength = 2000;

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

        // Validate the optional slot pair (mirror the speaker flow): if either
        // end is supplied, require both and end > start, so an invalid pair cannot
        // be persisted silently.
        DateTime? slotStart = null;
        DateTime? slotEnd = null;
        if (request.SlotStart is not null || request.SlotEnd is not null)
        {
            if (request.SlotStart is not { } pickedStart
                || request.SlotEnd is not { } pickedEnd
                || pickedEnd <= pickedStart)
            {
                throw new ApiException(ErrorCodes.DelegationMeetingRequestInvalid, 400,
                    "A valid meeting slot (start and end) is required.",
                    "يلزم اختيار فترة اجتماع صحيحة (بداية ونهاية).");
            }
            slotStart = pickedStart;
            slotEnd = pickedEnd;
        }

        // The per-user AllowsDelegationMeeting flag
        // (admin-assigned) is the SOLE authorization to request. The requester's
        // nationality is recorded as the requesting country and must be set + active, but
        // it NO LONGER has to be an invited delegation: the host/owner side (KSA is the
        // OWNER of the forum, not a visiting delegation, so it is deliberately not flagged
        // Country.IsInvited) must still be able to request meetings WITH the invited
        // delegations. The TARGET still must be an invited delegation (checked below), and
        // you still cannot request a meeting with your own country.
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
            .Select(c => new { c.Id })
            .SingleOrDefaultAsync(cancellationToken);
        if (requestingCountry is null)
        {
            throw new ApiException(ErrorCodes.DelegateCountryNotInvited, 400,
                "Your account has no active nationality set.",
                "لا توجد جنسية مفعّلة على حسابك.");
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

        // This supersedes the earlier rule that let a topic-only request go out whatever
        // the availability: a meeting request may no longer be sent when the target
        // delegation has nothing left to offer. GetAvailableSlotsAsync returns an empty
        // list for BOTH reasons — the delegation has no active future window at all, and
        // every slot the windows offer is past or already taken — so this one call covers
        // both. The app disables the send button on the same signal; this is the
        // server-side backstop.
        var freeSlots = await delegationAvailability.GetAvailableSlotsAsync(
            targetCountry.Id, cancellationToken);
        if (freeSlots.Count == 0)
        {
            throw new ApiException(ErrorCodes.DelegationMeetingNoAvailability, 409,
                "This delegation has no available meeting slots.",
                "لا توجد فترات متاحة لدى هذا الوفد.");
        }

        // A requester keeps ONE open request per target
        // delegation, but a repeat submission is NOT an error: MOVE the existing Pending
        // request (new slot / subject / attendee count) instead of a duplicate row or a
        // 409, so re-opening the sheet and picking a different time simply updates it.
        var openRequest = await appDbContext.DelegationMeetingRequests
            .SingleOrDefaultAsync(r => r.RequestedByUserId == requesterUserId
                && r.TargetCountryId == targetCountry.Id
                && r.Status == MeetingRequestStatus.Pending, cancellationToken);
        if (openRequest is not null)
        {
            openRequest.AttendeeCount = request.AttendeeCount;
            openRequest.Subject = subject;
            openRequest.SlotStart = slotStart;
            openRequest.SlotEnd = slotEnd;
            await appDbContext.SaveChangesAsync(cancellationToken);

            await auditLog.WriteSuccessAsync(
                AuditEvents.DelegationMeetingRequestSubmitted,
                requesterUserId,
                $"requestId={openRequest.Id}; target={targetCode}; moved=true",
                cancellationToken);

            return new DelegationMeetingRequestSubmitted(
                openRequest.Id, openRequest.Status, openRequest.CreatedAt);
        }

        var now = timeProvider.SimfNow();
        var req = new DelegationMeetingRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = requesterUserId,
            RequestingCountryId = requestingCountry.Id,
            TargetCountryId = targetCountry.Id,
            AttendeeCount = request.AttendeeCount,
            Subject = subject,
            SlotStart = slotStart,
            SlotEnd = slotEnd,
            Status = MeetingRequestStatus.Pending,
            CreatedAt = now,
        };
        appDbContext.DelegationMeetingRequests.Add(req);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.DelegationMeetingRequestSubmitted,
            requesterUserId,
            $"requestId={req.Id}; target={targetCode}",
            cancellationToken);

        logger.LogInformation(
            "Delegation meeting request {Id} submitted by {Actor} (target {Target})",
            req.Id, requesterUserId, targetCode);

        return new DelegationMeetingRequestSubmitted(req.Id, req.Status, req.CreatedAt);
    }

    /// <summary>The grid contract for /admin/delegation-meetings: one entry per key
    /// DelegationMeetingsList.razor can send. A key not declared here is a 400, not
    /// a silently ignored request.</summary>
    private static readonly GridColumns<DelegationMeetingRequest> Columns =
        new GridColumns<DelegationMeetingRequest>()
            .Add("status", request => request.Status)
            .Add("createdAt", request => request.CreatedAt)
            // Newest first — the review desk works the pending queue top-down.
            .DefaultOrder("createdAt", descending: true)
            .PageSize(fallback: 25, max: 200);

    /// <summary>The page as the database returns it. It carries
    /// <c>CheckedInByUserId</c>, which the row DTO does not, because the operator's
    /// display name lives in the Identity database and is resolved only after this
    /// page has materialised.</summary>
    private sealed record DelegationMeetingListRow(
        Guid Id,
        int RequestingCountryId,
        string RequestingCountry,
        int TargetCountryId,
        string TargetCountry,
        Guid RequestedByUserId,
        int AttendeeCount,
        string Subject,
        MeetingRequestStatus Status,
        DateTime? SlotStart,
        string? ResponseNote,
        DateTime CreatedAt,
        DateTime? RespondedAt,
        DateTime? CheckedInAt,
        Guid? CheckedInByUserId);

    private static readonly Expression<Func<DelegationMeetingRequest, DelegationMeetingListRow>>
        ToListRow = request => new DelegationMeetingListRow(
            request.Id,
            request.RequestingCountryId,
            request.RequestingCountry!.Name,
            request.TargetCountryId,
            request.TargetCountry!.Name,
            request.RequestedByUserId,
            request.AttendeeCount,
            request.Subject,
            request.Status,
            request.SlotStart,
            request.ResponseNote,
            request.CreatedAt,
            request.RespondedAt,
            // The hall check-in stamps, so the desk and its export
            // report who actually turned up, not only the decision.
            request.CheckedInAt,
            request.CheckedInByUserId);

    public async Task<GridPage<AdminDelegationMeetingRequestRow>> ListAllAsync(
        Guid actorUserId, GridQuery query, CancellationToken cancellationToken = default)
    {
        var page = await appDbContext.DelegationMeetingRequests.ToGridPageAsync(
            query, Columns, request => request.Id, ToListRow, cancellationToken);

        // Resolve the check-in operators' display names in ONE Identity-DB
        // query for the whole page. CheckedInByUserId is a bare logical FK,
        // so this is a second query merged in memory — never a cross-database JOIN.
        var operatorNames = await userDirectory.GetDisplayNamesAsync(
            page.Items.Where(row => row.CheckedInByUserId.HasValue)
                .Select(row => row.CheckedInByUserId!.Value)
                .Distinct()
                .ToList(),
            cancellationToken);

        var items = page.Items.Select(row => new AdminDelegationMeetingRequestRow(
            row.Id,
            row.RequestingCountryId,
            row.RequestingCountry,
            row.TargetCountryId,
            row.TargetCountry,
            row.RequestedByUserId,
            row.AttendeeCount,
            row.Subject,
            row.Status,
            row.SlotStart,
            row.ResponseNote,
            row.CreatedAt,
            row.RespondedAt,
            row.CheckedInAt,
            ResolveOperatorName(operatorNames, row.CheckedInByUserId)))
            .ToList();

        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminDelegationMeetingRequestsListed,
            actorUserId,
            $"count={items.Count}; total={page.Total}",
            cancellationToken);

        return GridPage<AdminDelegationMeetingRequestRow>.Of(
            items, page.Total, page.Skip, page.Top);
    }

    public async Task<AdminDelegationMeetingRequestDetail> GetAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var detail = await LoadDetailAsync(id, cancellationToken);
        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminDelegationMeetingRequestViewed,
            actorUserId,
            $"requestId={id}",
            cancellationToken);
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

        // SES §7 validation triple-lock — ResponseNote maps to nvarchar(2000). Refuse an
        // over-length note up front: without this the trimmed value below overflows the
        // column, SaveChanges raises a truncation DbUpdateException, and the catch around
        // the transaction reports it as a misleading "That hall slot is no longer
        // available." 409 (nonsensical for a Cancel, which binds no slot at all). The
        // speaker respond path carries the same guard for the same reason.
        if ((request.ResponseNote ?? string.Empty).Trim().Length > MaxResponseNoteLength)
        {
            throw new ApiException(ErrorCodes.DelegationMeetingRequestInvalid, 400,
                $"The response note must be {MaxResponseNoteLength} characters or fewer.",
                $"يجب ألا يتجاوز نص الردّ {MaxResponseNoteLength} حرف.");
        }

        // Read once here so a missing request is a fast 404 that never opens a
        // Serializable transaction. The unit of work below re-reads the row inside the
        // transaction, which is the copy every decision is actually taken on.
        var req = await LoadTrackedRequestAsync(id, cancellationToken);

        var now = timeProvider.SimfNow();

        // Unified 3-button model. Status=Rejected is CANCEL (with a
        // justification note). Status=Accepted with a bound HallId is APPROVE
        // (VerbalConfirmed=false → AwaitingSpeaker, awaiting the other party's confirm)
        // or CONFIRM (VerbalConfirmed=true → Accepted, the admin has the
        // verbal confirmation). CONFIRM is independent of the hall: from Pending the CP
        // binds a fresh slot (HallId set); from AwaitingSpeaker (already Approved) it
        // sends HallId=null so the already-bound slot is kept — tying confirmVerbal to
        // bindHall would 409 that finalise-after-approve path, since a null HallId would
        // be read as a plain accept whose only legal source state is Pending. A legacy
        // accept without a hall keeps the requester-proposed-slot behaviour.
        var cancel = request.Status == MeetingRequestStatus.Rejected;
        var bindHall = request.Status == MeetingRequestStatus.Accepted && request.HallId is not null;
        var confirmVerbal = request.Status == MeetingRequestStatus.Accepted && request.VerbalConfirmed;

        // On Approve (→ AwaitingSpeaker) mint the single-use confirm token so it commits
        // atomically with the transition; its link is emailed to the target members below.
        // A request can therefore never be AwaitingSpeaker without its token. Staged ONCE
        // here, OUTSIDE the retryable block, so a serialization retry re-commits the same
        // token rather than minting a duplicate — the same reason the speaker mint sits
        // outside its own. The condition is the state machine below read forwards: a
        // Cancel is Rejected (never AwaitingSpeaker) and cannot be a bindHall, and an
        // Accept with a hall lands on AwaitingSpeaker unless it is a verbal Confirm. The
        // URL is empty when the public base URL is unconfigured (the members are then
        // emailed without a link, as before).
        // Staged INSIDE the transaction below, next to the transition it must commit
        // with. Staging it out here left it tracked as Added across the retryable block
        // and it never reached an insert, so an approve bound the hall and minted no
        // token at all. The duplicate a retry would otherwise mint is prevented by
        // dropping any Added token when the block re-enters, not by hoisting the stage.
        string? confirmUrl = null;

        // A cancel from a state where the target delegation was already asked to confirm
        // (AwaitingSpeaker) or had confirmed (Accepted) must retract that request from
        // them — set below and dispatched with the requester's outcome notification.
        var retractFromTargetMembers = false;

        // Close the CROSS-FAMILY hall double-book race. The hall free-slot read below
        // already subtracts BOTH request families, and the table guard scans all three
        // (the admin-arranged business meeting owns tables too) — but every one of those
        // scans used to run with no locks held, so a rival approve on another family
        // could commit between a scan and this save and both writes stuck. The
        // filtered-unique (HallId, SlotStart) index cannot backstop that: it lives on
        // DelegationMeetingRequests alone, so it never sees the speaker row, and it keys
        // on SlotStart, so it catches only EQUAL starts, not a 10:00-10:30 against a
        // 10:15-10:45. Running the hall, table and delegation range scans and the write
        // in ONE Serializable transaction makes those scans hold key-range locks, so the
        // rival cannot slip in and the two serialize with one losing. Go through the EF
        // execution strategy so this composes with EnableRetryOnFailure (a bare user
        // transaction throws under the retrying strategy); on a serialization/deadlock
        // failure the strategy re-runs the whole unit and the re-scan sees the
        // now-committed rival and raises the clean 409. Same pattern as
        // SpeakerMeetingRequestService and BusinessMeetingService, which this now
        // serializes against.
        var strategy = appDbContext.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await appDbContext.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable, cancellationToken);

                // The strategy re-runs this whole lambda after a serialization failure,
                // and unlike its two siblings this unit READS the request's own status
                // and slot to decide what to do. The instance a failed attempt mutated is
                // still tracked, and EF identity resolution would hand that stale copy
                // back instead of the rolled-back row, so drop it and read again inside
                // the transaction — which also puts the already-responded guard under the
                // same lock. The staged confirm token is a different entity type and
                // stays tracked, so it is committed exactly once by the winning attempt.
                appDbContext.Entry(req).State = EntityState.Detached;
                req = await LoadTrackedRequestAsync(id, cancellationToken);

                // A failed attempt leaves its staged token tracked as Added. Drop it and
                // stage a fresh one, so the winning attempt commits exactly one.
                foreach (var staged in appDbContext.ChangeTracker
                    .Entries<DelegationMeetingActionToken>()
                    .Where(entry => entry.State == EntityState.Added)
                    .ToList())
                {
                    staged.State = EntityState.Detached;
                }

                confirmUrl = bindHall && !confirmVerbal
                    ? meetingActionTokens.StageDelegationConfirmToken(id)
                    : null;

                if (cancel)
                {
                    // Cancel/Decline is allowed from any non-terminal state: a Pending
                    // decline → Rejected; a post-approval cancel releases the held slot
                    // → Cancelled.
                    if (req.Status is MeetingRequestStatus.Rejected or MeetingRequestStatus.Cancelled
                        or MeetingRequestStatus.Done)
                    {
                        throw new ApiException(ErrorCodes.AppRequestAlreadyResponded, 409,
                            "This meeting request has already been responded to.",
                            "تمت معالجة طلب المقابلة هذا بالفعل.");
                    }
                    var wasPending = req.Status == MeetingRequestStatus.Pending;
                    // The target members were notified only once the request left Pending
                    // (Approve), so only a non-Pending cancel needs a retraction notice.
                    retractFromTargetMembers = !wasPending;
                    req.Status = wasPending
                        ? MeetingRequestStatus.Rejected : MeetingRequestStatus.Cancelled;
                    // Release any held hall slot so it frees up for another meeting.
                    req.HallId = null;
                    req.MeetingTableId = null;
                }
                else
                {
                    // Approve is valid only from Pending; Confirm may also finalise a
                    // previously Approved (AwaitingSpeaker) request.
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
                    else if (req.Status == MeetingRequestStatus.Pending
                        && req.SlotStart is { } sStart && req.SlotEnd is { } sEnd)
                    {
                        // Legacy accept-without-hall from PENDING — honour the
                        // requester-proposed slot with the past + cross-country overlap
                        // guard. This guard must NOT run on a verbal Confirm of an
                        // already-Approved (AwaitingSpeaker) request: that slot was
                        // validated + bound at approve time, so re-checking "in the past"
                        // here would spuriously 400 (and strand the meeting) once the
                        // bound slot's start has passed — the natural case for a
                        // meet-then-confirm at the hall.
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
                        // Approve with a hall → AwaitingSpeaker; legacy accept-without-hall
                        // → Accepted.
                        req.Status = bindHall
                            ? MeetingRequestStatus.AwaitingSpeaker : MeetingRequestStatus.Accepted;
                    }
                }

                req.ResponseNote = string.IsNullOrWhiteSpace(request.ResponseNote)
                    ? null : request.ResponseNote.Trim();
                req.RespondedAt = now;
                req.RespondedByUserId = actorUserId;

                await appDbContext.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            });
        }
        catch (DbUpdateException)
        {
            // The (HallId, SlotStart) filtered-unique index is the equal-start backstop
            // within this family: a concurrent delegation approve that won the index race
            // after both passed the free-slot re-check surfaces here. It is non-transient,
            // so the execution strategy does not retry it; return the same clean 409 the
            // re-check would have. Data integrity is intact — the index prevented the
            // double-book. The over-length note that used to reach here as a misleading
            // conflict is refused as a 400 at the top of this method.
            throw new ApiException(ErrorCodes.DelegationMeetingRequestInvalid, 409,
                "That hall slot is no longer available.",
                "لم تعد فترة القاعة هذه متاحة.");
        }

        await auditLog.WriteSuccessAsync(
            AuditEvents.DelegationMeetingRequestResponded,
            actorUserId,
            $"requestId={req.Id}; status={req.Status}",
            cancellationToken);

        // The detail (returned below) carries the requester email and the target
        // country name — load it once and reuse the name for the notification body
        // (no separate target-country round-trip).
        var detail = await LoadDetailAsync(id, cancellationToken);

        // The respond response discloses the requester email, so SOC must see
        // one Viewed event per disclosure regardless of which endpoint emitted it.
        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminDelegationMeetingRequestViewed,
            actorUserId,
            $"requestId={req.Id}",
            cancellationToken);

        // Notify (and email) the requesting delegate — they are a SimfUser, so the
        // dispatcher's email path applies. Best-effort.
        // Every outcome emails the requester. The comment here always promised
        // "in-app + email too" for a decline, but the decline arm passed SendEmail=false,
        // so a declined/cancelled requester only ever saw an in-app row.
        var (title, titleAr, body, bodyAr, kind) = req.Status switch
        {
            MeetingRequestStatus.Accepted => (
                "Delegation meeting confirmed", "تم تأكيد اجتماع الوفد",
                $"Your delegation meeting with {detail.TargetCountry} is confirmed.",
                $"تم تأكيد اجتماع وفدك مع {detail.TargetCountry}.",
                NotificationKind.MeetingScheduled),
            MeetingRequestStatus.AwaitingSpeaker => (
                "Delegation meeting approved", "تمت الموافقة على اجتماع الوفد",
                $"Your delegation meeting with {detail.TargetCountry} was approved and is awaiting confirmation.",
                $"تمت الموافقة على اجتماع وفدك مع {detail.TargetCountry} وهو بانتظار التأكيد.",
                NotificationKind.MeetingScheduled),
            _ => (
                "Delegation meeting declined", "تم رفض اجتماع الوفد",
                $"Your delegation meeting with {detail.TargetCountry} was declined.",
                $"تم رفض اجتماع وفدك مع {detail.TargetCountry}.",
                NotificationKind.MeetingCancelled),
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
            SendEmail = true,
            // A proper meeting notice (both delegations + topic + Saudi local time
            // + hall) in the same style as the target-member link email, instead of the
            // dispatcher's bare HtmlEncode'd single paragraph. Deliberately carries NO
            // action link: the confirm token authorises whoever holds it, and the
            // REQUESTER must never be able to confirm their own meeting — that link goes
            // only to the target delegation (EmailMemberConfirmLinkAsync).
            PreRenderedEmailHtml = BuildOutcomeEmailHtml(body, detail, req),
        }, logger, cancellationToken);

        // On Approve (AwaitingSpeaker) notify the OTHER PARTY (each eligible
        // target-delegation member) by app + email so they can confirm on tap
        // (or by the email link). A verbal Confirm skips this (already Accepted).
        if (req.Status == MeetingRequestStatus.AwaitingSpeaker)
        {
            await NotifyTargetMembersAsync(req, NotificationKind.MeetingRequested,
                "Delegation meeting request", "طلب اجتماع وفد",
                $"A meeting request from {detail.RequestingCountry} is awaiting your confirmation.",
                $"طلب اجتماع من {detail.RequestingCountry} بانتظار تأكيدك.",
                cancellationToken,
                // Carry the confirm link; each member is emailed it (and the
                // in-app card deep-links to the same tap-confirm).
                confirmUrl: confirmUrl, requestingCountry: detail.RequestingCountry);
        }
        else if (retractFromTargetMembers)
        {
            // Cancel of an approved/confirmed meeting — tell the target members it is off
            // so they are not left tapping a stale "please confirm" prompt (which 409s).
            await NotifyTargetMembersAsync(req, NotificationKind.MeetingCancelled,
                "Delegation meeting cancelled", "تم إلغاء اجتماع الوفد",
                $"The delegation meeting request from {detail.RequestingCountry} was cancelled.",
                $"تم إلغاء طلب اجتماع الوفد من {detail.RequestingCountry}.",
                cancellationToken);
        }
        else if (req.Status == MeetingRequestStatus.Accepted)
        {
            // The verbal-Confirm arm. Before this branch the chain notified the
            // target members only on Approve (AwaitingSpeaker) and on a post-approval
            // cancel, so a Confirm straight from Pending — the admin already holds the
            // verbal agreement and books it outright — told the TARGET delegation
            // nothing at all; their first notice was the 15-minute reminder. A Confirm
            // from AwaitingSpeaker lands here too: "please confirm" becomes "it is
            // booked", which is the outcome of the prompt they were already sent.
            var when = req.SlotStart.FormatSaudi(fallback: string.Empty);
            var whenEn = when.Length == 0 ? string.Empty : $" on {when}";
            var whenAr = when.Length == 0 ? string.Empty : $" بتاريخ {when}";
            await NotifyTargetMembersAsync(req, NotificationKind.MeetingScheduled,
                "Delegation meeting scheduled", "تم جدولة اجتماع الوفد",
                $"Your delegation meeting with {detail.RequestingCountry} is confirmed{whenEn}.",
                $"تم تأكيد اجتماع وفدكم مع {detail.RequestingCountry}{whenAr}.",
                cancellationToken);
        }

        return detail;
    }

    public async Task<AdminDelegationMeetingRequestDetail> ConfirmByOtherPartyAsync(
        Guid callerUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var req = await LoadAwaitingForOtherPartyAsync(callerUserId, id, cancellationToken);

        var now = timeProvider.SimfNow();

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

        await auditLog.WriteSuccessAsync(
            AuditEvents.DelegationMeetingRequestResponded,
            callerUserId,
            $"requestId={id}; status=Accepted; confirmedByOtherParty=true",
            cancellationToken);

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

        // This is an APP caller (the other-party confirmer), not an admin — never disclose
        // the requester's Identity login email over the app wire. LoadDetailAsync resolves
        // it for the admin desks (an audited surface); strip it here so a peer app user
        // cannot read another user's private email from the confirm response.
        return detail with { RequesterEmail = null };
    }

    /// <summary>The decline twin of <see cref="ConfirmByOtherPartyAsync"/>. Before
    /// this, the target delegation's only exits from an Approved (AwaitingSpeaker) meeting
    /// were to confirm it or to wait for an admin cancel. Same authorization model, same
    /// audit event, same notification treatment; flips AwaitingSpeaker → Rejected (the
    /// status the speaker decline-link already produces) and releases the held hall slot
    /// so it frees up immediately.</summary>
    public async Task<AdminDelegationMeetingRequestDetail> DeclineByOtherPartyAsync(
        Guid callerUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var req = await LoadAwaitingForOtherPartyAsync(callerUserId, id, cancellationToken);

        var now = timeProvider.SimfNow();

        // Race-safe conditional flip AwaitingSpeaker → Rejected (a concurrent confirm or a
        // second decline matches 0 rows and 409s cleanly). Rejected is not a slot-holding
        // state, and clearing the hall/table binding releases the slot for another meeting
        // exactly as the admin cancel path does.
        var affected = await appDbContext.DelegationMeetingRequests
            .Where(r => r.Id == id && r.Status == MeetingRequestStatus.AwaitingSpeaker)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, MeetingRequestStatus.Rejected)
                .SetProperty(r => r.HallId, (Guid?)null)
                .SetProperty(r => r.MeetingTableId, (Guid?)null)
                .SetProperty(r => r.RespondedAt, now), cancellationToken);
        if (affected == 0)
        {
            throw new ApiException(ErrorCodes.AppRequestAlreadyResponded, 409,
                "This meeting is not awaiting confirmation.",
                "هذا الاجتماع ليس بانتظار التأكيد.");
        }

        await auditLog.WriteSuccessAsync(
            AuditEvents.DelegationMeetingRequestResponded,
            callerUserId,
            $"requestId={id}; status=Rejected; declinedByOtherParty=true",
            cancellationToken);

        var detail = await LoadDetailAsync(id, cancellationToken);

        // Tell the requester their meeting was declined — in-app + email, matching the
        // confirm path — a decline always reaches the requester.
        await notifications.TryDispatchAsync(new NotificationRequest
        {
            UserId = req.RequestedByUserId,
            Kind = NotificationKind.MeetingCancelled,
            Title = "Delegation meeting declined",
            TitleArabic = "تم رفض اجتماع الوفد",
            Body = $"{detail.TargetCountry} could not confirm your delegation meeting.",
            BodyArabic = $"تعذّر على {detail.TargetCountry} تأكيد اجتماع وفدكم.",
            Severity = NotificationSeverity.Info,
            RelatedEntityType = nameof(DelegationMeetingRequest),
            RelatedEntityId = id,
            SendEmail = true,
            PreRenderedEmailHtml = BuildOutcomeEmailHtml(
                $"{detail.TargetCountry} could not confirm your delegation meeting.",
                detail, req),
        }, logger, cancellationToken);

        // The decline kills the meeting for the WHOLE target delegation, not just the
        // member who tapped. Every other eligible member still holds the "awaiting your
        // confirmation" card + the emailed confirm link from approve time, and both now
        // dead-end (409 APP_REQUEST_ALREADY_RESPONDED). Retract the prompt through the same
        // helper the admin cancel uses; the decliner is skipped — they just acted and
        // already have the response.
        await NotifyTargetMembersAsync(req, NotificationKind.MeetingCancelled,
            "Delegation meeting cancelled", "تم إلغاء اجتماع الوفد",
            $"The delegation meeting request from {detail.RequestingCountry} was declined by your delegation.",
            $"تم رفض طلب اجتماع الوفد من {detail.RequestingCountry} من قِبل وفدكم.",
            cancellationToken, excludeUserId: callerUserId);

        // Same PII rule as the confirm response — an app peer never sees the requester's
        // Identity login email.
        return detail with { RequesterEmail = null };
    }

    public async Task RetractTargetMemberPromptsAsync(
        Guid requestId, CancellationToken cancellationToken = default)
    {
        // The requester's own withdraw flips the status in MyRequestsService and
        // has no delegation-notification surface, so the retraction lives here next to the
        // admin cancel's. Projected read (no Identity round-trip, no cross-DB join): the
        // retraction only needs the target delegation and the requesting country's name.
        var req = await appDbContext.DelegationMeetingRequests.AsNoTracking()
            .Where(r => r.Id == requestId)
            .Select(r => new { Request = r, RequestingCountry = r.RequestingCountry!.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (req is null)
        {
            return;
        }

        await NotifyTargetMembersAsync(req.Request, NotificationKind.MeetingCancelled,
            "Delegation meeting cancelled", "تم إلغاء اجتماع الوفد",
            $"The delegation meeting request from {req.RequestingCountry} was cancelled.",
            $"تم إلغاء طلب اجتماع الوفد من {req.RequestingCountry}.",
            cancellationToken);
    }

    // The shared other-party guard for confirm + decline: the request must exist, be
    // Approved (AwaitingSpeaker), and the caller must be an eligible member of the TARGET
    // delegation (their profile country is the target country AND they hold the
    // admin-assigned delegation-meeting flag).
    private async Task<DelegationMeetingRequest> LoadAwaitingForOtherPartyAsync(
        Guid callerUserId, Guid id, CancellationToken cancellationToken)
    {
        var req = await appDbContext.DelegationMeetingRequests.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.DelegationMeetingRequestNotFound, 404,
                "Delegation meeting request not found.",
                "لم يتم العثور على طلب اجتماع الوفد.");

        if (req.Status != MeetingRequestStatus.AwaitingSpeaker)
        {
            throw new ApiException(ErrorCodes.AppRequestAlreadyResponded, 409,
                "This meeting is not awaiting confirmation.",
                "هذا الاجتماع ليس بانتظار التأكيد.");
        }

        var isTargetMember = await appDbContext.UserProfiles.AsNoTracking()
            .AnyAsync(p => p.UserId == callerUserId
                && p.NationalityId == req.TargetCountryId
                && p.AllowsDelegationMeeting, cancellationToken);
        if (!isTargetMember)
        {
            throw new ApiException(ErrorCodes.Forbidden, 403,
                "You are not permitted to respond to this meeting.",
                "غير مسموح لك بالردّ على هذا الاجتماع.");
        }

        return req;
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
        var now = timeProvider.SimfNow();
        req.Status = MeetingRequestStatus.Done;
        req.CheckedInAt = now;
        req.CheckedInByUserId = actorUserId;
        await appDbContext.SaveChangesAsync(cancellationToken);
        await auditLog.WriteSuccessAsync(
            AuditEvents.DelegationMeetingRequestCheckedIn,
            actorUserId,
            $"requestId={id}",
            cancellationToken);
        return await LoadDetailAsync(id, cancellationToken);
    }

    // Dispatch a notification to every eligible member of the target
    // delegation (profile country == target country AND AllowsDelegationMeeting). App row
    // (deep-link from NotificationKindCatalog) + email. Members are resolved on the App DB;
    // their emails are resolved by the dispatcher (Identity) — no cross-DB JOIN. Used on
    // Approve (request-to-confirm) and on every retraction — admin cancel-after-approval,
    // the other party's decline and the requester's own withdraw — so nobody is
    // left with a stale "please confirm" prompt. <paramref name="excludeUserId"/> skips the
    // member who triggered the retraction themselves (the decliner already has the answer).
    private async Task NotifyTargetMembersAsync(
        DelegationMeetingRequest req, NotificationKind kind,
        string title, string titleArabic, string body, string bodyArabic,
        CancellationToken cancellationToken,
        string? confirmUrl = null, string? requestingCountry = null,
        Guid? excludeUserId = null)
    {
        // When we have a usable confirm link (the Approve case), the members
        // get a clean in-app card (SendEmail=false, it deep-links to tap-confirm) PLUS a
        // dedicated link email; otherwise (retract, or an unconfigured base URL) the
        // dispatcher email carries the notice as before.
        var hasLink = !string.IsNullOrEmpty(confirmUrl);

        // A delegation member who holds no account has neither an in-app inbox nor
        // an Identity address, so there is nothing to dispatch to and nothing to
        // email — they drop out here rather than resolve to a missing user.
        var memberIds = await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.UserId != null
                && p.NationalityId == req.TargetCountryId && p.AllowsDelegationMeeting
                && (excludeUserId == null || p.UserId != excludeUserId))
            .Select(p => p.UserId!.Value)
            .ToListAsync(cancellationToken);
        foreach (var userId in memberIds)
        {
            await notifications.TryDispatchAsync(new NotificationRequest
            {
                UserId = userId,
                Kind = kind,
                Title = title,
                TitleArabic = titleArabic,
                Body = body,
                BodyArabic = bodyArabic,
                Severity = NotificationSeverity.Info,
                RelatedEntityType = nameof(DelegationMeetingRequest),
                RelatedEntityId = req.Id,
                SendEmail = !hasLink,
            }, logger, cancellationToken);

            if (hasLink)
            {
                await EmailMemberConfirmLinkAsync(
                    userId, confirmUrl!, req, requestingCountry ?? string.Empty, cancellationToken);
            }
        }
    }

    // Email one target-delegation member the confirm link (best-effort, like
    // the speaker links email). The member is a SimfUser, so the address is resolved on
    // Identity; a missing address or a queue failure never rolls back the committed Approve.
    private async Task EmailMemberConfirmLinkAsync(
        Guid memberUserId, string confirmUrl, DelegationMeetingRequest req,
        string requestingCountry, CancellationToken cancellationToken)
    {
        var email = await userDirectory.GetEmailAsync(memberUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }
        var slot = req.SlotStart is { } s
            ? s.FormatSaudi()
            : "to be scheduled";
        var html =
            $"<p>Your delegation has a meeting request from <strong>{HtmlEnc(requestingCountry)}</strong>.</p>"
            + $"<p>Topic: {HtmlEnc(req.Subject)}<br/>Proposed time: {HtmlEnc(slot)}</p>"
            + "<p>Please confirm — any one of your delegation confirming books the meeting:</p>"
            + $"<p><a href=\"{HtmlEnc(confirmUrl)}\">Confirm the meeting</a></p>"
            + "<p style=\"color:#666\">This link expires in 72 hours and can be used once.</p>";
        await emailQueue.TryEnqueueAsync(
            new EmailMessage(email!, "SIMF — please confirm a delegation meeting", html),
            purpose: "DelegationMeetingConfirm",
            subjectEmail: email!,
            subjectUserId: memberUserId,
            auditLog, logger, cancellationToken);
    }

    private static string HtmlEnc(string value) => System.Net.WebUtility.HtmlEncode(value);

    // Bi-Meeting rework — bind the meeting to a free hall slot on Approve/Confirm,
    // mirroring SpeakerMeetingRequestService.BindHallSlotAsync. Validates the hall
    // hosts meetings, the picked slot is currently free (hall availability already
    // subtracts the bound meetings of BOTH request families), neither delegation
    // overlaps, and the optional table belongs to the hall. Every scan it runs is
    // called from inside the caller's Serializable transaction, so the key-range locks
    // they take are what serializes a concurrent speaker or business-meeting write;
    // the (HallId, SlotStart) filtered-unique index remains the equal-start backstop
    // within this family.
    private async Task BindDelegationHallSlotAsync(
        DelegationMeetingRequest req,
        RespondToDelegationMeetingRequestRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var hallId = request.HallId!.Value;
        if (request.SlotStart is not { } start
            || request.SlotEnd is not { } end || end <= start)
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
        if (!slots.Any(s => s.Start == start && s.End == end))
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
            await MeetingTableOverlapGuard.EnsureTableIsFreeAsync(
                appDbContext, tableId, start, end,
                ErrorCodes.DelegationMeetingRequestInvalid,
                excludeDelegationRequestId: req.Id,
                excludeSpeakerRequestId: null,
                excludeBusinessMeetingId: null,
                cancellationToken);
            req.MeetingTableId = tableId;
        }
        req.HallId = hallId;
        req.SlotStart = start;
        req.SlotEnd = end;
    }

    // Neither delegation (as requester OR target) may already hold a LIVE meeting
    // (`MeetingRequestStatuses.SlotHolding`) overlapping [start, end) — the cross-country
    // double-book guard. The half-open range scan runs inside the respond path's
    // Serializable transaction, so it holds the key-range lock that serializes a
    // concurrent overlapping approve rather than merely reading around it.
    private async Task GuardDelegationOverlapAsync(
        DelegationMeetingRequest req, DateTime start, DateTime end,
        CancellationToken cancellationToken)
    {
        var reqId = req.Id;
        var homeCountryId = req.RequestingCountryId;
        var targetCountryId = req.TargetCountryId;
        var overlaps = await appDbContext.DelegationMeetingRequests.AsNoTracking()
            .Where(r => r.Id != reqId
                && MeetingRequestStatuses.SlotHolding.Contains(r.Status)
                && r.SlotStart != null && r.SlotEnd != null
                && (r.RequestingCountryId == homeCountryId || r.TargetCountryId == homeCountryId
                    || r.RequestingCountryId == targetCountryId || r.TargetCountryId == targetCountryId))
            .AnyAsync(r => r.SlotStart < end && start < r.SlotEnd, cancellationToken);
        if (overlaps)
        {
            throw new ApiException(ErrorCodes.DelegationMeetingRequestInvalid, 409,
                "One of the delegations already has a meeting at that time.",
                "لدى أحد الوفدين اجتماع بالفعل في ذلك الوقت.");
        }
    }

    // The shared outcome-email body for the REQUESTER (confirmed / approved /
    // declined). Same markup vocabulary as EmailMemberConfirmLinkAsync so the two
    // delegation emails read as one template family. Saudi local time only.
    private static string BuildOutcomeEmailHtml(
        string body, AdminDelegationMeetingRequestDetail detail, DelegationMeetingRequest req)
    {
        var slot = req.SlotStart is { } s ? s.FormatSaudi() : "to be scheduled";
        return $"<p>{HtmlEnc(body)}</p>"
            + $"<p>Delegations: <strong>{HtmlEnc(detail.RequestingCountry)}</strong>"
            + $" &nbsp;&mdash;&nbsp; <strong>{HtmlEnc(detail.TargetCountry)}</strong></p>"
            + $"<p>Topic: {HtmlEnc(req.Subject)}<br/>Time: {HtmlEnc(slot)}</p>"
            + (string.IsNullOrWhiteSpace(req.ResponseNote)
                ? string.Empty
                : $"<p>Note: {HtmlEnc(req.ResponseNote!)}</p>")
            + "<p style=\"color:#666\">You can follow this request in the SIMF app under"
            + " &quot;My requests&quot;.</p>";
    }

    /// <summary>The check-in operator's display name for one row, or null
    /// when the meeting has not been checked in (or the operator account is gone).
    /// The map comes from the single per-page Identity-DB lookup above.</summary>
    private static string? ResolveOperatorName(
        IReadOnlyDictionary<Guid, string> namesByUserId, Guid? checkedInByUserId)
    {
        if (checkedInByUserId is not { } userId) { return null; }
        return namesByUserId.TryGetValue(userId, out var name) ? name : null;
    }

    /// <summary>The tracked request row the respond unit of work mutates, or a bilingual
    /// 404. Called twice per respond — once up front for the fast miss, once inside the
    /// Serializable transaction for the copy the decision is actually taken on.</summary>
    private async Task<DelegationMeetingRequest> LoadTrackedRequestAsync(
        Guid id, CancellationToken cancellationToken) =>
        await appDbContext.DelegationMeetingRequests
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.DelegationMeetingRequestNotFound, 404,
                "Delegation meeting request not found.",
                "لم يتم العثور على طلب اجتماع الوفد.");

    private async Task<AdminDelegationMeetingRequestDetail> LoadDetailAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var detail = await appDbContext.DelegationMeetingRequests.AsNoTracking()
            .Where(request => request.Id == id)
            .Select(request => new
            {
                request.Id, request.RequestedByUserId, request.AttendeeCount,
                request.Subject, request.Status, request.SlotStart, request.SlotEnd,
                request.ResponseNote, request.CreatedAt, request.RespondedAt,
                RequestingCountry = request.RequestingCountry!.Name,
                TargetCountry = request.TargetCountry!.Name,
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(ErrorCodes.DelegationMeetingRequestNotFound, 404,
                "Delegation meeting request not found.",
                "لم يتم العثور على طلب اجتماع الوفد.");

        var email = await userDirectory.GetEmailAsync(
            detail.RequestedByUserId, cancellationToken);

        return new AdminDelegationMeetingRequestDetail(
            detail.Id, detail.RequestingCountry, detail.TargetCountry,
            detail.RequestedByUserId, email,
            detail.AttendeeCount, detail.Subject, detail.Status,
            detail.SlotStart, detail.SlotEnd,
            detail.ResponseNote, detail.CreatedAt, detail.RespondedAt);
    }
}
