// Part of AccountEndpoints - see AccountEndpoints.cs for the shared helpers.
// in-app notifications, change password, own avatar
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
    private static void MapSelfService(IEndpointRouteBuilder group)
    {
        // P12 — D-053: in-app notifications proxy. The CP bell + page
        // call these via simfAccount.{get,post,delete}Json (cookie auth);
        // they forward to the API with the access token.
        group.MapPost("/notifications/list",
            async (GridQuery body, HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListNotificationsAsync(body, token));
        });

        group.MapGet("/notifications/unread-count",
            async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetUnreadNotificationCountAsync(token));
        });

        group.MapPost("/notifications/{id:guid}/read",
            async (Guid id, HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.MarkNotificationReadAsync(id, token));
        });

        group.MapPost("/notifications/read-all",
            async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.MarkAllNotificationsReadAsync(token));
        });

        group.MapDelete("/notifications/{id:guid}",
            async (Guid id, HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteNotificationAsync(id, token));
        });

        group.MapPost("/change-password",
            async (ChangePasswordRequest body, HttpContext http, SimfAuthClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var envelope = await api.ChangePasswordAsync(body, token);
            // SimfAuthClient predates the status-forward refactor — until it
            // is migrated, infer the status from the envelope. A success is
            // 200; a failed envelope keeps the existing 400 mapping.
            return envelope.Success
                ? Results.Ok(envelope)
                : Results.Json(envelope, statusCode: 400);
        });

        // The cookie is SameSite=Lax, so a cross-site multipart POST never
        // carries it — that defeats CSRF without an antiforgery token.
        // Documented next to /auth/sign-out (D-029); repeated here for the
        // next reader. If the cookie is ever made SameSite=None, this route
        // and `/auth/sign-out` both need an antiforgery token.
        group.MapPost("/avatar",
            async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();

            var form = await http.Request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(ApiResult<object>.Fail(new ApiError
                {
                    Code = ErrorCodes.AvatarFileMissing,
                    Message = "An avatar file is required.",
                    MessageArabic = "ملف الصورة مطلوب.",
                }));
            }

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            return Forward(await api.UploadAvatarAsync(
                stream.ToArray(), file.ContentType, file.FileName, token));
        }).DisableAntiforgery();

        group.MapDelete("/avatar", async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteAvatarAsync(token));
        });

        // Streams the avatar bytes back to the browser — same-origin so the
        // <img src> the page renders carries the auth cookie automatically.
        // The CP fetches from the API with the cookie's access token and
        // forwards the bytes verbatim (D-039).
        group.MapGet("/avatar/{userId:guid}",
            async (Guid userId, HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();

            var (status, contentType, bytes) = await api.FetchAvatarAsync(userId, token);
            if (status != 200 || contentType is null || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }

            // Mirror the API's cache policy so the browser doesn't refetch on
            // every page navigation. The URL itself carries a ?v=ticks
            // cache-buster, so a fresh upload always replaces the cached one.
            http.Response.Headers.CacheControl = "private, max-age=300";
            return Results.File(bytes, contentType);
        });
    }
}
