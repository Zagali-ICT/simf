// Part of AccountEndpoints - see AccountEndpoints.cs for the shared helpers.
// countries, regions, contact inquiries, site settings
using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Localization;
using SIMF.ApiClient;
using SIMF.ControlPanel.Components.Assistant;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Archive;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.BusinessMeetings;
using SIMF.Contracts.Exhibitors;
using SIMF.Contracts.Exhibition;
using SIMF.Contracts.Email;
using SIMF.Contracts.Faq;
using SIMF.Contracts.Feedback;
using SIMF.Contracts.Organisations;
using SIMF.Contracts.Media;
using SIMF.Contracts.Programme;
using SIMF.Contracts.Requests;
using SIMF.Contracts.PublicRelations;
using SIMF.Contracts.Regions;
using SIMF.Contracts.Reporting;
using SIMF.Contracts.Sessions;
using SIMF.Common.Enums;

namespace SIMF.ControlPanel.Endpoints;

internal static partial class AccountEndpoints
{
    private static void MapGeography(IEndpointRouteBuilder group)
    {
        // D-151 — Country admin lookup BFF passthroughs.
        group.MapPost("/admin/countries/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListCountriesAsync(body, token));
        });
        group.MapGet("/admin/countries/{id:int}",
            async (int id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetCountryAsync(id, token));
        });
        group.MapPost("/admin/countries",
            async (AdminCreateCountryRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateCountryAsync(body, token));
        });
        group.MapPut("/admin/countries/{id:int}",
            async (int id, AdminUpdateCountryRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateCountryAsync(id, body, token));
        });
        group.MapDelete("/admin/countries/{id:int}",
            async (int id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateCountryAsync(id, token));
        });

        // D-547 — Region admin lookup BFF passthroughs (mirrors countries; Guid key).
        group.MapPost("/admin/regions/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListRegionsAsync(body, token));
        });
        group.MapGet("/admin/regions/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetRegionAsync(id, token));
        });
        group.MapPost("/admin/regions",
            async (CreateRegionRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateRegionAsync(body, token));
        });
        group.MapPut("/admin/regions/{id:guid}",
            async (Guid id, UpdateRegionRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateRegionAsync(id, body, token));
        });
        group.MapDelete("/admin/regions/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateRegionAsync(id, token));
        });

        // D-649 — Contact-inquiries inbox + Site-settings + Country-delegates
        //         BFF passthroughs (pages + API shipped, wiring was never added).
        group.MapPost("/admin/contact-inquiries/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListContactInquiriesAsync(body, token));
        });
        group.MapPost("/admin/contact-inquiries/{id:guid}/handled",
            async (Guid id, SetContactInquiryHandledBody body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.MarkContactInquiryHandledAsync(id, body.Handled, token));
        });
        group.MapGet("/admin/site-settings",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetSiteSettingsAsync(token));
        });
        group.MapPut("/admin/site-settings",
            async (AdminUpdateSiteSettingsRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateSiteSettingsAsync(body, token));
        });
        group.MapGet("/admin/countries/{id:int}/delegates",
            async (int id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListCountryDelegatesAsync(id, token));
        });
    }
}
