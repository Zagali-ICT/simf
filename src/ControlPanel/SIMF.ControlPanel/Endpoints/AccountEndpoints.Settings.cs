// Part of AccountEndpoints - see AccountEndpoints.cs for the shared helpers.
// system settings, organisation profile, venue map, bookings
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
    private static void MapSettings(IEndpointRouteBuilder group)
    {
        // P2.4 (D-229) — System Configuration settings passthroughs.
        group.MapPost("/admin/system-settings/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSystemSettingsAsync(body, token));
        });
        group.MapGet("/admin/system-settings/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetSystemSettingAsync(id, token));
        });
        group.MapPost("/admin/system-settings",
            async (AdminCreateSystemSettingRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateSystemSettingAsync(body, token));
        });
        group.MapPut("/admin/system-settings/{id:guid}",
            async (Guid id, AdminUpdateSystemSettingRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateSystemSettingAsync(id, body, token));
        });
        group.MapDelete("/admin/system-settings/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteSystemSettingAsync(id, token));
        });

        // D-495 — Organization / About profile passthroughs.
        group.MapGet("/admin/organization-profile",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetOrganizationProfileAsync(token));
        });
        group.MapPut("/admin/organization-profile",
            async (AdminUpdateOrganizationProfileRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SaveOrganizationProfileAsync(body, token));
        });
        // D-768 — hero background video upload / delete passthrough. Mirrors the
        // recording route: the per-request body + multipart limits are raised from
        // config (scoped to this route), and the file is STREAMED to the API without
        // buffering a byte[] in memory; the API does the authoritative validation.
        group.MapPost("/admin/organization-profile/hero-video",
            async (HttpContext http, SimfAdminClient api, IConfiguration config) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();

            var maxBytes = config.GetValue(
                "OrganizationHeroVideo:MaxUploadBytes", DefaultHeroVideoMaxUploadBytes);
            var sizeFeature = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (sizeFeature is { IsReadOnly: false })
            {
                sizeFeature.MaxRequestBodySize = maxBytes;
            }
            http.Features.Set<IFormFeature>(
                new FormFeature(http.Request,
                    new FormOptions { MultipartBodyLengthLimit = maxBytes }));

            var form = await http.Request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(ApiResult<object>.Fail(new ApiError
                {
                    Code = ErrorCodes.OrganizationProfileInvalid,
                    Message = "A video file is required.",
                    MessageArabic = "ملف الفيديو مطلوب.",
                }));
            }
            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "video/mp4" : file.ContentType;
            await using var stream = file.OpenReadStream();
            return Forward(await api.UploadOrganizationHeroVideoAsync(
                stream, contentType, file.FileName, token));
        }).DisableAntiforgery();
        group.MapDelete("/admin/organization-profile/hero-video",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteOrganizationHeroVideoAsync(token));
        });

        // P2.5 (D-230) — 2D venue-map node CRUD passthroughs.
        group.MapPost("/admin/venue-map/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListVenueMapNodesAsync(body, token));
        });
        group.MapGet("/admin/venue-map/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetVenueMapNodeAsync(id, token));
        });
        group.MapPost("/admin/venue-map",
            async (AdminCreateVenueMapNodeRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateVenueMapNodeAsync(body, token));
        });
        group.MapPut("/admin/venue-map/{id:guid}",
            async (Guid id, AdminUpdateVenueMapNodeRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateVenueMapNodeAsync(id, body, token));
        });
        group.MapDelete("/admin/venue-map/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteVenueMapNodeAsync(id, token));
        });

        // #6/#17 — booking monitor passthrough (read-only; bookings auto-confirm
        // and no-shows are released by a background worker, so there is no
        // approve/reject/bulk-approve action).
        group.MapPost("/admin/bookings/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListActiveBookingsAsync(body, token));
        });

        // Multipart gov-Excel import (same SameSite=Lax CSRF stance as media upload).
        group.MapPost("/admin/organisations/import",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();

            var form = await http.Request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(ApiResult<object>.Fail(new ApiError
                {
                    Code = ErrorCodes.ValidationFailed,
                    Message = "An Excel file is required.",
                    MessageArabic = "ملف Excel مطلوب.",
                }));
            }
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            return Forward(await api.ImportOrganisationsAsync(
                stream.ToArray(), file.ContentType, file.FileName, token));
        }).DisableAntiforgery();
    }
}
