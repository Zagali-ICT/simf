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

        // A1 — accepting a slot-bearing request must re-check the slot is still
        // free among Accepted rows (the submit-time check can go stale). Use the
        // same half-open overlap the availability layer uses (SpeakerAvailability
        // Service), not start-equality — overlapping windows with different slot
        // lengths can produce accepted slots that overlap but start at different
        // times. The DB filtered-unique/overlap backstop is a Wave B item.
        if (request.Status == MeetingRequestStatus.Accepted
            && req.SlotStartUtc is { } slotStart && req.SlotEndUtc is { } slotEnd)
        {
            var slotTaken = await appDbContext.SpeakerMeetingRequests.AsNoTracking()
                .AnyAsync(r => r.Id != req.Id
                    && r.SpeakerId == req.SpeakerId
                    && r.Status == MeetingRequestStatus.Accepted
                    && r.SlotStartUtc != null && r.SlotEndUtc != null
                    && r.SlotStartUtc < slotEnd && slotStart < r.SlotEndUtc, cancellationToken);
            if (slotTaken)
            {
                throw new ApiException(
                    ErrorCodes.SpeakerMeetingRequestInvalid, 409,
                    "That slot is no longer available.",
                    "لم تعد هذه الفترة متاحة.");
            }
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
        await NotifyOutcomeAsync(req, cancellationToken);

        return await LoadDetailAsync(id, cancellationToken);
    }

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
                    $"<p>A meeting request from <strong>{System.Net.WebUtility.HtmlEncode(req.RequesterName)}</strong> has been accepted.{slot}</p>"
                    + $"<p>Topic: {System.Net.WebUtility.HtmlEncode(req.Subject)}</p>";
                await emailQueue.TryEnqueueAsync(
                    new EmailMessage(contactEmail!, "SIMF — a meeting request was accepted", html),
                    purpose: "SpeakerMeetingAccepted",
                    subjectEmail: contactEmail!,
                    subjectUserId: Guid.Empty,
                    auditLog, logger, cancellationToken);
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

        return new AdminSpeakerMeetingRequestDetail(
            req.Id, req.SpeakerId, speaker.Name, speaker.NameArabic,
            req.RequestedByUserId, req.RequesterName, email,
            req.Subject, req.Status, req.ResponseNote,
            req.CreatedAt, req.RespondedAt);
    }
}
