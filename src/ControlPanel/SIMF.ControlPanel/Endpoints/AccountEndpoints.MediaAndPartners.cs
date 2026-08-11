// Part of AccountEndpoints - see AccountEndpoints.cs for the shared helpers.
// news, media, assets, presentations, partners, sponsors, booths
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
    private static void MapMediaAndPartners(IEndpointRouteBuilder group)
    {
        // News admin CRUD BFF passthroughs.
        group.MapPost("/admin/news/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListNewsAsync(body, token));
        });
        group.MapGet("/admin/news/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetNewsAsync(id, token));
        });
        group.MapPost("/admin/news",
            async (CreateNewsRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateNewsAsync(body, token));
        });
        group.MapPut("/admin/news/{id:guid}",
            async (Guid id, UpdateNewsRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateNewsAsync(id, body, token));
        });
        group.MapDelete("/admin/news/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteNewsAsync(id, token));
        });

        // Media gallery admin CRUD + image upload BFF passthroughs.
        group.MapPost("/admin/media/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListMediaAsync(body, token));
        });
        group.MapGet("/admin/media/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetMediaAsync(id, token));
        });
        group.MapPost("/admin/media",
            async (AdminCreateMediaRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateMediaAsync(body, token));
        });
        group.MapPut("/admin/media/{id:guid}",
            async (Guid id, AdminUpdateMediaRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateMediaAsync(id, body, token));
        });
        group.MapDelete("/admin/media/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteMediaAsync(id, token));
        });

        // Unified media-asset pipeline BFF passthroughs: per-entity upload /
        // link / preview-fetch + the central Media Library list / get / deactivate /
        // restore. Reused by every entity form's SimfImageUpload + the Media Library page.
        group.MapPost("/admin/assets/{category}/{ownerId:guid}/image",
            async (string category, Guid ownerId, HttpContext http, SimfAdminClient api) =>
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
                    Message = "A file is required.",
                    MessageArabic = "الملف مطلوب.",
                }));
            }
            var kind = http.Request.Query["kind"].ToString();
            if (string.IsNullOrWhiteSpace(kind)) { kind = form["kind"].ToString(); }
            if (string.IsNullOrWhiteSpace(kind)) { kind = "Image"; }
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            return Forward(await api.UploadAssetImageAsync(
                category, ownerId, kind, stream.ToArray(), file.ContentType, file.FileName, token));
        }).DisableAntiforgery();

        group.MapPut("/admin/assets/{category}/{ownerId:guid}/link",
            async (string category, Guid ownerId, SIMF.Contracts.Assets.SetAssetLinkRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SetAssetLinkAsync(category, ownerId, body, token));
        });

        group.MapGet("/admin/assets/{category}/{ownerId:guid}/image",
            async (string category, Guid ownerId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, contentType, bytes) = await api.FetchAssetImageAsync(category, ownerId, token);
            if (status != 200 || bytes.Length == 0) { return DownloadFailure(status, bytes); }
            return Results.File(bytes, contentType ?? "application/octet-stream");
        });

        group.MapPost("/admin/assets/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListAssetsAsync(body, token));
        });
        group.MapGet("/admin/assets/item/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetAssetAsync(id, token));
        });
        group.MapDelete("/admin/assets/item/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateAssetAsync(id, token));
        });
        group.MapPost("/admin/assets/item/{id:guid}/restore",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RestoreAssetAsync(id, token));
        });

        // Speaker presentation files (list / upload / download / delete).
        group.MapGet("/admin/speakers/{speakerId:guid}/presentations",
            async (Guid speakerId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSpeakerPresentationsAsync(speakerId, token));
        });
        group.MapPost("/admin/speakers/{speakerId:guid}/presentations",
            async (Guid speakerId, Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();

            var form = await http.Request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(ApiResult<object>.Fail(new ApiError
                {
                    Code = ErrorCodes.SpeakerPresentationInvalid,
                    Message = "A presentation file is required.",
                    MessageArabic = "ملف العرض مطلوب.",
                }));
            }
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            return Forward(await api.UploadSpeakerPresentationAsync(
                speakerId, sessionId, stream.ToArray(), file.ContentType, file.FileName, token));
        }).DisableAntiforgery();
        group.MapDelete("/admin/speaker-presentations/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteSpeakerPresentationAsync(id, token));
        });
        group.MapGet("/admin/speaker-presentations/{id:guid}/file",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, contentType, contentDisposition, bytes) =
                await api.FetchSpeakerPresentationAsync(id, token);
            if (status != 200 || bytes.Length == 0)
            {
                return DownloadFailure(status, bytes);
            }
            string? downloadName = null;
            if (!string.IsNullOrWhiteSpace(contentDisposition)
                && System.Net.Http.Headers.ContentDispositionHeaderValue.TryParse(
                    contentDisposition, out var parsed))
            {
                downloadName = parsed.FileNameStar ?? parsed.FileName?.Trim('"');
            }
            return Results.File(bytes, contentType ?? "application/octet-stream", downloadName);
        });

        // Media image upload (multipart; same SameSite=Lax CSRF stance
        // as /admin/visitors/{id}/id-document).
        group.MapPost("/admin/media/{id:guid}/image",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
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
                    Message = "An image file is required.",
                    MessageArabic = "ملف الصورة مطلوب.",
                }));
            }
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            return Forward(await api.UploadMediaImageAsync(
                id, stream.ToArray(), file.ContentType, file.FileName, token));
        }).DisableAntiforgery();

        // Media-partner admin CRUD BFF passthroughs.
        group.MapPost("/admin/media-partners/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListMediaPartnersAsync(body, token));
        });
        group.MapPost("/admin/media-partners",
            async (AdminCreateMediaPartnerRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateMediaPartnerAsync(body, token));
        });
        group.MapPut("/admin/media-partners/{id:guid}",
            async (Guid id, AdminUpdateMediaPartnerRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateMediaPartnerAsync(id, body, token));
        });
        group.MapDelete("/admin/media-partners/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateMediaPartnerAsync(id, token));
        });

        // Sponsor admin CRUD BFF passthroughs.
        group.MapPost("/admin/sponsors/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSponsorsAsync(body, token));
        });
        group.MapPost("/admin/sponsors",
            async (AdminCreateSponsorRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateSponsorAsync(body, token));
        });
        group.MapPut("/admin/sponsors/{id:guid}",
            async (Guid id, AdminUpdateSponsorRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateSponsorAsync(id, body, token));
        });
        group.MapDelete("/admin/sponsors/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateSponsorAsync(id, token));
        });

        // Booth admin CRUD BFF passthroughs.
        group.MapPost("/admin/booths/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListBoothsAsync(body, token));
        });
        group.MapGet("/admin/booths/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetBoothAsync(id, token));
        });
        group.MapPost("/admin/booths",
            async (AdminCreateBoothRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateBoothAsync(body, token));
        });
        group.MapPut("/admin/booths/{id:guid}",
            async (Guid id, AdminUpdateBoothRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateBoothAsync(id, body, token));
        });
        group.MapDelete("/admin/booths/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateBoothAsync(id, token));
        });

        // Organisation lookup admin CRUD + gov-Excel import passthroughs.
        group.MapPost("/admin/organisations/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListOrganisationsAsync(body, token));
        });
        group.MapGet("/admin/organisations/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetOrganisationAsync(id, token));
        });
        group.MapPost("/admin/organisations",
            async (CreateOrganisationRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateOrganisationAsync(body, token));
        });
        group.MapPut("/admin/organisations/{id:guid}",
            async (Guid id, UpdateOrganisationRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateOrganisationAsync(id, body, token));
        });
        group.MapDelete("/admin/organisations/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateOrganisationAsync(id, token));
        });

        // single-row GET passthroughs the Sponsor / MediaPartner edit modals
        // use to pre-load the row for editing.
        group.MapGet("/admin/sponsors/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetSponsorAsync(id, token));
        });
        group.MapGet("/admin/media-partners/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetMediaPartnerAsync(id, token));
        });
    }
}
