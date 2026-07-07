// Tests: bUnit coverage for the Website /account landing (Home.razor +
// Home.razor.cs). D-630 — the page's C# moved to a code-behind partial (Website
// clean-code, Phase 5) and it had zero component coverage. Pins the two
// branches: an unauthenticated visitor is redirected to /login, and a signed-in
// visitor sees the landing.
using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SIMF.ApiClient;
using SIMF.Contracts.Authentication;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Tests;

public sealed class HomePageTests : WebComponentTestBase
{
    private readonly SimfAuthSession _session = new();

    public HomePageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(_session);
        // The page injects the auth client; sign-out is only reached on a click,
        // so a no-op handler is enough for the render branches.
        Services.AddSingleton(new SimfAuthClient(new HttpClient(new NoopHandler())
        {
            BaseAddress = new Uri("https://api.test/"),
        }));
    }

    [Fact]
    public void Redirects_to_login_when_not_signed_in()
    {
        RenderComponent<Home>();

        Assert.EndsWith("/login", Navigation.Uri);
    }

    [Fact]
    public void Renders_the_signed_in_landing_when_signed_in()
    {
        _session.Complete(new AuthTokens("access", "refresh", "Bearer", 1800,
            new AuthUser(Guid.NewGuid(), "visitor@simf.test", "Visitor")));

        var cut = RenderComponent<Home>();

        Assert.Contains("You're signed in", cut.Markup);
        Assert.DoesNotContain("/login", Navigation.Uri);
    }

    private sealed class NoopHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
