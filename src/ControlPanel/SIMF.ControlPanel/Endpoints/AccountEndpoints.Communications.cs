// Part of AccountEndpoints - see AccountEndpoints.cs for the shared helpers.
// invitations, VIPs, notification broadcasts, content blocks, banners
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
    private static void MapCommunications(IEndpointRouteBuilder group)
    {
        // Public-relations BFF passthroughs.
        group.MapPost("/admin/invitations/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListInvitationsAsync(body, token));
        });
        group.MapGet("/admin/invitations/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetInvitationAsync(id, token));
        });
        group.MapPost("/admin/invitations",
            async (AdminCreateInvitationRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateInvitationAsync(body, token));
        });
        group.MapPut("/admin/invitations/{id:guid}",
            async (Guid id, AdminUpdateInvitationRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateInvitationAsync(id, body, token));
        });
        group.MapDelete("/admin/invitations/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateInvitationAsync(id, token));
        });
        group.MapPost("/admin/vips/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListVipsAsync(body, token));
        });
        group.MapPost("/admin/vips/notify",
            async (AdminNotifyVipsRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.NotifyVipsAsync(body, token));
        });

        // Notification broadcasts (Control Panel "Announcements" desk).
        group.MapPost("/admin/notifications/broadcast",
            async (AdminCreateBroadcastRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateBroadcastAsync(body, token));
        });
        group.MapPost("/admin/notifications/broadcast/estimate",
            async (AdminBroadcastEstimateRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.EstimateBroadcastAsync(body, token));
        });
        group.MapPost("/admin/notifications/broadcasts/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListBroadcastsAsync(body, token));
        });
        group.MapGet("/admin/notifications/broadcasts/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetBroadcastAsync(id, token));
        });

        // Dynamic content CMS BFF passthroughs.
        group.MapPost("/admin/content-blocks/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListContentBlocksAsync(body, token));
        });
        group.MapGet("/admin/content-blocks/{key}",
            async (string key, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetContentBlockAsync(key, token));
        });
        group.MapPut("/admin/content-blocks",
            async (UpsertContentBlockRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpsertContentBlockAsync(body, token));
        });
        group.MapDelete("/admin/content-blocks/{key}",
            async (string key, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteContentBlockAsync(key, token));
        });
        group.MapPost("/admin/banners/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListBannersAsync(body, token));
        });
        group.MapGet("/admin/banners/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetBannerAsync(id, token));
        });
        group.MapPost("/admin/banners",
            async (CreateBannerRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateBannerAsync(body, token));
        });
        group.MapPut("/admin/banners/{id:guid}",
            async (Guid id, UpdateBannerRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateBannerAsync(id, body, token));
        });
        group.MapDelete("/admin/banners/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteBannerAsync(id, token));
        });
    }
}
