// Tests: SIMF.Api.Tests/MyRequestsTests.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Requests.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Requests;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Requests;

/// <summary>D-500 (Wave 5, الطلبات 1408:9726) — the unified "My requests" feed.
/// Concats the signed-in user's speaker meetings (D-269), delegation meetings
/// (D-478, read-only), session-attendance seat bookings (D-175/D-227, surfaced
/// from the user's own reservations — owner decision, no new entity), and the
/// two new standalone types (participation-document + badge-update). Seat
/// bookings map their <see cref="BookingStatus"/> onto the unified
/// <see cref="MeetingRequestStatus"/>. Supersedes <c>MyMeetingsService</c>.
/// Self-cancel withdraws a still-pending speaker / document / badge request.</summary>
internal sealed class MyRequestsService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
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
        // rows carry a null ReservedForUserId, so this filter naturally excludes
        // them). Released/cancelled rows stay in the table for audit but are
        // excluded here — mirroring every other SeatReservation read — so a
        // cancel-then-rebook never shows the same session twice.
        var bookings = await appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.ReservedForUserId == userId && r.ReleasedAt == null)
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
            // R-1 — an AwaitingSpeaker request (admin accepted + bound a hall, speaker not
            // yet confirmed) is still "under review" to the requester, so let them withdraw
            // it; cancelling frees the held slot and voids the speaker's confirmation tokens.
            r.Status is MeetingRequestStatus.Pending or MeetingRequestStatus.AwaitingSpeaker,
            Subtitle: r.Rank, SubtitleArabic: r.RankArabic,
            SpeakerId: r.SpeakerId, CountryId: r.CountryId,
            ResponseNote: r.ResponseNote)));

        items.AddRange(delegation.Select(r => new AppRequestItem(
            AppRequestKind.DelegationMeeting, r.Id, r.Name, r.NameArabic,
            // Same fold as the speaker projection so the unified state machine's admin-only
            // states (AwaitingSpeaker / Done) never leak past the shipped wire contract (0–3).
            ToRequesterDisplayStatus(r.Status), r.SlotStart, r.CreatedAt, CanCancel: false,
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
        var now = timeProvider.GetUtcNow();
        switch (kind)
        {
            case AppRequestKind.SpeakerMeeting:
            {
                var r = await appDbContext.SpeakerMeetingRequests.AsNoTracking().SingleOrDefaultAsync(
                    x => x.Id == id && x.RequestedByUserId == userId, cancellationToken)
                    ?? throw NotFound();
                // R-1 — a speaker meeting may be withdrawn while Pending OR AwaitingSpeaker
                // (see the feed's CanCancel). Cancelling voids the double-opt-in tokens
                // (they validate against this status) and releases the held hall slot.
                EnsureSpeakerCancellable(r.Status);

                // #10 — the DB is the single arbiter, not the read above (mirrors
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
                // Delegation + session-attendance are not self-cancellable here.
                throw new ApiException(
                    ErrorCodes.AppRequestNotCancellable, 409,
                    "This request type cannot be cancelled from the app.",
                    "لا يمكن إلغاء هذا النوع من الطلبات من التطبيق.");
        }

        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AppRequestCancelled,
            Outcome = AuditOutcome.Success,
            ActorUserId = userId,
            Detail = JsonSerializer.Serialize(new { kind = kind.ToString(), id }),
        }, cancellationToken);

        logger.LogInformation(
            "App request {Kind}/{Id} cancelled by {Actor} at {When}", kind, id, userId, now);
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

    // R-1 — a speaker meeting is withdrawable while Pending OR AwaitingSpeaker.
    private static void EnsureSpeakerCancellable(MeetingRequestStatus status)
    {
        if (status is not (MeetingRequestStatus.Pending or MeetingRequestStatus.AwaitingSpeaker))
        {
            throw new ApiException(
                ErrorCodes.AppRequestNotCancellable, 409,
                "Only a pending request can be cancelled.",
                "لا يمكن إلغاء سوى طلب قيد المراجعة.");
        }
    }

    // D-716 (item 7, GAP-2) + Bi-Meeting rework — the unified state machine adds two
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
