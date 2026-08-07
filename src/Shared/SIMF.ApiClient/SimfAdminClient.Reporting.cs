// Part of SimfAdminClient - see SimfAdminClient.cs for the transport core.
// ratings, statistics, the reporting module, exhibitors
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Requests;
using SIMF.Contracts.Archive;
using SIMF.Contracts.Attendance;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.BusinessMeetings;
using SIMF.Contracts.Exhibition;
using SIMF.Contracts.Exhibitors;
using SIMF.Contracts.Email;
using SIMF.Contracts.Faq;
using SIMF.Contracts.Organisations;
using SIMF.Contracts.Feedback;
using SIMF.Contracts.Logs;
using SIMF.Contracts.Media;
using SIMF.Contracts.Organization;
using SIMF.Contracts.Programme;
using SIMF.Contracts.PublicRelations;
using SIMF.Contracts.Regions;
using SIMF.Contracts.Sessions;
using SIMF.Contracts.Statistics;
using SIMF.Contracts.Configuration;
using SIMF.Contracts.Ops;
using SIMF.Contracts.Support;
using SIMF.Common.Enums;

namespace SIMF.ApiClient;

public sealed partial class SimfAdminClient
{
    // -- Ratings admin read (SIMF.Contracts.Feedback) -----------------------

    public Task<ApiCallResult<AdminRatingResponsesPage>> ListRatingsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingResponsesPage>(
            HttpMethod.Post, "feedback/ratings",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingKpiView>> GetRatingKpiAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingKpiView>(
            HttpMethod.Get, "feedback/ratings/kpi",
            content: null, accessToken, cancellationToken);

    // -- Statistics dashboard (SIMF.Contracts.Statistics) --------------------

    public Task<ApiCallResult<StatisticsDashboard>> GetStatisticsAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<StatisticsDashboard>(
            HttpMethod.Get, "statistics", content: null,
            accessToken, cancellationToken);

    /// <summary>The programme dashboard: headline participant counts plus the
    /// per-forum-day figures behind the Control Panel's day-by-day chart.</summary>
    public Task<ApiCallResult<StatisticsProgramme>> GetStatisticsProgrammeAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<StatisticsProgramme>(
            HttpMethod.Get, "statistics/programme", content: null,
            accessToken, cancellationToken);

    // -- Reporting module (SIMF.Contracts.Reporting) --------------------------
    // One list + one export per report. Each list POSTs its ReportQuery (a
    // nested grid query plus a Saudi date range); each export returns raw XLSX
    // bytes and so bypasses the ApiResult envelope.

    public Task<ApiCallResult<SIMF.Contracts.Reporting.ReportPage<
        SIMF.Contracts.Reporting.AttendanceReportRow>>> ListAttendanceReportAsync(
        SIMF.Contracts.Reporting.ReportQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Reporting.ReportPage<
            SIMF.Contracts.Reporting.AttendanceReportRow>>(
            HttpMethod.Post, "reports/attendance/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<(int StatusCode, byte[] Bytes)> ExportAttendanceReportAsync(
        SIMF.Contracts.Reporting.ReportQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        PostForBytesAsync("reports/attendance/export", query, accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Reporting.ReportPage<
        SIMF.Contracts.Reporting.RegistrationReportRow>>> ListRegistrationsReportAsync(
        SIMF.Contracts.Reporting.ReportQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Reporting.ReportPage<
            SIMF.Contracts.Reporting.RegistrationReportRow>>(
            HttpMethod.Post, "reports/registrations/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<(int StatusCode, byte[] Bytes)> ExportRegistrationsReportAsync(
        SIMF.Contracts.Reporting.ReportQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        PostForBytesAsync("reports/registrations/export", query, accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Reporting.ReportPage<
        SIMF.Contracts.Reporting.GateActivityReportRow>>> ListGateActivityReportAsync(
        SIMF.Contracts.Reporting.ReportQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Reporting.ReportPage<
            SIMF.Contracts.Reporting.GateActivityReportRow>>(
            HttpMethod.Post, "reports/gates/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<(int StatusCode, byte[] Bytes)> ExportGateActivityReportAsync(
        SIMF.Contracts.Reporting.ReportQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        PostForBytesAsync("reports/gates/export", query, accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Reporting.ReportPage<
        SIMF.Contracts.Reporting.SessionsReportRow>>> ListSessionsReportAsync(
        SIMF.Contracts.Reporting.ReportQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Reporting.ReportPage<
            SIMF.Contracts.Reporting.SessionsReportRow>>(
            HttpMethod.Post, "reports/sessions/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<(int StatusCode, byte[] Bytes)> ExportSessionsReportAsync(
        SIMF.Contracts.Reporting.ReportQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        PostForBytesAsync("reports/sessions/export", query, accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Reporting.ReportPage<
        SIMF.Contracts.Reporting.RatingsReportRow>>> ListRatingsReportAsync(
        SIMF.Contracts.Reporting.ReportQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Reporting.ReportPage<
            SIMF.Contracts.Reporting.RatingsReportRow>>(
            HttpMethod.Post, "reports/ratings/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<(int StatusCode, byte[] Bytes)> ExportRatingsReportAsync(
        SIMF.Contracts.Reporting.ReportQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        PostForBytesAsync("reports/ratings/export", query, accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Reporting.ReportPage<
        SIMF.Contracts.Reporting.PartnersReportRow>>> ListPartnersReportAsync(
        SIMF.Contracts.Reporting.ReportQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Reporting.ReportPage<
            SIMF.Contracts.Reporting.PartnersReportRow>>(
            HttpMethod.Post, "reports/partners/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<(int StatusCode, byte[] Bytes)> ExportPartnersReportAsync(
        SIMF.Contracts.Reporting.ReportQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        PostForBytesAsync("reports/partners/export", query, accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Reporting.ReportPage<
        SIMF.Contracts.Reporting.MeetingsReportRow>>> ListMeetingsReportAsync(
        SIMF.Contracts.Reporting.ReportQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Reporting.ReportPage<
            SIMF.Contracts.Reporting.MeetingsReportRow>>(
            HttpMethod.Post, "reports/meetings/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<(int StatusCode, byte[] Bytes)> ExportMeetingsReportAsync(
        SIMF.Contracts.Reporting.ReportQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        PostForBytesAsync("reports/meetings/export", query, accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Reporting.ReportPage<
        SIMF.Contracts.Reporting.EngagementReportRow>>> ListEngagementReportAsync(
        SIMF.Contracts.Reporting.ReportQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Reporting.ReportPage<
            SIMF.Contracts.Reporting.EngagementReportRow>>(
            HttpMethod.Post, "reports/engagement/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<(int StatusCode, byte[] Bytes)> ExportEngagementReportAsync(
        SIMF.Contracts.Reporting.ReportQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        PostForBytesAsync("reports/engagement/export", query, accessToken, cancellationToken);

    // -- Session-attendance dashboard (SIMF.Contracts.Attendance) -------------

    public Task<ApiCallResult<SessionAttendanceSummary>> GetSessionAttendanceSummaryAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<SessionAttendanceSummary>(
            HttpMethod.Get, "attendance/summary", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<SessionAttendanceRow>>> ListSessionAttendanceAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<SessionAttendanceRow>>(
            HttpMethod.Post, "attendance/sessions/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    // -- Exhibitor admin CRUD + account provisioning ------------------------
    // (SIMF.Contracts.Exhibitors)

    public Task<ApiCallResult<GridPage<AdminExhibitorSummary>>> ListExhibitorsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminExhibitorSummary>>(
            HttpMethod.Post, "exhibitors/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminExhibitorDetail>> GetExhibitorAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminExhibitorDetail>(
            HttpMethod.Get, $"exhibitors/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminExhibitorDetail>> CreateExhibitorAsync(
        CreateExhibitorRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminExhibitorDetail>(
            HttpMethod.Post, "exhibitors",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminExhibitorDetail>> UpdateExhibitorAsync(
        Guid id, UpdateExhibitorRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminExhibitorDetail>(
            HttpMethod.Put, $"exhibitors/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateExhibitorAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"exhibitors/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<ExhibitorAccountSummary>>> ListExhibitorAccountsAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<ExhibitorAccountSummary>>(
            HttpMethod.Get, $"exhibitors/{id}/accounts", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<ExhibitorAccountSummary>> ProvisionExhibitorAccountAsync(
        Guid id, ProvisionExhibitorAccountRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<ExhibitorAccountSummary>(
            HttpMethod.Post, $"exhibitors/{id}/accounts",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // Attach an EXISTING account to the exhibitor (the Others-pipeline
    // lockout fix). Permission Exhibitors.LinkAccount.
    public Task<ApiCallResult<ExhibitorAccountSummary>> LinkExhibitorAccountAsync(
        Guid id, LinkExhibitorAccountRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<ExhibitorAccountSummary>(
            HttpMethod.Post, $"exhibitors/{id}/accounts/link",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);
}
