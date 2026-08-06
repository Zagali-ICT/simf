// Part of AccountEndpoints - see AccountEndpoints.cs for the shared helpers.
// speakers, sessions, registration gate, archive visibility
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
    private static void MapProgramme(IEndpointRouteBuilder group)
    {
        // Speaker admin BFF passthroughs.
        group.MapPost("/admin/speakers/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSpeakersAsync(body, token));
        });
        group.MapGet("/admin/speakers/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetSpeakerAsync(id, token));
        });
        group.MapPost("/admin/speakers",
            async (AdminCreateSpeakerRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateSpeakerAsync(body, token));
        });
        group.MapPut("/admin/speakers/{id:guid}",
            async (Guid id, AdminUpdateSpeakerRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateSpeakerAsync(id, body, token));
        });
        group.MapDelete("/admin/speakers/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateSpeakerAsync(id, token));
        });

        // Session admin BFF passthroughs.
        group.MapPost("/admin/sessions/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSessionsAsync(body, token));
        });
        group.MapGet("/admin/sessions/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetSessionAsync(id, token));
        });
        group.MapPost("/admin/sessions",
            async (AdminCreateSessionRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateSessionAsync(body, token));
        });
        group.MapPut("/admin/sessions/{id:guid}",
            async (Guid id, AdminUpdateSessionRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateSessionAsync(id, body, token));
        });
        // Subtitle fetch-from-video passthrough (Sessions editor).
        group.MapPost("/admin/sessions/subtitle/fetch-from-video",
            async (FetchSubtitleRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.FetchSessionSubtitleAsync(body, token));
        });
        group.MapDelete("/admin/sessions/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateSessionAsync(id, token));
        });
        // Session broadcast-lifecycle transition.
        group.MapPut("/admin/sessions/{id:guid}/status",
            async (Guid id, SetSessionStatusRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SetSessionStatusAsync(id, body, token));
        });
        // Session recording upload / delete passthrough. The
        // per-request body + multipart limits are raised from config (mirrors the
        // API's SessionRecordingStorage:MaxUploadBytes) — scoped to this route, so
        // every other CP endpoint keeps its smaller limit. ReadFormAsync stages a
        // large file to a temp file on disk (the established CP upload convention,
        // as for images/presentations) then StreamContent forwards it to the API
        // without holding a byte[] in memory; the API does the authoritative checks.
        group.MapPost("/admin/sessions/{id:guid}/recording",
            async (Guid id, HttpContext http, SimfAdminClient api, IConfiguration config) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();

            var maxBytes = config.GetValue(
                "SessionRecordingStorage:MaxUploadBytes", DefaultRecordingMaxUploadBytes);
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
                    Code = ErrorCodes.SessionRecordingInvalid,
                    Message = "A recording file is required.",
                    MessageArabic = "ملف التسجيل مطلوب.",
                }));
            }
            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "video/mp4" : file.ContentType;
            await using var stream = file.OpenReadStream();
            return Forward(await api.UploadSessionRecordingAsync(
                id, stream, contentType, file.FileName, token));
        }).DisableAntiforgery();
        group.MapDelete("/admin/sessions/{id:guid}/recording",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteSessionRecordingAsync(id, token));
        });

        // Registration gate + archive visibility BFF passthroughs.
        group.MapGet("/admin/registration-gate",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetRegistrationGateAsync(token));
        });
        group.MapPut("/admin/registration-gate",
            async (UpdateRegistrationGateRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateRegistrationGateAsync(body, token));
        });
        group.MapGet("/admin/archive/visibility",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetArchiveVisibilityAsync(token));
        });
        group.MapPut("/admin/archive/visibility",
            async (UpdateArchiveVisibilityRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateArchiveVisibilityAsync(body, token));
        });
    }
}
