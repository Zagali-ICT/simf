namespace SIMF.Api.Tests;

/// <summary>
/// A <see cref="SimfApiFactory"/> with dormant-account auto-disable switched on
/// (<c>IdentityLifecycle:DormantAccountDisableDays = 30</c>) so the A1-19 sweep can
/// be exercised independently — the general suite leaves it at the default 0
/// (disabled, reset in the base factory), so no other test class is affected.
/// </summary>
public sealed class DormantAccountApiFactory : SimfApiFactory
{
    public DormantAccountApiFactory()
    {
        Environment.SetEnvironmentVariable("IdentityLifecycle__DormantAccountDisableDays", "30");
    }
}
