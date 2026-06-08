// User-side account proxy — forwards to the SIMF API with the cookie's
// access token, so the browser never sees the token (mirrors the CP's
// proxy pattern from D-037). P8 renamed the proxy paths off
// /account/api/visitor-profile/* onto /account/api/profile/*.
using Microsoft.AspNetCore.Authentication;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.UserProfile;

namespace SIMF.Web.Endpoints;

/// <summary>
/// The Website's authenticated account-area endpoints — proxies the
/// user-profile API calls (decisions D-046 c, P8 — D-049) so the
/// browser exchanges only a same-origin cookie with the Website.
/// </summary>
internal static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/account/api").RequireAuthorization();

        group.MapGet("/user-profile",
            async (HttpContext http, SimfAccountClient api, CancellationToken ct) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetUserProfileAsync(token, ct));
        });

        group.MapPost("/user-profile",
            async (UpsertUserProfileRequest body, HttpContext http,
                   SimfAccountClient api, CancellationToken ct) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpsertUserProfileAsync(body, token, ct));
        });

        group.MapGet("/user-profile/countries",
            async (HttpContext http, SimfAccountClient api, CancellationToken ct) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetProfileCountriesAsync(token, ct));
        });

        // P9 — active interests for the user-profile picker (الاهتمامات).
        group.MapGet("/interests",
            async (HttpContext http, SimfAccountClient api, CancellationToken ct) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetActiveInterestsAsync(token, ct));
        });

        // P12 — notifications proxy.
        group.MapPost("/notifications/list",
            async (GridQuery body, HttpContext http, SimfAccountClient api, CancellationToken ct) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListNotificationsAsync(body, token, ct));
        });

        group.MapGet("/notifications/unread-count",
            async (HttpContext http, SimfAccountClient api, CancellationToken ct) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetUnreadNotificationCountAsync(token, ct));
        });

        group.MapPost("/notifications/{id:guid}/read",
            async (Guid id, HttpContext http, SimfAccountClient api, CancellationToken ct) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.MarkNotificationReadAsync(id, token, ct));
        });

        group.MapPost("/notifications/read-all",
            async (HttpContext http, SimfAccountClient api, CancellationToken ct) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.MarkAllNotificationsReadAsync(token, ct));
        });

        group.MapDelete("/notifications/{id:guid}",
            async (Guid id, HttpContext http, SimfAccountClient api, CancellationToken ct) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteNotificationAsync(id, token, ct));
        });

        // SameSite=Lax cookie + DisableAntiforgery is acceptable for multi-
        // part — a cross-site multipart POST never carries the cookie. The
        // same trade-off is documented on the CP side (D-029).
        group.MapPost("/user-profile/id-image",
            async (HttpContext http, SimfAccountClient api, CancellationToken ct) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();

            var form = await http.Request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(ApiResult<object>.Fail(new ApiError
                {
                    Code = ErrorCodes.VisitorIdImageMissing,
                    Message = "An ID image is required.",
                    MessageArabic = "صورة الهوية مطلوبة.",
                }));
            }

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, ct);
            return Forward(await api.UploadUserIdDocumentAsync(
                stream.ToArray(), file.ContentType, file.FileName, token, ct));
        }).DisableAntiforgery();

        // Streams the user's decrypted ID-document image back same-origin
        // so the <img src> the page renders rides the auth cookie
        // automatically.
        group.MapGet("/user-profile/id-image",
            async (HttpContext http, SimfAccountClient api, CancellationToken ct) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();

            var (status, contentType, bytes) = await api.FetchUserIdDocumentAsync(token, ct);
            if (status != 200 || contentType is null || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            http.Response.Headers.CacheControl = "private, max-age=300";
            return Results.File(bytes, contentType);
        });
    }

    private static IResult Forward<T>(ApiCallResult<T> result) =>
        Results.Json(result.Body, statusCode: result.StatusCode);
}
