using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SIMF.Api.Middleware;
using SIMF.Common;
using SIMF.Common.Options;

namespace SIMF.Api.Tests;

// The X-App-Key gate on the mobile surface, in isolation (no host).
//
// The app and the website have sent this header since the API contract was
// written and nothing read it, so the contract described a control that did not
// exist. These tests pin the read side - and, more importantly, pin that it
// stays INERT until keys are configured. That fail-open default is the only
// reason the gate can be deployed before a client build carrying a key exists,
// so a change that made it fail closed would lock out every installed app on the
// next deploy, with no error anywhere until users reported it.
[Trait(TestAreas.TraitName, TestAreas.Security)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class AppKeyMiddlewareTests
{
    private const string GoodKey = "simf-app-key-live";
    private const string RotationKey = "simf-app-key-next";
    private const string MobilePath = "/api/v1/app/news";

    private static (AppKeyMiddleware Middleware, Func<bool> NextCalled) Build(
        params string[] keys)
    {
        var nextCalled = false;
        var middleware = new AppKeyMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new AppKeyOptions { Keys = keys });
        return (middleware, () => nextCalled);
    }

    private static DefaultHttpContext ContextFor(
        string path,
        string? appKey = null,
        string method = "GET")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        if (appKey is not null)
        {
            context.Request.Headers["X-App-Key"] = appKey;
        }
        return context;
    }

    private static Task Invoke(AppKeyMiddleware middleware, HttpContext context) =>
        middleware.InvokeAsync(context, NullLogger<AppKeyMiddleware>.Instance);

    [Fact]
    public async Task NoKeysConfigured_MobileRequestPassesUngated()
    {
        // The default configuration. Deploying the gate must change nothing.
        var (middleware, nextCalled) = Build();

        await Invoke(middleware, ContextFor(MobilePath));

        nextCalled().Should().BeTrue();
    }

    [Fact]
    public async Task NoKeysConfigured_GateReportsItselfDisabled()
    {
        new AppKeyOptions().IsEnabled.Should().BeFalse();
        new AppKeyOptions { Keys = [GoodKey] }.IsEnabled.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ConfiguredAndCorrectKey_PassesThrough()
    {
        var (middleware, nextCalled) = Build(GoodKey);

        await Invoke(middleware, ContextFor(MobilePath, GoodKey));

        nextCalled().Should().BeTrue();
    }

    [Fact]
    public async Task ConfiguredAndMissingKey_IsRefused()
    {
        var (middleware, nextCalled) = Build(GoodKey);

        var act = async () => await Invoke(middleware, ContextFor(MobilePath));

        (await act.Should().ThrowAsync<ApiException>())
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled().Should().BeFalse();
    }

    [Fact]
    public async Task ConfiguredAndWrongKey_IsRefused()
    {
        // A present-but-wrong key must be treated exactly like a missing one.
        var (middleware, nextCalled) = Build(GoodKey);

        var act = async () =>
            await Invoke(middleware, ContextFor(MobilePath, "not-the-key"));

        (await act.Should().ThrowAsync<ApiException>())
            .Which.Code.Should().Be(ErrorCodes.Forbidden);
        nextCalled().Should().BeFalse();
    }

    [Fact]
    public async Task AnySecondKeyIsAccepted_SoAKeyCanBeRotated()
    {
        // Rotation needs both values live while builds roll out; if only the
        // first entry were honoured, rotating would lock out the older build.
        var (middleware, nextCalled) = Build(GoodKey, RotationKey);

        await Invoke(middleware, ContextFor(MobilePath, RotationKey));

        nextCalled().Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/v1/auth/sign-in")]
    [InlineData("/api/v1/admin/users")]
    [InlineData("/api/v1/public/organization")]
    [InlineData("/health")]
    public async Task NonMobileSurfaces_AreNeverGated(string path)
    {
        // The website and the Control Panel drive /auth/ and /admin/, and their
        // protection is authentication and permissions - never a value shipped
        // inside a client. Gating them here would be both wrong and breaking.
        var (middleware, nextCalled) = Build(GoodKey);

        await Invoke(middleware, ContextFor(path));

        nextCalled().Should().BeTrue();
    }

    [Fact]
    public async Task CorsPreflight_IsNeverGated()
    {
        // A preflight carries no custom header by definition - it is the request
        // ASKING whether X-App-Key may be sent. Refusing it would break every
        // cross-origin call before the real request was made.
        var (middleware, nextCalled) = Build(GoodKey);

        await Invoke(middleware, ContextFor(MobilePath, method: "OPTIONS"));

        nextCalled().Should().BeTrue();
    }

    [Fact]
    public async Task MobileSurfaceIsMatchedCaseInsensitively()
    {
        var (middleware, nextCalled) = Build(GoodKey);

        var act = async () =>
            await Invoke(middleware, ContextFor("/API/V1/APP/News"));

        await act.Should().ThrowAsync<ApiException>();
        nextCalled().Should().BeFalse();
    }

    [Fact]
    public void KeyComparisonIsOrdinalAndRejectsEmpty()
    {
        var options = new AppKeyOptions { Keys = [GoodKey] };

        options.Accepts(GoodKey).Should().BeTrue();
        options.Accepts(null).Should().BeFalse();
        options.Accepts(string.Empty).Should().BeFalse();
        options.Accepts(GoodKey.ToUpperInvariant()).Should().BeFalse();
        options.Accepts($" {GoodKey}").Should().BeFalse();
    }
}
