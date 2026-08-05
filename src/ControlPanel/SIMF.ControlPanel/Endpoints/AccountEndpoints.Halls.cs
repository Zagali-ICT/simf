// Part of AccountEndpoints - see AccountEndpoints.cs for the shared helpers.
// themes, halls, meeting tables, hall allocations, business meetings
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
    private static void MapHalls(IEndpointRouteBuilder group)
    {
        // D-134 Sprint B — Themes CRUD proxy (D-135 freeze-lift).
        group.MapPost("/admin/themes/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListThemesAsync(body, token));
        });

        group.MapGet("/admin/themes/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetThemeAsync(id, token));
        });

        group.MapPost("/admin/themes",
            async (AdminCreateThemeRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateThemeAsync(body, token));
        });

        group.MapPut("/admin/themes/{id:guid}",
            async (Guid id, AdminUpdateThemeRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateThemeAsync(id, body, token));
        });

        group.MapDelete("/admin/themes/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateThemeAsync(id, token));
        });

        // D-134 Sprint B — Halls CRUD proxy (D-135 freeze-lift).
        group.MapPost("/admin/halls/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListHallsAsync(body, token));
        });

        group.MapGet("/admin/halls/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetHallAsync(id, token));
        });

        group.MapPost("/admin/halls",
            async (AdminCreateHallRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateHallAsync(body, token));
        });

        group.MapPut("/admin/halls/{id:guid}",
            async (Guid id, AdminUpdateHallRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateHallAsync(id, body, token));
        });

        group.MapDelete("/admin/halls/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateHallAsync(id, token));
        });

        // QA B16 — the hall's occupancy view (sessions assigned to this hall).
        group.MapGet("/admin/halls/{id:guid}/schedule",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetHallScheduleAsync(id, token));
        });

        // SIMF-FDS-013 (D-248) — meeting tables + hall allocations + business
        // meetings BFF passthroughs (mirrors the Halls block above). Without
        // these the /admin/meeting-tables + /admin/business-meetings pages 400
        // on every data call (the backend endpoints exist; only these proxies
        // were missing).
        group.MapPut("/admin/halls/{hallId:guid}/purpose",
            async (Guid hallId, SetHallPurposeRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SetHallPurposeAsync(hallId, body, token));
        });

        group.MapPost("/admin/halls/{hallId:guid}/meeting-tables/list",
            async (Guid hallId, GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListMeetingTablesAsync(hallId, body, token));
        });

        group.MapPost("/admin/halls/{hallId:guid}/meeting-tables",
            async (Guid hallId, CreateMeetingTableRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateMeetingTableAsync(hallId, body, token));
        });

        group.MapPut("/admin/meeting-tables/{id:guid}",
            async (Guid id, UpdateMeetingTableRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateMeetingTableAsync(id, body, token));
        });

        group.MapDelete("/admin/meeting-tables/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteMeetingTableAsync(id, token));
        });

        group.MapPost("/admin/halls/{hallId:guid}/meeting-tables/generate",
            async (Guid hallId, GenerateMeetingTablesRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GenerateMeetingTablesAsync(hallId, body, token));
        });

        group.MapPost("/admin/halls/{hallId:guid}/hall-allocations/list",
            async (Guid hallId, GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListHallAllocationsAsync(hallId, body, token));
        });

        group.MapPost("/admin/halls/{hallId:guid}/hall-allocations",
            async (Guid hallId, CreateHallAllocationRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateHallAllocationAsync(hallId, body, token));
        });

        group.MapDelete("/admin/hall-allocations/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ReleaseHallAllocationAsync(id, token));
        });

        group.MapPost("/admin/business-meetings/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListBusinessMeetingsAsync(body, token));
        });

        group.MapGet("/admin/business-meetings/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetBusinessMeetingAsync(id, token));
        });

        group.MapPost("/admin/business-meetings",
            async (ScheduleMeetingRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ScheduleBusinessMeetingAsync(body, token));
        });

        group.MapPost("/admin/business-meetings/{id:guid}/cancel",
            async (Guid id, CancelMeetingRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CancelBusinessMeetingAsync(id, body, token));
        });
    }
}
