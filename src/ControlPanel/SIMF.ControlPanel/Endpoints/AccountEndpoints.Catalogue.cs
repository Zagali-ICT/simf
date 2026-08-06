// Part of AccountEndpoints - see AccountEndpoints.cs for the shared helpers.
// session categories, programme days, the forum window
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
    private static void MapCatalogue(IEndpointRouteBuilder group)
    {
        // B9b (D-226) — session-category dynamic lookup admin CRUD passthroughs.
        group.MapPost("/admin/session-categories/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSessionCategoriesAsync(body, token));
        });
        group.MapGet("/admin/session-categories/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetSessionCategoryAsync(id, token));
        });
        group.MapPost("/admin/session-categories",
            async (AdminCreateSessionCategoryRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateSessionCategoryAsync(body, token));
        });
        group.MapPut("/admin/session-categories/{id:guid}",
            async (Guid id, AdminUpdateSessionCategoryRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateSessionCategoryAsync(id, body, token));
        });
        group.MapDelete("/admin/session-categories/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateSessionCategoryAsync(id, token));
        });

        // Programme-days admin CRUD passthroughs (date + bilingual
        // title; the logo rides the generic asset endpoints).
        group.MapPost("/admin/programme-days/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListProgrammeDaysAsync(body, token));
        });
        group.MapGet("/admin/programme-days/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetProgrammeDayAsync(id, token));
        });
        group.MapPost("/admin/programme-days",
            async (AdminCreateProgrammeDayRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateProgrammeDayAsync(body, token));
        });
        group.MapPut("/admin/programme-days/{id:guid}",
            async (Guid id, AdminUpdateProgrammeDayRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateProgrammeDayAsync(id, body, token));
        });
        group.MapDelete("/admin/programme-days/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateProgrammeDayAsync(id, token));
        });

        // Forum-day window (MIN/MAX over active ProgrammeDay.Date). The CP
        // business-meetings + speaker-availability pages read it to bound their
        // datetime-local pickers to the event days. Gated at the backend by the
        // existing BusinessMeetings.View permission (no new permission code).
        group.MapGet("/admin/programme/forum-window",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetForumWindowAsync(token));
        });
    }
}
