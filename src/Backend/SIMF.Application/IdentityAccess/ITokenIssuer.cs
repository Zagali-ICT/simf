using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// itokenissuer-extraction (2026-07-30) — the single place a SIMF session is
/// minted. Every entry point that turns a proven identity into tokens goes
/// through here: the password sign-in, the badge-QR sign-in (which delegates to
/// the password pipeline) and the device-key / biometric ceremony.
///
/// <para><b>Why it exists.</b> Those paths each carried their own ~15-line copy
/// of "resolve roles, resolve permissions, resolve the mobile app role, sign an
/// access token, persist a refresh token". Two copies had already drifted: the
/// device-key path did not record <c>LastSuccessfulSignInAtUtc</c>, so an
/// attendee who only ever signed in with Face ID looked dormant to the A1-19
/// sweep, and it returned no <c>PreviousSignInAtUtc</c>. A future claim or a
/// change to the D-443 absolute session cap would have had to be made in every
/// copy, and a missed one is invisible until an auditor finds it.</para>
///
/// <para>What stays at the call site: the entry-point-specific audit row
/// (<c>SignIn.Succeeded</c> vs <c>SignIn.WithDeviceKey</c>) and its log line.
/// The token contents and lifetimes are here.</para>
///
/// <para>Token REFRESH is deliberately not routed through this interface.
/// Rotation must inherit the presented chain's absolute deadline instead of
/// starting a new one (D-443), and it runs inside its own transaction with
/// <c>RotatedFromId</c> — different lifecycle, not a duplicate of this one.</para>
/// </summary>
public interface ITokenIssuer
{
    /// <summary>
    /// Mints an access token + a fresh refresh token for <paramref name="user"/>
    /// and records the sign-in.
    /// </summary>
    /// <param name="secondFactorCompleted">
    /// Held-item #2c — <c>true</c> on every path that cleared a second factor
    /// (TOTP, recovery code, emailed OTP, mandatory enrolment), <c>false</c> on
    /// the password-only path for an account with 2FA off, and <c>null</c> to
    /// leave the <c>amr</c> claim derived from <c>TwoFactorEnabled</c> — which is
    /// what the device-key ceremony does, because the signed challenge is a
    /// possession factor rather than one of the three the flag describes.
    /// Stated by the caller rather than inferred here, so a new issuance path has
    /// to answer the question consciously.
    /// </param>
    Task<AuthTokens> IssueAsync(
        SimfUser user,
        bool? secondFactorCompleted = null,
        CancellationToken cancellationToken = default);
}
