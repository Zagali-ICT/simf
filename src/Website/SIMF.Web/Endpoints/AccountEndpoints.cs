// Visitor-side account proxy — forwards to the SIMF API with the cookie's
// access token, so the browser never sees the token (mirrors the CP's
// proxy pattern from D-037).
using Microsoft.AspNetCore.Authentication;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.VisitorProfile;

namespace SIMF.Web.Endpoints;

/// <summary>
/// The Website's authenticated account-area endpoints — proxies the
/// visitor-profile API calls (decision D-046 c, myComment #18) so the
/// browser exchanges only a same-origin cookie with the Website.
/// </summary>
internal static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/account/api").RequireAuthorization();

        group.MapGet("/visitor-profile",
            async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetVisitorProfileAsync(token));
        });

        group.MapPost("/visitor-profile",
            async (UpsertVisitorProfileRequest body, HttpContext http,
                   SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpsertVisitorProfileAsync(body, token));
        });

        group.MapGet("/visitor-profile/countries",
            async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetVisitorCountriesAsync(token));
        });

        // SameSite=Lax cookie + DisableAntiforgery is acceptable for multi-
        // part — a cross-site multipart POST never carries the cookie. The
        // same trade-off is documented on the CP side (D-029).
        group.MapPost("/visitor-profile/id-image",
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
                    Code = ErrorCodes.VisitorIdImageMissing,
                    Message = "An ID image is required.",
                    MessageArabic = "صورة الهوية مطلوبة.",
                }));
            }

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            return Forward(await api.UploadVisitorIdImageAsync(
                stream.ToArray(), file.ContentType, file.FileName, token));
        }).DisableAntiforgery();

        // Streams the visitor's decrypted ID image back same-origin so the
        // <img src> the page renders rides the auth cookie automatically.
        group.MapGet("/visitor-profile/id-image",
            async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();

            var (status, contentType, bytes) = await api.FetchVisitorIdImageAsync(token);
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
