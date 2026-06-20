// Tests: SIMF.ControlPanel.Tests/AccountEndpointsTests.cs (todo).
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Archive;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.BusinessMeetings;
using SIMF.Contracts.Exhibitors;
using SIMF.Contracts.Exhibition;
using SIMF.Contracts.Faq;
using SIMF.Contracts.Organisations;
using SIMF.Contracts.Contacts;
using SIMF.Contracts.Logs;
using SIMF.Contracts.Media;
using SIMF.Contracts.Programme;
using SIMF.Contracts.PublicRelations;
using SIMF.Contracts.Sessions;

using SIMF.Common.Enums;

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
    // P3.2b — D-232: fallback if SessionRecordingStorage:MaxUploadBytes is absent
    // from CP config (1 GiB). The live value is read from configuration so it is
    // sourced once, not baked into code — see the recording-upload BFF route.
    private const long DefaultRecordingMaxUploadBytes = 1_073_741_824L;

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

        // D-096: returns the QR for the caller's CURRENT secret (no rotation).
        // Drives the /account/totp-pairing CP page used to re-pair a lost
        // authenticator without resetting the seeded super-admin's secret.
        group.MapGet("/totp/pairing", async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.TotpPairingAsync(token));
        });

        // D-102: verifies a code against the active secret without mutating state.
        group.MapPost("/totp/pairing/verify",
            async (TotpConfirmRequest body, HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.TotpPairingVerifyAsync(body, token));
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

        // D-127 — countries picker for the walk-in form's nationality dropdown.
        group.MapGet("/admin/walk-in/countries",
            async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetProfileCountriesAsync(token));
        });

        // B3 — D-221 — organisations picker for the walk-in form's الجهة field.
        group.MapGet("/admin/walk-in/organisations",
            async (string? search, int? top, HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SearchOrganisationsAsync(search, top ?? 20, token));
        });

        // D-127 — active interests picker for the visitor walk-in form.
        group.MapGet("/interests",
            async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetActiveInterestsAsync(token));
        });

        // D-130 — print-bag station: QR-id lookup.
        group.MapGet("/admin/qr-lookup/{qrId}",
            async (string qrId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.LookupByQrIdAsync(qrId, token));
        });

        // D-129 — admin upload of the subject's ID-document image (multipart).
        // The CP page hosts a hidden <input type="file">; simfAccount.uploadFile
        // sends it here. Same SameSite=Lax CSRF stance as /avatar (D-029).
        group.MapPost("/admin/visitors/{id:guid}/id-document",
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
                    Code = ErrorCodes.VisitorIdImageMissing,
                    Message = "An ID image is required.",
                    MessageArabic = "صورة الهوية مطلوبة.",
                }));
            }
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            return Forward(await api.UploadVisitorIdDocumentAsync(
                id, stream.ToArray(), file.ContentType, file.FileName, token));
        }).DisableAntiforgery();

        group.MapPost("/admin/others/{id:guid}/id-document",
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
                    Code = ErrorCodes.VisitorIdImageMissing,
                    Message = "An ID image is required.",
                    MessageArabic = "صورة الهوية مطلوبة.",
                }));
            }
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            return Forward(await api.UploadOtherIdDocumentAsync(
                id, stream.ToArray(), file.ContentType, file.FileName, token));
        }).DisableAntiforgery();

        // D-427 (CS-3) — admin upload of the subject's profile photo (avatar).
        // Mirrors the ID-document upload proxy (multipart, SameSite=Lax CSRF).
        group.MapPost("/admin/visitors/{id:guid}/avatar",
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
                    Code = ErrorCodes.AvatarFileMissing,
                    Message = "An avatar file is required.",
                    MessageArabic = "ملف الصورة مطلوب.",
                }));
            }
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            return Forward(await api.UploadVisitorAvatarAsync(
                id, stream.ToArray(), file.ContentType, file.FileName, token));
        }).DisableAntiforgery();

        group.MapPost("/admin/others/{id:guid}/avatar",
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
                    Code = ErrorCodes.AvatarFileMissing,
                    Message = "An avatar file is required.",
                    MessageArabic = "ملف الصورة مطلوب.",
                }));
            }
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            return Forward(await api.UploadOtherAvatarAsync(
                id, stream.ToArray(), file.ContentType, file.FileName, token));
        }).DisableAntiforgery();

        // V-1 (D-429) — admin upload of the subject's VVIP/VIP welcome photo.
        // Mirrors the avatar upload proxy (multipart, SameSite=Lax CSRF).
        group.MapPost("/admin/visitors/{id:guid}/vip-photo",
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
                    Code = ErrorCodes.AvatarFileMissing,
                    Message = "A photo file is required.",
                    MessageArabic = "ملف الصورة مطلوب.",
                }));
            }
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            return Forward(await api.UploadVisitorVipPhotoAsync(
                id, stream.ToArray(), file.ContentType, file.FileName, token));
        }).DisableAntiforgery();

        // V-1 (D-429) — admin stream-read of the subject's VIP welcome photo for
        // the roster / export page. Mirrors the avatar GET proxy.
        group.MapGet("/admin/visitors/{id:guid}/vip-photo",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, contentType, bytes) =
                await api.FetchVisitorVipPhotoAsync(id, token);
            if (status != 200 || contentType is null || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            http.Response.Headers.CacheControl = "private, max-age=60";
            return Results.File(bytes, contentType);
        });

        // V-1 (D-429) — VVIP/VIP welcome roster (موج): JSON feed for the export
        // page + the file download. ("vip" never matches the {id:guid} routes.)
        group.MapGet("/admin/visitors/vip/roster",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetVipRosterAsync(token));
        });

        group.MapPost("/admin/visitors/vip/roster/list",
            async (GridQuery query, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListVipRosterAsync(query, token));
        });

        group.MapGet("/admin/visitors/vip/roster/export",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var format = http.Request.Query["format"].ToString();
            if (string.IsNullOrWhiteSpace(format)) { format = "csv"; }
            var (status, contentType, bytes) =
                await api.FetchVipRosterFileAsync(format, token);
            if (status != 200 || contentType is null || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            var fileName = format.Equals("xlsx", StringComparison.OrdinalIgnoreCase)
                ? "vip-welcome-roster.xlsx"
                : "vip-welcome-roster.csv";
            return Results.File(bytes, contentType, fileName);
        });

        // D-129 — admin stream-read of the subject's ID-document image. The
        // Details / View modals render this via <img src="..."> so the
        // browser refreshes it whenever the modal opens.
        group.MapGet("/admin/visitors/{id:guid}/id-document",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, contentType, bytes) =
                await api.FetchVisitorIdDocumentAsync(id, token);
            if (status != 200 || contentType is null || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            http.Response.Headers.CacheControl = "private, max-age=60";
            return Results.File(bytes, contentType);
        });

        group.MapGet("/admin/others/{id:guid}/id-document",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, contentType, bytes) =
                await api.FetchOtherIdDocumentAsync(id, token);
            if (status != 200 || contentType is null || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            http.Response.Headers.CacheControl = "private, max-age=60";
            return Results.File(bytes, contentType);
        });

        // CS-4 — admin stream-read of the subject's profile photo (avatar) for
        // the approve modal. Mirrors the ID-document GET proxy.
        group.MapGet("/admin/visitors/{id:guid}/avatar",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, contentType, bytes) =
                await api.FetchVisitorAvatarAsync(id, token);
            if (status != 200 || contentType is null || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            http.Response.Headers.CacheControl = "private, max-age=60";
            return Results.File(bytes, contentType);
        });

        group.MapGet("/admin/others/{id:guid}/avatar",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, contentType, bytes) =
                await api.FetchOtherAvatarAsync(id, token);
            if (status != 200 || contentType is null || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            http.Response.Headers.CacheControl = "private, max-age=60";
            return Results.File(bytes, contentType);
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

        // D-356 — generic grid Excel proxies. One line per resource registers
        // both /admin/{slug}/export (binary) and /admin/{slug}/import (multipart),
        // forwarding to the API's generic grid endpoints. Interests is the pilot.
        MapGridExcel(group, "interests");
        MapGridExcel(group, "countries");
        MapGridExcel(group, "themes");
        MapGridExcel(group, "halls");
        MapGridExcel(group, "gates");
        MapGridExcel(group, "session-categories");
        MapGridExcel(group, "roles");
        MapGridExcel(group, "banners"); 
        MapGridExcel(group, "content-blocks"); 
        MapGridExcel(group, "media-partners"); 
        MapGridExcel(group, "archive"); 
        MapGridExcel(group, "media"); 
        MapGridExcel(group, "system-settings"); 
        MapGridExcel(group, "contacts"); 
        MapGridExcel(group, "news"); 
        MapGridExcel(group, "ai/prompts");
        MapGridExcel(group, "sponsors");
        MapGridExcel(group, "exhibitors");
        MapGridExcel(group, "speakers");
        MapGridExcel(group, "booths");
        MapGridExcel(group, "venue-map");
        MapGridExport(group, "invitations");
        MapGridExport(group, "comments-moderation");
        MapGridExport(group, "ratings");
        MapGridExport(group, "speaker-presentations");
        MapGridExport(group, "vips");
        MapGridExport(group, "bookings");
        MapGridExport(group, "session-summaries");
        MapGridExport(group, "speaker-meeting-requests");
        MapGridExport(group, "meeting-tables");
        MapGridExcel(group, "sessions");
        MapGridExport(group, "session-moderators");
        MapGridExport(group, "questions");
        MapGridExport(group, "business-meetings");
        // Organisations keeps its bespoke government-Excel bulk import, so it
        // gets the generic EXPORT only (no generic /import route).
        MapGridExport(group, "organisations");

        // D-118 — D-113 type-scoped bulk proxies for Visitors and Others.
        // The visitors/others CP list pages (D-114) call these JS endpoints
        // and they forward to the API's D-113 routes with the access token.
        group.MapPost("/admin/visitors/bulk-delete",
            async (AdminBulkDeleteRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.BulkDeleteVisitorsAsync(body, token));
        });

        group.MapPost("/admin/others/bulk-delete",
            async (AdminBulkDeleteRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.BulkDeleteOthersAsync(body, token));
        });

        group.MapPost("/admin/visitors/duplicate",
            async (AdminDuplicateUserRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DuplicateVisitorAsync(body, token));
        });

        group.MapPost("/admin/others/duplicate",
            async (AdminDuplicateUserRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DuplicateOtherAsync(body, token));
        });

        group.MapPost("/admin/visitors/export",
            async (AdminExportUsersRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, bytes) = await api.ExportVisitorsAsync(body, token);
            if (status != 200 || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"simf-visitors-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx");
        });

        group.MapPost("/admin/others/export",
            async (AdminExportUsersRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, bytes) = await api.ExportOthersAsync(body, token);
            if (status != 200 || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"simf-others-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx");
        });

        group.MapPost("/admin/visitors/import",
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
            return Forward(await api.ImportVisitorsAsync(
                stream.ToArray(), file.FileName, token));
        }).DisableAntiforgery();

        group.MapPost("/admin/others/import",
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
            return Forward(await api.ImportOthersAsync(
                stream.ToArray(), file.FileName, token));
        }).DisableAntiforgery();

        // D-118 — ProfileTypes CRUD proxy (consumer of D-115).
        group.MapPost("/admin/profile-types/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListAdminProfileTypesAsync(body, token));
        });

        group.MapGet("/admin/profile-types/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetAdminProfileTypeAsync(id, token));
        });

        group.MapPost("/admin/profile-types",
            async (AdminCreateProfileTypeRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateAdminProfileTypeAsync(body, token));
        });

        group.MapPut("/admin/profile-types/{id:guid}",
            async (Guid id, AdminUpdateProfileTypeRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateAdminProfileTypeAsync(id, body, token));
        });

        group.MapDelete("/admin/profile-types/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateAdminProfileTypeAsync(id, token));
        });

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

        // P2.1 (D-211) — FAQ management proxy (two-level group → entry).
        group.MapPost("/admin/faq/groups/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListFaqGroupsAsync(body, token));
        });
        group.MapGet("/admin/faq/groups/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetFaqGroupAsync(id, token));
        });
        group.MapPost("/admin/faq/groups",
            async (CreateFaqGroupRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateFaqGroupAsync(body, token));
        });
        group.MapPut("/admin/faq/groups/{id:guid}",
            async (Guid id, UpdateFaqGroupRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateFaqGroupAsync(id, body, token));
        });
        group.MapDelete("/admin/faq/groups/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteFaqGroupAsync(id, token));
        });
        group.MapPost("/admin/faq/groups/{groupId:guid}/entries/list",
            async (Guid groupId, GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListFaqEntriesAsync(groupId, body, token));
        });
        group.MapPost("/admin/faq/entries",
            async (CreateFaqEntryRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateFaqEntryAsync(body, token));
        });
        group.MapPut("/admin/faq/entries/{id:guid}",
            async (Guid id, UpdateFaqEntryRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateFaqEntryAsync(id, body, token));
        });
        group.MapDelete("/admin/faq/entries/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteFaqEntryAsync(id, token));
        });

        // D-134 Sprint A — Roles CRUD proxy (existing schema, no migration).
        group.MapPost("/admin/roles/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListRolesAsync(body, token));
        });

        group.MapGet("/admin/roles/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetRoleAsync(id, token));
        });

        group.MapPost("/admin/roles",
            async (AdminCreateRoleRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateRoleAsync(body, token));
        });

        group.MapPut("/admin/roles/{id:guid}",
            async (Guid id, AdminUpdateRoleRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateRoleAsync(id, body, token));
        });

        group.MapDelete("/admin/roles/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteRoleAsync(id, token));
        });

        // Issue-1 — role -> permission grants (read + replace).
        group.MapGet("/admin/roles/{id:guid}/permissions",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetRolePermissionsAsync(id, token));
        });

        group.MapPut("/admin/roles/{id:guid}/permissions",
            async (Guid id, AdminSetRolePermissionsRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SetRolePermissionsAsync(id, body, token));
        });

        // Issue-1 — an admin user's RBAC roles (read + replace).
        group.MapGet("/admin/admins/{id:guid}/roles",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetUserRolesAsync(id, token));
        });

        group.MapPut("/admin/admins/{id:guid}/roles",
            async (Guid id, AdminSetUserRolesRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SetUserRolesAsync(id, body, token));
        });

        // D-134 Sprint A — Operation log viewer proxy (read-only over
        // the existing OperationLogEntry table; no schema change).
        group.MapPost("/admin/operation-log/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListOperationLogAsync(body, token));
        });

        group.MapGet("/admin/operation-log/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetOperationLogAsync(id, token));
        });

        // P1.6 — binary XLSX download. Cannot reuse Forward() because the
        // response body is the workbook bytes, not the JSON envelope.
        group.MapPost("/admin/operation-log/export",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, bytes) = await api.ExportOperationLogAsync(body, token);
            if (status != 200 || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"simf-operation-log-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx");
        });

        // D-134 Sprint A — Attendees roster proxy (read-only join over
        // SimfUser + UserProfile + ProfileType; no schema change).
        group.MapPost("/admin/attendees/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListAttendeesAsync(body, token));
        });

        // P1.6 — binary XLSX download of the filtered roster.
        group.MapPost("/admin/attendees/export",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, bytes) = await api.ExportAttendeesAsync(body, token);
            if (status != 200 || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"simf-attendees-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx");
        });

        // D-134 Sprint B — Themes CRUD proxy (D-135 freeze-lift).
        group.MapPost("/admin/themes/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListThemesAsync(body, token));
        });

        group.MapGet("/admin/themes/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetThemeAsync(id, token));
        });

        group.MapPost("/admin/themes",
            async (AdminCreateThemeRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateThemeAsync(body, token));
        });

        group.MapPut("/admin/themes/{id:guid}",
            async (Guid id, AdminUpdateThemeRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateThemeAsync(id, body, token));
        });

        group.MapDelete("/admin/themes/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateThemeAsync(id, token));
        });

        // D-134 Sprint B — Halls CRUD proxy (D-135 freeze-lift).
        group.MapPost("/admin/halls/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListHallsAsync(body, token));
        });

        group.MapGet("/admin/halls/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetHallAsync(id, token));
        });

        group.MapPost("/admin/halls",
            async (AdminCreateHallRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateHallAsync(body, token));
        });

        group.MapPut("/admin/halls/{id:guid}",
            async (Guid id, AdminUpdateHallRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateHallAsync(id, body, token));
        });

        group.MapDelete("/admin/halls/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateHallAsync(id, token));
        });

        // SIMF-FDS-013 (D-248) — meeting tables + hall allocations + business
        // meetings BFF passthroughs (mirrors the Halls block above). Without
        // these the /admin/meeting-tables + /admin/business-meetings pages 400
        // on every data call (the backend endpoints exist; only these proxies
        // were missing).
        group.MapPut("/admin/halls/{hallId:guid}/purpose",
            async (Guid hallId, SetHallPurposeRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SetHallPurposeAsync(hallId, body, token));
        });

        group.MapPost("/admin/halls/{hallId:guid}/meeting-tables/list",
            async (Guid hallId, GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListMeetingTablesAsync(hallId, body, token));
        });

        group.MapPost("/admin/halls/{hallId:guid}/meeting-tables",
            async (Guid hallId, CreateMeetingTableRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateMeetingTableAsync(hallId, body, token));
        });

        group.MapPut("/admin/meeting-tables/{id:guid}",
            async (Guid id, UpdateMeetingTableRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateMeetingTableAsync(id, body, token));
        });

        group.MapDelete("/admin/meeting-tables/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteMeetingTableAsync(id, token));
        });

        group.MapPost("/admin/halls/{hallId:guid}/meeting-tables/generate",
            async (Guid hallId, GenerateMeetingTablesRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GenerateMeetingTablesAsync(hallId, body, token));
        });

        group.MapPost("/admin/halls/{hallId:guid}/hall-allocations/list",
            async (Guid hallId, GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListHallAllocationsAsync(hallId, body, token));
        });

        group.MapPost("/admin/halls/{hallId:guid}/hall-allocations",
            async (Guid hallId, CreateHallAllocationRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateHallAllocationAsync(hallId, body, token));
        });

        group.MapDelete("/admin/hall-allocations/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ReleaseHallAllocationAsync(id, token));
        });

        group.MapPost("/admin/business-meetings/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListBusinessMeetingsAsync(body, token));
        });

        group.MapGet("/admin/business-meetings/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetBusinessMeetingAsync(id, token));
        });

        group.MapPost("/admin/business-meetings",
            async (ScheduleMeetingRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ScheduleBusinessMeetingAsync(body, token));
        });

        group.MapPost("/admin/business-meetings/{id:guid}/cancel",
            async (Guid id, CancelMeetingRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CancelBusinessMeetingAsync(id, body, token));
        });

        // D-151 — Country admin lookup BFF passthroughs.
        group.MapPost("/admin/countries/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListCountriesAsync(body, token));
        });
        group.MapGet("/admin/countries/{id:int}",
            async (int id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetCountryAsync(id, token));
        });
        group.MapPost("/admin/countries",
            async (AdminCreateCountryRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateCountryAsync(body, token));
        });
        group.MapPut("/admin/countries/{id:int}",
            async (int id, AdminUpdateCountryRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateCountryAsync(id, body, token));
        });
        group.MapDelete("/admin/countries/{id:int}",
            async (int id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateCountryAsync(id, token));
        });

        // D-153 — Speaker admin BFF passthroughs.
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

        // D-165 (gap doc G3) — Session admin BFF passthroughs.
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
        group.MapDelete("/admin/sessions/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateSessionAsync(id, token));
        });
        // P3.2 — D-231: session broadcast-lifecycle transition.
        group.MapPut("/admin/sessions/{id:guid}/status",
            async (Guid id, SetSessionStatusRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SetSessionStatusAsync(id, body, token));
        });
        // P3.2b — D-232: session recording upload / delete passthrough. The
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

        // D-166 (gap doc G4) — registration gate + archive visibility BFF passthroughs.
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

        // D-168 (gap doc G5) — public-relations BFF passthroughs.
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

        // D-173 (gap doc G8) — Dynamic content CMS BFF passthroughs.
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

        // D-169 (gap doc G6) — session-question moderation BFF passthroughs.
        group.MapPost("/admin/session-moderators/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSessionModeratorsAsync(body, token));
        });
        group.MapPost("/admin/session-moderators",
            async (AssignSessionModeratorRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.AssignSessionModeratorAsync(body, token));
        });
        group.MapDelete("/admin/session-moderators/{sessionId:guid}/{userId:guid}",
            async (Guid sessionId, Guid userId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RevokeSessionModeratorAsync(sessionId, userId, token));
        });
        // P3.3 — D-234: Scientific-Committee Q&A queue passthroughs.
        group.MapGet("/admin/questions/queue",
            async (QuestionStatus? status, Guid? sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListQuestionQueueAsync(status, sessionId, token));
        });
        group.MapPut("/admin/questions/{questionId:guid}/approve",
            async (Guid questionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ApproveQuestionAsync(questionId, token));
        });
        group.MapPut("/admin/questions/{questionId:guid}/hide",
            async (Guid questionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.HideQuestionFromQueueAsync(questionId, token));
        });
        group.MapPut("/admin/questions/{questionId:guid}/escalate",
            async (Guid questionId, EscalateQuestionRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.EscalateQuestionAsync(questionId, body, token));
        });
        group.MapGet("/sessions/{sessionId:guid}/questions/moderate",
            async (Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListModeratorQueueAsync(sessionId, token));
        });
        group.MapPut("/sessions/{sessionId:guid}/questions/{questionId:guid}/hide",
            async (Guid sessionId, Guid questionId,
                SIMF.Contracts.Sessions.SetQuestionHiddenRequest body,
                HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.HideQuestionAsync(sessionId, questionId, body.IsHidden, token));
        });
        group.MapPut("/sessions/{sessionId:guid}/questions/{questionId:guid}/push",
            async (Guid sessionId, Guid questionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.PushQuestionAsync(sessionId, questionId, token));
        });
        group.MapPut("/sessions/{sessionId:guid}/questions/reorder",
            async (Guid sessionId,
                SIMF.Contracts.Sessions.ReorderQuestionsRequest body,
                HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ReorderQuestionsAsync(
                sessionId, body.OrderedQuestionIds.ToList(), token));
        });

        // P4.1 — D-238: AI session-summary / محضر committee desk passthroughs.
        group.MapGet("/admin/session-summaries",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSessionSummariesAsync(token));
        });
        group.MapGet("/admin/session-summaries/{sessionId:guid}",
            async (Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetSessionSummaryAsync(sessionId, token));
        });
        group.MapPost("/admin/session-summaries/{sessionId:guid}/generate",
            async (Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GenerateSessionSummaryAsync(sessionId, token));
        });
        group.MapPut("/admin/session-summaries/{sessionId:guid}",
            async (Guid sessionId, SaveSessionSummaryRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SaveSessionSummaryAsync(sessionId, body, token));
        });
        group.MapPut("/admin/session-summaries/{sessionId:guid}/publish",
            async (Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.PublishSessionSummaryAsync(sessionId, token));
        });
        group.MapPut("/admin/session-summaries/{sessionId:guid}/unpublish",
            async (Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UnpublishSessionSummaryAsync(sessionId, token));
        });
        // D-472 (#9) — the team review/approval workflow passthroughs.
        group.MapPut("/admin/session-summaries/{sessionId:guid}/submit-review",
            async (Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SubmitSessionSummaryForReviewAsync(sessionId, token));
        });
        group.MapPut("/admin/session-summaries/{sessionId:guid}/approve",
            async (Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ApproveSessionSummaryAsync(sessionId, token));
        });
        group.MapPut("/admin/session-summaries/{sessionId:guid}/return-to-draft",
            async (Guid sessionId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ReturnSessionSummaryToDraftAsync(sessionId, token));
        });

        // P5.1d — D-244: operator hall-door QR arrival passthrough.
        group.MapPost("/admin/sessions/{sessionId:guid}/arrivals",
            async (Guid sessionId, SIMF.Contracts.Sessions.RecordQrArrivalRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RecordQrArrivalAsync(sessionId, body, token));
        });

        // D-148 — Gate Module BFF passthroughs (admin + operator).
        group.MapPost("/admin/gates/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListGatesAsync(body, token));
        });
        group.MapGet("/admin/gates/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetGateAsync(id, token));
        });
        group.MapPost("/admin/gates",
            async (AdminCreateGateRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateGateAsync(body, token));
        });
        group.MapPut("/admin/gates/{id:guid}",
            async (Guid id, AdminUpdateGateRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateGateAsync(id, body, token));
        });
        group.MapDelete("/admin/gates/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateGateAsync(id, token));
        });
        group.MapGet("/admin/gates/{id:guid}/assignments",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListGateAssignmentsAsync(id, token));
        });
        group.MapPost("/admin/gates/reports/scans",
            async (AdminGateScanReportFilter body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListGateScansAsync(body, token));
        });
        group.MapGet("/admin/gates/reports/currently-inside",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListCurrentlyInsideAsync(token));
        });
        group.MapGet("/gates/my-assignments",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListMyGateAssignmentsAsync(token));
        });
        group.MapPost("/gates/{gateId:guid}/scans",
            async (Guid gateId, SIMF.Contracts.Gates.GateScanRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.PostScanAsync(gateId, body, token));
        });
        group.MapGet("/gates/my-reports/today",
            async (Guid? gateId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetMyDailyReportAsync(gateId, token));
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

        // D-176 (gap doc G12) — AI module admin CRUD + invocations log.
        group.MapPost("/admin/ai/prompts/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListAiPromptsAsync(body, token));
        });

        group.MapGet("/admin/ai/prompts/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetAiPromptAsync(id, token));
        });

        group.MapPost("/admin/ai/prompts",
            async (CreateAiPromptRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateAiPromptAsync(body, token));
        });

        group.MapPut("/admin/ai/prompts/{id:guid}",
            async (Guid id, UpdateAiPromptRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateAiPromptAsync(id, body, token));
        });

        group.MapDelete("/admin/ai/prompts/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateAiPromptAsync(id, token));
        });

        group.MapPost("/admin/ai/prompts/{id:guid}/test",
            async (Guid id, TestAiPromptRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.TestAiPromptAsync(id, body, token));
        });

        group.MapPost("/admin/ai/invocations/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListAiInvocationsAsync(body, token));
        });

        // D-182 (CP UI for D-175 seat reservations).
        group.MapGet("/admin/halls/{hallId:guid}/seat-layout",
            async (Guid hallId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetHallSeatLayoutAsync(hallId, token));
        });

        group.MapPut("/admin/halls/{hallId:guid}/seat-layout",
            async (Guid hallId, SetHallSeatLayoutRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SetHallSeatLayoutAsync(hallId, body, token));
        });

        group.MapPost("/admin/sessions/{sessionId:guid}/seats/list",
            async (Guid sessionId, GridQuery body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSessionSeatReservationsAsync(
                sessionId, body, token));
        });

        group.MapPost("/admin/sessions/{sessionId:guid}/seats/reserve-row",
            async (Guid sessionId, AdminReserveRowRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.AdminReserveSessionRowAsync(
                sessionId, body, token));
        });

        group.MapDelete("/admin/sessions/{sessionId:guid}/seats/{reservationId:guid}",
            async (Guid sessionId, Guid reservationId,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.AdminReleaseSessionSeatAsync(
                sessionId, reservationId, token));
        });

        // D-269 — speaker meeting requests BFF passthroughs.
        group.MapPost("/admin/speaker-meeting-requests/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListAdminSpeakerMeetingRequestsAsync(body, token));
        });

        group.MapGet("/admin/speaker-meeting-requests/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetAdminSpeakerMeetingRequestAsync(id, token));
        });

        group.MapPut("/admin/speaker-meeting-requests/{id:guid}/respond",
            async (Guid id, RespondToSpeakerMeetingRequestRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RespondToAdminSpeakerMeetingRequestAsync(
                id, body, token));
        });

        // D-199 — News admin CRUD BFF passthroughs.
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

        // D-199 — Media gallery admin CRUD + image upload BFF passthroughs.
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

        // D-357 — unified media-asset pipeline BFF passthroughs: per-entity upload /
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
            if (status != 200 || bytes.Length == 0) { return Results.StatusCode(status); }
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

        // P2.3 (D-228) — speaker presentation files (list / upload / download / delete).
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
                return Results.StatusCode(status);
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

        // D-199 — Media image upload (multipart; same SameSite=Lax CSRF stance
        // as /admin/visitors/{id}/id-document, D-029).
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

        // D-199 — Media-partner admin CRUD BFF passthroughs.
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

        // D-199 — Sponsor admin CRUD BFF passthroughs.
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

        // D-199 — Booth admin CRUD BFF passthroughs.
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

        // B3 (D-220) — Organisation lookup admin CRUD + gov-Excel import passthroughs.
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

        // SIMF-FDS-014 (D-281/C2) — shared Contact directory admin CRUD + picker
        // passthroughs (backend /api/v1/admin/contacts/*; gated Contacts.View/Edit).
        group.MapPost("/admin/contacts/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListContactsAsync(body, token));
        });
        group.MapGet("/admin/contacts/picker",
            async (string? search, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.PickerContactsAsync(search, token));
        });
        group.MapGet("/admin/contacts/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetContactAsync(id, token));
        });
        group.MapPost("/admin/contacts",
            async (CreateContactRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateContactAsync(body, token));
        });
        group.MapPut("/admin/contacts/{id:guid}",
            async (Guid id, UpdateContactRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateContactAsync(id, body, token));
        });
        group.MapDelete("/admin/contacts/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateContactAsync(id, token));
        });

        // SIMF-FDS-014 (D-283/C2b) — single-row GET passthroughs the Sponsor /
        // MediaPartner edit modals use to pre-load the linked ContactId for the
        // contact picker (the list/create/update/delete proxies already exist).
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

        // B9b (D-226) — session-category dynamic lookup admin CRUD passthroughs.
        group.MapPost("/admin/session-categories/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSessionCategoriesAsync(body, token));
        });
        group.MapGet("/admin/session-categories/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetSessionCategoryAsync(id, token));
        });
        group.MapPost("/admin/session-categories",
            async (AdminCreateSessionCategoryRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateSessionCategoryAsync(body, token));
        });
        group.MapPut("/admin/session-categories/{id:guid}",
            async (Guid id, AdminUpdateSessionCategoryRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateSessionCategoryAsync(id, body, token));
        });
        group.MapDelete("/admin/session-categories/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateSessionCategoryAsync(id, token));
        });

        // D-452 — programme-days admin CRUD passthroughs (date + bilingual
        // title; the logo rides the generic asset endpoints).
        group.MapPost("/admin/programme-days/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListProgrammeDaysAsync(body, token));
        });
        group.MapGet("/admin/programme-days/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetProgrammeDayAsync(id, token));
        });
        group.MapPost("/admin/programme-days",
            async (AdminCreateProgrammeDayRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateProgrammeDayAsync(body, token));
        });
        group.MapPut("/admin/programme-days/{id:guid}",
            async (Guid id, AdminUpdateProgrammeDayRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateProgrammeDayAsync(id, body, token));
        });
        group.MapDelete("/admin/programme-days/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateProgrammeDayAsync(id, token));
        });

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

        // P2.2 (D-227) — booking approval queue passthroughs.
        group.MapPost("/admin/bookings/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListPendingBookingsAsync(body, token));
        });
        group.MapPost("/admin/bookings/{id:guid}/approve",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ApproveBookingAsync(id, token));
        });
        group.MapPost("/admin/bookings/{id:guid}/reject",
            async (Guid id, RejectBookingRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.RejectBookingAsync(id, body, token));
        });
        group.MapPost("/admin/bookings/bulk-approve",
            async (AdminBulkApprovalRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.BulkApproveBookingsAsync(
                body.Ids?.ToList() ?? new List<Guid>(), token));
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

        // D-199 — Archive edition admin CRUD BFF passthroughs.
        group.MapPost("/admin/archive/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListArchiveEditionsAsync(body, token));
        });
        group.MapGet("/admin/archive/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetArchiveEditionAsync(id, token));
        });
        group.MapPost("/admin/archive",
            async (CreateArchiveEditionRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateArchiveEditionAsync(body, token));
        });
        group.MapPut("/admin/archive/{id:guid}",
            async (Guid id, UpdateArchiveEditionRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateArchiveEditionAsync(id, body, token));
        });
        group.MapDelete("/admin/archive/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteArchiveEditionAsync(id, token));
        });
        // D-275 — "make this year history" snapshot.
        group.MapPost("/admin/archive/snapshot-current",
            async (SnapshotCurrentEditionRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SnapshotCurrentArchiveEditionAsync(body, token));
        });

        // D-199 — Ratings admin read BFF passthrough.
        group.MapPost("/admin/feedback/ratings",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListRatingsAsync(body, token));
        });

        // D-199 — Session-comment moderation BFF passthroughs.
        group.MapPost("/admin/sessions/{sessionId:guid}/comments/list",
            async (Guid sessionId, AdminListSessionCommentsRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSessionCommentsAsync(sessionId, body, token));
        });
        group.MapPut("/admin/sessions/{sessionId:guid}/comments/{commentId:guid}/status",
            async (Guid sessionId, Guid commentId, SetSessionCommentStatusRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SetSessionCommentStatusAsync(
                sessionId, commentId, body, token));
        });
        group.MapDelete("/admin/sessions/{sessionId:guid}/comments/{commentId:guid}",
            async (Guid sessionId, Guid commentId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateSessionCommentAsync(
                sessionId, commentId, token));
        });

        // D-202 Track-2 — Statistics dashboard BFF passthrough.
        group.MapGet("/admin/statistics",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetStatisticsAsync(token));
        });

        // FR-506 — session-attendance dashboard BFF passthroughs.
        group.MapGet("/admin/attendance/summary",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetSessionAttendanceSummaryAsync(token));
        });
        group.MapPost("/admin/attendance/sessions/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSessionAttendanceAsync(body, token));
        });

        // D-202 Track-2 — Exhibitor admin CRUD + account-provisioning BFF passthroughs.
        group.MapPost("/admin/exhibitors/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListExhibitorsAsync(body, token));
        });
        group.MapGet("/admin/exhibitors/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetExhibitorAsync(id, token));
        });
        group.MapPost("/admin/exhibitors",
            async (CreateExhibitorRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateExhibitorAsync(body, token));
        });
        group.MapPut("/admin/exhibitors/{id:guid}",
            async (Guid id, UpdateExhibitorRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateExhibitorAsync(id, body, token));
        });
        group.MapDelete("/admin/exhibitors/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeactivateExhibitorAsync(id, token));
        });
        group.MapGet("/admin/exhibitors/{id:guid}/accounts",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListExhibitorAccountsAsync(id, token));
        });
        group.MapPost("/admin/exhibitors/{id:guid}/accounts",
            async (Guid id, ProvisionExhibitorAccountRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ProvisionExhibitorAccountAsync(id, body, token));
        });
    }

    /// <summary>
    /// Forwards the upstream <see cref="ApiCallResult{T}"/> verbatim — the
    /// browser sees the same status the API returned (200 / 400 / 401 / 423
    /// / 429 / 503), and the same envelope body either way.
    /// </summary>
    private static IResult Forward<T>(ApiCallResult<T> result) =>
        Results.Json(result.Body, statusCode: result.StatusCode);

    /// <summary>
    /// D-356 — registers the generic grid Excel EXPORT proxy for one resource:
    /// <c>POST /admin/{slug}/export</c> returns the XLSX bytes for the browser to
    /// save, forwarding to the API with the cookie's access token (the browser
    /// never sees it). Used standalone for a resource that has a bespoke import
    /// (e.g. Organisations) and as half of <see cref="MapGridExcel"/>.
    /// </summary>
    private static void MapGridExport(IEndpointRouteBuilder group, string slug)
    {
        group.MapPost($"/admin/{slug}/export",
            async (AdminGridExportRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, bytes) = await api.ExportGridAsync(slug, body, token);
            if (status != 200 || bytes.Length == 0)
            {
                return Results.StatusCode(status);
            }
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"simf-{slug}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx");
        });
    }

    /// <summary>
    /// D-356 — registers the generic grid Excel proxy PAIR for one resource:
    /// <see cref="MapGridExport"/> + <c>POST /admin/{slug}/import</c> (multipart
    /// upload, forwards the per-row result).
    /// </summary>
    private static void MapGridExcel(IEndpointRouteBuilder group, string slug)
    {
        MapGridExport(group, slug);

        group.MapPost($"/admin/{slug}/import",
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
            return Forward(await api.ImportGridAsync(slug, stream.ToArray(), file.FileName, token));
        }).DisableAntiforgery();
    }

    /// <summary>CS-D (D-386) — optional body for the approve-visitor
    /// passthrough. <see cref="ProfileTypeId"/> null (or an empty body)
    /// leaves the visitor's tier unchanged.</summary>
    private sealed record ApproveVisitorBody(Guid? ProfileTypeId);
}
