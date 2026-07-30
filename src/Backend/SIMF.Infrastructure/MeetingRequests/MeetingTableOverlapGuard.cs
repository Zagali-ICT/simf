// Tests: SIMF.Api.Tests/MeetingTableOverlapTests.cs
// Tests: SIMF.Api.Tests/DelegationMeetingQaFixesTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.MeetingRequests;

/// <summary>D-773 — the single authority for the invariant "a meeting TABLE holds
/// one meeting at a time". THREE families can occupy a <c>MeetingTable</c>: a
/// delegation meeting request, a speaker meeting request, and the admin-arranged
/// <c>BusinessMeeting</c> (FDS-013, the only one with a real FK). A34 (D-771) put the
/// scan on the delegation bind and D3 (D-772) taught it the business family, but it
/// stayed private to <see cref="DelegationMeetingRequestService"/> — so the guard was
/// ONE-DIRECTIONAL: a speaker bind assigned <c>MeetingTableId</c> after only an
/// "active + in this hall" check, and <c>BusinessMeetingService</c> scanned its own
/// family only. Either could therefore take a table another family already held.
/// Extracted here and called from all three bind / create paths so the invariant holds
/// in every direction.
///
/// Overlap is HALF-OPEN (<c>a.Start &lt; end &amp;&amp; start &lt; a.End</c>), so touching
/// windows (<c>end == start</c>) do NOT collide — the same rule the hall / speaker
/// guards and the hall free-slot generator use. "Live" for the two request families is
/// <see cref="MeetingRequestStatuses.SlotHolding"/> (the same single authority the DB
/// filtered-unique indexes key off); for a business meeting it is
/// <see cref="BusinessMeetingStatus.Confirmed"/>. Read-then-write on its own: the
/// delegation and speaker binds are admin-brokered, low-concurrency desks, and
/// <c>BusinessMeetingService</c> already calls this inside its Serializable
/// transaction, where the range scans hold the key-range locks.</summary>
internal static class MeetingTableOverlapGuard
{
    /// <summary>"This family has no row to exclude". No persisted row carries
    /// <see cref="Guid.Empty"/>, so the comparison is simply always true.</summary>
    private static readonly Guid NoExclusion = Guid.Empty;

    /// <summary>Throws a 409 <see cref="ApiException"/> carrying
    /// <paramref name="errorCode"/> when any of the three families already holds
    /// <paramref name="tableId"/> over <c>[start, end)</c>. Each caller passes its own
    /// family error code (the shipped per-family contract) and the id of the row it is
    /// about to write, so re-binding the same request to the same table is not a
    /// self-conflict.</summary>
    public static async Task EnsureTableIsFreeAsync(
        SimfAppDbContext appDbContext,
        Guid tableId,
        DateTimeOffset start,
        DateTimeOffset end,
        string errorCode,
        Guid? excludeDelegationRequestId,
        Guid? excludeSpeakerRequestId,
        Guid? excludeBusinessMeetingId,
        CancellationToken cancellationToken)
    {
        var skipDelegation = excludeDelegationRequestId ?? NoExclusion;
        var skipSpeaker = excludeSpeakerRequestId ?? NoExclusion;
        var skipBusiness = excludeBusinessMeetingId ?? NoExclusion;

        var delegationClash = await appDbContext.DelegationMeetingRequests.AsNoTracking()
            .Where(r => r.Id != skipDelegation
                && r.MeetingTableId == tableId
                && MeetingRequestStatuses.SlotHolding.Contains(r.Status)
                && r.SlotStart != null && r.SlotEnd != null)
            .AnyAsync(r => r.SlotStart < end && start < r.SlotEnd, cancellationToken);

        var speakerClash = !delegationClash
            && await appDbContext.SpeakerMeetingRequests.AsNoTracking()
                .Where(r => r.Id != skipSpeaker
                    && r.MeetingTableId == tableId
                    && MeetingRequestStatuses.SlotHolding.Contains(r.Status)
                    && r.SlotStart != null && r.SlotEnd != null)
                .AnyAsync(r => r.SlotStart < end && start < r.SlotEnd, cancellationToken);

        var businessClash = !delegationClash && !speakerClash
            && await appDbContext.BusinessMeetings.AsNoTracking()
                .Where(m => m.Id != skipBusiness
                    && m.MeetingTableId == tableId
                    && m.Status == BusinessMeetingStatus.Confirmed)
                .AnyAsync(m => m.Start < end && start < m.End, cancellationToken);

        if (delegationClash || speakerClash || businessClash)
        {
            throw new ApiException(errorCode, 409,
                "That meeting table is already booked at that time.",
                "طاولة الاجتماع هذه محجوزة بالفعل في ذلك الوقت.");
        }
    }
}
