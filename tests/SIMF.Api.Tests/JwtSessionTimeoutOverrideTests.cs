// Session:TimeoutHours ops override (env SIMF_API_Session__TimeoutHours) — unit
// tests for JwtOptions.ResolveAccessTokenMinutes, which lengthens the access
// token beyond the NCA-default 5 min, clamped to the 24h absolute cap (D-443).
using SIMF.Common.Options;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class JwtSessionTimeoutOverrideTests
{
    [Fact]
    public void Absent_override_keeps_the_nca_default()
    {
        var minutes = JwtOptions.ResolveAccessTokenMinutes(
            defaultMinutes: 5, overrideHours: 0, sessionLifetimeHours: 24);

        Assert.Equal(5, minutes);
    }

    [Fact]
    public void Negative_override_keeps_the_nca_default()
    {
        var minutes = JwtOptions.ResolveAccessTokenMinutes(
            defaultMinutes: 5, overrideHours: -3, sessionLifetimeHours: 24);

        Assert.Equal(5, minutes);
    }

    [Fact]
    public void Override_extends_the_access_token_to_the_requested_hours()
    {
        var minutes = JwtOptions.ResolveAccessTokenMinutes(
            defaultMinutes: 5, overrideHours: 2, sessionLifetimeHours: 24);

        Assert.Equal(120, minutes);
    }

    [Fact]
    public void Override_is_clamped_to_the_absolute_session_cap()
    {
        // A 48h override cannot exceed the 24h NCA absolute cap.
        var minutes = JwtOptions.ResolveAccessTokenMinutes(
            defaultMinutes: 5, overrideHours: 48, sessionLifetimeHours: 24);

        Assert.Equal(24 * 60, minutes);
    }

    [Fact]
    public void Override_exactly_at_the_cap_is_allowed()
    {
        var minutes = JwtOptions.ResolveAccessTokenMinutes(
            defaultMinutes: 5, overrideHours: 24, sessionLifetimeHours: 24);

        Assert.Equal(24 * 60, minutes);
    }
}
