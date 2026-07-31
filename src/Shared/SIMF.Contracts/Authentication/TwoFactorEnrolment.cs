namespace SIMF.Contracts.Authentication;

/// <summary>
/// #2 (Q1, 2026-07-30) — the body of
/// <c>POST /api/v1/app/auth/totp/enrolment/start</c>. Begins MANDATORY
/// authenticator enrolment for a Control Panel account that signed in with the
/// correct password but has no authenticator secret paired. The caller holds no
/// access token at this point — the single-use enrolment ticket the sign-in step
/// returned is the credential.
/// </summary>
public sealed class StartTwoFactorEnrolmentRequest
{
    /// <summary>The <c>TwoFactorEnrolmentToken</c> from the sign-in response.</summary>
    public string EnrolmentToken { get; set; } = string.Empty;
}

/// <summary>
/// #2 — the body of <c>POST /api/v1/app/auth/totp/enrolment/complete</c>.
/// Verifies the first authenticator code against the staged secret and, on
/// success, completes the sign-in that was held back at the password step.
/// </summary>
public sealed class CompleteTwoFactorEnrolmentRequest
{
    /// <summary>The same ticket <see cref="StartTwoFactorEnrolmentRequest"/> used.</summary>
    public string EnrolmentToken { get; set; } = string.Empty;

    /// <summary>The six-digit code from the authenticator app.</summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// #2 — the result of a completed mandatory enrolment: the account is now
/// <c>TwoFactorEnabled</c> with an active secret, and the session that the
/// password step withheld is issued here.
/// </summary>
/// <param name="Tokens">The issued session. The access token carries
/// <c>amr=mfa</c> — the code just verified IS the second factor.</param>
/// <param name="RecoveryCodes">The freshly minted single-use recovery codes,
/// shown plaintext exactly once (D-040). The API never returns them again.</param>
public sealed record CompleteTwoFactorEnrolmentResponse(
    AuthTokens Tokens,
    IReadOnlyList<string> RecoveryCodes);
