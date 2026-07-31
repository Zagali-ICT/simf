namespace SIMF.Contracts.Authentication;

/// <summary>
/// Part B — the body of <c>POST /api/v1/app/auth/resolve-badge</c>. The app
/// sends the 12-char QR id scanned from the printed badge so it can decide which
/// path to take: an account that already has a password continues to the normal
/// password + OTP sign-in; an account with no password yet goes through the
/// set-password activation flow.
/// </summary>
public sealed class ResolveBadgeRequest
{
    /// <summary>The 12-char Crockford-base32 QR id printed on the badge.</summary>
    public string QrId { get; set; } = string.Empty;
}

/// <summary>
/// Part B — the body of <c>POST /api/v1/app/auth/badge-sign-in</c>. A returning
/// holder (an account that already has a password) scans their badge QR and
/// finishes sign-in with only their password: the QR selects the account and the
/// password (plus any 2FA / lockout) runs through the normal sign-in pipeline.
/// The response is the standard <see cref="SignInResponse"/> — the issued tokens
/// or the 2FA challenge, identical to email sign-in. An unknown QR is
/// indistinguishable from a wrong password — the public badge never bypasses the
/// password.
/// </summary>
public sealed class BadgeSignInRequest
{
    /// <summary>The 12-char QR id scanned from the badge.</summary>
    public string QrId { get; set; } = string.Empty;

    /// <summary>The holder's existing account password.</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Part B — the outcome of resolving a scanned badge. Returned to the app so it
/// can branch. Only an <b>approved</b>, active account resolves; an unknown or
/// not-yet-approved QR returns <see cref="Found"/> = false.
/// </summary>
/// <param name="Found">True when the QR resolves to an approved, active account.</param>
/// <param name="HasPassword">True when the account already has a password — the
/// app routes to the normal sign-in (password + OTP). False routes to activation.</param>
/// <param name="DisplayName">The holder's display name, for a greeting; null when not found.</param>
/// <param name="NeedsEmail">Only meaningful when <see cref="HasPassword"/> is false:
/// true when the account has no real email on file (a walk-in registered without
/// one), so the app must ask the holder to enter one; false when the account
/// already has a real email and the code will be sent there automatically.</param>
/// <param name="MaskedEmail">The masked on-file email (e.g. <c>k****@gmail.com</c>)
/// shown to the holder when <see cref="NeedsEmail"/> is false; null otherwise.</param>
public sealed record ResolveBadgeResponse(
    bool Found,
    bool HasPassword,
    string? DisplayName,
    bool NeedsEmail,
    string? MaskedEmail);

/// <summary>
/// Part B — the body of <c>POST /api/v1/app/auth/badge-activation/start</c>.
/// Issues the email verification code that gates setting the first password.
/// <see cref="Email"/> is required only when the resolved account has no real
/// email on file (<see cref="ResolveBadgeResponse.NeedsEmail"/> = true); when the
/// account already has one the server ignores any supplied email and sends the
/// code to the on-file address.
/// </summary>
public sealed class BadgeActivationStartRequest
{
    /// <summary>The 12-char QR id scanned from the badge.</summary>
    public string QrId { get; set; } = string.Empty;

    /// <summary>The email to verify + attach — required only for an account that
    /// has no real email on file. Ignored when the account already has one.</summary>
    public string? Email { get; set; }
}

/// <summary>Part B — the result of starting activation: where the code went
/// (masked) and how long it is valid.</summary>
public sealed record BadgeActivationStartResponse(
    string MaskedEmail,
    int CodeExpiresInSeconds);

/// <summary>
/// Part B — the body of <c>POST /api/v1/app/auth/badge-activation/complete</c>.
/// Verifies the emailed code and sets the account's first password (and confirms
/// the email). On success the holder signs in normally with email + password.
///
/// <para>#10 phase 4 — a bulk-generated badge is minted against a <b>placeholder</b>
/// profile (a generated display name such as "VIP #3", <c>NationalityId = 0</c>, no
/// interests). Self-claim is the only moment the real holder is at the keyboard, so
/// the profile fields below are captured here and written onto that placeholder row.
/// Every one of them is optional and appended with a default, so the shipped wire
/// contract stays append-only (D-219): a client that sends none still activates
/// exactly as before, it just leaves the placeholder unfilled.</para>
/// </summary>
public sealed class BadgeActivationCompleteRequest
{
    /// <summary>The 12-char QR id scanned from the badge.</summary>
    public string QrId { get; set; } = string.Empty;

    /// <summary>The 6-digit code emailed by the start step.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The first password the holder chooses (password-policy enforced).</summary>
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>Must equal <see cref="NewPassword"/>.</summary>
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>#10 phase 4 — the holder's full name in English, exactly as printed
    /// in the passport. When supplied it replaces the generated placeholder name on
    /// the profile (and the account's placeholder display name). Null / blank leaves
    /// the placeholder untouched.</summary>
    public string? EnglishName { get; set; }

    /// <summary>#10 phase 4 — the holder's full name in Arabic. Same rules as
    /// <see cref="EnglishName"/>.</summary>
    public string? ArabicName { get; set; }

    /// <summary>#10 phase 4 — ISO 3166-1 alpha country code of the holder's
    /// nationality (the same wire shape the profile upsert uses). When supplied it
    /// replaces the placeholder's <c>NationalityId = 0</c>. An unknown or inactive
    /// code is rejected with <c>PROFILE_NATIONALITY_UNKNOWN</c>.</summary>
    public string? NationalityCode { get; set; }

    /// <summary>#10 phase 4 — the holder's picked interests (الاهتمامات), up to 10.
    /// Unknown or deactivated ids are rejected with <c>INTEREST_INVALID</c>. An empty
    /// list leaves the placeholder's interests untouched.</summary>
    public List<Guid> InterestIds { get; set; } = [];
}

/// <summary>Part B — the result of completing activation.</summary>
public sealed record BadgeActivationCompleteResponse(bool Activated);
