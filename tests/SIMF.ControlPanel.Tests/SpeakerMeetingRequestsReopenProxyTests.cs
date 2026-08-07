// Tests: the CP "Reopen request" row action end-to-end on the Control Panel side.
//
// The QA B20 round added the row action, the API endpoint and API-level tests, but
// the button posts to the CP BFF proxy (/account/api/...), and that proxy maps every
// upstream call EXPLICITLY in AccountEndpoints — there is no catch-all. A missing
// MapPost therefore turns the button into a silent 404 that no API test can see.
//
// These tests close that hole two ways:
//   1. the reopen proxy route is registered, is a POST, and carries the same
//      authorization metadata as its sibling check-in route (never laxer);
//   2. the URL the page actually posts when an admin confirms Reopen resolves to a
//      registered POST proxy route — the page↔proxy parity that was missing;
//   3. the action stays behind SpeakerMeetingRequests.Manage.
using System.Reflection;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Programme;
using SIMF.ControlPanel;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class SpeakerMeetingRequestsReopenProxyTests : CpComponentTestBase
{
    // The PassThroughStringLocalizer echoes the resx key.
    private const string ReopenKey = "Admin.SpeakerMeetingRequests.Reopen";
    private const string PostJson = "simfAccount.postJson";

    private static readonly AdminSpeakerMeetingRequestRow RejectedRow = new(
        Guid.NewGuid(), Guid.NewGuid(), "Capt. QA Speaker", "متحدّث الاختبار",
        Guid.NewGuid(), "QA Requester", "Maritime cooperation",
        MeetingRequestStatus.Rejected, "Declined by mistake.",
        SimfClock.Now, SimfClock.Now);

    // -- 1. the proxy route exists and is no laxer than its siblings -------------

    [Fact]
    public void Reopen_proxy_route_is_registered_as_an_authorized_post()
    {
        var routes = BffRoutes.Build();

        // Control: the sibling check-in route proves the harness really enumerated
        // the proxy table (so a missing reopen below is a real gap, not an empty list).
        var checkIn = FindRoute(routes,
            "/account/api/admin/speaker-meeting-requests/{id:guid}/check-in");
        Assert.NotNull(checkIn);

        var reopen = FindRoute(routes,
            "/account/api/admin/speaker-meeting-requests/{id:guid}/reopen");
        Assert.NotNull(reopen);

        Assert.Contains("POST",
            reopen!.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
        // Cookie-authenticated like every other admin passthrough — never anonymous.
        Assert.NotEmpty(reopen.Metadata.GetOrderedMetadata<IAuthorizeData>());
        Assert.Null(reopen.Metadata.GetMetadata<IAllowAnonymous>());
        // And not one notch weaker than the action next to it on the same page.
        Assert.Equal(
            Describe(checkIn!.Metadata.GetOrderedMetadata<IAuthorizeData>()),
            Describe(reopen.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    // -- 2. the page posts to a route that actually exists -----------------------

    [Fact]
    public void Reopen_row_action_posts_to_a_registered_proxy_route()
    {
        Arrange(grantManage: true);
        var reopened = new AdminSpeakerMeetingRequestDetail(
            RejectedRow.Id, RejectedRow.SpeakerId, RejectedRow.SpeakerName,
            RejectedRow.SpeakerNameArabic, RejectedRow.RequestedByUserId,
            RejectedRow.RequesterName, null, RejectedRow.Subject,
            MeetingRequestStatus.Pending, null, RejectedRow.CreatedAt, null);
        var handler = JSInterop.Setup<ApiResult<AdminSpeakerMeetingRequestDetail>>(
                PostJson, _ => true)
            .SetResult(ApiResult<AdminSpeakerMeetingRequestDetail>.Ok(reopened));

        var cut = RenderComponent<SpeakerMeetingRequestsList>();
        cut.WaitForAssertion(() => Assert.Contains(ReopenKey, cut.Markup));

        // Row action → must-decide dialog → confirm.
        cut.FindAll("button").First(b => b.GetAttribute("title") == ReopenKey).Click();
        cut.WaitForAssertion(() => Assert.Contains(
            "Admin.SpeakerMeetingRequests.Reopen.ConfirmMsg", cut.Markup));
        cut.FindAll("button").First(b => b.TextContent.Trim() == ReopenKey).Click();

        var posted = (string)handler.Invocations.Single().Arguments[0]!;
        Assert.Equal(
            $"/account/api/admin/speaker-meeting-requests/{RejectedRow.Id}/reopen", posted);

        // The whole point: that URL has to hit a mapped BFF route. Without the
        // MapPost the CP answers 404 and the admin only ever sees the load-failed toast.
        Assert.True(MatchesAPostRoute(BffRoutes.Build(), posted),
            $"No CP proxy route is mapped for POST {posted}.");
    }

    // -- 3. the action stays permission-gated ------------------------------------

    [Fact]
    public void Reopen_row_action_is_hidden_without_the_manage_permission()
    {
        Arrange(grantManage: false);

        var cut = RenderComponent<SpeakerMeetingRequestsList>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(RejectedRow.RequesterName, cut.Markup);
            Assert.DoesNotContain(ReopenKey, cut.Markup);
        });
    }

    // -- Helpers -----------------------------------------------------------------

    private void Arrange(bool grantManage)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new CpPreferences(JSInterop.JSRuntime));
        if (grantManage)
        {
            Authorization.SetPolicies(
                PermissionCatalog.PolicyFor(PermissionCatalog.SpeakerMeetingRequests.Manage));
        }
        var page = GridPage<AdminSpeakerMeetingRequestRow>.Of(
            new[] { RejectedRow }, 1, new GridQuery());
        JSInterop.Setup<ApiResult<GridPage<AdminSpeakerMeetingRequestRow>>>(
                inv => inv.Identifier == PostJson)
            .SetResult(ApiResult<GridPage<AdminSpeakerMeetingRequestRow>>.Ok(page));
    }

    private static RouteEndpoint? FindRoute(
        IReadOnlyList<RouteEndpoint> routes, string rawTemplate) =>
        routes.FirstOrDefault(r => string.Equals(
            r.RoutePattern.RawText, rawTemplate, StringComparison.OrdinalIgnoreCase));

    private static string Describe(IReadOnlyList<IAuthorizeData> data) =>
        string.Join("|", data.Select(d => $"{d.Policy}/{d.Roles}/{d.AuthenticationSchemes}"));

    // Structural match of a concrete path against the mapped POST templates
    // (route constraints are not re-evaluated — the shape is what proves the map).
    private static bool MatchesAPostRoute(IReadOnlyList<RouteEndpoint> routes, string path) =>
        routes
            .Where(r => r.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                .Contains("POST") == true)
            .Any(r => new TemplateMatcher(
                    TemplateParser.Parse(r.RoutePattern.RawText!), new RouteValueDictionary())
                .TryMatch(path, new RouteValueDictionary()));

    /// <summary>Materialises the CP BFF proxy table (<c>AccountEndpoints</c> is
    /// internal, so it is invoked reflectively) without booting the CP host.</summary>
    private static class BffRoutes
    {
        public static IReadOnlyList<RouteEndpoint> Build()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddRouting();
            services.AddAuthorization();
            var builder = new CollectingRouteBuilder(
                new AllTypesAreServices(services.BuildServiceProvider()));

            var type = typeof(SpeakerMeetingRequestsList).Assembly
                .GetType("SIMF.ControlPanel.Endpoints.AccountEndpoints", throwOnError: true)!;
            var map = type.GetMethod(
                "MapAccountEndpoints", BindingFlags.Public | BindingFlags.Static)!;
            map.Invoke(null, new object[] { builder });

            return builder.DataSources
                .SelectMany(d => d.Endpoints)
                .OfType<RouteEndpoint>()
                .ToList();
        }
    }

    private sealed class CollectingRouteBuilder(IServiceProvider services) : IEndpointRouteBuilder
    {
        public ICollection<EndpointDataSource> DataSources { get; } =
            new List<EndpointDataSource>();

        public IServiceProvider ServiceProvider { get; } = services;

        public IApplicationBuilder CreateApplicationBuilder() =>
            new ApplicationBuilder(ServiceProvider);
    }

    /// <summary>The handlers take their upstream clients from DI. Nothing is ever
    /// invoked here — only the route table is read — so every complex parameter is
    /// declared a service and the delegate factory never tries to infer a body.</summary>
    private sealed class AllTypesAreServices(IServiceProvider inner)
        : IServiceProvider, IServiceProviderIsService
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IServiceProviderIsService) ? this : inner.GetService(serviceType);

        public bool IsService(Type serviceType) => true;
    }
}
