// Tests: SIMF.Api.Tests/MeetingTableOverlapTests.cs
// Tests: SIMF.Api.Tests/DelegationMeetingQaFixesTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.MeetingRequests;

/// <summary>The single authority for the invariant "a meeting TABLE holds
/// one meeting at a time". THREE families can occupy a <c>MeetingTable</c>: a
/// delegation meeting request, a speaker meeting request, and the admin-arranged
/// <c>BusinessMeeting</c> (the only one with a real FK). The scan started on the
/// delegation bind and later learned the business family, but it
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
        DateTime start,
        DateTime end,
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

    /// <summary>The removal twin of <see cref="EnsureTableIsFreeAsync"/>: throws a 409
    /// naming the offending table codes when any of <paramref name="tableIds"/> still
    /// carries a booking that has not finished yet, in ANY of the three families.
    ///
    /// <para>Removing a table is a soft delete, so nothing in the database detaches the
    /// bookings that point at it: the two request families hold <c>MeetingTableId</c> as
    /// a nullable FK configured <c>OnDelete(SetNull)</c>, which a soft delete never
    /// fires. The row simply stops being listed, so the hall plan loses the table while
    /// the meetings still name it and the check-in desk has nowhere to send the parties.
    /// The single-delete guard used to scan the business family alone and the
    /// generate-with-reset path scanned nothing at all, which is the same
    /// one-directional shape <see cref="EnsureTableIsFreeAsync"/> was extracted to
    /// fix.</para></summary>
    public static async Task EnsureTablesHaveNoFutureBookingAsync(
        SimfAppDbContext appDbContext,
        IReadOnlyCollection<Guid> tableIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (tableIds.Count == 0) { return; }
        var ids = tableIds.Distinct().ToList();

        var booked = new HashSet<Guid>(
            await appDbContext.BusinessMeetings.AsNoTracking()
                .Where(m => ids.Contains(m.MeetingTableId)
                    && m.Status == BusinessMeetingStatus.Confirmed
                    && m.End > now)
                .Select(m => m.MeetingTableId)
                .Distinct()
                .ToListAsync(cancellationToken));

        booked.UnionWith(
            await appDbContext.SpeakerMeetingRequests.AsNoTracking()
                .Where(r => r.MeetingTableId != null
                    && ids.Contains(r.MeetingTableId.Value)
                    && MeetingRequestStatuses.SlotHolding.Contains(r.Status)
                    && r.SlotEnd != null && r.SlotEnd > now)
                .Select(r => r.MeetingTableId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken));

        booked.UnionWith(
            await appDbContext.DelegationMeetingRequests.AsNoTracking()
                .Where(r => r.MeetingTableId != null
                    && ids.Contains(r.MeetingTableId.Value)
                    && MeetingRequestStatuses.SlotHolding.Contains(r.Status)
                    && r.SlotEnd != null && r.SlotEnd > now)
                .Select(r => r.MeetingTableId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken));

        if (booked.Count == 0) { return; }

        // The codes, not the ids: the admin identifies a table by the code printed on
        // the hall plan, and a bulk re-lay can trip on several at once.
        var bookedIds = booked.ToList();
        var codes = await appDbContext.MeetingTables.AsNoTracking()
            .Where(table => bookedIds.Contains(table.Id))
            .OrderBy(table => table.Code)
            .Select(table => table.Code)
            .ToListAsync(cancellationToken);
        var named = string.Join(", ", codes);

        throw new ApiException(ErrorCodes.MeetingTableInvalid, 409,
            $"Cancel the upcoming meetings on these tables first: {named}.",
            $"يرجى إلغاء الاجتماعات القادمة على هذه الطاولات أولاً: {named}.");
    }
}
