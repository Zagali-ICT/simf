// Tests: SIMF.ControlPanel.Tests/AccountEndpointsTests.cs (todo).
using Microsoft.AspNetCore.Authentication;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Logs;

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

        group.MapPost("/recovery-codes/regenerate",
            async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RegenerateRecoveryCodesAsync(token));
        });

        group.MapPost("/admin/reset-2fa",
            async (AdminResetTwoFactorRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ResetTwoFactorAsync(body, token));
        });

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

        group.MapPost("/admin/visitors/{id:guid}/approve",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ApproveVisitorAsync(id, token));
        });

        group.MapPost("/admin/visitors/{id:guid}/reject",
            async (Guid id, AdminRejectRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RejectVisitorAsync(id, body, token));
        });

        // P7c — ProfileTypes picker, filtered by UserType.
        group.MapGet("/admin/profile-types",
            async (string userType, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListProfileTypesAsync(userType, token));
        });

        group.MapPost("/admin/admins/bulk-delete",
            async (AdminBulkDeleteRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.BulkDeleteUsersAsync(body, token));
        });

        group.MapPost("/admin/admins/duplicate",
            async (AdminDuplicateUserRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DuplicateUserAsync(body, token));
        });

        // Binary download — the browser saves the XLSX. Cannot reuse Forward()
        // because the response body is the workbook bytes, not the JSON envelope.
        group.MapPost("/admin/admins/export",
            async (AdminExportUsersRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, bytes) = await api.ExportUsersAsync(body, token);
            if (status != 200 || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"simf-staff-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx");
        });

        // Multipart upload — same SameSite=Lax CSRF stance as /avatar (D-029).
        group.MapPost("/admin/admins/import",
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
                    Code = ErrorCodes.AdminImportEmpty,
                    Message = "An Excel file is required.",
                    MessageArabic = "ملف Excel مطلوب.",
                }));
            }
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            return Forward(await api.ImportUsersAsync(
                stream.ToArray(), file.FileName, token));
        }).DisableAntiforgery();

        // P9 — Interests CRUD proxy (D-050; الاهتمامات).
        group.MapPost("/admin/interests/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListInterestsAsync(body, token));
        });

        group.MapGet("/admin/interests/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetInterestAsync(id, token));
        });

        group.MapPost("/admin/interests",
            async (AdminCreateInterestRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateInterestAsync(body, token));
        });

        group.MapPut("/admin/interests/{id:guid}",
            async (Guid id, AdminUpdateInterestRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateInterestAsync(id, body, token));
        });

        group.MapDelete("/admin/interests/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateInterestAsync(id, token));
        });

        // P6 — log viewer proxy. The CP page is server-side Blazor, so it
        // talks to these endpoints via fetch (cookie auth) and they forward
        // to the API with the access token.
        group.MapGet("/admin/logs/list",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListLogsAsync(token));
        });

        group.MapGet("/admin/logs/tail",
            async (string project, string file, int? lines,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.TailLogAsync(project, file, lines ?? 500, token));
        });

        group.MapGet("/admin/logs/download",
            async (string project, string file,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, bytes, safeFileName) =
                await api.DownloadLogAsync(project, file, token);
            if (status != 200 || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            return Results.File(bytes, "text/plain", safeFileName);
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

    /// <summary>
    /// Forwards the upstream <see cref="ApiCallResult{T}"/> verbatim — the
    /// browser sees the same status the API returned (200 / 400 / 401 / 423
    /// / 429 / 503), and the same envelope body either way.
    /// </summary>
    private static IResult Forward<T>(ApiCallResult<T> result) =>
        Results.Json(result.Body, statusCode: result.StatusCode);
}
