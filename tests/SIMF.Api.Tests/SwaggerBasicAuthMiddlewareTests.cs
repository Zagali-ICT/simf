using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SIMF.Api.Middleware;
using SIMF.Common.Options;

namespace SIMF.Api.Tests;

// Covers the D-355 production OpenAPI Basic-auth gate in isolation (no host):
// the /swagger surface must challenge without valid credentials and pass through
// with them, while non-Swagger paths are never gated.
[Trait(TestAreas.TraitName, TestAreas.Security)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class SwaggerBasicAuthMiddlewareTests
{
    private const string User = "simf-docs";
    private const string Pass = "s3cr3t-pass";

    private static (SwaggerBasicAuthMiddleware Middleware, Func<bool> NextCalled) Build()
    {
        var nextCalled = false;
        var middleware = new SwaggerBasicAuthMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new SwaggerOptions { AllowSwagger = true, Username = User, Password = Pass });
        return (middleware, () => nextCalled);
    }

    private static DefaultHttpContext ContextFor(string path, string? user = null, string? pass = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (user is not null)
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
            context.Request.Headers.Authorization = $"Basic {token}";
        }
        return context;
    }

    [Fact]
    public async Task NonSwaggerPath_PassesThroughUngated()
    {
        var (middleware, nextCalled) = Build();
        var context = ContextFor("/api/v1/app/news");

        await middleware.InvokeAsync(context);

        nextCalled().Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task SwaggerPath_NoCredentials_Challenges401()
    {
        var (middleware, nextCalled) = Build();
        var context = ContextFor("/swagger");

        await middleware.InvokeAsync(context);

        nextCalled().Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.Headers.WWWAuthenticate.ToString().Should().StartWith("Basic");
    }

    [Fact]
    public async Task SwaggerJsonPath_WrongPassword_Challenges401()
    {
        var (middleware, nextCalled) = Build();
        var context = ContextFor("/swagger/cp/swagger.json", User, "wrong");

        await middleware.InvokeAsync(context);

        nextCalled().Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task SwaggerPath_CorrectCredentials_PassesThrough()
    {
        var (middleware, nextCalled) = Build();
        var context = ContextFor("/swagger", User, Pass);

        await middleware.InvokeAsync(context);

        nextCalled().Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }
}
