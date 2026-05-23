// Tests: SIMF.ControlPanel.Tests/AccountEndpointsTests.cs (todo).
using Microsoft.AspNetCore.Authentication;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Endpoints;

/// <summary>
/// Control Panel proxy endpoints for the account-management calls
/// (myComment item #11). Each endpoint reads the access token from the
/// cookie's stored auth tokens and forwards the request to the SIMF API,
/// returning the upstream HTTP status verbatim so the page can react to
/// 401 / 423 / 429 distinctly (5-agent review SEV-1.3).
///
/// The Blazor profile page calls these endpoints via <c>fetch</c> so the
/// browser sends the auth cookie automatically (the page itself never sees
/// the access token).
/// </summary>
internal static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/account/api").RequireAuthorization();

        group.MapGet("/profile", async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetProfileAsync(token));
        });

        group.MapPost("/totp/setup", async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.TotpSetupAsync(token));
        });

        group.MapPost("/totp/confirm",
            async (TotpConfirmRequest body, HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.TotpConfirmAsync(body, token));
        });

        group.MapPost("/totp/disable",
            async (TotpDisableRequest body, HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.TotpDisableAsync(body, token));
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
    }

    /// <summary>
    /// Forwards the upstream <see cref="ApiCallResult{T}"/> verbatim — the
    /// browser sees the same status the API returned (200 / 400 / 401 / 423
    /// / 429 / 503), and the same envelope body either way.
    /// </summary>
    private static IResult Forward<T>(ApiCallResult<T> result) =>
        Results.Json(result.Body, statusCode: result.StatusCode);
}
