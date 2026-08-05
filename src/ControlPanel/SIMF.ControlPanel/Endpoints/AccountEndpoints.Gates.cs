// Part of AccountEndpoints - see AccountEndpoints.cs for the shared helpers.
// gate administration, the operator surface, log viewer
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
    private static void MapGates(IEndpointRouteBuilder group)
    {
        // D-148 — Gate Module BFF passthroughs (admin + operator).
        group.MapPost("/admin/gates/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListGatesAsync(body, token));
        });
        group.MapGet("/admin/gates/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetGateAsync(id, token));
        });
        group.MapPost("/admin/gates",
            async (AdminCreateGateRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateGateAsync(body, token));
        });
        group.MapPut("/admin/gates/{id:guid}",
            async (Guid id, AdminUpdateGateRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateGateAsync(id, body, token));
        });
        group.MapDelete("/admin/gates/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateGateAsync(id, token));
        });
        group.MapGet("/admin/gates/{id:guid}/assignments",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListGateAssignmentsAsync(id, token));
        });
        // BUG-018 — the gate form's own lookups (operator candidates + the
        // profile-type / hall options), both gated on Gates.Manage upstream.
        group.MapPost("/admin/gates/operator-candidates/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListGateOperatorCandidatesAsync(body, token));
        });
        group.MapGet("/admin/gates/form-options",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetGateFormOptionsAsync(token));
        });
        group.MapPost("/admin/gates/reports/scans",
            async (AdminGateScanReportFilter body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListGateScansAsync(body, token));
        });
        group.MapGet("/admin/gates/reports/currently-inside",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListCurrentlyInsideAsync(token));
        });
        group.MapGet("/gates/my-assignments",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListMyGateAssignmentsAsync(token));
        });
        group.MapPost("/gates/{gateId:guid}/scans",
            async (Guid gateId, SIMF.Contracts.Gates.GateScanRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.PostScanAsync(gateId, body, token));
        });
        group.MapGet("/gates/my-reports/today",
            async (Guid? gateId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetMyDailyReportAsync(gateId, token));
        });

        // P6 — log viewer proxy. The CP page is server-side Blazor, so it
        // talks to these endpoints via fetch (cookie auth) and they forward
        // to the API with the access token.
        group.MapGet("/admin/logs/list",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListLogsAsync(token));
        });

        group.MapGet("/admin/logs/tail",
            async (string project, string file, int? lines,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.TailLogAsync(project, file, lines ?? 500, token));
        });

        group.MapGet("/admin/logs/download",
            async (string project, string file,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, bytes, safeFileName) =
                await api.DownloadLogAsync(project, file, token);
            if (status != 200 || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            return Results.File(bytes, "text/plain", safeFileName);
        });
    }
}
