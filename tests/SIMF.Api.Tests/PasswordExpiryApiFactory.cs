namespace SIMF.Api.Tests;

/// <summary>
/// A <see cref="SimfApiFactory"/> with password expiry switched on
/// (<c>IdentityLifecycle:PasswordMaxAgeDays = 30</c>) so the A7-13 expiry gate can
/// be exercised independently — the general suite leaves it at the default 0
/// (disabled), so no other test class is affected.
/// </summary>
public sealed class PasswordExpiryApiFactory : SimfApiFactory
{
    public PasswordExpiryApiFactory()
    {
        Environment.SetEnvironmentVariable("IdentityLifecycle__PasswordMaxAgeDays", "30");
    }
}
