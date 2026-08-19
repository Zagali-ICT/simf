// Tests: SIMF.Api.Tests/MeetingRequesterOverlapTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.MeetingRequests;

/// <summary>The single authority for the invariant "one PERSON holds one meeting at
/// a time". Two families are booked against a requester: a speaker meeting request
/// and a delegation meeting request, both keyed on <c>RequestedByUserId</c>.
///
/// <para>Each bind path used to scan its own family only — the speaker accept
/// checked <c>SpeakerMeetingRequests</c>, the delegation accept checked country
/// overlap on <c>DelegationMeetingRequests</c> — so a single VIP carrying both
/// <c>AllowsSpeakerMeeting</c> and <c>AllowsDelegationMeeting</c> could be approved
/// into a speaker meeting and a delegation meeting at the same instant in two
/// different halls, with both parties emailed and both reminders sent. That is the
/// one-directional shape <see cref="MeetingTableOverlapGuard"/> was extracted to fix
/// for tables, reproduced at the person level because the two services were written
/// as copies of each other.</para>
///
/// <para>Overlap is HALF-OPEN (<c>SlotStart &lt; end &amp;&amp; start &lt; SlotEnd</c>),
/// so touching windows do not collide, and "live" is
/// <see cref="MeetingRequestStatuses.SlotHolding"/> — the same rules the table and
/// hall guards use. Both callers run it inside their Serializable transaction, so its
/// range scans hold the key-range locks that serialize a concurrent rival.</para>
/// </summary>
internal static class MeetingRequesterOverlapGuard
{
    /// <summary>"This family has no row to exclude". No persisted row carries
    /// <see cref="Guid.Empty"/>, so the comparison is simply always true.</summary>
    private static readonly Guid NoExclusion = Guid.Empty;

    /// <summary>Throws a 409 <see cref="ApiException"/> carrying
    /// <paramref name="errorCode"/> when <paramref name="requesterUserId"/> already
    /// holds a live meeting of either family over <c>[start, end)</c>. Each caller
    /// passes its own family error code (the shipped per-family contract) and the id of
    /// the row it is about to write, so re-binding the same request is not a
    /// self-conflict.</summary>
    public static async Task EnsureRequesterIsFreeAsync(
        SimfAppDbContext appDbContext,
        Guid requesterUserId,
        DateTime start,
        DateTime end,
        string errorCode,
        Guid? excludeSpeakerRequestId,
        Guid? excludeDelegationRequestId,
        CancellationToken cancellationToken)
    {
        var skipSpeaker = excludeSpeakerRequestId ?? NoExclusion;
        var skipDelegation = excludeDelegationRequestId ?? NoExclusion;

        var speakerClash = await appDbContext.SpeakerMeetingRequests.AsNoTracking()
            .Where(r => r.Id != skipSpeaker
                && r.RequestedByUserId == requesterUserId
                && MeetingRequestStatuses.SlotHolding.Contains(r.Status)
                && r.SlotStart != null && r.SlotEnd != null)
            .AnyAsync(r => r.SlotStart < end && start < r.SlotEnd, cancellationToken);

        var delegationClash = !speakerClash
            && await appDbContext.DelegationMeetingRequests.AsNoTracking()
                .Where(r => r.Id != skipDelegation
                    && r.RequestedByUserId == requesterUserId
                    && MeetingRequestStatuses.SlotHolding.Contains(r.Status)
                    && r.SlotStart != null && r.SlotEnd != null)
                .AnyAsync(r => r.SlotStart < end && start < r.SlotEnd, cancellationToken);

        if (speakerClash || delegationClash)
        {
            throw new ApiException(errorCode, 409,
                "The requester already has a meeting booked at that time.",
                "لدى مقدّم الطلب اجتماع محجوز بالفعل في هذا الوقت.");
        }
    }
}
