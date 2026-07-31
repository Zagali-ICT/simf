namespace SIMF.Api.Tests;

/// <summary>
/// #2 (Q1, 2026-07-30) — a <see cref="SimfApiFactory"/> with mandatory
/// Control-Panel two-factor enrolment switched ON
/// (<c>IdentityLifecycle:RequireControlPanelTwoFactorEnrolment = true</c>),
/// which is the PRODUCTION default. The base factory now pins the same value
/// (the general suite's admin fixtures enrol through
/// <c>AuthFlow.SignInControlPanelAsync</c> and complete a real TOTP step), so
/// this type is kept as the explicit statement that
/// <c>ControlPanelTwoFactorEnrolmentTests</c> depends on the gate being on —
/// its assertions are meaningless without it. Mirrors the existing
/// <see cref="PasswordExpiryApiFactory"/> / <c>FaceGateApiFactory</c> pattern.
/// </summary>
public sealed class ControlPanelTwoFactorApiFactory : SimfApiFactory
{
    public ControlPanelTwoFactorApiFactory()
    {
        Environment.SetEnvironmentVariable(
            "IdentityLifecycle__RequireControlPanelTwoFactorEnrolment", "true");
    }
}
