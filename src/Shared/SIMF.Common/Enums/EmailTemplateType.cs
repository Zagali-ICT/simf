namespace SIMF.Common.Enums;

/// <summary>D-735 — the fixed set of transactional identity emails whose
/// bilingual subject/body an admin can edit in the Control Panel. Each value
/// keys one <c>EmailTemplate</c> override row (when customised) and one entry in
/// the code-owned default catalogue (the always-present fallback). Persisted by
/// NAME (see <c>EmailTemplateConfiguration</c>), so the integer order is not a
/// wire contract — new values may be appended.</summary>
public enum EmailTemplateType
{
    /// <summary>Second-factor OTP sent on sign-in.</summary>
    SignInOtp = 0,

    /// <summary>Email-address verification code (sign-up / resend).</summary>
    EmailVerification = 1,

    /// <summary>Heads-up to the owner of an existing account (D-198). No code.</summary>
    AccountExists = 2,

    /// <summary>Password-reset code (forgot password).</summary>
    PasswordReset = 3,

    /// <summary>Badge / account-activation code.</summary>
    BadgeActivation = 4,

    /// <summary>Biometric-enrolment step-up confirmation code.</summary>
    BiometricStepUp = 5,

    /// <summary>D-751 — cover note for a bulk-generated badge batch emailed to the
    /// organiser, carrying the QR badge PNGs as a single ZIP attachment.</summary>
    BulkBadgeDelivery = 6,

    /// <summary>#24 — verification code sent to the NEW address when a signed-in
    /// user changes their login email, proving they control the new inbox before
    /// the change completes. Appended (persisted by name — see the type doc).</summary>
    EmailChangeVerification = 7,

    /// <summary>#24 — security alert sent to the OLD address once a login email
    /// change completes, so the previous owner is warned out-of-band if the change
    /// was not theirs. Appended (persisted by name — see the type doc).</summary>
    EmailChangedNotice = 8,

    /// <summary>BUG-024 — the lead card emailed to an exhibitor's own account
    /// address after they scan a visitor's badge at their booth. Appended
    /// (persisted by name — see the type doc).</summary>
    ExhibitorLeadCapture = 9,
}
