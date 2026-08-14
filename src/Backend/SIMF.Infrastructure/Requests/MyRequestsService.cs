// Tests: SIMF.Api.Tests/MyRequestsTests.cs
//        SIMF.Api.Tests/SpeakerMeetingQaTests.cs (CheckedIn + cancel)
//        SIMF.Api.Tests/DelegationMeetingQaFixesTests.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Application.Requests.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Requests;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Requests;

/// <summary>The unified "My requests" (الطلبات) feed. Concats the signed-in
/// user's speaker meetings, delegation meetings (read-only),
/// session-attendance seat bookings (a design ruling: surfaced from the user's
/// own reservations, with no new entity), and the two standalone types
/// (participation-document + badge-update). Seat bookings map their
/// <see cref="BookingStatus"/> onto the unified
/// <see cref="MeetingRequestStatus"/>. Supersedes <c>MyMeetingsService</c>.
/// Self-cancel withdraws a still-pending speaker / delegation / document /
/// badge request.</summary>
internal sealed class MyRequestsService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    IEmailQueue emailQueue,
    IDelegationMeetingRequestService delegationMeetings,
    TimeProvider timeProvider,
    ILogger<MyRequestsService> logger) : IMyRequestsService
{
    public async Task<IReadOnlyList<AppRequestItem>> GetMyRequestsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var speaker = await appDbContext.SpeakerMeetingRequests.AsNoTracking()
            .Where(r => r.RequestedByUserId == userId)
            .Join(appDbContext.Speakers, r => r.SpeakerId, s => s.Id, (r, s) => new
            {
                r.Id, SpeakerId = s.Id, s.Name, s.NameArabic, s.Rank, s.RankArabic, s.CountryId,
                r.Status, r.SlotStart, r.CreatedAt, r.ResponseNote,
            })
            .ToListAsync(cancellationToken);

        var delegation = await appDbContext.DelegationMeetingRequests.AsNoTracking()
            .Where(r => r.RequestedByUserId == userId)
            .Select(r => new
            {
                r.Id,
                Name = r.TargetCountry!.Name,
                NameArabic = r.TargetCountry!.NameArabic,
                r.TargetCountryId,
                r.Status, r.SlotStart, r.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        // Session-attendance = the user's own LIVE seat bookings (AdminReservedRow
        // rows carry a null ReservedForProfileId, so this filter naturally excludes
        // them). Released/cancelled rows stay in the table for audit but are
        // excluded here — mirroring every other SeatReservation read — so a
        // cancel-then-rebook never shows the same session twice. The caller is a
        // signed-in account; a booking is held by their attendee profile.
        //
        // The non-null test is load-bearing, not defensive noise: an account with
        // no profile yields a null id, and `ReservedForProfileId == null` is
        // exactly how an ADMIN BLOCK is stored — so without it such a caller would
        // be shown every blocked seat in the venue as their own booking.
        var profileId = await appDbContext.ProfileIdForAccountAsync(userId, cancellationToken);
        var bookings = await appDbContext.SeatReservations.AsNoTracking()
            .Where(r => profileId != null
                && r.ReservedForProfileId == profileId
                && r.ReleasedAt == null)
            .Join(appDbContext.Sessions, r => r.SessionId, s => s.Id, (r, s) => new { r, s })
            .Join(appDbContext.Halls, x => x.s.HallId, h => h.Id, (x, h) => new
            {
                x.r.Id, x.s.Title, x.s.TitleArabic,
                HallName = h.Name, HallNameArabic = h.NameArabic,
                x.s.Start, x.r.Status, x.r.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var documents = await appDbContext.ParticipationDocumentRequests.AsNoTracking()
            .Where(r => r.RequestedByUserId == userId)
            .Select(r => new { r.Id, r.DocumentType, r.Status, r.CreatedAt, r.ResponseNote })
            .ToListAsync(cancellationToken);

        var badges = await appDbContext.BadgeUpdateRequests.AsNoTracking()
            .Where(r => r.RequestedByUserId == userId)
            .Select(r => new { r.Id, r.RequestedJobTitle, r.Status, r.CreatedAt, r.ResponseNote })
            .ToListAsync(cancellationToken);

        var items = new List<AppRequestItem>(
            speaker.Count + delegation.Count + bookings.Count + documents.Count + badges.Count);

        items.AddRange(speaker.Select(r => new AppRequestItem(
            AppRequestKind.SpeakerMeeting, r.Id, r.Name, r.NameArabic,
            ToRequesterDisplayStatus(r.Status), r.SlotStart, r.CreatedAt,
            // An AwaitingSpeaker request (admin accepted + bound a hall, speaker not
            // yet confirmed) is still "under review" to the requester, so let them withdraw
            // it; cancelling frees the held slot and voids the speaker's confirmation tokens.
            r.Status is MeetingRequestStatus.Pending or MeetingRequestStatus.AwaitingSpeaker,
            Subtitle: r.Rank, SubtitleArabic: r.RankArabic,
            SpeakerId: r.SpeakerId, CountryId: r.CountryId,
            ResponseNote: r.ResponseNote,
            // Done still folds to Accepted on the wire (values 0–3), so the
            // check-in reaches the requester through this append-only flag instead:
            // their card can now read "attended" rather than staying on "accepted".
            CheckedIn: r.Status == MeetingRequestStatus.Done)));

        items.AddRange(delegation.Select(r => new AppRequestItem(
            AppRequestKind.DelegationMeeting, r.Id, r.Name, r.NameArabic,
            // Same fold as the speaker projection so the unified state machine's admin-only
            // states (AwaitingSpeaker / Done) never leak past the shipped wire contract (0–3).
            ToRequesterDisplayStatus(r.Status), r.SlotStart, r.CreatedAt,
            // A delegation meeting is withdrawable on exactly the speaker rule
            // (Pending OR AwaitingSpeaker). It used to report CanCancel:false while the
            // cancel switch fell through to a 409, so the requester could never withdraw
            // one — asymmetric with the speaker meeting sitting next to it in the feed.
            r.Status is MeetingRequestStatus.Pending or MeetingRequestStatus.AwaitingSpeaker,
            CountryId: r.TargetCountryId)));

        items.AddRange(bookings.Select(r => new AppRequestItem(
            AppRequestKind.SessionAttendance, r.Id,
            $"{r.Title} · {r.HallName}", $"{r.TitleArabic} · {r.HallNameArabic}",
            ToDisplayStatus(r.Status), r.Start, r.CreatedAt, CanCancel: false)));

        items.AddRange(documents.Select(r =>
        {
            var (en, ar) = DocumentTypeLabel(r.DocumentType);
            return new AppRequestItem(
                AppRequestKind.ParticipationDocument, r.Id, en, ar,
                r.Status, EventDate: null, r.CreatedAt,
                r.Status == MeetingRequestStatus.Pending,
                ResponseNote: r.ResponseNote);
        }));

        items.AddRange(badges.Select(r => new AppRequestItem(
            AppRequestKind.BadgeUpdate, r.Id, r.RequestedJobTitle, r.RequestedJobTitle,
            r.Status, EventDate: null, r.CreatedAt,
            r.Status == MeetingRequestStatus.Pending,
            ResponseNote: r.ResponseNote)));

        return items.OrderByDescending(i => i.CreatedAt).ToList();
    }

    public async Task CancelAsync(
        Guid userId, AppRequestKind kind, Guid id,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.SimfNow();
        // Set when this withdraw killed a delegation meeting the TARGET delegation had
        // already been asked to confirm, so their live prompt is retracted once the cancel
        // is durable (see the dispatch after the audit write).
        Guid? retractDelegationPromptFor = null;
        switch (kind)
        {
            case AppRequestKind.SpeakerMeeting:
            {
                var r = await appDbContext.SpeakerMeetingRequests.AsNoTracking().SingleOrDefaultAsync(
                    x => x.Id == id && x.RequestedByUserId == userId, cancellationToken)
                    ?? throw NotFound();
                // A speaker meeting may be withdrawn while Pending OR AwaitingSpeaker
                // (see the feed's CanCancel). Cancelling voids the double-opt-in tokens
                // (they validate against this status) and releases the held hall slot.
                EnsureMeetingCancellable(r.Status);

                // The DB is the single arbiter, not the read above (mirrors
                // MeetingActionTokenService.ApplyAsync). While the requester was on the
                // cancel screen the speaker may have Approved (AwaitingSpeaker -> Accepted)
                // via the double-opt-in link; SpeakerMeetingRequest carries no rowversion
                // (frozen schema), so a tracked read-modify-save would emit an
                // unconditional UPDATE and silently overwrite that just-confirmed decision.
                // Flip the status with a conditional UPDATE guarded on the still-cancellable
                // states; if the speaker's decision landed first it matches 0 rows and we
                // surface the 409 instead of a lost update.
                var affected = await appDbContext.SpeakerMeetingRequests
                    .Where(x => x.Id == id && x.RequestedByUserId == userId
                        && (x.Status == MeetingRequestStatus.Pending
                            || x.Status == MeetingRequestStatus.AwaitingSpeaker))
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(x => x.Status, MeetingRequestStatus.Cancelled),
                        cancellationToken);
                if (affected == 0)
                {
                    throw new ApiException(
                        ErrorCodes.AppRequestNotCancellable, 409,
                        "Only a pending request can be cancelled.",
                        "لا يمكن إلغاء سوى طلب قيد المراجعة.");
                }

                // A cancel used to leave the speaker's emailed Approve/Reject
                // tokens alive (they only stopped working because ApplyAsync re-checks
                // the status, so the link 404'd neutrally weeks later) and told the
                // speaker nothing at all. Void every live token for this request so a
                // stale inbox link can never resurrect a withdrawn meeting, then tell
                // the speaker — they were emailed when it was proposed, so silence is
                // not an acceptable ending.
                await appDbContext.MeetingActionTokens
                    .Where(t => t.SpeakerMeetingRequestId == id && t.UsedAt == null)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(t => t.UsedAt, now), cancellationToken);
                // ...but only a request the speaker was ACTUALLY told about earns the
                // withdrawal notice. A Pending cancel never minted a token and never
                // emailed anyone, so mailing the speaker now would hand an uninvolved
                // outsider the requester's real name and the meeting subject for a
                // request they never saw — and the "any confirmation link you received"
                // line would be false. AwaitingSpeaker is the only status that emailed them.
                if (r.Status == MeetingRequestStatus.AwaitingSpeaker)
                {
                    await EmailSpeakerCancellationAsync(
                        r.SpeakerId, r.RequesterName, r.Subject, cancellationToken);
                }
                break;
            }
            case AppRequestKind.DelegationMeeting:
            {
                var r = await appDbContext.DelegationMeetingRequests.AsNoTracking()
                    .SingleOrDefaultAsync(
                        x => x.Id == id && x.RequestedByUserId == userId, cancellationToken)
                    ?? throw NotFound();
                // The same withdraw rule as a speaker meeting (see the feed's
                // CanCancel). Cancelling from AwaitingSpeaker voids the target
                // delegation's confirm token (it validates against this status) and
                // clearing the hall binding releases the held slot.
                EnsureMeetingCancellable(r.Status);

                // Same lost-update guard as the speaker arm: while the requester was on
                // the cancel screen the other delegation may have confirmed
                // (AwaitingSpeaker -> Accepted) by app tap or email link. A conditional
                // UPDATE guarded on the still-cancellable states means their confirm wins
                // and we surface the 409 instead of silently overwriting it.
                var affected = await appDbContext.DelegationMeetingRequests
                    .Where(x => x.Id == id && x.RequestedByUserId == userId
                        && (x.Status == MeetingRequestStatus.Pending
                            || x.Status == MeetingRequestStatus.AwaitingSpeaker))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.Status, MeetingRequestStatus.Cancelled)
                        .SetProperty(x => x.HallId, (Guid?)null)
                        .SetProperty(x => x.MeetingTableId, (Guid?)null),
                        cancellationToken);
                if (affected == 0)
                {
                    throw new ApiException(
                        ErrorCodes.AppRequestNotCancellable, 409,
                        "Only a pending request can be cancelled.",
                        "لا يمكن إلغاء سوى طلب قيد المراجعة.");
                }

                // Only an APPROVED (AwaitingSpeaker) meeting ever reached the target
                // delegation, so only that one leaves a live "please confirm" card + emailed
                // confirm link behind. Withdrawing from Pending must stay silent: those
                // members were never told the request existed.
                if (r.Status == MeetingRequestStatus.AwaitingSpeaker)
                {
                    retractDelegationPromptFor = id;
                }
                break;
            }
            case AppRequestKind.ParticipationDocument:
            {
                var r = await appDbContext.ParticipationDocumentRequests.SingleOrDefaultAsync(
                    x => x.Id == id && x.RequestedByUserId == userId, cancellationToken)
                    ?? throw NotFound();
                EnsurePending(r.Status);
                r.Status = MeetingRequestStatus.Cancelled;
                break;
            }
            case AppRequestKind.BadgeUpdate:
            {
                var r = await appDbContext.BadgeUpdateRequests.SingleOrDefaultAsync(
                    x => x.Id == id && x.RequestedByUserId == userId, cancellationToken)
                    ?? throw NotFound();
                EnsurePending(r.Status);
                r.Status = MeetingRequestStatus.Cancelled;
                break;
            }
            default:
                // Session-attendance is not self-cancellable here (it has its own
                // seat-release path); delegation meetings have their own arm above.
                throw new ApiException(
                    ErrorCodes.AppRequestNotCancellable, 409,
                    "This request type cannot be cancelled from the app.",
                    "لا يمكن إلغاء هذا النوع من الطلبات من التطبيق.");
        }

        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.AppRequestCancelled,
            userId,
            JsonSerializer.Serialize(new { kind = kind.ToString(), id }),
            cancellationToken);

        logger.LogInformation(
            "App request {Kind}/{Id} cancelled by {Actor} at {When}", kind, id, userId, now);

        // Dispatched only after the cancel is durable and audited: every eligible
        // member of the target delegation is holding a MeetingRequested card that deep-links
        // to /meeting-confirm plus an emailed confirm link, and both now dead-end (409
        // APP_REQUEST_ALREADY_RESPONDED). The admin cancel path retracts them the same way.
        if (retractDelegationPromptFor is { } delegationRequestId)
        {
            await delegationMeetings.RetractTargetMemberPromptsAsync(
                delegationRequestId, cancellationToken);
        }
    }

    // Tell the speaker their proposed meeting was withdrawn. Bilingual (AR
    // first, matching the app's default locale) and best-effort: the enqueue failure
    // path audits + swallows, so a mail problem never undoes the committed cancel.
    // The speaker is not a SIMF account, hence subjectUserId = Guid.Empty.
    private async Task EmailSpeakerCancellationAsync(
        Guid speakerId, string requesterName, string subject,
        CancellationToken cancellationToken)
    {
        var contactEmail = await appDbContext.Speakers.AsNoTracking()
            .Where(s => s.Id == speakerId)
            .Select(s => s.Email)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(contactEmail))
        {
            logger.LogWarning(
                "Speaker {SpeakerId} has no contact email — the meeting withdrawal "
                + "notice was skipped.", speakerId);
            return;
        }

        var name = System.Net.WebUtility.HtmlEncode(requesterName);
        var topic = System.Net.WebUtility.HtmlEncode(subject);
        var html =
            $"<p dir=\"rtl\">تم سحب طلب المقابلة المقدَّم من <strong>{name}</strong>.</p>"
            + $"<p dir=\"rtl\">الموضوع: {topic}</p>"
            + "<p dir=\"rtl\">أي رابط تأكيد وصلك بشأن هذا الطلب لم يعد صالحاً.</p>"
            + "<hr/>"
            + $"<p>The meeting request from <strong>{name}</strong> was withdrawn.</p>"
            + $"<p>Topic: {topic}</p>"
            + "<p>Any confirmation link you received for it is no longer valid.</p>";
        await emailQueue.TryEnqueueAsync(
            new EmailMessage(contactEmail!, "SIMF — a meeting request was withdrawn", html),
            purpose: "SpeakerMeetingWithdrawn",
            subjectEmail: contactEmail!,
            subjectUserId: Guid.Empty,
            auditLog, logger, cancellationToken);
    }

    private static ApiException NotFound() => new(
        ErrorCodes.AppRequestNotFound, 404,
        "The request was not found.",
        "لم يتم العثور على الطلب.");

    private static void EnsurePending(MeetingRequestStatus status)
    {
        if (status != MeetingRequestStatus.Pending)
        {
            throw new ApiException(
                ErrorCodes.AppRequestNotCancellable, 409,
                "Only a pending request can be cancelled.",
                "لا يمكن إلغاء سوى طلب قيد المراجعة.");
        }
    }

    // A speaker meeting is withdrawable while Pending OR AwaitingSpeaker;
    // delegation meetings follow exactly the same rule.
    private static void EnsureMeetingCancellable(MeetingRequestStatus status)
    {
        if (status is not (MeetingRequestStatus.Pending or MeetingRequestStatus.AwaitingSpeaker))
        {
            throw new ApiException(
                ErrorCodes.AppRequestNotCancellable, 409,
                "Only a pending request can be cancelled.",
                "لا يمكن إلغاء سوى طلب قيد المراجعة.");
        }
    }

    // The unified state machine adds two
    // admin/operator-only states the shipped mobile wire contract (values 0–3) never sees:
    //   • AwaitingSpeaker (accepted + bound to a hall, awaiting the other party's confirmation)
    //     is still "under review" from the requester's view      -> fold to Pending;
    //   • Done (the meeting took place, marked at hall check-in)  -> fold to Accepted.
    private static MeetingRequestStatus ToRequesterDisplayStatus(MeetingRequestStatus status) => status switch
    {
        MeetingRequestStatus.AwaitingSpeaker => MeetingRequestStatus.Pending,
        MeetingRequestStatus.Done => MeetingRequestStatus.Accepted,
        _ => status,
    };

    // BookingStatus → the unified display status (Approved counts as Accepted;
    // Cancelled/Rejected map straight across).
    private static MeetingRequestStatus ToDisplayStatus(BookingStatus status) => status switch
    {
        BookingStatus.Approved => MeetingRequestStatus.Accepted,
        BookingStatus.Rejected => MeetingRequestStatus.Rejected,
        BookingStatus.Cancelled => MeetingRequestStatus.Cancelled,
        _ => MeetingRequestStatus.Pending,
    };

    private static (string En, string Ar) DocumentTypeLabel(ParticipationDocumentType type) => type switch
    {
        ParticipationDocumentType.ParticipationLetter => ("Participation letter", "خطاب مشاركة"),
        ParticipationDocumentType.InvitationLetter => ("Invitation letter", "خطاب دعوة"),
        _ => ("Official attendance certificate", "شهادة حضور رسمية"),
    };
}
