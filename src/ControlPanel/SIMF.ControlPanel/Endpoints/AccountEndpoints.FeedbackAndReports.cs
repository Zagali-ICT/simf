// Part of AccountEndpoints - see AccountEndpoints.cs for the shared helpers.
// archive editions, ratings, statistics, the reporting module
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
    private static void MapFeedbackAndReports(IEndpointRouteBuilder group)
    {
        // Archive edition admin CRUD BFF passthroughs.
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
        // "make this year history" snapshot.
        group.MapPost("/admin/archive/snapshot-current",
            async (SnapshotCurrentEditionRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.SnapshotCurrentArchiveEditionAsync(body, token));
        });

        // Ratings admin read BFF passthrough.
        group.MapPost("/admin/feedback/ratings",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListRatingsAsync(body, token));
        });
        group.MapGet("/admin/feedback/ratings/kpi",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetRatingKpiAsync(token));
        });

        // Rating configuration (types → groups → questions) BFF passthroughs.
        group.MapPost("/admin/ratings/types/list",
            async (GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListRatingTypesAsync(body, token));
        });
        group.MapGet("/admin/ratings/types/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetRatingTypeAsync(id, token));
        });
        group.MapPost("/admin/ratings/types",
            async (CreateRatingTypeRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateRatingTypeAsync(body, token));
        });
        group.MapPut("/admin/ratings/types/{id:guid}",
            async (Guid id, UpdateRatingTypeRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateRatingTypeAsync(id, body, token));
        });
        group.MapDelete("/admin/ratings/types/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteRatingTypeAsync(id, token));
        });
        group.MapPost("/admin/ratings/types/{typeId:guid}/groups/list",
            async (Guid typeId, GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListRatingGroupsAsync(typeId, body, token));
        });
        group.MapGet("/admin/ratings/groups/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetRatingGroupAsync(id, token));
        });
        group.MapPost("/admin/ratings/groups",
            async (CreateRatingQuestionGroupRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateRatingGroupAsync(body, token));
        });
        group.MapPut("/admin/ratings/groups/{id:guid}",
            async (Guid id, UpdateRatingQuestionGroupRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateRatingGroupAsync(id, body, token));
        });
        group.MapDelete("/admin/ratings/groups/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteRatingGroupAsync(id, token));
        });
        group.MapPost("/admin/ratings/types/{typeId:guid}/questions/list",
            async (Guid typeId, GridQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListRatingQuestionsAsync(typeId, body, token));
        });
        group.MapGet("/admin/ratings/questions/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetRatingQuestionAsync(id, token));
        });
        group.MapPost("/admin/ratings/questions",
            async (CreateRatingQuestionRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.CreateRatingQuestionAsync(body, token));
        });
        group.MapPut("/admin/ratings/questions/{id:guid}",
            async (Guid id, UpdateRatingQuestionRequest body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.UpdateRatingQuestionAsync(id, body, token));
        });
        group.MapDelete("/admin/ratings/questions/{id:guid}",
            async (Guid id, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.DeleteRatingQuestionAsync(id, token));
        });

        // Statistics dashboard BFF passthrough.
        group.MapGet("/admin/statistics",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetStatisticsAsync(token));
        });

        // Programme dashboard (the day-by-day chart on the CP landing page).
        group.MapGet("/admin/statistics/programme",
            async (HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.GetStatisticsProgrammeAsync(token));
        });

        // -- Reporting module BFF passthroughs --------------------------------
        // The CP has no catch-all proxy, so every report route is declared here
        // as well as on the API. Miss one and the page compiles, the API answers,
        // and the browser still gets a 404 with the grid silently empty.
        group.MapPost("/admin/reports/attendance/list",
            async (ReportQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListAttendanceReportAsync(body, token));
        });
        group.MapPost("/admin/reports/attendance/export",
            async (ReportQuery body, HttpContext http, SimfAdminClient api) =>
                await ForwardReportExportAsync(http, "attendance",
                    (token) => api.ExportAttendanceReportAsync(body, token)));

        group.MapPost("/admin/reports/registrations/list",
            async (ReportQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListRegistrationsReportAsync(body, token));
        });
        group.MapPost("/admin/reports/registrations/export",
            async (ReportQuery body, HttpContext http, SimfAdminClient api) =>
                await ForwardReportExportAsync(http, "registrations",
                    (token) => api.ExportRegistrationsReportAsync(body, token)));

        group.MapPost("/admin/reports/gates/list",
            async (ReportQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListGateActivityReportAsync(body, token));
        });
        group.MapPost("/admin/reports/gates/export",
            async (ReportQuery body, HttpContext http, SimfAdminClient api) =>
                await ForwardReportExportAsync(http, "gate-activity",
                    (token) => api.ExportGateActivityReportAsync(body, token)));

        group.MapPost("/admin/reports/sessions/list",
            async (ReportQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListSessionsReportAsync(body, token));
        });
        group.MapPost("/admin/reports/sessions/export",
            async (ReportQuery body, HttpContext http, SimfAdminClient api) =>
                await ForwardReportExportAsync(http, "sessions",
                    (token) => api.ExportSessionsReportAsync(body, token)));

        group.MapPost("/admin/reports/ratings/list",
            async (ReportQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListRatingsReportAsync(body, token));
        });
        group.MapPost("/admin/reports/ratings/export",
            async (ReportQuery body, HttpContext http, SimfAdminClient api) =>
                await ForwardReportExportAsync(http, "ratings",
                    (token) => api.ExportRatingsReportAsync(body, token)));

        group.MapPost("/admin/reports/partners/list",
            async (ReportQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListPartnersReportAsync(body, token));
        });
        group.MapPost("/admin/reports/partners/export",
            async (ReportQuery body, HttpContext http, SimfAdminClient api) =>
                await ForwardReportExportAsync(http, "partners",
                    (token) => api.ExportPartnersReportAsync(body, token)));

        group.MapPost("/admin/reports/meetings/list",
            async (ReportQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListMeetingsReportAsync(body, token));
        });
        group.MapPost("/admin/reports/meetings/export",
            async (ReportQuery body, HttpContext http, SimfAdminClient api) =>
                await ForwardReportExportAsync(http, "meetings",
                    (token) => api.ExportMeetingsReportAsync(body, token)));

        group.MapPost("/admin/reports/engagement/list",
            async (ReportQuery body, HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.ListEngagementReportAsync(body, token));
        });
        group.MapPost("/admin/reports/engagement/export",
            async (ReportQuery body, HttpContext http, SimfAdminClient api) =>
                await ForwardReportExportAsync(http, "engagement",
                    (token) => api.ExportEngagementReportAsync(body, token)));

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

        // Exhibitor admin CRUD + account-provisioning BFF passthroughs.
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
        // Attach an EXISTING account to the exhibitor (the Others-pipeline
        // lockout fix). The API gates it on Exhibitors.LinkAccount.
        group.MapPost("/admin/exhibitors/{id:guid}/accounts/link",
            async (Guid id, LinkExhibitorAccountRequest body,
                   HttpContext http, SimfAdminClient api) =>
        {
            var token = await http.GetTokenAsync("access_token");
            if (token is null) return Results.Unauthorized();
            return Forward(await api.LinkExhibitorAccountAsync(id, body, token));
        });
    }
}
