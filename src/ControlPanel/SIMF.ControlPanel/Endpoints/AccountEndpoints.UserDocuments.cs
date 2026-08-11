// Part of AccountEndpoints - see AccountEndpoints.cs for the shared helpers.
// walk-in desk, QR lookup, ID documents, avatars, VIP photos, grid bulk ops
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
    private static void MapUserDocuments(IEndpointRouteBuilder group)
    {
        // Countries picker for the walk-in form's nationality dropdown.
        group.MapGet("/admin/walk-in/countries",
            async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetProfileCountriesAsync(token));
        });

        // Organisations picker for the walk-in form's الجهة field.
        group.MapGet("/admin/walk-in/organisations",
            async (string? search, int? top, HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SearchOrganisationsAsync(search, top ?? 20, token));
        });

        // Active interests picker for the visitor walk-in form.
        group.MapGet("/interests",
            async (HttpContext http, SimfAccountClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetActiveInterestsAsync(token));
        });

        // Print-bag station: QR-id lookup.
        group.MapGet("/admin/qr-lookup/{qrId}",
            async (string qrId, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.LookupByQrIdAsync(qrId, token));
        });

        // Admin upload of the subject's ID-document image (multipart).
        // The CP page hosts a hidden <input type="file">; simfAccount.uploadFile
        // sends it here. Same SameSite=Lax CSRF stance as /avatar.
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

        // Admin upload of the subject's profile photo (avatar).
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

        // Admin upload of the subject's VVIP/VIP welcome photo.
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

        // Admin stream-read of the subject's VIP welcome photo for
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
                return DownloadFailure(status, bytes);
            }
            http.Response.Headers.CacheControl = "private, max-age=60";
            return Results.File(bytes, contentType);
        });

        // VVIP/VIP welcome roster (موج): JSON feed for the export
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
                return DownloadFailure(status, bytes);
            }
            var fileName = format.Equals("xlsx", StringComparison.OrdinalIgnoreCase)
                ? "vip-welcome-roster.xlsx"
                : "vip-welcome-roster.csv";
            return Results.File(bytes, contentType, fileName);
        });

        // Admin stream-read of the subject's ID-document image. The
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
                return DownloadFailure(status, bytes);
            }
            // A2-14 (NCA App-Sec Standard) — ID document is high-value PII; never cache.
            http.Response.Headers.CacheControl = "no-store";
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
                return DownloadFailure(status, bytes);
            }
            // A2-14 (NCA App-Sec Standard) — ID document is high-value PII; never cache.
            http.Response.Headers.CacheControl = "no-store";
            return Results.File(bytes, contentType);
        });

        // Admin stream-read of the subject's profile photo (avatar) for
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
                return DownloadFailure(status, bytes);
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
                return DownloadFailure(status, bytes);
            }
            http.Response.Headers.CacheControl = "private, max-age=60";
            return Results.File(bytes, contentType);
        });

        // Stream an admin account's avatar for the Admins-list thumbnail.
        // Mirrors the visitors/others avatar GET proxy; API side is gated by Admins.View.
        group.MapGet("/admin/admins/{id:guid}/avatar",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, contentType, bytes) =
                await api.FetchAdminAvatarAsync(id, token);
            if (status != 200 || contentType is null || bytes.Length == 0)
            {
                return DownloadFailure(status, bytes);
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
                return DownloadFailure(status, bytes);
            }
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"simf-staff-{SimfClock.Now:yyyyMMddHHmmss}.xlsx");
        });

        // Multipart upload — same SameSite=Lax CSRF stance as /avatar.
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

        // Generic grid Excel proxies. One line per resource registers
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
        MapGridExcel(group, "news");
        MapGridExcel(group, "ai/prompts");
        MapGridExcel(group, "sponsors");
        MapGridExcel(group, "exhibitors");
        MapGridExcel(group, "speakers");
        MapGridExcel(group, "booths");
        MapGridExcel(group, "venue-map");
        MapGridExport(group, "invitations");
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

        // Type-scoped bulk proxies for Visitors and Others. The visitors/others
        // CP list pages call these JS endpoints and they forward to the API's
        // bulk routes with the access token.
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
                return DownloadFailure(status, bytes);
            }
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"simf-visitors-{SimfClock.Now:yyyyMMddHHmmss}.xlsx");
        });

        group.MapPost("/admin/others/export",
            async (AdminExportUsersRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            var (status, bytes) = await api.ExportOthersAsync(body, token);
            if (status != 200 || bytes.Length == 0)
            {
                return DownloadFailure(status, bytes);
            }
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"simf-others-{SimfClock.Now:yyyyMMddHHmmss}.xlsx");
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
    }
}
