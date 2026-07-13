// Tests: SIMF.Api.Tests/SpeakerMeetingRequestsTests.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
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

/// <summary>D-269 (Mockup page 20 "Speaker profile") — speaker meeting-request
/// service. Submission validates the speaker is active and opted in
/// (<c>AllowsMeetingRequests</c>); admin response sets RespondedAt +
/// RespondedByUserId. Audit-only, no notification (consistent with the
/// now-removed session-scoped flow, D-278).</summary>
internal sealed class SpeakerMeetingRequestService(
    SimfAppDbContext appDbContext,
    IIdentityUserDirectory userDirectory,
    ISpeakerAvailabilityService availability,
    IHallAvailabilityService hallAvailability,
    IMeetingActionTokenService meetingActionTokens,
    INotificationDispatcher notifications,
    IEmailQueue emailQueue,
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

        // D-729 (owner item 15) — requesting a speaker meeting is now VIP-only
        // (VVIP/VIP tier, via ProfileType.AllowsVipMeetingSlots). Previously any
        // approved attendee could submit a topic-only request and VIP was
        // required only to *book a slot* (the later check). The owner restricted
        // the whole request to VIP guests: the app hides the CTA for non-VIP
        // (UserProfileResponse.IsVip) and this is the server-side backstop. The
        // slot-only VIP check below is now subsumed but kept as defence in depth.
        if (!await IsVipAsync(requesterUserId, cancellationToken))
        {
            throw new ApiException(
                ErrorCodes.Forbidden, 403,
                "Requesting a speaker meeting is available to VIP guests only.",
                "طلب مقابلة المتحدّث متاح لضيوف كبار الشخصيات فقط.");
        }

        // A1 — one open request per (requester, speaker): a duplicate Pending
        // submission floods the review queue and (for VIP slots) stacks rival
        // claims on one slot. The DB filtered-unique backstop is a Wave B item.
        var hasOpenRequest = await appDbContext.SpeakerMeetingRequests.AsNoTracking()
            .AnyAsync(r => r.RequestedByUserId == requesterUserId
                && r.SpeakerId == speakerId
                && r.Status == MeetingRequestStatus.Pending, cancellationToken);
        if (hasOpenRequest)
        {
            throw new ApiException(
                ErrorCodes.AppRequestDuplicatePending, 409,
                "You already have a pending meeting request for this speaker.",
                "لديك بالفعل طلب مقابلة قيد المراجعة لهذا المتحدّث.");
        }

        // D-474 (#11) — the VIP slot flow: when the requester picked a slot, they
        // must be a VIP/VVIP and the slot must still be free. A null slot is the
        // legacy topic-only request (any approved attendee).
        DateTimeOffset? slotStart = null;
        DateTimeOffset? slotEnd = null;
        Guid? availabilityWindowId = null;
        if (request.SlotStartUtc is { } pickedStart)
        {
            if (request.SlotEndUtc is not { } pickedEnd || pickedEnd <= pickedStart)
            {
                throw new ApiException(
                    ErrorCodes.SpeakerMeetingRequestInvalid, 400,
                    "A valid meeting slot (start and end) is required.",
                    "يلزم اختيار فترة اجتماع صحيحة (بداية ونهاية).");
            }
            if (!await IsVipAsync(requesterUserId, cancellationToken))
            {
                throw new ApiException(
                    ErrorCodes.Forbidden, 403,
                    "Booking a meeting slot is available to VIP guests only.",
                    "حجز فترة اجتماع متاح لضيوف كبار الشخصيات فقط.");
            }
            var slots = await availability.GetAvailableSlotsAsync(speakerId, cancellationToken);
            if (!slots.Any(s => s.StartUtc == pickedStart && s.EndUtc == pickedEnd))
            {
                throw new ApiException(
                    ErrorCodes.SpeakerMeetingRequestInvalid, 409,
                    "That slot is no longer available.",
                    "لم تعد هذه الفترة متاحة.");
            }
            slotStart = pickedStart;
            slotEnd = pickedEnd;

            // D-612 — persist which availability window the picked slot came from
            // (the D-611 SpeakerMeetingRequests.AvailabilityWindowId FK, SetNull).
            // The slot falls inside exactly one active window; resolve it by range.
            availabilityWindowId = await appDbContext.SpeakerAvailabilityWindows.AsNoTracking()
                .Where(w => w.SpeakerId == speakerId && w.IsActive
                    && w.StartUtc <= pickedStart && w.EndUtc >= pickedEnd)
                .Select(w => (Guid?)w.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        var req = new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speakerId,
            RequestedByUserId = requesterUserId,
            RequesterName = name,
            Subject = subject,
            SlotStartUtc = slotStart,
            SlotEndUtc = slotEnd,
            AvailabilityWindowId = availabilityWindowId,
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
        var (skip, top) = query.ClampPage(25, 200);

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
            skip, top);
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
        if (request.Status is not (MeetingRequestStatus.Accepted or MeetingRequestStatus.Rejected))
        {
            throw new ApiException(
                ErrorCodes.SpeakerMeetingRequestStatusInvalid, 400,
                "Response status must be Accepted or Rejected.",
                "يجب أن تكون حالة الردّ مقبولة أو مرفوضة.");
        }

        // SES §7 validation triple-lock — ResponseNote maps to nvarchar(2000)
        // (SpeakerMeetingRequestConfiguration.HasMaxLength(2000)). Reject an
        // over-length note up front with a clear 400 rather than letting the trimmed
        // value below overflow the column and throw a truncation DbUpdateException,
        // which the catch around SaveChanges would mask as a misleading "That slot is
        // no longer available." 409 (nonsensical for a Reject that has no slot).
        if ((request.ResponseNote ?? string.Empty).Trim().Length > 2000)
        {
            throw new ApiException(
                ErrorCodes.SpeakerMeetingRequestInvalid, 400,
                "The response note must be 2000 characters or fewer.",
                "يجب ألا يتجاوز نص الردّ 2000 حرف.");
        }

        var req = await appDbContext.SpeakerMeetingRequests
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SpeakerMeetingRequestNotFound, 404,
                "Speaker meeting request not found.",
                "لم يتم العثور على طلب مقابلة المتحدّث.");

        // A1 — only a Pending request may be decided. Without this guard a second
        // Accept could double-book a VIP slot and any prior decision could be
        // silently overwritten.
        if (req.Status != MeetingRequestStatus.Pending)
        {
            throw new ApiException(
                ErrorCodes.AppRequestAlreadyResponded, 409,
                "This meeting request has already been responded to.",
                "تمت معالجة طلب المقابلة هذا بالفعل.");
        }

        // D-716 (item 7, GAP-2) — accept-with-hall (Option A): the admin bound the
        // meeting to a free hall slot, so the picked hall slot becomes the meeting
        // time and the request moves to AwaitingSpeaker (awaiting the speaker's own
        // confirmation, Slice C). An accept with no HallId keeps the legacy
        // straight-to-Accepted behaviour below.
        var bindHall = request.Status == MeetingRequestStatus.Accepted
            && request.HallId is not null;

        var now = timeProvider.GetUtcNow();

        // D-717 — stage the speaker Approve/Reject tokens into the SAME unit of work
        // as the AwaitingSpeaker transition (they are durable domain state, not a
        // notification): the SaveChanges inside the transaction below commits status +
        // tokens atomically, so the request can never be AwaitingSpeaker without its
        // token pair. Staged ONCE here, before the retryable block, so a serialization
        // retry re-commits the same pair rather than minting a duplicate. Only the
        // email that follows is best-effort.
        var links = bindHall ? meetingActionTokens.StageTokensForRequest(req.Id) : null;

        // FIX #22 (R-1 held item) — close the CONCURRENT speaker double-book race. The
        // app-level overlap re-check (SpeakerHasOverlappingMeetingAsync) already blocks
        // the sequential case, but two concurrent accepts of overlapping-but-different-
        // start slots can each pass the check before either commits, and the frozen
        // (SpeakerId, SlotStartUtc) filtered-unique index only catches an EQUAL-start
        // collision. Running the half-open range scan and the status flip in ONE
        // Serializable transaction makes the scan hold key-range locks, so a concurrent
        // overlapping accept cannot slip its write in between our check and our save —
        // the two serialize and one loses. Go through the EF execution strategy so this
        // composes with EnableRetryOnFailure (a bare user transaction throws under the
        // retrying strategy); on the serialization/deadlock failure the strategy re-runs
        // the whole unit and the re-check sees the now-committed rival and raises the
        // clean 409. Same pattern as BusinessMeetingService (M-5).
        var strategy = appDbContext.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await appDbContext.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable, cancellationToken);

                if (bindHall)
                {
                    await BindHallSlotAsync(req, request, cancellationToken);
                }
                else if (request.Status == MeetingRequestStatus.Accepted
                    && req.SlotStartUtc is { } slotStart && req.SlotEndUtc is { } slotEnd)
                {
                    // A1 — accepting a slot-bearing request must re-check the slot is
                    // still free among the speaker's LIVE meetings (Accepted OR
                    // AwaitingSpeaker, per the shared helper; D-716). Inside the
                    // Serializable transaction this half-open range scan holds the
                    // key-range lock that serializes a concurrent overlapping accept.
                    if (await SpeakerHasOverlappingMeetingAsync(
                            req.SpeakerId, req.Id, slotStart, slotEnd, cancellationToken))
                    {
                        throw new ApiException(
                            ErrorCodes.SpeakerMeetingRequestInvalid, 409,
                            "That slot is no longer available.",
                            "لم تعد هذه الفترة متاحة.");
                    }

                    // M-7 — the requester must not already hold another live meeting then.
                    if (await RequesterHasOverlappingMeetingAsync(
                            req.RequestedByUserId, req.Id, slotStart, slotEnd, cancellationToken))
                    {
                        throw new ApiException(
                            ErrorCodes.SpeakerMeetingRequestInvalid, 409,
                            "The requester already has a meeting booked at that time.",
                            "لدى مقدّم الطلب اجتماع محجوز بالفعل في هذا الوقت.");
                    }
                }

                req.Status = bindHall ? MeetingRequestStatus.AwaitingSpeaker : request.Status;
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
            // D-716 — the (hall|speaker, SlotStartUtc) filtered-unique index is the
            // equal-start backstop: a concurrent accept that won the index race after
            // both passed the app-level re-check surfaces here. It is non-transient, so
            // the execution strategy does not retry it; return the same clean 409 the
            // app re-check would have (mirrors SeatReservationService's uniqueness
            // guard). Data integrity is intact — the index prevented the double-book.
            throw new ApiException(
                ErrorCodes.SpeakerMeetingRequestInvalid, 409,
                "That slot is no longer available.",
                "لم تعد هذه الفترة متاحة.");
        }

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

        // D-474 (#11) — notify the requester in-app, and on Accept email the speaker.
        // D-717 — an accept-WITH-hall is not a terminal decision: instead of the
        // outcome notification it emails the SPEAKER a double-opt-in Approve/Reject
        // link (the tokens were already committed atomically above). The requester
        // "confirmed"/"declined" notification fires only when the speaker acts.
        if (bindHall)
        {
            await meetingActionTokens.AuditMintedAsync(req.Id, cancellationToken);
            await EmailSpeakerConfirmationLinksAsync(req, links!, cancellationToken);
        }
        else
        {
            await NotifyOutcomeAsync(req, cancellationToken);
        }

        return await LoadDetailAsync(id, cancellationToken);
    }

    // R-1 — an admin re-sends the speaker confirmation links for a request that is still
    // AwaitingSpeaker: the prior token pair expired (or the email never went out because
    // the public URL / speaker email was unset). Invalidate any live token, mint a fresh
    // pair in the same unit of work, then best-effort re-email. Only an AwaitingSpeaker
    // request qualifies — a decided/reverted request is a 409.
    public async Task ResendSpeakerConfirmationAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var req = await appDbContext.SpeakerMeetingRequests
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SpeakerMeetingRequestNotFound, 404,
                "Speaker meeting request not found.",
                "لم يتم العثور على طلب مقابلة المتحدّث.");
        if (req.Status != MeetingRequestStatus.AwaitingSpeaker)
        {
            throw new ApiException(
                ErrorCodes.SpeakerMeetingRequestStatusInvalid, 409,
                "Only a request awaiting the speaker's confirmation can be re-sent.",
                "لا يمكن إعادة الإرسال إلا لطلب بانتظار تأكيد المتحدّث.");
        }

        var now = timeProvider.GetUtcNow();
        // Kill any still-live token so only the fresh pair can decide the request.
        await appDbContext.MeetingActionTokens
            .Where(t => t.SpeakerMeetingRequestId == req.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), cancellationToken);

        var links = meetingActionTokens.StageTokensForRequest(req.Id);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SpeakerMeetingConfirmationResent,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = DetailJson(new { speakerMeetingRequestId = req.Id }),
        }, cancellationToken);
        await meetingActionTokens.AuditMintedAsync(req.Id, cancellationToken);
        await EmailSpeakerConfirmationLinksAsync(req, links, cancellationToken);
    }

    // D-717 (item 7, GAP-3) — email the speaker the Approve/Reject links (the tokens
    // are already committed). Best-effort (swallow-and-log): an email failure leaves
    // the request AwaitingSpeaker with valid tokens; it never rolls back the accept.
    // A re-send admin action is a future follow-up.
    private async Task EmailSpeakerConfirmationLinksAsync(
        SpeakerMeetingRequest req, MeetingActionLinks links, CancellationToken cancellationToken)
    {
        if (!links.HasUrls)
        {
            logger.LogWarning(
                "Meeting request {Id} is AwaitingSpeaker but MeetingLinks:PublicWebBaseUrl "
                + "is unconfigured — the speaker confirmation email was skipped.", req.Id);
            return;
        }
        var contactEmail = await ResolveSpeakerContactEmailAsync(req.SpeakerId, cancellationToken);
        if (string.IsNullOrWhiteSpace(contactEmail))
        {
            return;
        }

        var slot = req.SlotStartUtc is { } s
            ? $"{s:yyyy-MM-dd HH:mm} UTC"
            : "to be scheduled";
        var html =
            $"<p>You have a meeting request from <strong>{HtmlEnc(req.RequesterName)}</strong>.</p>"
            + $"<p>Topic: {HtmlEnc(req.Subject)}<br/>Proposed time: {HtmlEnc(slot)}</p>"
            + "<p>Please confirm — this decides whether the meeting goes ahead:</p>"
            + $"<p><a href=\"{HtmlEnc(links.ApproveUrl)}\">Approve the meeting</a>"
            + $" &nbsp;|&nbsp; <a href=\"{HtmlEnc(links.RejectUrl)}\">Decline</a></p>"
            + "<p style=\"color:#666\">These links expire in 72 hours and each can be used once.</p>";
        await SendSpeakerEmailAsync(
            contactEmail!, "SIMF — please confirm a meeting request", html,
            purpose: "SpeakerMeetingConfirm", cancellationToken);
    }

    private static string HtmlEnc(string value) => System.Net.WebUtility.HtmlEncode(value);

    // The speaker's contact email — the ContactId on Speaker is a bare Guid resolved
    // on read (no nav). Shared by the accept-outcome email and the D-717 links email.
    private async Task<string?> ResolveSpeakerContactEmailAsync(
        Guid speakerId, CancellationToken cancellationToken)
    {
        var contactId = await appDbContext.Speakers.AsNoTracking()
            .Where(s => s.Id == speakerId)
            .Select(s => s.ContactId)
            .SingleOrDefaultAsync(cancellationToken);
        if (contactId is not { } id)
        {
            return null;
        }
        return await appDbContext.Contacts.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => c.Email)
            .SingleOrDefaultAsync(cancellationToken);
    }

    // The fixed enqueue shape for a speaker-facing email (subjectUserId is Guid.Empty
    // — the speaker is not a SIMF account). Shared by the two speaker emails.
    private Task SendSpeakerEmailAsync(
        string toEmail, string subject, string html, string purpose,
        CancellationToken cancellationToken) =>
        emailQueue.TryEnqueueAsync(
            new EmailMessage(toEmail, subject, html),
            purpose: purpose,
            subjectEmail: toEmail,
            subjectUserId: Guid.Empty,
            auditLog, logger, cancellationToken);

    // D-716 (item 7, GAP-2) — bind an accepted meeting to a free hall slot
    // (Option A: the picked hall slot is the meeting time of record). Validates the
    // hall hosts meetings, the slot is still free, the speaker is not already
    // committed at that time, and the optional table belongs to the hall — then
    // writes the binding onto the request. The caller sets the status to
    // AwaitingSpeaker. The DB filtered-unique index (HallId, SlotStartUtc) is the
    // race backstop.
    private async Task BindHallSlotAsync(
        SpeakerMeetingRequest req,
        RespondToSpeakerMeetingRequestRequest request,
        CancellationToken cancellationToken)
    {
        var hallId = request.HallId!.Value;
        if (request.SlotStartUtc is not { } start
            || request.SlotEndUtc is not { } end || end <= start)
        {
            throw new ApiException(
                ErrorCodes.SpeakerMeetingRequestInvalid, 400,
                "A valid hall slot (start and end) is required to bind a hall.",
                "يلزم اختيار فترة قاعة صحيحة (بداية ونهاية) لربط القاعة.");
        }

        var hall = await appDbContext.Halls.AsNoTracking()
            .Where(h => h.Id == hallId)
            .Select(h => new { h.IsActive, h.Purpose })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.HallNotFound, 404,
                "The hall was not found.", "لم يتم العثور على القاعة.");
        if (!hall.IsActive
            || hall.Purpose is not (HallPurpose.Meeting or HallPurpose.General))
        {
            throw new ApiException(
                ErrorCodes.HallNotFound, 404,
                "The hall does not host meetings.",
                "هذه القاعة لا تستضيف الاجتماعات.");
        }

        // The picked slot must still be a currently-free slot for the hall — the
        // availability layer already excludes slots taken by a bound meeting
        // (D-716 taken-filter), so membership is the free check.
        var slots = await hallAvailability.GetAvailableSlotsAsync(hallId, cancellationToken);
        if (!slots.Any(s => s.StartUtc == start && s.EndUtc == end))
        {
            throw new ApiException(
                ErrorCodes.SpeakerMeetingRequestInvalid, 409,
                "That hall slot is no longer available.",
                "لم تعد فترة القاعة هذه متاحة.");
        }

        // The speaker cannot be double-booked: no other live meeting for the same
        // speaker may overlap the picked slot (shared with the legacy accept path).
        if (await SpeakerHasOverlappingMeetingAsync(
                req.SpeakerId, req.Id, start, end, cancellationToken))
        {
            throw new ApiException(
                ErrorCodes.SpeakerMeetingRequestInvalid, 409,
                "The speaker already has a meeting at that time.",
                "لدى المتحدّث اجتماع بالفعل في هذا الوقت.");
        }

        // M-7 — the requester must not already hold another live meeting at that time.
        if (await RequesterHasOverlappingMeetingAsync(
                req.RequestedByUserId, req.Id, start, end, cancellationToken))
        {
            throw new ApiException(
                ErrorCodes.SpeakerMeetingRequestInvalid, 409,
                "The requester already has a meeting booked at that time.",
                "لدى مقدّم الطلب اجتماع محجوز بالفعل في هذا الوقت.");
        }

        if (request.MeetingTableId is { } tableId)
        {
            var tableOk = await appDbContext.MeetingTables.AsNoTracking()
                .AnyAsync(t => t.Id == tableId && t.HallId == hallId && t.IsActive,
                    cancellationToken);
            if (!tableOk)
            {
                throw new ApiException(
                    ErrorCodes.SpeakerMeetingRequestInvalid, 400,
                    "The meeting table was not found in this hall.",
                    "لم يتم العثور على طاولة الاجتماع في هذه القاعة.");
            }
            req.MeetingTableId = tableId;
        }

        req.HallId = hallId;
        req.SlotStartUtc = start;
        req.SlotEndUtc = end;
    }

    // D-716 — does the speaker already hold a LIVE meeting (Accepted or
    // AwaitingSpeaker, per MeetingRequestStatuses.SlotHolding) that overlaps
    // [start, end) — excluding this request? Half-open overlap, the same rule the
    // availability layer uses. Shared by the legacy accept re-check and the
    // accept-with-hall bind so the two never diverge on which states hold a slot.
    private Task<bool> SpeakerHasOverlappingMeetingAsync(
        Guid speakerId, Guid excludeRequestId,
        DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken) =>
        appDbContext.SpeakerMeetingRequests.AsNoTracking()
            .AnyAsync(r => r.Id != excludeRequestId
                && r.SpeakerId == speakerId
                && MeetingRequestStatuses.SlotHolding.Contains(r.Status)
                && r.SlotStartUtc != null && r.SlotEndUtc != null
                && r.SlotStartUtc < end && start < r.SlotEndUtc, cancellationToken);

    // M-7 — does the REQUESTER already hold a LIVE meeting (Accepted or AwaitingSpeaker)
    // overlapping [start, end) with any speaker — excluding this request? The speaker-side
    // guard above stops one speaker being double-booked; this stops one VIP holding two
    // concurrent meetings with two different speakers. Same half-open overlap rule.
    private Task<bool> RequesterHasOverlappingMeetingAsync(
        Guid requesterUserId, Guid excludeRequestId,
        DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken) =>
        appDbContext.SpeakerMeetingRequests.AsNoTracking()
            .AnyAsync(r => r.Id != excludeRequestId
                && r.RequestedByUserId == requesterUserId
                && MeetingRequestStatuses.SlotHolding.Contains(r.Status)
                && r.SlotStartUtc != null && r.SlotEndUtc != null
                && r.SlotStartUtc < end && start < r.SlotEndUtc, cancellationToken);

    // D-474 / D-611 — VIP gate: the requester's profile type opts into VIP
    // meeting slots. D-611 replaced the former brittle "profile-type Name
    // contains 'VIP'" substring test with the explicit
    // ProfileType.AllowsVipMeetingSlots flag (the seeder sets it for VVIP + VIP),
    // so a future type whose name merely embeds "VIP" no longer matches by accident.
    private async Task<bool> IsVipAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == userId && p.ProfileTypeId != null)
            .Select(p => p.ProfileType!.AllowsVipMeetingSlots)
            .SingleOrDefaultAsync(cancellationToken);
    }

    // D-474 — in-app notify the requester of the decision; on Accept also email the
    // speaker (resolved via their Contact). Both best-effort (swallow-and-log) so a
    // notification/email failure never undoes the committed response.
    private async Task NotifyOutcomeAsync(
        SpeakerMeetingRequest req, CancellationToken cancellationToken)
    {
        var accepted = req.Status == MeetingRequestStatus.Accepted;
        var speaker = await appDbContext.Speakers.AsNoTracking()
            .Where(s => s.Id == req.SpeakerId)
            .Select(s => new { s.Name, s.ContactId })
            .SingleOrDefaultAsync(cancellationToken);
        var speakerName = speaker?.Name ?? "the speaker";

        await notifications.TryDispatchAsync(new NotificationRequest
        {
            UserId = req.RequestedByUserId,
            Kind = accepted ? NotificationKind.MeetingScheduled : NotificationKind.MeetingCancelled,
            Title = accepted ? "Meeting request accepted" : "Meeting request declined",
            TitleArabic = accepted ? "تم قبول طلب المقابلة" : "تم رفض طلب المقابلة",
            Body = accepted
                ? $"Your meeting request with {speakerName} was accepted."
                : $"Your meeting request with {speakerName} was declined.",
            BodyArabic = accepted
                ? $"تم قبول طلب مقابلتك مع {speakerName}."
                : $"تم رفض طلب مقابلتك مع {speakerName}.",
            Severity = NotificationSeverity.Info,
            RelatedEntityType = nameof(SpeakerMeetingRequest),
            RelatedEntityId = req.Id,
            SendEmail = false,
        }, logger, cancellationToken);

        if (accepted && speaker?.ContactId is { } contactId)
        {
            var contactEmail = await appDbContext.Contacts.AsNoTracking()
                .Where(c => c.Id == contactId)
                .Select(c => c.Email)
                .SingleOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(contactEmail))
            {
                var slot = req.SlotStartUtc is { } s
                    ? $" Proposed slot: {s:yyyy-MM-dd HH:mm} UTC."
                    : string.Empty;
                var html =
                    $"<p>A meeting request from <strong>{HtmlEnc(req.RequesterName)}</strong> has been accepted.{slot}</p>"
                    + $"<p>Topic: {HtmlEnc(req.Subject)}</p>";
                await SendSpeakerEmailAsync(
                    contactEmail!, "SIMF — a meeting request was accepted", html,
                    purpose: "SpeakerMeetingAccepted", cancellationToken);
            }
        }
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

        var email = await userDirectory.GetEmailAsync(
            req.RequestedByUserId, cancellationToken);

        // D-716 — resolve the bound hall/table names for display (only when bound).
        string? hallName = null;
        if (req.HallId is { } hallId)
        {
            hallName = await appDbContext.Halls.AsNoTracking()
                .Where(h => h.Id == hallId).Select(h => h.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }
        string? tableCode = null;
        if (req.MeetingTableId is { } tableId)
        {
            tableCode = await appDbContext.MeetingTables.AsNoTracking()
                .Where(t => t.Id == tableId).Select(t => t.Code)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return new AdminSpeakerMeetingRequestDetail(
            req.Id, req.SpeakerId, speaker.Name, speaker.NameArabic,
            req.RequestedByUserId, req.RequesterName, email,
            req.Subject, req.Status, req.ResponseNote,
            req.CreatedAt, req.RespondedAt,
            req.SlotStartUtc, req.SlotEndUtc,
            req.HallId, hallName, req.MeetingTableId, tableCode);
    }
}
