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
}
