// Tests: every routable page of the public Website renders without throwing.
//
// The Control Panel got this suite first (D-831), after a page shipped that
// compiled, passed a clean Release build, a full test suite, an adversarial review
// and a simplify pass, and then broke on contact with a renderer - because every
// one of those gates reads source and nothing had ever rendered it.
//
// The Website is in better shape: 17 of its 20 routable pages already have a
// per-page suite. This covers the remainder and holds the line, on the surface the
// public actually sees. It asserts nothing about content - the per-page suites do
// that, in detail. It only asserts the page exists.
using System.Globalization;
using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.ApiClient;
using SIMF.Web.Content;

namespace SIMF.Web.Tests;

public sealed class PageRenderSmokeTests : WebComponentTestBase
{
    /// <summary>Pages that cannot be rendered bare, with the reason. An entry is a
    /// page with no render coverage at all, which is the hole this class closes -
    /// keep the list empty if you can.</summary>
    private static readonly Dictionary<string, string> CannotRenderBare = new(StringComparer.Ordinal);

    public PageRenderSmokeTests()
    {
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;

        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new ResourceAssetCollection(Array.Empty<ResourceAsset>()));

        // Landing injects HeroMedia, which reads the organisation profile from the
        // API. The API is DELIBERATELY unreachable here: a public page that
        // white-screens when the backend is down is a defect worth failing on, and
        // this is the only test that would notice.
        Services.AddSingleton(new SimfPublicClient(
            new HttpClient(new UnreachableApi()) { BaseAddress = new Uri("https://api.test/") }));
        Services.AddMemoryCache();
        Services.AddScoped<HeroMedia>();
    }

    private sealed class UnreachableApi : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(
                System.Net.HttpStatusCode.ServiceUnavailable));
    }

    public static TheoryData<string> RoutablePages()
    {
        var data = new TheoryData<string>();
        foreach (var page in Pages())
        {
            data.Add(page.FullName!);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(RoutablePages))]
    public void The_page_renders(string typeName)
    {
        if (CannotRenderBare.ContainsKey(typeName)) { return; }

        var type = Pages().Single(candidate => candidate.FullName == typeName);

        // The page type is only known at run time, so build the fragment by hand
        // rather than going through the generic RenderComponent<T>.
        var exception = Record.Exception(() => Render(builder =>
        {
            builder.OpenComponent(0, type);
            builder.CloseComponent();
        }));

        Assert.True(
            exception is null,
            $"{typeName} threw while rendering, so the page is broken for every "
            + $"visitor who opens it:\n{exception}");
    }

    [Fact]
    public void The_cannot_render_list_has_no_stale_entries()
    {
        var known = Pages().Select(page => page.FullName!).ToHashSet(StringComparer.Ordinal);
        var stale = CannotRenderBare.Keys.Where(name => !known.Contains(name)).ToList();

        Assert.True(
            stale.Count == 0,
            "Entries name pages that no longer exist:\n  " + string.Join("\n  ", stale));
    }

    /// <summary>Every component in the Website carrying a <c>@page</c> route.</summary>
    private static IEnumerable<Type> Pages() =>
        typeof(SIMF.Web.Components.Pages.Objectives).Assembly
            .GetTypes()
            .Where(type => typeof(IComponent).IsAssignableFrom(type)
                && !type.IsAbstract
                && type.GetCustomAttributes<RouteAttribute>().Any())
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
}
