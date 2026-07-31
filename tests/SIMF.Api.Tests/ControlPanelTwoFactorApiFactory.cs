namespace SIMF.Api.Tests;

/// <summary>
/// #2 (Q1, 2026-07-30) — a <see cref="SimfApiFactory"/> with mandatory
/// Control-Panel two-factor enrolment switched ON
/// (<c>IdentityLifecycle:RequireControlPanelTwoFactorEnrolment = true</c>),
/// which is the PRODUCTION default. The general suite pins it off because its
/// admin fixtures predate enrolment-first; this factory restores the shipping
/// posture so <c>ControlPanelTwoFactorEnrolmentTests</c> exercises what
/// production actually does. Mirrors the existing
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
