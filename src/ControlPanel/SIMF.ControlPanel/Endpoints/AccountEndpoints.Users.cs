// Part of AccountEndpoints - see AccountEndpoints.cs for the shared helpers.
// the three account families: create, list, approve, reject, bulk
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
    private static void MapUsers(IEndpointRouteBuilder group)
    {
        // P7c — three-family proxy surface (Admin / Other / Visitor).
        group.MapPost("/admin/admins",
            async (AdminCreateAdminRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateAdminAsync(body, token));
        });

        group.MapPost("/admin/admins/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListAdminsAsync(body, token));
        });

        group.MapPost("/admin/others",
            async (AdminCreateOtherRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateOtherAsync(body, token));
        });

        group.MapPost("/admin/others/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListOthersAsync(body, token));
        });

        group.MapPost("/admin/visitors",
            async (AdminCreateVisitorRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateVisitorAsync(body, token));
        });

        group.MapPost("/admin/visitors/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListVisitorsAsync(body, token));
        });

        // P7c — pending-list + approval/reject proxies, one per family.
        group.MapPost("/admin/admins/pending/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListPendingAdminsAsync(body, token));
        });

        group.MapPost("/admin/others/pending/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListPendingOthersAsync(body, token));
        });

        group.MapPost("/admin/visitors/pending/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListPendingVisitorsAsync(body, token));
        });

        group.MapPost("/admin/admins/{id:guid}/approve",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ApproveAdminAsync(id, token));
        });

        group.MapPost("/admin/admins/{id:guid}/reject",
            async (Guid id, AdminRejectRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RejectAdminAsync(id, body, token));
        });

        group.MapPost("/admin/others/{id:guid}/approve",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ApproveOtherAsync(id, token));
        });

        group.MapPost("/admin/others/{id:guid}/reject",
            async (Guid id, AdminRejectRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RejectOtherAsync(id, body, token));
        });

        // CS-D (D-386) — the approve body may optionally carry a ProfileTypeId
        // to set the visitor's tier on approval. An empty body ({}) keeps the
        // tier unchanged (backward-compatible with the bulk / no-tier paths).
        group.MapPost("/admin/visitors/{id:guid}/approve",
            async (Guid id, ApproveVisitorBody? body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ApproveVisitorAsync(id, token, body?.ProfileTypeId));
        });

        group.MapPost("/admin/visitors/{id:guid}/reject",
            async (Guid id, AdminRejectRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RejectVisitorAsync(id, body, token));
        });
        // P1.3 (D-214) — visitor edit passthrough.
        group.MapPut("/admin/visitors/{id:guid}",
            async (Guid id, AdminUpdateVisitorRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateVisitorAsync(id, body, token));
        });
        // P1.3 (D-214) — Other edit passthrough.
        group.MapPut("/admin/others/{id:guid}",
            async (Guid id, AdminUpdateOtherRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateOtherAsync(id, body, token));
        });
        // D-728 (owner item 9) — change an account's type (Visitor <-> Other).
        group.MapPost("/admin/accounts/{id:guid}/change-type",
            async (Guid id, AdminChangeAccountTypeRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ChangeAccountTypeAsync(id, body, token));
        });

        // D-164 (gap doc G2) — bulk approve passthroughs.
        group.MapPost("/admin/visitors/bulk-approve",
            async (AdminBulkApprovalRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.BulkApproveVisitorsAsync(body, token));
        });
        group.MapPost("/admin/others/bulk-approve",
            async (AdminBulkApprovalRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.BulkApproveOthersAsync(body, token));
        });
        // P1.3 (D-214) — admin-queue bulk approve passthrough.
        group.MapPost("/admin/admins/bulk-approve",
            async (AdminBulkApprovalRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.BulkApproveAdminsAsync(body, token));
        });

        // D-209 — bulk reject passthroughs (the reject counterpart of D-164).
        group.MapPost("/admin/visitors/bulk-reject",
            async (AdminBulkRejectRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.BulkRejectVisitorsAsync(body, token));
        });
        group.MapPost("/admin/others/bulk-reject",
            async (AdminBulkRejectRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.BulkRejectOthersAsync(body, token));
        });
        // P1.3 (D-214) — admin-queue bulk reject passthrough.
        group.MapPost("/admin/admins/bulk-reject",
            async (AdminBulkRejectRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.BulkRejectAdminsAsync(body, token));
        });

        // D-124 — scoped pending-profile reads for the CP "preview before approve"
        // modal. 404-for-mismatch is preserved by Forward() since the API returns
        // an ApiResult error envelope with status 404.
        group.MapGet("/admin/visitors/{id:guid}/profile-for-approval",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetPendingVisitorProfileAsync(id, token));
        });

        group.MapGet("/admin/others/{id:guid}/profile-for-approval",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetPendingOtherProfileAsync(id, token));
        });

        // D-127 — on-site walk-in registration desk passthroughs.
        group.MapPost("/admin/visitors/register-onsite",
            async (AdminWalkInRegistrationRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RegisterVisitorOnSiteAsync(body, token));
        });

        // D-473 (#10) — bulk-generate placeholder badges (visitors / delegates).
        group.MapPost("/admin/visitors/bulk-generate",
            async (AdminBulkGenerateBadgesRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.BulkGenerateBadgesAsync(body, token));
        });

        // D-758 (#10 Phase 2) — persisted bulk-badge batches: list / re-email / revoke.
        group.MapPost("/admin/visitors/badge-batches/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListBadgeBatchesAsync(body, token));
        });
        group.MapPost("/admin/visitors/badge-batches/re-email",
            async (AdminReEmailBadgeBatchRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ReEmailBadgeBatchAsync(body, token));
        });
        group.MapPost("/admin/visitors/badge-batches/revoke",
            async (AdminRevokeBadgeBatchRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RevokeBadgeBatchAsync(body, token));
        });

        group.MapPost("/admin/others/register-onsite",
            async (AdminWalkInRegistrationRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RegisterOtherOnSiteAsync(body, token));
        });

        // D-126 — broadened admin profile-read passthroughs (Q-G reversed).
        group.MapGet("/admin/visitors/{id:guid}/profile",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetVisitorProfileAsync(id, token));
        });

        group.MapGet("/admin/others/{id:guid}/profile",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetOtherProfileAsync(id, token));
        });
    }
}
