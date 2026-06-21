namespace SIMF.Api.Tests;

/// <summary>
/// A <see cref="SimfApiFactory"/> with password-history reuse prevention switched
/// on (<c>IdentityLifecycle:PasswordHistoryCount = 5</c>) so the A7-20 check can be
/// exercised independently — the general suite leaves it at the default 0
/// (disabled, reset in the base factory), so no other test class is affected.
/// </summary>
public sealed class PasswordHistoryApiFactory : SimfApiFactory
{
    public PasswordHistoryApiFactory()
    {
        Environment.SetEnvironmentVariable("IdentityLifecycle__PasswordHistoryCount", "5");
    }
}
