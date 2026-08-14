// Tests: SIMF.Api.Tests/AccountlessAttendeeTests.cs (hall door, seat, lead, merge)
using Microsoft.EntityFrameworkCore;

namespace SIMF.Infrastructure.Persistence;

/// <summary>
/// Translates between the two ids one attendee can be known by: the
/// <c>UserProfile.Id</c> that every attendee has, and the Identity
/// <c>SimfUser.Id</c> that only those who wanted app access ever got.
///
/// <para>Attendance, seating and exhibitor leads are keyed by the PROFILE, so
/// that a walk-in or a bulk-minted badge can be recorded at all. Some edges still
/// speak accounts, and they are the reason this exists.</para>
///
/// <para>This covers the INBOUND edge: a signed-in app endpoint knows its caller
/// only as a JWT subject, which is an account, so it converts once on the way in.
/// The OUTBOUND edge — a notification, which is delivered to the account that owns
/// the devices and the mailbox — is not here on purpose: those callers already
/// hold an <c>IQueryable</c> and join to <c>UserProfile</c> in SQL, so the
/// estimate paths keep counting in the database instead of materialising ids, and
/// attendees with no account fall out of the join naturally.</para>
///
/// <para>Both ids are <see cref="Guid"/>, so nothing but the parameter name
/// tells them apart; passing one where the other belongs matches no row rather
/// than failing loudly. That is why the conversion goes through here instead of
/// being written out at each call site.</para>
/// </summary>
internal static class AttendeeProfiles
{
    /// <summary>The profile id for a signed-in account, or null when that
    /// account has no profile at all (an admin-typed user carries none).</summary>
    public static async Task<Guid?> ProfileIdForAccountAsync(
        this SimfAppDbContext appDbContext, Guid userId,
        CancellationToken cancellationToken = default) =>
        await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);
}
