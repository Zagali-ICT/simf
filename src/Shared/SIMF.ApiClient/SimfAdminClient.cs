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
using SIMF.Contracts.Contacts;
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

/// <summary>
/// A typed client over the SIMF Admin API (decision D-041). Today: reset
/// another user's 2FA. The actor must hold the Administrator role; the
/// access-token check is at the API.
/// </summary>
public sealed class SimfAdminClient(HttpClient http)
{
    private const string BasePath = "api/v1/admin/";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Wipes the target user's 2FA after Administrator-role verification.</summary>
    public Task<ApiCallResult<bool>> ResetTwoFactorAsync(
        AdminResetTwoFactorRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, "admins/reset-two-factor",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>The live status of every in-process background worker, for the CP
    /// services monitor. Reads the API's heartbeat-registry snapshot.</summary>
    public Task<ApiCallResult<WorkerStatusListResponse>> GetWorkerStatusesAsync(
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<WorkerStatusListResponse>(
            HttpMethod.Get, "ops/workers", content: null,
            accessToken, cancellationToken);

    // -- P7c — three-family create + list ------------------------------------

    /// <summary>Creates a new Admin user (P7c — renamed from CreateStaffAsync).</summary>
    public Task<ApiCallResult<AdminCreateUserResponse>> CreateAdminAsync(
        AdminCreateAdminRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCreateUserResponse>(
            HttpMethod.Post, "admins",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Creates a new Other user (P7c — new).</summary>
    public Task<ApiCallResult<AdminCreateUserResponse>> CreateOtherAsync(
        AdminCreateOtherRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCreateUserResponse>(
            HttpMethod.Post, "others",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Creates a new Visitor user (P3 — P7c added optional ProfileTypeId).</summary>
    public Task<ApiCallResult<AdminCreateUserResponse>> CreateVisitorAsync(
        AdminCreateVisitorRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCreateUserResponse>(
            HttpMethod.Post, "visitors",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One page of Admin-typed accounts (P7c).</summary>
    public Task<ApiCallResult<GridPage<AdminUserSummary>>> ListAdminsAsync(
        GridQuery query,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminUserSummary>>(
            HttpMethod.Post, "admins/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One page of Other-typed accounts (P7c — new).</summary>
    public Task<ApiCallResult<GridPage<AdminUserSummary>>> ListOthersAsync(
        GridQuery query,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminUserSummary>>(
            HttpMethod.Post, "others/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One page of Visitor-typed accounts (P3 — P7c rekeyed to UserType).</summary>
    public Task<ApiCallResult<GridPage<AdminUserSummary>>> ListVisitorsAsync(
        GridQuery query,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminUserSummary>>(
            HttpMethod.Post, "visitors/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Soft-deletes one or many users (D-044 b; P7c renamed URL to /admins).</summary>
    public Task<ApiCallResult<AdminBulkDeleteResponse>> BulkDeleteUsersAsync(
        AdminBulkDeleteRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkDeleteResponse>(
            HttpMethod.Post, "admins/bulk-delete",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Creates a copy of an existing user with a new email (D-044 b).</summary>
    public Task<ApiCallResult<AdminCreateUserResponse>> DuplicateUserAsync(
        AdminDuplicateUserRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCreateUserResponse>(
            HttpMethod.Post, "admins/duplicate",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Returns the bytes of an XLSX workbook with the selected users (D-044 b).</summary>
    public async Task<(int StatusCode, byte[] Bytes)> ExportUsersAsync(
        AdminExportUsersRequest request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, BasePath + "admins/export")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return ((int)response.StatusCode, bytes);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException)
        {
            return ((int)HttpStatusCode.ServiceUnavailable, Array.Empty<byte>());
        }
    }

    /// <summary>P1.6 — XLSX of the filtered operation log.</summary>
    public Task<(int StatusCode, byte[] Bytes)> ExportOperationLogAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        PostForBytesAsync("operation-log/export", query, accessToken, cancellationToken);

    /// <summary>P1.6 — XLSX of the filtered attendee roster.</summary>
    public Task<(int StatusCode, byte[] Bytes)> ExportAttendeesAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        PostForBytesAsync("attendees/export", query, accessToken, cancellationToken);

    /// <summary>P1.6 — POSTs a JSON body and returns the raw response bytes
    /// (an XLSX workbook). Shared by the read-only-grid exports; the response
    /// body is binary, so it bypasses the <c>ApiResult</c> envelope.</summary>
    private async Task<(int StatusCode, byte[] Bytes)> PostForBytesAsync(
        string relativePath, object body, string accessToken,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, BasePath + relativePath)
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return ((int)response.StatusCode, bytes);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException)
        {
            return ((int)HttpStatusCode.ServiceUnavailable, Array.Empty<byte>());
        }
    }

    /// <summary>D-356 — generic grid XLSX export by resource slug (e.g. "interests").
    /// Posts the selected ids / grid query and returns the workbook bytes.</summary>
    public Task<(int StatusCode, byte[] Bytes)> ExportGridAsync(
        string resource,
        SIMF.Contracts.Admin.AdminGridExportRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        PostForBytesAsync($"{resource}/export", request, accessToken, cancellationToken);

    /// <summary>D-356 — generic grid XLSX import by resource slug. Multipart
    /// upload, single file field "file"; returns the per-row outcome summary.</summary>
    public async Task<ApiCallResult<SIMF.Contracts.Admin.AdminGridImportResult>> ImportGridAsync(
        string resource,
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

        using var message = new HttpRequestMessage(HttpMethod.Post, BasePath + $"{resource}/import")
        {
            Content = content,
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<ApiResult<SIMF.Contracts.Admin.AdminGridImportResult>>(
                JsonOptions, cancellationToken);
            return new ApiCallResult<SIMF.Contracts.Admin.AdminGridImportResult>(
                (int)response.StatusCode,
                body ?? TransportFailure<SIMF.Contracts.Admin.AdminGridImportResult>(
                    "The server returned an empty response.",
                    "أعاد الخادم استجابة فارغة."));
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException or JsonException)
        {
            return new ApiCallResult<SIMF.Contracts.Admin.AdminGridImportResult>(
                (int)HttpStatusCode.ServiceUnavailable,
                TransportFailure<SIMF.Contracts.Admin.AdminGridImportResult>(
                    "The SIMF service could not be reached. Please try again.",
                    "تعذّر الوصول إلى خدمة SIMF. حاول مرة أخرى."));
        }
    }

    // -- D-113 — type-scoped bulk operations for Visitors and Others ---------

    /// <summary>Soft-deletes one or many visitor accounts (D-113).</summary>
    public Task<ApiCallResult<AdminBulkDeleteResponse>> BulkDeleteVisitorsAsync(
        AdminBulkDeleteRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkDeleteResponse>(
            HttpMethod.Post, "visitors/bulk-delete",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Soft-deletes one or many Other accounts (D-113).</summary>
    public Task<ApiCallResult<AdminBulkDeleteResponse>> BulkDeleteOthersAsync(
        AdminBulkDeleteRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkDeleteResponse>(
            HttpMethod.Post, "others/bulk-delete",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Duplicates an existing visitor account (D-113).</summary>
    public Task<ApiCallResult<AdminCreateUserResponse>> DuplicateVisitorAsync(
        AdminDuplicateUserRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCreateUserResponse>(
            HttpMethod.Post, "visitors/duplicate",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Duplicates an existing Other account (D-113).</summary>
    public Task<ApiCallResult<AdminCreateUserResponse>> DuplicateOtherAsync(
        AdminDuplicateUserRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCreateUserResponse>(
            HttpMethod.Post, "others/duplicate",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Exports visitor accounts to an XLSX workbook (D-113).</summary>
    public async Task<(int StatusCode, byte[] Bytes)> ExportVisitorsAsync(
        AdminExportUsersRequest request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, BasePath + "visitors/export")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return ((int)response.StatusCode, bytes);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException)
        {
            return ((int)HttpStatusCode.ServiceUnavailable, Array.Empty<byte>());
        }
    }

    /// <summary>Exports Other accounts to an XLSX workbook (D-113).</summary>
    public async Task<(int StatusCode, byte[] Bytes)> ExportOthersAsync(
        AdminExportUsersRequest request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, BasePath + "others/export")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return ((int)response.StatusCode, bytes);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException)
        {
            return ((int)HttpStatusCode.ServiceUnavailable, Array.Empty<byte>());
        }
    }

    /// <summary>Bulk-creates visitor accounts from an XLSX workbook upload (D-113).</summary>
    public async Task<ApiCallResult<AdminImportUsersResponse>> ImportVisitorsAsync(
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

        using var message = new HttpRequestMessage(HttpMethod.Post, BasePath + "visitors/import")
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

    /// <summary>Bulk-creates Other accounts from an XLSX workbook upload (D-113).</summary>
    public async Task<ApiCallResult<AdminImportUsersResponse>> ImportOthersAsync(
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

        using var message = new HttpRequestMessage(HttpMethod.Post, BasePath + "others/import")
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

    // -- P4 + P7c — approval workflow (Admin / Other / Visitor) --------------

    /// <summary>Approves a pending Admin (P7c — renamed from ApproveStaffAsync).</summary>
    public Task<ApiCallResult<bool>> ApproveAdminAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"admins/{subjectId}/approve",
            JsonContent.Create(new { }, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Rejects a pending Admin with a free-text reason (P7c).</summary>
    public Task<ApiCallResult<bool>> RejectAdminAsync(
        Guid subjectId, AdminRejectRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"admins/{subjectId}/reject",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Approves a pending Other (P7c — new).</summary>
    public Task<ApiCallResult<bool>> ApproveOtherAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"others/{subjectId}/approve",
            JsonContent.Create(new { }, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Rejects a pending Other with a free-text reason (P7c — new).</summary>
    public Task<ApiCallResult<bool>> RejectOtherAsync(
        Guid subjectId, AdminRejectRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"others/{subjectId}/reject",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Approves a pending Visitor (P4). CS-D (D-386) — an optional
    /// <paramref name="profileTypeId"/> sets the visitor's tier as part of
    /// the approval; null sends an empty body (backward-compatible).</summary>
    public Task<ApiCallResult<bool>> ApproveVisitorAsync(
        Guid subjectId, string accessToken, Guid? profileTypeId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"visitors/{subjectId}/approve",
            JsonContent.Create(new { ProfileTypeId = profileTypeId }, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Rejects a pending Visitor with a free-text reason (P4).</summary>
    public Task<ApiCallResult<bool>> RejectVisitorAsync(
        Guid subjectId, AdminRejectRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"visitors/{subjectId}/reject",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>P1.3 (D-214) — edit a visitor (email, display name, tier).</summary>
    public Task<ApiCallResult<bool>> UpdateVisitorAsync(
        Guid id, AdminUpdateVisitorRequest body, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Put, $"visitors/{id}",
            JsonContent.Create(body, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>P1.3 (D-214) — edit a partner (Other) account.</summary>
    public Task<ApiCallResult<bool>> UpdateOtherAsync(
        Guid id, AdminUpdateOtherRequest body, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Put, $"others/{id}",
            JsonContent.Create(body, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>D-728 (owner item 9) — flip an account's type between Visitor
    /// and Other by reassigning it to a profile type in the opposite scope.</summary>
    public Task<ApiCallResult<bool>> ChangeAccountTypeAsync(
        Guid id, AdminChangeAccountTypeRequest body, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"accounts/{id}/change-type",
            JsonContent.Create(body, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>D-164 (gap doc G2) — bulk-approve a batch of pending visitors.
    /// Up to 500 ids per request; per-subject failures are reported in
    /// <see cref="AdminBulkApprovalResponse.Failures"/>.</summary>
    public Task<ApiCallResult<AdminBulkApprovalResponse>> BulkApproveVisitorsAsync(
        AdminBulkApprovalRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkApprovalResponse>(
            HttpMethod.Post, "visitors/bulk-approve",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>D-164 — bulk-approve a batch of pending Other-tier users.</summary>
    public Task<ApiCallResult<AdminBulkApprovalResponse>> BulkApproveOthersAsync(
        AdminBulkApprovalRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkApprovalResponse>(
            HttpMethod.Post, "others/bulk-approve",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>P1.3 (D-214) — bulk-approve a batch of pending admins.</summary>
    public Task<ApiCallResult<AdminBulkApprovalResponse>> BulkApproveAdminsAsync(
        AdminBulkApprovalRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkApprovalResponse>(
            HttpMethod.Post, "admins/bulk-approve",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>D-209 — bulk-reject a batch of pending visitors with a shared
    /// reason. Per-subject failures are reported in
    /// <see cref="AdminBulkRejectResponse.Failures"/>.</summary>
    public Task<ApiCallResult<AdminBulkRejectResponse>> BulkRejectVisitorsAsync(
        AdminBulkRejectRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkRejectResponse>(
            HttpMethod.Post, "visitors/bulk-reject",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>D-209 — bulk-reject a batch of pending Other-tier users.</summary>
    public Task<ApiCallResult<AdminBulkRejectResponse>> BulkRejectOthersAsync(
        AdminBulkRejectRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkRejectResponse>(
            HttpMethod.Post, "others/bulk-reject",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>P1.3 (D-214) — bulk-reject a batch of pending admins.</summary>
    public Task<ApiCallResult<AdminBulkRejectResponse>> BulkRejectAdminsAsync(
        AdminBulkRejectRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkRejectResponse>(
            HttpMethod.Post, "admins/bulk-reject",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One page of pending-approval Admins (P7c — renamed from ListPendingStaffAsync).</summary>
    public Task<ApiCallResult<GridPage<AdminPendingUserSummary>>> ListPendingAdminsAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminPendingUserSummary>>(
            HttpMethod.Post, "admins/pending/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One page of pending-approval Others (P7c — new).</summary>
    public Task<ApiCallResult<GridPage<AdminPendingUserSummary>>> ListPendingOthersAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminPendingUserSummary>>(
            HttpMethod.Post, "others/pending/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One page of pending-approval Visitors (P4).</summary>
    public Task<ApiCallResult<GridPage<AdminPendingUserSummary>>> ListPendingVisitorsAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminPendingUserSummary>>(
            HttpMethod.Post, "visitors/pending/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>D-124 — scoped read of a pending Visitor's full profile.
    /// Returns 404 (not 403) for unknown / approved / wrong-type ids; the
    /// CP approve / reject flow renders this body in a preview modal.</summary>
    public Task<ApiCallResult<PendingProfileResponse>> GetPendingVisitorProfileAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<PendingProfileResponse>(
            HttpMethod.Get, $"visitors/{subjectId}/profile-for-approval",
            content: null,
            accessToken, cancellationToken);

    /// <summary>D-124 — scoped read of a pending Other's full profile.
    /// Twin of <see cref="GetPendingVisitorProfileAsync"/>.</summary>
    public Task<ApiCallResult<PendingProfileResponse>> GetPendingOtherProfileAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<PendingProfileResponse>(
            HttpMethod.Get, $"others/{subjectId}/profile-for-approval",
            content: null,
            accessToken, cancellationToken);

    // -- D-127 — walk-in registration desk -----------------------------------

    /// <summary>D-127 — on-site walk-in visitor registration. Auto-approves
    /// and returns the minted QR id for the badge handover.</summary>
    public Task<ApiCallResult<AdminWalkInRegistrationResponse>> RegisterVisitorOnSiteAsync(
        AdminWalkInRegistrationRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminWalkInRegistrationResponse>(
            HttpMethod.Post, "visitors/register-onsite",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>D-473 (#10) — bulk-generate placeholder badges by profile type + count.</summary>
    public Task<ApiCallResult<AdminBulkGenerateBadgesResponse>> BulkGenerateBadgesAsync(
        AdminBulkGenerateBadgesRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkGenerateBadgesResponse>(
            HttpMethod.Post, "visitors/bulk-generate",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>D-758 (#10 Phase 2) — the server-paged list of persisted bulk-badge batches.</summary>
    public Task<ApiCallResult<GridPage<AdminBadgeBatchSummary>>> ListBadgeBatchesAsync(
        GridQuery query,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminBadgeBatchSummary>>(
            HttpMethod.Post, "visitors/badge-batches/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>D-758 (#10 Phase 2) — re-email a batch's QR pack to an organiser.</summary>
    public Task<ApiCallResult<AdminReEmailBadgeBatchResponse>> ReEmailBadgeBatchAsync(
        AdminReEmailBadgeBatchRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminReEmailBadgeBatchResponse>(
            HttpMethod.Post, "visitors/badge-batches/re-email",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>D-758 (#10 Phase 2) — revoke a batch (disable its accounts + mark it inactive).</summary>
    public Task<ApiCallResult<AdminRevokeBadgeBatchResponse>> RevokeBadgeBatchAsync(
        AdminRevokeBadgeBatchRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminRevokeBadgeBatchResponse>(
            HttpMethod.Post, "visitors/badge-batches/revoke",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>D-127 — on-site walk-in Other registration.</summary>
    public Task<ApiCallResult<AdminWalkInRegistrationResponse>> RegisterOtherOnSiteAsync(
        AdminWalkInRegistrationRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminWalkInRegistrationResponse>(
            HttpMethod.Post, "others/register-onsite",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // -- D-126 — broadened admin profile read (Q-G reversed) -----------------

    /// <summary>D-126 — full visitor profile, any state. 404 for unknown
    /// id or wrong UserType.</summary>
    public Task<ApiCallResult<AdminUserProfileView>> GetVisitorProfileAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminUserProfileView>(
            HttpMethod.Get, $"visitors/{subjectId}/profile",
            content: null,
            accessToken, cancellationToken);

    /// <summary>D-126 — full Other profile, any state.</summary>
    public Task<ApiCallResult<AdminUserProfileView>> GetOtherProfileAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminUserProfileView>(
            HttpMethod.Get, $"others/{subjectId}/profile",
            content: null,
            accessToken, cancellationToken);

    /// <summary>D-130 — print-bag station: resolve a QR id to the
    /// walk-in badge response so the page can render and reprint.</summary>
    public Task<ApiCallResult<AdminWalkInRegistrationResponse>> LookupByQrIdAsync(
        string qrId, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminWalkInRegistrationResponse>(
            HttpMethod.Get, $"qr-lookup/{Uri.EscapeDataString(qrId)}",
            content: null,
            accessToken, cancellationToken);

    // -- D-129 — admin ID-document upload + read -----------------------------

    /// <summary>D-129 — admin-side upload of a visitor's ID-document image.</summary>
    public Task<ApiCallResult<bool>> UploadVisitorIdDocumentAsync(
        Guid subjectId, byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default) =>
        UploadIdDocumentAsync(
            $"visitors/{subjectId}/id-document",
            content, contentType, fileName, accessToken, cancellationToken);

    /// <summary>D-129 — admin-side upload of an Other account's ID-document image.</summary>
    public Task<ApiCallResult<bool>> UploadOtherIdDocumentAsync(
        Guid subjectId, byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default) =>
        UploadIdDocumentAsync(
            $"others/{subjectId}/id-document",
            content, contentType, fileName, accessToken, cancellationToken);

    private Task<ApiCallResult<bool>> UploadIdDocumentAsync(
        string path, byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        return SendAsync<bool>(
            HttpMethod.Post, path, multipart, accessToken, cancellationToken);
    }

    /// <summary>V-1 (D-429) — admin-side upload of a visitor's VVIP/VIP welcome
    /// photo (موج). Multipart "file"; the API returns a plain bool envelope.</summary>
    public Task<ApiCallResult<bool>> UploadVisitorVipPhotoAsync(
        Guid subjectId, byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default) =>
        UploadIdDocumentAsync(
            $"visitors/{subjectId}/vip-photo",
            content, contentType, fileName, accessToken, cancellationToken);

    /// <summary>D-427 (CS-3) — admin-side upload of a visitor's profile photo (avatar).</summary>
    public Task<ApiCallResult<AvatarResponse>> UploadVisitorAvatarAsync(
        Guid subjectId, byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default) =>
        UploadAvatarAsync(
            $"visitors/{subjectId}/avatar",
            content, contentType, fileName, accessToken, cancellationToken);

    /// <summary>D-427 (CS-3) — admin-side upload of an Other account's profile photo (avatar).</summary>
    public Task<ApiCallResult<AvatarResponse>> UploadOtherAvatarAsync(
        Guid subjectId, byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default) =>
        UploadAvatarAsync(
            $"others/{subjectId}/avatar",
            content, contentType, fileName, accessToken, cancellationToken);

    private Task<ApiCallResult<AvatarResponse>> UploadAvatarAsync(
        string path, byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        return SendAsync<AvatarResponse>(
            HttpMethod.Post, path, multipart, accessToken, cancellationToken);
    }

    /// <summary>D-129 — admin-side streamed read of a visitor's ID-document image.</summary>
    public Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchVisitorIdDocumentAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        FetchIdDocumentAsync($"visitors/{subjectId}/id-document", accessToken, cancellationToken);

    /// <summary>D-129 — admin-side streamed read of an Other account's ID-document image.</summary>
    public Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchOtherIdDocumentAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        FetchIdDocumentAsync($"others/{subjectId}/id-document", accessToken, cancellationToken);

    /// <summary>CS-4 — admin streamed read of a visitor's profile photo (avatar).
    /// Reuses the generic byte-fetch helper below.</summary>
    public Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchVisitorAvatarAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        FetchIdDocumentAsync($"visitors/{subjectId}/avatar", accessToken, cancellationToken);

    /// <summary>CS-4 — admin streamed read of an Other account's profile photo (avatar).</summary>
    public Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchOtherAvatarAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        FetchIdDocumentAsync($"others/{subjectId}/avatar", accessToken, cancellationToken);

    /// <summary>D-357 — admin streamed read of an admin account's profile photo
    /// (avatar) for the Admins-list thumbnail. Gated by Admins.View.</summary>
    public Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchAdminAvatarAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        FetchIdDocumentAsync($"admins/{subjectId}/avatar", accessToken, cancellationToken);

    /// <summary>V-1 (D-429) — admin streamed read of a visitor's VVIP/VIP welcome
    /// photo (موج). Reuses the generic byte-fetch helper.</summary>
    public Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchVisitorVipPhotoAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        FetchIdDocumentAsync($"visitors/{subjectId}/vip-photo", accessToken, cancellationToken);

    /// <summary>V-1 (D-429) — the VVIP/VIP welcome roster (موج) as JSON.</summary>
    public Task<ApiCallResult<IReadOnlyList<VipRosterRow>>> GetVipRosterAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<VipRosterRow>>(
            HttpMethod.Get, "visitors/vip/roster", content: null,
            accessToken, cancellationToken);

    /// <summary>V-1 (D-429) — one page of the VVIP/VIP roster for the CP export grid.</summary>
    public Task<ApiCallResult<GridPage<VipRosterRow>>> ListVipRosterAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<VipRosterRow>>(
            HttpMethod.Post, "visitors/vip/roster/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>V-1 (D-429) — download the VVIP/VIP welcome roster (موج) as a
    /// CSV or Excel file. Reuses the generic byte-fetch helper.</summary>
    public Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchVipRosterFileAsync(
        string format, string accessToken, CancellationToken cancellationToken = default) =>
        FetchIdDocumentAsync(
            $"visitors/vip/roster/export?format={Uri.EscapeDataString(format)}",
            accessToken, cancellationToken);

    private async Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchIdDocumentAsync(
        string path, string accessToken, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Get, $"{BasePath}{path}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ((int)response.StatusCode, null, []);
            }
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return (
                (int)response.StatusCode,
                response.Content.Headers.ContentType?.MediaType,
                bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException)
        {
            return ((int)HttpStatusCode.ServiceUnavailable, null, []);
        }
    }

    /// <summary>ProfileTypes filtered by UserType — for the create-page picker (P7c).</summary>
    public Task<ApiCallResult<IReadOnlyList<AdminProfileTypeSummary>>> ListProfileTypesAsync(
        string userType, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminProfileTypeSummary>>(
            HttpMethod.Get, $"profile-types?userType={Uri.EscapeDataString(userType)}",
            content: null,
            accessToken, cancellationToken);

    // -- P9 — Interests CRUD (الاهتمامات) -----------------------------------

    /// <summary>One page of interests for the admin grid (P9 — D-050).</summary>
    public Task<ApiCallResult<GridPage<AdminInterestSummary>>> ListInterestsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminInterestSummary>>(
            HttpMethod.Post, "interests/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One interest by id (P9 — D-050).</summary>
    public Task<ApiCallResult<AdminInterestSummary>> GetInterestAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminInterestSummary>(
            HttpMethod.Get, $"interests/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>Creates an interest (P9 — D-050).</summary>
    public Task<ApiCallResult<AdminInterestSummary>> CreateInterestAsync(
        AdminCreateInterestRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminInterestSummary>(
            HttpMethod.Post, "interests",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Updates an interest (P9 — D-050).</summary>
    public Task<ApiCallResult<AdminInterestSummary>> UpdateInterestAsync(
        Guid id, AdminUpdateInterestRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminInterestSummary>(
            HttpMethod.Put, $"interests/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Soft-deletes (deactivates) an interest (P9 — D-050).</summary>
    public Task<ApiCallResult<bool>> DeactivateInterestAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"interests/{id}", content: null,
            accessToken, cancellationToken);

    // -- P2.1 (D-211) — FAQ management (two-level group → entry) --

    public Task<ApiCallResult<GridPage<AdminFaqGroupSummary>>> ListFaqGroupsAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminFaqGroupSummary>>(HttpMethod.Post, "faq/groups/list",
            JsonContent.Create(query, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminFaqGroupSummary>> GetFaqGroupAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminFaqGroupSummary>(HttpMethod.Get, $"faq/groups/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminFaqGroupSummary>> CreateFaqGroupAsync(
        CreateFaqGroupRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminFaqGroupSummary>(HttpMethod.Post, "faq/groups",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminFaqGroupSummary>> UpdateFaqGroupAsync(
        Guid id, UpdateFaqGroupRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminFaqGroupSummary>(HttpMethod.Put, $"faq/groups/{id}",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteFaqGroupAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(HttpMethod.Delete, $"faq/groups/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<AdminFaqEntrySummary>>> ListFaqEntriesAsync(
        Guid groupId, GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminFaqEntrySummary>>(HttpMethod.Post, $"faq/groups/{groupId}/entries/list",
            JsonContent.Create(query, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminFaqEntrySummary>> CreateFaqEntryAsync(
        CreateFaqEntryRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminFaqEntrySummary>(HttpMethod.Post, "faq/entries",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminFaqEntrySummary>> UpdateFaqEntryAsync(
        Guid id, UpdateFaqEntryRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminFaqEntrySummary>(HttpMethod.Put, $"faq/entries/{id}",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteFaqEntryAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(HttpMethod.Delete, $"faq/entries/{id}", content: null,
            accessToken, cancellationToken);

    // -- Rating configuration (types → groups → questions) ------------------

    public Task<ApiCallResult<GridPage<AdminRatingTypeSummary>>> ListRatingTypesAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminRatingTypeSummary>>(HttpMethod.Post, "ratings/types/list",
            JsonContent.Create(query, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingTypeSummary>> GetRatingTypeAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingTypeSummary>(HttpMethod.Get, $"ratings/types/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingTypeSummary>> CreateRatingTypeAsync(
        CreateRatingTypeRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingTypeSummary>(HttpMethod.Post, "ratings/types",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingTypeSummary>> UpdateRatingTypeAsync(
        Guid id, UpdateRatingTypeRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingTypeSummary>(HttpMethod.Put, $"ratings/types/{id}",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteRatingTypeAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(HttpMethod.Delete, $"ratings/types/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<AdminRatingQuestionGroupSummary>>> ListRatingGroupsAsync(
        Guid typeId, GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminRatingQuestionGroupSummary>>(HttpMethod.Post, $"ratings/types/{typeId}/groups/list",
            JsonContent.Create(query, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingQuestionGroupSummary>> GetRatingGroupAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingQuestionGroupSummary>(HttpMethod.Get, $"ratings/groups/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingQuestionGroupSummary>> CreateRatingGroupAsync(
        CreateRatingQuestionGroupRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingQuestionGroupSummary>(HttpMethod.Post, "ratings/groups",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingQuestionGroupSummary>> UpdateRatingGroupAsync(
        Guid id, UpdateRatingQuestionGroupRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingQuestionGroupSummary>(HttpMethod.Put, $"ratings/groups/{id}",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteRatingGroupAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(HttpMethod.Delete, $"ratings/groups/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<AdminRatingQuestionSummary>>> ListRatingQuestionsAsync(
        Guid typeId, GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminRatingQuestionSummary>>(HttpMethod.Post, $"ratings/types/{typeId}/questions/list",
            JsonContent.Create(query, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingQuestionSummary>> GetRatingQuestionAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingQuestionSummary>(HttpMethod.Get, $"ratings/questions/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingQuestionSummary>> CreateRatingQuestionAsync(
        CreateRatingQuestionRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingQuestionSummary>(HttpMethod.Post, "ratings/questions",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRatingQuestionSummary>> UpdateRatingQuestionAsync(
        Guid id, UpdateRatingQuestionRequest request, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminRatingQuestionSummary>(HttpMethod.Put, $"ratings/questions/{id}",
            JsonContent.Create(request, options: JsonOptions), accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteRatingQuestionAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(HttpMethod.Delete, $"ratings/questions/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-134 Sprint A — Roles admin CRUD (existing schema, no migration) --

    /// <summary>One page of roles for the admin grid (D-134 Sprint A).</summary>
    public Task<ApiCallResult<GridPage<AdminRoleSummary>>> ListRolesAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminRoleSummary>>(
            HttpMethod.Post, "roles/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One role by id (D-134 Sprint A).</summary>
    public Task<ApiCallResult<AdminRoleSummary>> GetRoleAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminRoleSummary>(
            HttpMethod.Get, $"roles/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>Creates a custom role (D-134 Sprint A).</summary>
    public Task<ApiCallResult<AdminRoleSummary>> CreateRoleAsync(
        AdminCreateRoleRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminRoleSummary>(
            HttpMethod.Post, "roles",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Renames a custom role (D-134 Sprint A).</summary>
    public Task<ApiCallResult<AdminRoleSummary>> UpdateRoleAsync(
        Guid id, AdminUpdateRoleRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminRoleSummary>(
            HttpMethod.Put, $"roles/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Deletes a custom role (D-134 Sprint A). Refused for
    /// baseline roles or roles still held by any user.</summary>
    public Task<ApiCallResult<bool>> DeleteRoleAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"roles/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>Issue-1 — a role's currently granted permission codes.</summary>
    public Task<ApiCallResult<AdminRolePermissionsResponse>> GetRolePermissionsAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminRolePermissionsResponse>(
            HttpMethod.Get, $"roles/{id}/permissions", content: null,
            accessToken, cancellationToken);

    /// <summary>Issue-1 — replaces a custom role's permission grants.</summary>
    public Task<ApiCallResult<bool>> SetRolePermissionsAsync(
        Guid id, AdminSetRolePermissionsRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Put, $"roles/{id}/permissions",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Issue-1 — the RBAC roles an admin user currently holds.</summary>
    public Task<ApiCallResult<AdminUserRolesResponse>> GetUserRolesAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminUserRolesResponse>(
            HttpMethod.Get, $"admins/{id}/roles", content: null,
            accessToken, cancellationToken);

    /// <summary>Issue-1 — replaces an admin user's RBAC roles.</summary>
    public Task<ApiCallResult<bool>> SetUserRolesAsync(
        Guid id, AdminSetUserRolesRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Put, $"admins/{id}/roles",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // -- D-134 Sprint A — Operation log read-only viewer ---------------------

    /// <summary>One page of OperationLog entries (D-134 Sprint A). Filters
    /// via <c>GridQuery.Filters</c>: eventType, outcome, actorUserId,
    /// subjectEmail, from, to.</summary>
    public Task<ApiCallResult<GridPage<AdminOperationLogSummary>>> ListOperationLogAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminOperationLogSummary>>(
            HttpMethod.Post, "operation-log/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One full OperationLog entry by id (D-134 Sprint A).</summary>
    public Task<ApiCallResult<AdminOperationLogDetail>> GetOperationLogAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminOperationLogDetail>(
            HttpMethod.Get, $"operation-log/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-134 Sprint A — Attendees roster (read-only) -----------------------

    /// <summary>One page of the attendee roster (D-134 Sprint A). Filters
    /// via <c>GridQuery.Filters</c>: userType (Visitor|Other|All),
    /// profileTypeId, accountState.</summary>
    public Task<ApiCallResult<GridPage<AdminAttendeeSummary>>> ListAttendeesAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminAttendeeSummary>>(
            HttpMethod.Post, "attendees/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    // -- D-134 Sprint B — Themes CRUD (D-135 freeze-lift) --------------------

    /// <summary>One page of themes (D-134 Sprint B).</summary>
    public Task<ApiCallResult<GridPage<AdminThemeSummary>>> ListThemesAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminThemeSummary>>(
            HttpMethod.Post, "themes/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One theme by id (D-134 Sprint B).</summary>
    public Task<ApiCallResult<AdminThemeDetail>> GetThemeAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminThemeDetail>(
            HttpMethod.Get, $"themes/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>Creates a theme (D-134 Sprint B).</summary>
    public Task<ApiCallResult<AdminThemeDetail>> CreateThemeAsync(
        AdminCreateThemeRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminThemeDetail>(
            HttpMethod.Post, "themes",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Updates a theme (D-134 Sprint B).</summary>
    public Task<ApiCallResult<AdminThemeDetail>> UpdateThemeAsync(
        Guid id, AdminUpdateThemeRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminThemeDetail>(
            HttpMethod.Put, $"themes/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Soft-deletes (deactivates) a theme (D-134 Sprint B).</summary>
    public Task<ApiCallResult<bool>> DeactivateThemeAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"themes/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-134 Sprint B — Halls CRUD ----------------------------------------

    public Task<ApiCallResult<GridPage<AdminHallSummary>>> ListHallsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminHallSummary>>(
            HttpMethod.Post, "halls/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminHallDetail>> GetHallAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminHallDetail>(
            HttpMethod.Get, $"halls/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminHallDetail>> CreateHallAsync(
        AdminCreateHallRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminHallDetail>(
            HttpMethod.Post, "halls",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminHallDetail>> UpdateHallAsync(
        Guid id, AdminUpdateHallRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminHallDetail>(
            HttpMethod.Put, $"halls/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateHallAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"halls/{id}", content: null,
            accessToken, cancellationToken);

    // -- SIMF-FDS-013 (D-248) — meeting tables + hall allocations + meetings -

    public Task<ApiCallResult<bool>> SetHallPurposeAsync(
        Guid hallId, SetHallPurposeRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Put, $"halls/{hallId}/purpose",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<MeetingTableRow>>> ListMeetingTablesAsync(
        Guid hallId, GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<MeetingTableRow>>(
            HttpMethod.Post, $"halls/{hallId}/meeting-tables/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<MeetingTableRow>> CreateMeetingTableAsync(
        Guid hallId, CreateMeetingTableRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<MeetingTableRow>(
            HttpMethod.Post, $"halls/{hallId}/meeting-tables",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<MeetingTableRow>> UpdateMeetingTableAsync(
        Guid id, UpdateMeetingTableRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<MeetingTableRow>(
            HttpMethod.Put, $"meeting-tables/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteMeetingTableAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"meeting-tables/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<MeetingTablesGenerated>> GenerateMeetingTablesAsync(
        Guid hallId, GenerateMeetingTablesRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<MeetingTablesGenerated>(
            HttpMethod.Post, $"halls/{hallId}/meeting-tables/generate",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<HallAllocationRow>>> ListHallAllocationsAsync(
        Guid hallId, GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<HallAllocationRow>>(
            HttpMethod.Post, $"halls/{hallId}/hall-allocations/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<HallAllocationRow>> CreateHallAllocationAsync(
        Guid hallId, CreateHallAllocationRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<HallAllocationRow>(
            HttpMethod.Post, $"halls/{hallId}/hall-allocations",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> ReleaseHallAllocationAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"hall-allocations/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<BusinessMeetingRow>>> ListBusinessMeetingsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<BusinessMeetingRow>>(
            HttpMethod.Post, "business-meetings/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<BusinessMeetingDetail>> GetBusinessMeetingAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<BusinessMeetingDetail>(
            HttpMethod.Get, $"business-meetings/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<BusinessMeetingScheduled>> ScheduleBusinessMeetingAsync(
        ScheduleMeetingRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<BusinessMeetingScheduled>(
            HttpMethod.Post, "business-meetings",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> CancelBusinessMeetingAsync(
        Guid id, CancelMeetingRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"business-meetings/{id}/cancel",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // -- D-151 — Country admin lookup CRUD ----------------------------------

    public Task<ApiCallResult<GridPage<AdminCountrySummary>>> ListCountriesAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminCountrySummary>>(
            HttpMethod.Post, "countries/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminCountryDetail>> GetCountryAsync(
        int id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCountryDetail>(
            HttpMethod.Get, $"countries/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminCountryDetail>> CreateCountryAsync(
        AdminCreateCountryRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCountryDetail>(
            HttpMethod.Post, "countries",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminCountryDetail>> UpdateCountryAsync(
        int id, AdminUpdateCountryRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCountryDetail>(
            HttpMethod.Put, $"countries/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateCountryAsync(
        int id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"countries/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-547 — Region admin lookup CRUD (mirrors the Country block; Guid key) --

    public Task<ApiCallResult<GridPage<AdminRegionSummary>>> ListRegionsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminRegionSummary>>(
            HttpMethod.Post, "regions/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRegionDetail>> GetRegionAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminRegionDetail>(
            HttpMethod.Get, $"regions/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRegionDetail>> CreateRegionAsync(
        CreateRegionRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminRegionDetail>(
            HttpMethod.Post, "regions",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminRegionDetail>> UpdateRegionAsync(
        Guid id, UpdateRegionRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminRegionDetail>(
            HttpMethod.Put, $"regions/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateRegionAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"regions/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-649 — Contact-inquiries inbox + Site-settings + Country delegates --
    //    (pages + API shipped, but the CP client/BFF wiring was never added). --

    public Task<ApiCallResult<GridPage<AdminContactInquiryRow>>> ListContactInquiriesAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminContactInquiryRow>>(
            HttpMethod.Post, "contact-inquiries/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> MarkContactInquiryHandledAsync(
        Guid id, bool handled, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"contact-inquiries/{id}/handled",
            JsonContent.Create(new { handled }, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<SiteSettingsResponse>> GetSiteSettingsAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<SiteSettingsResponse>(
            HttpMethod.Get, "site-settings", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<SiteSettingsResponse>> UpdateSiteSettingsAsync(
        AdminUpdateSiteSettingsRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<SiteSettingsResponse>(
            HttpMethod.Put, "site-settings",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<AdminCountryDelegateOption>>> ListCountryDelegatesAsync(
        int countryId, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminCountryDelegateOption>>(
            HttpMethod.Get, $"countries/{countryId}/delegates", content: null,
            accessToken, cancellationToken);

    // -- D-153 — Speaker admin CRUD -----------------------------------------

    public Task<ApiCallResult<GridPage<AdminSpeakerSummary>>> ListSpeakersAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminSpeakerSummary>>(
            HttpMethod.Post, "speakers/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSpeakerDetail>> GetSpeakerAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSpeakerDetail>(
            HttpMethod.Get, $"speakers/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSpeakerDetail>> CreateSpeakerAsync(
        AdminCreateSpeakerRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSpeakerDetail>(
            HttpMethod.Post, "speakers",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSpeakerDetail>> UpdateSpeakerAsync(
        Guid id, AdminUpdateSpeakerRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSpeakerDetail>(
            HttpMethod.Put, $"speakers/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateSpeakerAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"speakers/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-165 (gap doc G3) — Session admin CRUD -----------------------------

    // NOTE: returns AdminSessionSummary (the sessions GRID row — Code/Title/Hall/
    // times/capacity/status), NOT AdminSessionSummaryDetail (the AI session-summary
    // detail). The two names collide and the wrong one was bound here, which made
    // /admin/sessions deserialize every row to defaults (blank code, 0001 dates,
    // inactive). The CP SessionsList grid is TItem="AdminSessionSummary".
    public Task<ApiCallResult<GridPage<AdminSessionSummary>>> ListSessionsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminSessionSummary>>(
            HttpMethod.Post, "sessions/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionDetail>> GetSessionAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionDetail>(
            HttpMethod.Get, $"sessions/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionDetail>> CreateSessionAsync(
        AdminCreateSessionRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionDetail>(
            HttpMethod.Post, "sessions",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionDetail>> UpdateSessionAsync(
        Guid id, AdminUpdateSessionRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionDetail>(
            HttpMethod.Put, $"sessions/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateSessionAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"sessions/{id}", content: null,
            accessToken, cancellationToken);

    // D-578 — server-side subtitle fetch from a session's video (YouTube).
    public Task<ApiCallResult<FetchSubtitleResponse>> FetchSessionSubtitleAsync(
        FetchSubtitleRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<FetchSubtitleResponse>(
            HttpMethod.Post, "sessions/subtitle/fetch-from-video",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // P3.2 — D-231: session broadcast-lifecycle transition.
    public Task<ApiCallResult<AdminSessionDetail>> SetSessionStatusAsync(
        Guid id, SetSessionStatusRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionDetail>(
            HttpMethod.Put, $"sessions/{id}/status",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // P3.2b — D-232: attach the session recording. The body is STREAMED (not a
    // byte[]) so a large video is not buffered whole in the CP — the caller's
    // stream is forwarded straight through to the API.
    public Task<ApiCallResult<AdminSessionDetail>> UploadSessionRecordingAsync(
        Guid id, Stream content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        return SendAsync<AdminSessionDetail>(
            HttpMethod.Post, $"sessions/{id}/recording",
            multipart, accessToken, cancellationToken);
    }

    public Task<ApiCallResult<AdminSessionDetail>> DeleteSessionRecordingAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionDetail>(
            HttpMethod.Delete, $"sessions/{id}/recording", content: null,
            accessToken, cancellationToken);

    // -- D-166 (gap doc G4) — registration gate + archive visibility -------

    public Task<ApiCallResult<RegistrationGateState>> GetRegistrationGateAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<RegistrationGateState>(
            HttpMethod.Get, "registration-gate", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<RegistrationGateState>> UpdateRegistrationGateAsync(
        UpdateRegistrationGateRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<RegistrationGateState>(
            HttpMethod.Put, "registration-gate",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<ArchiveVisibilityState>> GetArchiveVisibilityAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<ArchiveVisibilityState>(
            HttpMethod.Get, "archive/visibility", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<ArchiveVisibilityState>> UpdateArchiveVisibilityAsync(
        UpdateArchiveVisibilityRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<ArchiveVisibilityState>(
            HttpMethod.Put, "archive/visibility",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // -- D-168 (gap doc G5) — public-relations: invitations + VIPs ----------

    public Task<ApiCallResult<GridPage<AdminInvitationSummary>>> ListInvitationsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminInvitationSummary>>(
            HttpMethod.Post, "invitations/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminInvitationDetail>> GetInvitationAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminInvitationDetail>(
            HttpMethod.Get, $"invitations/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminInvitationDetail>> CreateInvitationAsync(
        AdminCreateInvitationRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminInvitationDetail>(
            HttpMethod.Post, "invitations",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminInvitationDetail>> UpdateInvitationAsync(
        Guid id, AdminUpdateInvitationRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminInvitationDetail>(
            HttpMethod.Put, $"invitations/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateInvitationAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"invitations/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<AdminVipSummary>>> ListVipsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminVipSummary>>(
            HttpMethod.Post, "vips/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminNotifyVipsResult>> NotifyVipsAsync(
        AdminNotifyVipsRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminNotifyVipsResult>(
            HttpMethod.Post, "vips/notify",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // -- D-173 (gap doc G8) — Dynamic content CMS ---------------------------

    public Task<ApiCallResult<GridPage<AdminContentBlockSummary>>> ListContentBlocksAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminContentBlockSummary>>(
            HttpMethod.Post, "content-blocks/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminContentBlockSummary>> GetContentBlockAsync(
        string key, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminContentBlockSummary>(
            HttpMethod.Get, $"content-blocks/{Uri.EscapeDataString(key)}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminContentBlockSummary>> UpsertContentBlockAsync(
        UpsertContentBlockRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminContentBlockSummary>(
            HttpMethod.Put, "content-blocks",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteContentBlockAsync(
        string key, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"content-blocks/{Uri.EscapeDataString(key)}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<AdminBannerSummary>>> ListBannersAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminBannerSummary>>(
            HttpMethod.Post, "banners/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBannerDetail>> GetBannerAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBannerDetail>(
            HttpMethod.Get, $"banners/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBannerDetail>> CreateBannerAsync(
        CreateBannerRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBannerDetail>(
            HttpMethod.Post, "banners",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBannerDetail>> UpdateBannerAsync(
        Guid id, UpdateBannerRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBannerDetail>(
            HttpMethod.Put, $"banners/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteBannerAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"banners/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-169 (gap doc G6) — session-question moderation -------------------

    public Task<ApiCallResult<GridPage<AdminSessionModeratorRow>>> ListSessionModeratorsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminSessionModeratorRow>>(
            HttpMethod.Post, "session-moderators/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionModeratorRow>> AssignSessionModeratorAsync(
        AssignSessionModeratorRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionModeratorRow>(
            HttpMethod.Post, "session-moderators",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> RevokeSessionModeratorAsync(
        Guid sessionId, Guid userId, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"session-moderators/{sessionId}/{userId}", content: null,
            accessToken, cancellationToken);

    // P3.3 — D-234: Scientific-Committee Q&A queue (admin base, /admin/questions/*).
    public Task<ApiCallResult<IReadOnlyList<SIMF.Contracts.Sessions.SessionQuestionQueueRow>>>
        ListQuestionQueueAsync(
            SIMF.Common.Enums.QuestionStatus? status, Guid? sessionId,
            string accessToken, CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        if (status is { } s) { parts.Add($"status={s}"); }
        if (sessionId is { } sid) { parts.Add($"sessionId={sid}"); }
        var qs = parts.Count > 0 ? "?" + string.Join("&", parts) : string.Empty;
        return SendAsync<IReadOnlyList<SIMF.Contracts.Sessions.SessionQuestionQueueRow>>(
            HttpMethod.Get, $"questions/queue{qs}", content: null,
            accessToken, cancellationToken);
    }

    public Task<ApiCallResult<SIMF.Contracts.Sessions.SessionQuestionQueueRow>>
        ApproveQuestionAsync(Guid questionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Sessions.SessionQuestionQueueRow>(
            HttpMethod.Put, $"questions/{questionId}/approve", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Sessions.SessionQuestionQueueRow>>
        HideQuestionFromQueueAsync(Guid questionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Sessions.SessionQuestionQueueRow>(
            HttpMethod.Put, $"questions/{questionId}/hide", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Sessions.SessionQuestionQueueRow>>
        EscalateQuestionAsync(Guid questionId, SIMF.Contracts.Sessions.EscalateQuestionRequest request,
            string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Sessions.SessionQuestionQueueRow>(
            HttpMethod.Put, $"questions/{questionId}/escalate",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // P4.1 — D-238: AI session-summary / محضر committee desk (/admin/session-summaries/*).
    public Task<ApiCallResult<IReadOnlyList<AdminSessionSummaryRow>>>
        ListSessionSummariesAsync(string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminSessionSummaryRow>>(
            HttpMethod.Get, "session-summaries", content: null, accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionSummaryDetail>>
        GetSessionSummaryAsync(Guid sessionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionSummaryDetail>(
            HttpMethod.Get, $"session-summaries/{sessionId}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionSummaryDetail>>
        GenerateSessionSummaryAsync(Guid sessionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionSummaryDetail>(
            HttpMethod.Post, $"session-summaries/{sessionId}/generate", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionSummaryDetail>>
        SaveSessionSummaryAsync(Guid sessionId, SaveSessionSummaryRequest request,
            string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionSummaryDetail>(
            HttpMethod.Put, $"session-summaries/{sessionId}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionSummaryDetail>>
        PublishSessionSummaryAsync(Guid sessionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionSummaryDetail>(
            HttpMethod.Put, $"session-summaries/{sessionId}/publish", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionSummaryDetail>>
        UnpublishSessionSummaryAsync(Guid sessionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionSummaryDetail>(
            HttpMethod.Put, $"session-summaries/{sessionId}/unpublish", content: null,
            accessToken, cancellationToken);

    // D-472 (#9) — the team review/approval workflow.
    public Task<ApiCallResult<AdminSessionSummaryDetail>>
        SubmitSessionSummaryForReviewAsync(Guid sessionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionSummaryDetail>(
            HttpMethod.Put, $"session-summaries/{sessionId}/submit-review", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionSummaryDetail>>
        ApproveSessionSummaryAsync(Guid sessionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionSummaryDetail>(
            HttpMethod.Put, $"session-summaries/{sessionId}/approve", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionSummaryDetail>>
        ReturnSessionSummaryToDraftAsync(Guid sessionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionSummaryDetail>(
            HttpMethod.Put, $"session-summaries/{sessionId}/return-to-draft", content: null,
            accessToken, cancellationToken);

    // P5.1d — D-244: operator hall-door QR arrival (/admin/sessions/{id}/arrivals).
    public Task<ApiCallResult<SIMF.Contracts.Sessions.QrArrivalResult>>
        RecordQrArrivalAsync(Guid sessionId, SIMF.Contracts.Sessions.RecordQrArrivalRequest request,
            string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Sessions.QrArrivalResult>(
            HttpMethod.Post, $"sessions/{sessionId}/arrivals",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // 2026-07-18: operator hall-door QR departure / check-out (/admin/sessions/{id}/departures).
    public Task<ApiCallResult<SIMF.Contracts.Sessions.QrArrivalResult>>
        RecordQrDepartureAsync(Guid sessionId, SIMF.Contracts.Sessions.RecordQrArrivalRequest request,
            string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Sessions.QrArrivalResult>(
            HttpMethod.Post, $"sessions/{sessionId}/departures",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<SIMF.Contracts.Sessions.SessionQuestionModeratorRow>>>
        ListModeratorQueueAsync(Guid sessionId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendSessionsAsync<IReadOnlyList<SIMF.Contracts.Sessions.SessionQuestionModeratorRow>>(
            HttpMethod.Get, $"{sessionId}/questions/moderate", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Sessions.SessionQuestionModeratorRow>>
        HideQuestionAsync(Guid sessionId, Guid questionId, bool isHidden,
            string accessToken, CancellationToken cancellationToken = default) =>
        SendSessionsAsync<SIMF.Contracts.Sessions.SessionQuestionModeratorRow>(
            HttpMethod.Put, $"{sessionId}/questions/{questionId}/hide",
            JsonContent.Create(new { isHidden }, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<SIMF.Contracts.Sessions.SessionQuestionModeratorRow>>
        PushQuestionAsync(Guid sessionId, Guid questionId,
            string accessToken, CancellationToken cancellationToken = default) =>
        SendSessionsAsync<SIMF.Contracts.Sessions.SessionQuestionModeratorRow>(
            HttpMethod.Put, $"{sessionId}/questions/{questionId}/push",
            JsonContent.Create(new { }, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> ReorderQuestionsAsync(
        Guid sessionId, IReadOnlyList<Guid> orderedIds, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendSessionsAsync<bool>(
            HttpMethod.Put, $"{sessionId}/questions/reorder",
            JsonContent.Create(new { orderedQuestionIds = orderedIds }, options: JsonOptions),
            accessToken, cancellationToken);

    // -- D-148 — Gate Module admin CRUD + reports ---------------------------

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

    // -- D-148 — Gate Module operator surface --------------------------------

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

    // -- D-115 — ProfileTypes CRUD (admin-managed lookup table) --------------

    /// <summary>One page of profile types for the admin grid (D-115).</summary>
    public Task<ApiCallResult<GridPage<AdminProfileTypeSummary>>> ListAdminProfileTypesAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminProfileTypeSummary>>(
            HttpMethod.Post, "profile-types/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One profile type by id (D-115).</summary>
    public Task<ApiCallResult<AdminProfileTypeSummary>> GetAdminProfileTypeAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminProfileTypeSummary>(
            HttpMethod.Get, $"profile-types/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>Creates a profile type (D-115).</summary>
    public Task<ApiCallResult<AdminProfileTypeSummary>> CreateAdminProfileTypeAsync(
        AdminCreateProfileTypeRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminProfileTypeSummary>(
            HttpMethod.Post, "profile-types",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Updates a profile type (D-115). UserType is immutable post-creation.</summary>
    public Task<ApiCallResult<AdminProfileTypeSummary>> UpdateAdminProfileTypeAsync(
        Guid id, AdminUpdateProfileTypeRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminProfileTypeSummary>(
            HttpMethod.Put, $"profile-types/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Soft-deletes (deactivates) a profile type (D-115). 409 if in use.</summary>
    public Task<ApiCallResult<bool>> DeactivateAdminProfileTypeAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"profile-types/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>Lists every project's log files (P6).</summary>
    public Task<ApiCallResult<LogListResponse>> ListLogsAsync(
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<LogListResponse>(
            HttpMethod.Get, "logs/list",
            content: null,
            accessToken, cancellationToken);

    /// <summary>Returns the last <paramref name="lines"/> of one log file (P6).</summary>
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

    /// <summary>Streams one full log file (P6).</summary>
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

    /// <summary>Bulk-creates users from an XLSX workbook upload (D-044 b).</summary>
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

    /// <summary>D-148 — operator-surface helper. Routes through the
    /// <c>/api/v1/app/gates/</c> prefix rather than <c>/api/v1/admin/</c>.</summary>
    private Task<ApiCallResult<T>> SendOperatorAsync<T>(
        HttpMethod method, string path, HttpContent? content,
        string accessToken, CancellationToken cancellationToken) =>
        SendWithBaseAsync<T>("api/v1/app/gates/", method, path, content,
            accessToken, cancellationToken);

    /// <summary>D-169 (gap doc G6) — sessions/questions helper. Routes
    /// through the <c>/api/v1/app/sessions/</c> prefix.</summary>
    private Task<ApiCallResult<T>> SendSessionsAsync<T>(
        HttpMethod method, string path, HttpContent? content,
        string accessToken, CancellationToken cancellationToken) =>
        SendWithBaseAsync<T>("api/v1/app/sessions/", method, path, content,
            accessToken, cancellationToken);

    // -- D-495 — Organization / About profile editor --------------------------

    /// <summary>D-495 — read the full Organization Profile (incl. child-row ids)
    /// for the CP editor. Gated by OrganizationProfile.View.</summary>
    public Task<ApiCallResult<OrganizationProfileResponse>> GetOrganizationProfileAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<OrganizationProfileResponse>(
            HttpMethod.Get, "organization-profile", null, accessToken, cancellationToken);

    /// <summary>D-495 — save the Organization Profile (full-document upsert).
    /// Gated by OrganizationProfile.Manage.</summary>
    public Task<ApiCallResult<OrganizationProfileResponse>> SaveOrganizationProfileAsync(
        AdminUpdateOrganizationProfileRequest request,
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<OrganizationProfileResponse>(
            HttpMethod.Put, "organization-profile",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    private Task<ApiCallResult<T>> SendAsync<T>(
        HttpMethod method, string path, HttpContent? content,
        string accessToken, CancellationToken cancellationToken) =>
        SendWithBaseAsync<T>(BasePath, method, path, content, accessToken, cancellationToken);

    private async Task<ApiCallResult<T>> SendWithBaseAsync<T>(
        string basePath, HttpMethod method, string path, HttpContent? content,
        string accessToken, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, basePath + path) { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<ApiResult<T>>(
                JsonOptions, cancellationToken);
            return new ApiCallResult<T>(
                (int)response.StatusCode,
                body ?? TransportFailure<T>(
                    "The server returned an empty response.",
                    "أعاد الخادم استجابة فارغة."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException or JsonException or NotSupportedException)
        {
            return new ApiCallResult<T>(
                (int)HttpStatusCode.ServiceUnavailable,
                TransportFailure<T>(
                    "The SIMF service could not be reached. Please try again.",
                    "تعذّر الوصول إلى خدمة SIMF. حاول مرة أخرى."));
        }
    }

    // -- D-176 (gap doc G12) — centralised AI module -----------------------

    public Task<ApiCallResult<GridPage<AdminAiPromptSummary>>> ListAiPromptsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminAiPromptSummary>>(
            HttpMethod.Post, "ai/prompts/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminAiPromptDetail>> GetAiPromptAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminAiPromptDetail>(
            HttpMethod.Get, $"ai/prompts/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>D-188 — append-only edit history for one prompt.
    /// Newest first. Empty list when the prompt has never been
    /// updated past v1.</summary>
    public Task<ApiCallResult<IReadOnlyList<AdminAiPromptHistoryEntry>>> GetAiPromptHistoryAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminAiPromptHistoryEntry>>(
            HttpMethod.Get, $"ai/prompts/{id}/history", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminAiPromptDetail>> CreateAiPromptAsync(
        CreateAiPromptRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminAiPromptDetail>(
            HttpMethod.Post, "ai/prompts",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminAiPromptDetail>> UpdateAiPromptAsync(
        Guid id, UpdateAiPromptRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminAiPromptDetail>(
            HttpMethod.Put, $"ai/prompts/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateAiPromptAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"ai/prompts/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AiCallResult>> TestAiPromptAsync(
        Guid id, TestAiPromptRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AiCallResult>(
            HttpMethod.Post, $"ai/prompts/{id}/test",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<AdminAiInvocationRow>>> ListAiInvocationsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminAiInvocationRow>>(
            HttpMethod.Post, "ai/invocations/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>The Control Panel operator assistant — sends the operator's
    /// question plus the grounding page directory to the <c>cp-assistant</c>
    /// prompt and returns the answer.</summary>
    public Task<ApiCallResult<AiCallResult>> AssistAsync(
        CpAssistantRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AiCallResult>(
            HttpMethod.Post, "ai/assistant",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>D-179 — full redacted payload for one invocation (SOC drill-down).</summary>
    public Task<ApiCallResult<AdminAiInvocationDetail>> GetAiInvocationAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminAiInvocationDetail>(
            HttpMethod.Get, $"ai/invocations/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>CP Phase-1 — the AI dashboard 24h health aggregate.</summary>
    public Task<ApiCallResult<AdminAiDashboard>> GetAiDashboardAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminAiDashboard>(
            HttpMethod.Get, "ai/dashboard", content: null,
            accessToken, cancellationToken);

    // -- D-735 — transactional email templates (list / read / edit / reset /
    //    preview). The {type} segment is the EmailTemplateType name (the DB holds
    //    only overrides; the catalogue backs every read so the grid shows all six).

    public Task<ApiCallResult<GridPage<AdminEmailTemplateSummary>>> ListEmailTemplatesAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminEmailTemplateSummary>>(
            HttpMethod.Post, "email/templates/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminEmailTemplateDetail>> GetEmailTemplateAsync(
        string type, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminEmailTemplateDetail>(
            HttpMethod.Get, $"email/templates/{type}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminEmailTemplateDetail>> UpdateEmailTemplateAsync(
        string type, UpdateEmailTemplateRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminEmailTemplateDetail>(
            HttpMethod.Put, $"email/templates/{type}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminEmailTemplateDetail>> ResetEmailTemplateAsync(
        string type, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminEmailTemplateDetail>(
            HttpMethod.Post, $"email/templates/{type}/reset", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<EmailTemplatePreviewResult>> PreviewEmailTemplateAsync(
        string type, PreviewEmailTemplateRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<EmailTemplatePreviewResult>(
            HttpMethod.Post, $"email/templates/{type}/preview",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // -- D-182 (CP UI for D-175 seat reservations) -------------------------

    public Task<ApiCallResult<HallSeatLayoutSnapshot>> GetHallSeatLayoutAsync(
        Guid hallId, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<HallSeatLayoutSnapshot>(
            HttpMethod.Get, $"halls/{hallId}/seat-layout", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<HallSeatLayoutSnapshot>> SetHallSeatLayoutAsync(
        Guid hallId, SetHallSeatLayoutRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<HallSeatLayoutSnapshot>(
            HttpMethod.Put, $"halls/{hallId}/seat-layout",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<SessionSeatCell>>> ListSessionSeatReservationsAsync(
        Guid sessionId, GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<SessionSeatCell>>(
            HttpMethod.Post, $"sessions/{sessionId}/seats/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> AdminReserveSessionRowAsync(
        Guid sessionId, AdminReserveRowRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"sessions/{sessionId}/seats/reserve-row",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> AdminReserveSessionSeatAsync(
        Guid sessionId, AdminReserveSeatRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"sessions/{sessionId}/seats/reserve-seat",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> AdminReleaseSessionSeatAsync(
        Guid sessionId, Guid reservationId, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"sessions/{sessionId}/seats/{reservationId}",
            content: null, accessToken, cancellationToken);

    // 2026-07-18 (live per-session hall view, CP page 2e) — the session's 4-state
    // seat map (no "my seat" cell) and everyone currently present in the hall.
    // Both API-side gated Attendance.View.
    public Task<ApiCallResult<SessionSeatMap>> GetAdminSessionSeatMapAsync(
        Guid sessionId, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<SessionSeatMap>(
            HttpMethod.Get, $"sessions/{sessionId}/seat-map", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<SessionPresentAttendee>>> GetSessionPresentAttendeesAsync(
        Guid sessionId, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<SessionPresentAttendee>>(
            HttpMethod.Get, $"sessions/{sessionId}/present", content: null,
            accessToken, cancellationToken);

    // -- D-269 — speaker meeting requests (SIMF.Contracts.Programme) ---------

    public Task<ApiCallResult<GridPage<AdminSpeakerMeetingRequestRow>>> ListAdminSpeakerMeetingRequestsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminSpeakerMeetingRequestRow>>(
            HttpMethod.Post, "speaker-meeting-requests/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSpeakerMeetingRequestDetail>> GetAdminSpeakerMeetingRequestAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSpeakerMeetingRequestDetail>(
            HttpMethod.Get, $"speaker-meeting-requests/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSpeakerMeetingRequestDetail>> RespondToAdminSpeakerMeetingRequestAsync(
        Guid id, RespondToSpeakerMeetingRequestRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSpeakerMeetingRequestDetail>(
            HttpMethod.Put, $"speaker-meeting-requests/{id}/respond",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // R-1 — re-send the speaker's Approve/Reject confirmation links (AwaitingSpeaker only).
    public Task<ApiCallResult<bool>> ResendSpeakerMeetingConfirmationAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"speaker-meeting-requests/{id}/resend-confirmation",
            content: null, accessToken, cancellationToken);

    // Bi-Meeting rework — an operator checks a confirmed speaker meeting in at the hall → Done.
    public Task<ApiCallResult<AdminSpeakerMeetingRequestDetail>> CheckInSpeakerMeetingAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSpeakerMeetingRequestDetail>(
            HttpMethod.Post, $"speaker-meeting-requests/{id}/check-in",
            content: null, accessToken, cancellationToken);

    // -- D-500 (Wave 5, الطلبات) — participation-document + badge-update request
    //    desks (SIMF.Contracts.Requests) -------------------------------------

    public Task<ApiCallResult<GridPage<AdminParticipationDocumentRequestRow>>> ListAdminParticipationDocumentRequestsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminParticipationDocumentRequestRow>>(
            HttpMethod.Post, "document-requests/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminParticipationDocumentRequestDetail>> GetAdminParticipationDocumentRequestAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminParticipationDocumentRequestDetail>(
            HttpMethod.Get, $"document-requests/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminParticipationDocumentRequestDetail>> RespondToAdminParticipationDocumentRequestAsync(
        Guid id, RespondToParticipationDocumentRequestRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminParticipationDocumentRequestDetail>(
            HttpMethod.Put, $"document-requests/{id}/respond",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<GridPage<AdminBadgeUpdateRequestRow>>> ListAdminBadgeUpdateRequestsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminBadgeUpdateRequestRow>>(
            HttpMethod.Post, "badge-requests/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBadgeUpdateRequestDetail>> GetAdminBadgeUpdateRequestAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBadgeUpdateRequestDetail>(
            HttpMethod.Get, $"badge-requests/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBadgeUpdateRequestDetail>> RespondToAdminBadgeUpdateRequestAsync(
        Guid id, RespondToBadgeUpdateRequestRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBadgeUpdateRequestDetail>(
            HttpMethod.Put, $"badge-requests/{id}/respond",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // D-474 (#11) — speaker availability windows.
    public Task<ApiCallResult<IReadOnlyList<AdminSpeakerAvailabilityWindow>>>
        ListSpeakerAvailabilityWindowsAsync(Guid speakerId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminSpeakerAvailabilityWindow>>(
            HttpMethod.Get, $"speakers/{speakerId}/availability-windows", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSpeakerAvailabilityWindow>>
        CreateSpeakerAvailabilityWindowAsync(Guid speakerId,
            CreateSpeakerAvailabilityWindowRequest request, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminSpeakerAvailabilityWindow>(
            HttpMethod.Post, $"speakers/{speakerId}/availability-windows",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>>
        DeleteSpeakerAvailabilityWindowAsync(Guid windowId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"speaker-availability-windows/{windowId}", content: null,
            accessToken, cancellationToken);

    // D-753 — the forum-day window (MIN/MAX over active ProgrammeDay.Date). The CP
    // meeting-scheduling pages read it to bound their date pickers to the event days.
    public Task<ApiCallResult<ForumWindowResponse>> GetForumWindowAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<ForumWindowResponse>(
            HttpMethod.Get, "programme/forum-window", content: null,
            accessToken, cancellationToken);

    // -- D-715 (item 7, FDS-013 §15 GAP-1) — hall availability windows ---------

    public Task<ApiCallResult<IReadOnlyList<AdminHallAvailabilityWindow>>>
        ListHallAvailabilityWindowsAsync(Guid hallId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminHallAvailabilityWindow>>(
            HttpMethod.Get, $"halls/{hallId}/availability-windows", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminHallAvailabilityWindow>>
        CreateHallAvailabilityWindowAsync(Guid hallId,
            CreateHallAvailabilityWindowRequest request, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminHallAvailabilityWindow>(
            HttpMethod.Post, $"halls/{hallId}/availability-windows",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>>
        DeleteHallAvailabilityWindowAsync(Guid windowId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"hall-availability-windows/{windowId}", content: null,
            accessToken, cancellationToken);

    // D-716 (item 7, GAP-2) — the hall's currently-free meeting slots (the
    // meeting-review flow reads these before binding an accepted request to one).
    public Task<ApiCallResult<IReadOnlyList<HallAvailableSlot>>>
        GetHallAvailableSlotsAsync(Guid hallId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<HallAvailableSlot>>(
            HttpMethod.Get, $"halls/{hallId}/available-slots", content: null,
            accessToken, cancellationToken);

    // -- D-478 (#11) — delegation meeting requests (SIMF.Contracts.Programme) -

    public Task<ApiCallResult<GridPage<AdminDelegationMeetingRequestRow>>>
        ListAdminDelegationMeetingRequestsAsync(GridQuery query, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminDelegationMeetingRequestRow>>(
            HttpMethod.Post, "delegation-meeting-requests/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminDelegationMeetingRequestDetail>>
        GetAdminDelegationMeetingRequestAsync(Guid id, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminDelegationMeetingRequestDetail>(
            HttpMethod.Get, $"delegation-meeting-requests/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminDelegationMeetingRequestDetail>>
        RespondToAdminDelegationMeetingRequestAsync(
            Guid id, RespondToDelegationMeetingRequestRequest request, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminDelegationMeetingRequestDetail>(
            HttpMethod.Put, $"delegation-meeting-requests/{id}/respond",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // Bi-Meeting rework — an operator checks a confirmed delegation meeting in → Done.
    public Task<ApiCallResult<AdminDelegationMeetingRequestDetail>> CheckInDelegationMeetingAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminDelegationMeetingRequestDetail>(
            HttpMethod.Post, $"delegation-meeting-requests/{id}/check-in",
            content: null, accessToken, cancellationToken);

    // Bi-Meeting rework — delegation availability windows (parity with the speaker
    // stack): the team defines a country/delegation's free windows; the app offers
    // their slots. Keyed on the ISO-numeric CountryId (int), not a Guid.
    public Task<ApiCallResult<IReadOnlyList<AdminDelegationAvailabilityWindow>>>
        ListDelegationAvailabilityWindowsAsync(int countryId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminDelegationAvailabilityWindow>>(
            HttpMethod.Get, $"countries/{countryId}/availability-windows", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminDelegationAvailabilityWindow>>
        CreateDelegationAvailabilityWindowAsync(int countryId,
            CreateDelegationAvailabilityWindowRequest request, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<AdminDelegationAvailabilityWindow>(
            HttpMethod.Post, $"countries/{countryId}/availability-windows",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>>
        DeleteDelegationAvailabilityWindowAsync(Guid windowId, string accessToken,
            CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"delegation-availability-windows/{windowId}", content: null,
            accessToken, cancellationToken);

    // -- D-199 — News admin CRUD (SIMF.Contracts.PublicRelations) -----------

    public Task<ApiCallResult<GridPage<AdminNewsSummary>>> ListNewsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminNewsSummary>>(
            HttpMethod.Post, "news/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminNewsDetail>> GetNewsAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminNewsDetail>(
            HttpMethod.Get, $"news/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminNewsDetail>> CreateNewsAsync(
        CreateNewsRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminNewsDetail>(
            HttpMethod.Post, "news",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminNewsDetail>> UpdateNewsAsync(
        Guid id, UpdateNewsRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminNewsDetail>(
            HttpMethod.Put, $"news/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteNewsAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"news/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-199 — Media gallery admin CRUD (SIMF.Contracts.Media) ------------

    public Task<ApiCallResult<GridPage<AdminMediaSummary>>> ListMediaAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminMediaSummary>>(
            HttpMethod.Post, "media/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminMediaDetail>> GetMediaAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminMediaDetail>(
            HttpMethod.Get, $"media/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminMediaDetail>> CreateMediaAsync(
        AdminCreateMediaRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminMediaDetail>(
            HttpMethod.Post, "media",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminMediaDetail>> UpdateMediaAsync(
        Guid id, AdminUpdateMediaRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminMediaDetail>(
            HttpMethod.Put, $"media/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteMediaAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"media/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>D-199 — multipart upload of a media item's primary image
    /// (models <see cref="UploadVisitorIdDocumentAsync"/>).</summary>
    public Task<ApiCallResult<AdminMediaDetail>> UploadMediaImageAsync(
        Guid id, byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        return SendAsync<AdminMediaDetail>(
            HttpMethod.Post, $"media/{id}/image", multipart,
            accessToken, cancellationToken);
    }

    // -- D-357 — unified media-asset pipeline (one upload / link / fetch for every entity) --

    /// <summary>Upload (or replace) a media asset's file for (category, owner).</summary>
    public Task<ApiCallResult<bool>> UploadAssetImageAsync(
        string category, Guid ownerId, string kind,
        byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        multipart.Add(new StringContent(kind), "kind");
        return SendAsync<bool>(
            HttpMethod.Post, $"assets/{category}/{ownerId}/image", multipart,
            accessToken, cancellationToken);
    }

    /// <summary>Set (or replace) a media asset to an external link for (category, owner).</summary>
    public Task<ApiCallResult<bool>> SetAssetLinkAsync(
        string category, Guid ownerId,
        SIMF.Contracts.Assets.SetAssetLinkRequest request,
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Put, $"assets/{category}/{ownerId}/link",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Fetch an asset's bytes for the CP admin preview proxy (the API
    /// 302s an external-link asset, which HttpClient follows transparently).</summary>
    public async Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchAssetImageAsync(
        string category, Guid ownerId, string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Get, $"{BasePath}assets/{category}/{ownerId}/image");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode) { return ((int)response.StatusCode, null, []); }
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return ((int)response.StatusCode,
                response.Content.Headers.ContentType?.MediaType, bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return (503, null, []);
        }
    }

    /// <summary>One page of all media assets for the central Media Library.</summary>
    public Task<ApiCallResult<GridPage<SIMF.Contracts.Assets.AdminAssetSummary>>> ListAssetsAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<SIMF.Contracts.Assets.AdminAssetSummary>>(
            HttpMethod.Post, "assets/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One media asset by id.</summary>
    public Task<ApiCallResult<SIMF.Contracts.Assets.AdminAssetSummary>> GetAssetAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<SIMF.Contracts.Assets.AdminAssetSummary>(
            HttpMethod.Get, $"assets/item/{id}", null, accessToken, cancellationToken);

    /// <summary>Soft-delete (deactivate) a media asset.</summary>
    public Task<ApiCallResult<bool>> DeactivateAssetAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(HttpMethod.Delete, $"assets/item/{id}", null, accessToken, cancellationToken);

    /// <summary>Restore a soft-deleted media asset.</summary>
    public Task<ApiCallResult<bool>> RestoreAssetAsync(
        Guid id, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(HttpMethod.Post, $"assets/item/{id}/restore", null, accessToken, cancellationToken);

    // -- D-199 — Media-partner admin CRUD (SIMF.Contracts.PublicRelations) --

    public Task<ApiCallResult<GridPage<AdminMediaPartnerSummary>>> ListMediaPartnersAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminMediaPartnerSummary>>(
            HttpMethod.Post, "media-partners/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminMediaPartnerDetail>> CreateMediaPartnerAsync(
        AdminCreateMediaPartnerRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminMediaPartnerDetail>(
            HttpMethod.Post, "media-partners",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminMediaPartnerDetail>> UpdateMediaPartnerAsync(
        Guid id, AdminUpdateMediaPartnerRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminMediaPartnerDetail>(
            HttpMethod.Put, $"media-partners/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateMediaPartnerAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"media-partners/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-199 — Sponsor admin CRUD (SIMF.Contracts.Admin) ------------------

    public Task<ApiCallResult<GridPage<AdminSponsorSummary>>> ListSponsorsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminSponsorSummary>>(
            HttpMethod.Post, "sponsors/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSponsorDetail>> CreateSponsorAsync(
        AdminCreateSponsorRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSponsorDetail>(
            HttpMethod.Post, "sponsors",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSponsorDetail>> UpdateSponsorAsync(
        Guid id, AdminUpdateSponsorRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSponsorDetail>(
            HttpMethod.Put, $"sponsors/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateSponsorAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"sponsors/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-199 — Booth admin CRUD (SIMF.Contracts.Exhibition) ---------------

    public Task<ApiCallResult<GridPage<AdminBoothSummary>>> ListBoothsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminBoothSummary>>(
            HttpMethod.Post, "booths/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBoothDetail>> GetBoothAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBoothDetail>(
            HttpMethod.Get, $"booths/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBoothDetail>> CreateBoothAsync(
        AdminCreateBoothRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBoothDetail>(
            HttpMethod.Post, "booths",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminBoothDetail>> UpdateBoothAsync(
        Guid id, AdminUpdateBoothRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBoothDetail>(
            HttpMethod.Put, $"booths/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateBoothAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"booths/{id}", content: null,
            accessToken, cancellationToken);

    // -- B3 (D-220) — Organisation lookup admin CRUD + gov-Excel import
    //    (SIMF.Contracts.Organisations) ------------------------------------

    public Task<ApiCallResult<GridPage<AdminOrganisationSummary>>> ListOrganisationsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminOrganisationSummary>>(
            HttpMethod.Post, "organisations/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminOrganisationDetail>> GetOrganisationAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminOrganisationDetail>(
            HttpMethod.Get, $"organisations/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminOrganisationDetail>> CreateOrganisationAsync(
        CreateOrganisationRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminOrganisationDetail>(
            HttpMethod.Post, "organisations",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminOrganisationDetail>> UpdateOrganisationAsync(
        Guid id, UpdateOrganisationRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminOrganisationDetail>(
            HttpMethod.Put, $"organisations/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateOrganisationAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"organisations/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>B3 (D-220) — bulk-import a government Excel sheet of Saudi
    /// companies (multipart; form field "file"). Idempotent upsert by CR.</summary>
    public Task<ApiCallResult<OrganisationImportResult>> ImportOrganisationsAsync(
        byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        return SendAsync<OrganisationImportResult>(
            HttpMethod.Post, "organisations/import", multipart,
            accessToken, cancellationToken);
    }

    // -- SIMF-FDS-014 (D-281/C2) — shared Contact directory admin CRUD + picker
    //    (SIMF.Contracts.Contacts) -------------------------------------------

    public Task<ApiCallResult<GridPage<AdminContactSummary>>> ListContactsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminContactSummary>>(
            HttpMethod.Post, "contacts/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminContactDetail>> GetContactAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminContactDetail>(
            HttpMethod.Get, $"contacts/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminContactDetail>> CreateContactAsync(
        CreateContactRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminContactDetail>(
            HttpMethod.Post, "contacts",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminContactDetail>> UpdateContactAsync(
        Guid id, UpdateContactRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminContactDetail>(
            HttpMethod.Put, $"contacts/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateContactAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"contacts/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<ContactPickerItem>>> PickerContactsAsync(
        string? search, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<ContactPickerItem>>(
            HttpMethod.Get,
            $"contacts/picker?search={Uri.EscapeDataString(search ?? string.Empty)}",
            content: null, accessToken, cancellationToken);

    // SIMF-FDS-014 (D-283/C2b) — single-row detail fetch so the Sponsor /
    // MediaPartner edit modals can pre-load the linked ContactId for the picker
    // (their backend GET endpoints already exist; only these client + BFF
    // passthroughs were missing). Mirrors GetOrganisationAsync.
    public Task<ApiCallResult<AdminSponsorDetail>> GetSponsorAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSponsorDetail>(
            HttpMethod.Get, $"sponsors/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminMediaPartnerDetail>> GetMediaPartnerAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminMediaPartnerDetail>(
            HttpMethod.Get, $"media-partners/{id}", content: null,
            accessToken, cancellationToken);

    // -- B9b (D-226) — Session-category dynamic lookup admin CRUD
    //    (SIMF.Contracts.Admin) ---------------------------------------------

    public Task<ApiCallResult<GridPage<AdminSessionCategorySummary>>> ListSessionCategoriesAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminSessionCategorySummary>>(
            HttpMethod.Post, "session-categories/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionCategoryDetail>> GetSessionCategoryAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionCategoryDetail>(
            HttpMethod.Get, $"session-categories/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionCategoryDetail>> CreateSessionCategoryAsync(
        AdminCreateSessionCategoryRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionCategoryDetail>(
            HttpMethod.Post, "session-categories",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSessionCategoryDetail>> UpdateSessionCategoryAsync(
        Guid id, AdminUpdateSessionCategoryRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSessionCategoryDetail>(
            HttpMethod.Put, $"session-categories/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateSessionCategoryAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"session-categories/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-452 — Programme-days admin CRUD (SIMF.Contracts.Admin). Date +
    //    bilingual title; the logo rides the generic asset endpoints
    //    (AssetCategory.ProgrammeDayImage). Mirrors the session-category shape.

    public Task<ApiCallResult<GridPage<AdminProgrammeDaySummary>>> ListProgrammeDaysAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminProgrammeDaySummary>>(
            HttpMethod.Post, "programme-days/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminProgrammeDayDetail>> GetProgrammeDayAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminProgrammeDayDetail>(
            HttpMethod.Get, $"programme-days/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminProgrammeDayDetail>> CreateProgrammeDayAsync(
        AdminCreateProgrammeDayRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminProgrammeDayDetail>(
            HttpMethod.Post, "programme-days",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminProgrammeDayDetail>> UpdateProgrammeDayAsync(
        Guid id, AdminUpdateProgrammeDayRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminProgrammeDayDetail>(
            HttpMethod.Put, $"programme-days/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateProgrammeDayAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"programme-days/{id}", content: null,
            accessToken, cancellationToken);

    // -- #6/#17 — Booking monitor (read-only; SIMF.Contracts.Sessions) --------
    // Bookings auto-confirm (no approval step) and no-shows are released by a
    // background worker, so the CP only reads the active-reservations list.

    public Task<ApiCallResult<GridPage<ActiveBookingRow>>> ListActiveBookingsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<ActiveBookingRow>>(
            HttpMethod.Post, "bookings/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    // -- P2.3 (D-228) — Speaker presentation files (SIMF.Contracts.Admin) ----

    public Task<ApiCallResult<IReadOnlyList<AdminSpeakerPresentationRow>>> ListSpeakerPresentationsAsync(
        Guid speakerId, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminSpeakerPresentationRow>>(
            HttpMethod.Get, $"speakers/{speakerId}/presentations", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSpeakerPresentationRow>> UploadSpeakerPresentationAsync(
        Guid speakerId, Guid sessionId, byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        return SendAsync<AdminSpeakerPresentationRow>(
            HttpMethod.Post, $"speakers/{speakerId}/presentations?sessionId={sessionId}",
            multipart, accessToken, cancellationToken);
    }

    public Task<ApiCallResult<bool>> DeleteSpeakerPresentationAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"speaker-presentations/{id}", content: null,
            accessToken, cancellationToken);

    /// <summary>P2.3 — streamed read of a presentation file for the CP download
    /// proxy. Bypasses the <c>ApiResult</c> envelope (binary body); returns the
    /// status, content type, content-disposition and bytes verbatim.</summary>
    public async Task<(int StatusCode, string? ContentType, string? ContentDisposition, byte[] Bytes)>
        FetchSpeakerPresentationAsync(
            Guid id, string accessToken, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Get, $"{BasePath}speaker-presentations/{id}/file");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ((int)response.StatusCode, null, null, []);
            }
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return (
                (int)response.StatusCode,
                response.Content.Headers.ContentType?.ToString(),
                response.Content.Headers.ContentDisposition?.ToString(),
                bytes);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException)
        {
            return ((int)HttpStatusCode.ServiceUnavailable, null, null, []);
        }
    }

    // -- P2.4 (D-229) — System Configuration settings (SIMF.Contracts.Admin) -

    public Task<ApiCallResult<GridPage<AdminSystemSettingSummary>>> ListSystemSettingsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminSystemSettingSummary>>(
            HttpMethod.Post, "system-settings/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSystemSettingDetail>> GetSystemSettingAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSystemSettingDetail>(
            HttpMethod.Get, $"system-settings/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSystemSettingDetail>> CreateSystemSettingAsync(
        AdminCreateSystemSettingRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSystemSettingDetail>(
            HttpMethod.Post, "system-settings",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminSystemSettingDetail>> UpdateSystemSettingAsync(
        Guid id, AdminUpdateSystemSettingRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminSystemSettingDetail>(
            HttpMethod.Put, $"system-settings/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteSystemSettingAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"system-settings/{id}", content: null,
            accessToken, cancellationToken);

    // -- P2.5 (D-230) — 2D venue-map node CRUD (SIMF.Contracts.Admin) --------

    public Task<ApiCallResult<GridPage<AdminVenueMapNodeSummary>>> ListVenueMapNodesAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminVenueMapNodeSummary>>(
            HttpMethod.Post, "venue-map/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminVenueMapNodeDetail>> GetVenueMapNodeAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminVenueMapNodeDetail>(
            HttpMethod.Get, $"venue-map/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminVenueMapNodeDetail>> CreateVenueMapNodeAsync(
        AdminCreateVenueMapNodeRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminVenueMapNodeDetail>(
            HttpMethod.Post, "venue-map",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminVenueMapNodeDetail>> UpdateVenueMapNodeAsync(
        Guid id, AdminUpdateVenueMapNodeRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminVenueMapNodeDetail>(
            HttpMethod.Put, $"venue-map/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteVenueMapNodeAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"venue-map/{id}", content: null,
            accessToken, cancellationToken);

    // -- D-199 — Archive edition admin CRUD (SIMF.Contracts.Archive) --------

    public Task<ApiCallResult<GridPage<AdminArchiveEditionSummary>>> ListArchiveEditionsAsync(
        GridQuery query, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminArchiveEditionSummary>>(
            HttpMethod.Post, "archive/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminArchiveEditionDetail>> GetArchiveEditionAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminArchiveEditionDetail>(
            HttpMethod.Get, $"archive/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminArchiveEditionDetail>> CreateArchiveEditionAsync(
        CreateArchiveEditionRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminArchiveEditionDetail>(
            HttpMethod.Post, "archive",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminArchiveEditionDetail>> UpdateArchiveEditionAsync(
        Guid id, UpdateArchiveEditionRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminArchiveEditionDetail>(
            HttpMethod.Put, $"archive/{id}",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    public Task<ApiCallResult<bool>> DeleteArchiveEditionAsync(
        Guid id, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"archive/{id}", content: null,
            accessToken, cancellationToken);

    public Task<ApiCallResult<AdminArchiveEditionDetail>> SnapshotCurrentArchiveEditionAsync(
        SnapshotCurrentEditionRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminArchiveEditionDetail>(
            HttpMethod.Post, "archive/snapshot-current",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

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

    // -- D-202 Track-2 — Statistics dashboard (SIMF.Contracts.Statistics) ----

    public Task<ApiCallResult<StatisticsDashboard>> GetStatisticsAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<StatisticsDashboard>(
            HttpMethod.Get, "statistics", content: null,
            accessToken, cancellationToken);

    // -- FR-506 — session-attendance dashboard (SIMF.Contracts.Attendance) ----

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

    // -- D-202 Track-2 — Exhibitor admin CRUD + account provisioning --------
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

    private static ApiResult<T> TransportFailure<T>(string message, string messageArabic) =>
        ApiResult<T>.Fail(new ApiError
        {
            Code = ErrorCodes.InternalError,
            Message = message,
            MessageArabic = messageArabic,
        });
}
