// Part of AccountEndpoints - see AccountEndpoints.cs for the shared helpers.
// FAQ, roles and permissions, operation log, attendees
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
    private static void MapFaqAndRoles(IEndpointRouteBuilder group)
    {
        // P2.1 (D-211) — FAQ management proxy (two-level group → entry).
        group.MapPost("/admin/faq/groups/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListFaqGroupsAsync(body, token));
        });
        group.MapGet("/admin/faq/groups/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetFaqGroupAsync(id, token));
        });
        group.MapPost("/admin/faq/groups",
            async (CreateFaqGroupRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateFaqGroupAsync(body, token));
        });
        group.MapPut("/admin/faq/groups/{id:guid}",
            async (Guid id, UpdateFaqGroupRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateFaqGroupAsync(id, body, token));
        });
        group.MapDelete("/admin/faq/groups/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteFaqGroupAsync(id, token));
        });
        group.MapPost("/admin/faq/groups/{groupId:guid}/entries/list",
            async (Guid groupId, GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListFaqEntriesAsync(groupId, body, token));
        });
        group.MapPost("/admin/faq/entries",
            async (CreateFaqEntryRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateFaqEntryAsync(body, token));
        });
        group.MapPut("/admin/faq/entries/{id:guid}",
            async (Guid id, UpdateFaqEntryRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateFaqEntryAsync(id, body, token));
        });
        group.MapDelete("/admin/faq/entries/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteFaqEntryAsync(id, token));
        });

        // D-134 Sprint A — Roles CRUD proxy (existing schema, no migration).
        group.MapPost("/admin/roles/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListRolesAsync(body, token));
        });

        group.MapGet("/admin/roles/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetRoleAsync(id, token));
        });

        group.MapPost("/admin/roles",
            async (AdminCreateRoleRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateRoleAsync(body, token));
        });

        group.MapPut("/admin/roles/{id:guid}",
            async (Guid id, AdminUpdateRoleRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateRoleAsync(id, body, token));
        });

        group.MapDelete("/admin/roles/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteRoleAsync(id, token));
        });

        // Issue-1 — role -> permission grants (read + replace).
        group.MapGet("/admin/roles/{id:guid}/permissions",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetRolePermissionsAsync(id, token));
        });

        group.MapPut("/admin/roles/{id:guid}/permissions",
            async (Guid id, AdminSetRolePermissionsRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SetRolePermissionsAsync(id, body, token));
        });

        // Issue-1 — an admin user's RBAC roles (read + replace).
        group.MapGet("/admin/admins/{id:guid}/roles",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetUserRolesAsync(id, token));
        });

        group.MapPut("/admin/admins/{id:guid}/roles",
            async (Guid id, AdminSetUserRolesRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SetUserRolesAsync(id, body, token));
        });

        // D-134 Sprint A — Operation log viewer proxy (read-only over
        // the existing OperationLogEntry table; no schema change).
        group.MapPost("/admin/operation-log/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListOperationLogAsync(body, token));
        });

        group.MapGet("/admin/operation-log/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetOperationLogAsync(id, token));
        });

        // Services monitor proxy - live background-worker health from the API's
        // in-process heartbeat registry (read-only, no query).
        group.MapGet("/admin/ops/workers",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetWorkerStatusesAsync(token));
        });

        // P1.6 — binary XLSX download. Cannot reuse Forward() because the
        // response body is the workbook bytes, not the JSON envelope.
        group.MapPost("/admin/operation-log/export",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, bytes) = await api.ExportOperationLogAsync(body, token);
            if (status != 200 || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"simf-operation-log-{SimfClock.Now:yyyyMMddHHmmss}.xlsx");
        });

        // D-134 Sprint A — Attendees roster proxy (read-only join over
        // SimfUser + UserProfile + ProfileType; no schema change).
        group.MapPost("/admin/attendees/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListAttendeesAsync(body, token));
        });

        // P1.6 — binary XLSX download of the filtered roster.
        group.MapPost("/admin/attendees/export",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, bytes) = await api.ExportAttendeesAsync(body, token);
            if (status != 200 || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"simf-attendees-{SimfClock.Now:yyyyMMddHHmmss}.xlsx");
        });
    }
}
