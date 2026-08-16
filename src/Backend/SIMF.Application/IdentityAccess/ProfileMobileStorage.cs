// Tests: SIMF.Api.Tests/UserProfileTests.cs (a Saudi-only and an
//        international-only registrant each fill the canonical column and still
//        round-trip BOTH shipped wire keys; the Saudi local spelling stores
//        folded; both keys survive on the raw HTTP response)
//        SIMF.Api.Tests/AdminAccountMobileTests.cs (the desk edit writes the
//        canonical column, and a number supplied on one side replaces the number
//        stored on the other instead of leaving the row holding two)
//        SIMF.Api.Tests/MobileNumberTests.cs (the fold itself)
using SIMF.Common;
using SIMF.Domain.Profiles;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// The write side of the collapsed mobile-number storage — the one place that
/// turns whatever a registrant typed into the columns that hold it.
///
/// <para>Saudi and international were never two attributes. A Saudi mobile IS an
/// international mobile with <c>+966</c> on the front, so two columns bought
/// nothing and cost three things: a row could hold two DIFFERENT numbers with
/// nothing on it saying which one to ring, every reader had to coalesce, and the
/// same number written both ways de-duplicated against neither. The number now
/// lives once, in canonical E.164, on <see cref="UserProfile.MobileNumber"/>.</para>
///
/// <para>It exists as its own class, and not as a private method on
/// <c>UserProfileService</c>, for the same reason
/// <see cref="ProfileIdentityStorage"/> does: there are three write paths that
/// must produce byte-identical storage — the self-service upsert
/// (<c>UserProfileService</c>, in this assembly), the walk-in desk create and the
/// admin edit (both <c>AdminAccountService</c>, in Infrastructure, the first of
/// which also carries the offline badge upload). A second copy of the split rule
/// is exactly how one path comes to write a row the others cannot read.</para>
///
/// <para><b>What this deliberately does NOT do.</b> It does not touch the wire.
/// <c>saudiMobile</c> and <c>internationalMobile</c> are decoded BY NAME by the
/// shipped Flutter app, so both keys keep being emitted and accepted whatever the
/// storage does — which is why the two superseded columns are still written here,
/// in lockstep, rather than dropped. See <see cref="UserProfile.SaudiMobile"/>
/// for the list of readers that must move first.</para>
/// </summary>
public static class ProfileMobileStorage
{
    /// <summary>
    /// Writes the canonical number and the two columns it supersedes, together,
    /// from whichever of the two shipped request fields carried a value.
    ///
    /// <para>Saudi wins when both arrive. That is not an arbitrary tie-break: it
    /// is the precedence the VIP roster already displays with
    /// (<c>VipRosterService</c> renders <c>SaudiMobile ?? InternationalMobile</c>),
    /// so the collapsed row rings the same number the PR desk was already
    /// reading.</para>
    ///
    /// <para>The two superseded columns are exact complements — the canonical
    /// number goes to <see cref="UserProfile.SaudiMobile"/> when it is Saudi and
    /// to <see cref="UserProfile.InternationalMobile"/> when it is not, and the
    /// other is NULLed. Nulling the other one is the point rather than tidiness:
    /// leaving a stale number behind is precisely the "row holding two numbers"
    /// this collapse exists to end.</para>
    /// </summary>
    public static void Sync(
        UserProfile profile, string? saudiMobile, string? internationalMobile)
    {
        var canonical = Canonical(saudiMobile, internationalMobile);
        var isSaudi = MobileNumber.IsSaudi(canonical);
        profile.MobileNumber = canonical;
        profile.SaudiMobile = isSaudi ? canonical : null;
        profile.InternationalMobile = isSaudi ? null : canonical;
    }

    /// <summary>The one canonical E.164 number the two shipped request fields
    /// describe between them, or <c>null</c> when neither carried one.</summary>
    public static string? Canonical(string? saudiMobile, string? internationalMobile) =>
        MobileNumber.NormalizeOptional(saudiMobile)
        ?? MobileNumber.NormalizeOptional(internationalMobile);
}
