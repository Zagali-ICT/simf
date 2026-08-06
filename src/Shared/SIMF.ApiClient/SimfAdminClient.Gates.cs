// Part of SimfAdminClient - see SimfAdminClient.cs for the transport core.
// gate administration, the operator surface, profile types
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
    // -- Gate Module admin CRUD + reports -----------------------------------

    public Task<ApiCallResult<GridPage<AdminGateSummary>>> ListGatesAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminGateSummary>>(
            HttpMethod.Post, "gates/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminGateDetail>> GetGateAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminGateDetail>(
            HttpMethod.Get, $"gates/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminGateDetail>> CreateGateAsync(
        AdminCreateGateRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminGateDetail>(
            HttpMethod.Post, "gates",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminGateDetail>> UpdateGateAsync(
        Guid id, AdminUpdateGateRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminGateDetail>(
            HttpMethod.Put, $"gates/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateGateAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"gates/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<AdminGateAssignmentRow>>> ListGateAssignmentsAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminGateAssignmentRow>>(
            HttpMethod.Get, $"gates/{id}/assignments", content: null,
            accessToken, cancellationToken);

    // The gate form's own lookups, both gated on Gates.Manage so a gate
    // manager no longer needs Admins.View / ProfileTypes.View / Halls.View to fill
    // the Add/Edit form.

    public Task<ApiCallResult<GridPage<AdminGateOperatorCandidate>>>
        ListGateOperatorCandidatesAsync(
            GridQuery query, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminGateOperatorCandidate>>(
            HttpMethod.Post, "gates/operator-candidates/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminGateFormOptions>> GetGateFormOptionsAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminGateFormOptions>(
            HttpMethod.Get, "gates/form-options", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<AdminGateScanRow>>> ListGateScansAsync(
        AdminGateScanReportFilter filter, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminGateScanRow>>(
            HttpMethod.Post, "gates/reports/scans",
            JsonContent.Create(filter, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<AdminCurrentlyInsideRow>>> ListCurrentlyInsideAsync(
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminCurrentlyInsideRow>>(
            HttpMethod.Get, "gates/reports/currently-inside", content: null,
            accessToken, cancellationToken);

    // -- Gate Module operator surface ----------------------------------------

    public Task<ApiCallResult<IReadOnlyList<SIMF.Contracts.Gates.OperatorGateAssignment>>>
        ListMyGateAssignmentsAsync(string accessToken,
            CancellationToken cancellationToken = default) =>
        SendOperatorAsync<IReadOnlyList<SIMF.Contracts.Gates.OperatorGateAssignment>>(
            HttpMethod.Get, "my-assignments", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Gates.GateScanResponse>>
        PostScanAsync(Guid gateId, SIMF.Contracts.Gates.GateScanRequest request,
            string accessToken, CancellationToken cancellationToken = default) =>
        SendOperatorAsync<SIMF.Contracts.Gates.GateScanResponse>(
            HttpMethod.Post, $"{gateId}/scans",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Gates.OperatorDailyReport>>
        GetMyDailyReportAsync(Guid? gateId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendOperatorAsync<SIMF.Contracts.Gates.OperatorDailyReport>(
            HttpMethod.Get,
            gateId is { } id ? $"my-reports/today?gateId={id}" : "my-reports/today",
            content: null, accessToken, cancellationToken);

    // -- ProfileTypes CRUD (admin-managed lookup table) ----------------------

    /// <summary>One page of profile types for the admin grid.</summary>
    public Task<ApiCallResult<GridPage<AdminProfileTypeSummary>>> ListAdminProfileTypesAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminProfileTypeSummary>>(
            HttpMethod.Post, "profile-types/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One profile type by id.</summary>
    public Task<ApiCallResult<AdminProfileTypeSummary>> GetAdminProfileTypeAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminProfileTypeSummary>(
            HttpMethod.Get, $"profile-types/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>Creates a profile type.</summary>
    public Task<ApiCallResult<AdminProfileTypeSummary>> CreateAdminProfileTypeAsync(
        AdminCreateProfileTypeRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminProfileTypeSummary>(
            HttpMethod.Post, "profile-types",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Updates a profile type. UserType is immutable post-creation.</summary>
    public Task<ApiCallResult<AdminProfileTypeSummary>> UpdateAdminProfileTypeAsync(
        Guid id, AdminUpdateProfileTypeRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminProfileTypeSummary>(
            HttpMethod.Put, $"profile-types/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Soft-deletes (deactivates) a profile type. 409 if in use.</summary>
    public Task<ApiCallResult<bool>> DeactivateAdminProfileTypeAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"profile-types/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>Lists every project's log files.</summary>
    public Task<ApiCallResult<LogListResponse>> ListLogsAsync(
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<LogListResponse>(
            HttpMethod.Get, "logs/list",
            content: null,
            accessToken, cancellationToken);

    /// <summary>Returns the last <paramref name="lines"/> of one log file.</summary>
    public Task<ApiCallResult<LogTailResponse>> TailLogAsync(
        string project,
        string fileName,
        int lines,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var query = "logs/tail"
            + $"?project={Uri.EscapeDataString(project)}"
            + $"&file={Uri.EscapeDataString(fileName)}"
            + $"&lines={lines}";
        return SendAsync<LogTailResponse>(
            HttpMethod.Get, query,
            content: null,
            accessToken, cancellationToken);
    }

    /// <summary>Streams one full log file.</summary>
    public async Task<(int StatusCode, byte[] Bytes, string FileName)> DownloadLogAsync(
        string project,
        string fileName,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var query = "logs/download"
            + $"?project={Uri.EscapeDataString(project)}"
            + $"&file={Uri.EscapeDataString(fileName)}";
        using var message = new HttpRequestMessage(HttpMethod.Get, BasePath + query);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return ((int)response.StatusCode, bytes, Path.GetFileName(fileName));
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException)
        {
            return ((int)HttpStatusCode.ServiceUnavailable, Array.Empty<byte>(), fileName);
        }
    }

    /// <summary>Bulk-creates users from an XLSX workbook upload.</summary>
    public async Task<ApiCallResult<AdminImportUsersResponse>> ImportUsersAsync(
        byte[] xlsx,
        string fileName,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(xlsx);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "file", fileName);

        using var message = new HttpRequestMessage(HttpMethod.Post, BasePath + "admins/import")
        {
            Content = content,
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<ApiResult<AdminImportUsersResponse>>(
                JsonOptions, cancellationToken);
            return new ApiCallResult<AdminImportUsersResponse>(
                (int)response.StatusCode,
                body ?? TransportFailure<AdminImportUsersResponse>(
                    "The server returned an empty response.",
                    "أعاد الخادم استجابة فارغة."));
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException or JsonException)
        {
            return new ApiCallResult<AdminImportUsersResponse>(
                (int)HttpStatusCode.ServiceUnavailable,
                TransportFailure<AdminImportUsersResponse>(
                    "The SIMF service could not be reached. Please try again.",
                    "تعذّر الوصول إلى خدمة SIMF. حاول مرة أخرى."));
        }
    }
}
