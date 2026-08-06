// Tests: SIMF.ControlPanel.Tests/AccountEndpointsTests.cs (todo).
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

/// <summary>
/// Control Panel proxy endpoints for the account-management calls.
/// Each endpoint reads the access token from the
/// cookie's stored auth tokens and forwards the request to the SIMF API,
/// returning the upstream HTTP status verbatim so the page can react to
/// 401 / 423 / 429 distinctly.
///
/// The Blazor profile page calls these endpoints via <c>fetch</c> so the
/// browser sends the auth cookie automatically (the page itself never sees
/// the access token).
/// </summary>
internal static partial class AccountEndpoints
{
    // Fallback if SessionRecordingStorage:MaxUploadBytes is absent
    // from CP config (1 GiB). The live value is read from configuration so it is
    // sourced once, not baked into code — see the recording-upload BFF route.
    private const long DefaultRecordingMaxUploadBytes = 1_073_741_824L;

    // Fallback if OrganizationHeroVideo:MaxUploadBytes is absent from CP
    // config (200 MiB — a hero loop should be short + web-optimised).
    private const long DefaultHeroVideoMaxUploadBytes = 209_715_200L;

    public static void MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/account/api").RequireAuthorization();


        MapAccount(group);
        MapUsers(group);
        MapUserDocuments(group);
        MapLookups(group);
        MapFaqAndRoles(group);
        MapHalls(group);
        MapGeography(group);
        MapProgramme(group);
        MapCommunications(group);
        MapModeration(group);
        MapGates(group);
        MapSelfService(group);
        MapAiAndEmail(group);
        MapSeatingAndMeetings(group);
        MapMediaAndPartners(group);
        MapCatalogue(group);
        MapSettings(group);
        MapFeedbackAndReports(group);
    }

    /// <summary>
    /// Forwards the upstream <see cref="ApiCallResult{T}"/> verbatim — the
    /// browser sees the same status the API returned (200 / 400 / 401 / 423
    /// / 429 / 503), and the same envelope body either way.
    /// </summary>
    private static IResult Forward<T>(ApiCallResult<T> result) =>
        Results.Json(result.Body, statusCode: result.StatusCode);

    /// <summary>
    /// Registers the generic grid Excel EXPORT proxy for one resource:
    /// <c>POST /admin/{slug}/export</c> returns the XLSX bytes for the browser to
    /// save, forwarding to the API with the cookie's access token (the browser
    /// never sees it). Used standalone for a resource that has a bespoke import
    /// (e.g. Organisations) and as half of <see cref="MapGridExcel"/>.
    /// </summary>
    /// <summary>
    /// Forwards one report export: pulls the caller's access token, calls the
    /// API, and streams the workbook back as a download. Shared by every report
    /// so the content type and the file-name convention live in one place.
    /// The stamp is Saudi local, not a zoned stamp.
    /// </summary>
    private static async Task<IResult> ForwardReportExportAsync(
        HttpContext http,
        string slug,
        Func<string, Task<(int StatusCode, byte[] Bytes)>> export)
    {
        var token = await http.GetTokenAsync("access_token");
        if (token is null) { return Results.Unauthorized(); }

        var (status, bytes) = await export(token);
        if (status != 200 || bytes.Length == 0)
        {
            return Results.StatusCode(status);
        }

        return Results.File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"simf-{slug}-{SimfClock.Now.FormatSaudi("yyyyMMdd-HHmmss")}.xlsx");
    }

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
                $"simf-{slug}-{SimfClock.Now:yyyyMMddHHmmss}.xlsx");
        });
    }

    /// <summary>
    /// Registers the generic grid Excel proxy PAIR for one resource:
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

    /// <summary>Optional body for the approve-visitor
    /// passthrough. <see cref="ProfileTypeId"/> null (or an empty body)
    /// leaves the visitor's tier unchanged.</summary>
    private sealed record ApproveVisitorBody(Guid? ProfileTypeId);

    /// <summary>Body for the contact-inquiry handled/reopen toggle
    /// passthrough (the CP posts <c>{ handled }</c>).</summary>
    private sealed record SetContactInquiryHandledBody(bool Handled = true);
}
