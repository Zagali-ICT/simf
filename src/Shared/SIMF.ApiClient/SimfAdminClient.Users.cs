// Part of SimfAdminClient - see SimfAdminClient.cs for the transport core.
// the three account families, bulk operations, approval, walk-in, ID documents
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
    // -- three-family create + list ------------------------------------------

    /// <summary>Creates a new Admin user. Replaces <c>CreateStaffAsync</c>.</summary>
    public Task<ApiCallResult<AdminCreateUserResponse>> CreateAdminAsync(
        AdminCreateAdminRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCreateUserResponse>(
            HttpMethod.Post, "admins",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Creates a new Other user.</summary>
    public Task<ApiCallResult<AdminCreateUserResponse>> CreateOtherAsync(
        AdminCreateOtherRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCreateUserResponse>(
            HttpMethod.Post, "others",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Creates a new Visitor user; the <c>ProfileTypeId</c> tier on the
    /// request is optional.</summary>
    public Task<ApiCallResult<AdminCreateUserResponse>> CreateVisitorAsync(
        AdminCreateVisitorRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCreateUserResponse>(
            HttpMethod.Post, "visitors",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One page of Admin-typed accounts.</summary>
    public Task<ApiCallResult<GridPage<AdminUserSummary>>> ListAdminsAsync(
        GridQuery query,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminUserSummary>>(
            HttpMethod.Post, "admins/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One page of Other-typed accounts.</summary>
    public Task<ApiCallResult<GridPage<AdminUserSummary>>> ListOthersAsync(
        GridQuery query,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminUserSummary>>(
            HttpMethod.Post, "others/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One page of Visitor-typed accounts, keyed off UserType.</summary>
    public Task<ApiCallResult<GridPage<AdminUserSummary>>> ListVisitorsAsync(
        GridQuery query,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminUserSummary>>(
            HttpMethod.Post, "visitors/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Soft-deletes one or many admin accounts. Despite the method
    /// name, the route is <c>admins/bulk-delete</c>.</summary>
    public Task<ApiCallResult<AdminBulkDeleteResponse>> BulkDeleteUsersAsync(
        AdminBulkDeleteRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkDeleteResponse>(
            HttpMethod.Post, "admins/bulk-delete",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Creates a copy of an existing user with a new email.</summary>
    public Task<ApiCallResult<AdminCreateUserResponse>> DuplicateUserAsync(
        AdminDuplicateUserRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCreateUserResponse>(
            HttpMethod.Post, "admins/duplicate",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Returns the bytes of an XLSX workbook with the selected users.</summary>
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

    /// <summary>XLSX of the filtered operation log.</summary>
    public Task<(int StatusCode, byte[] Bytes)> ExportOperationLogAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        PostForBytesAsync("operation-log/export", query, accessToken, cancellationToken);

    /// <summary>XLSX of the filtered attendee roster.</summary>
    public Task<(int StatusCode, byte[] Bytes)> ExportAttendeesAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        PostForBytesAsync("attendees/export", query, accessToken, cancellationToken);


    /// <summary>Generic grid XLSX export by resource slug (e.g. "interests").
    /// Posts the selected ids / grid query and returns the workbook bytes.</summary>
    public Task<(int StatusCode, byte[] Bytes)> ExportGridAsync(
        string resource,
        SIMF.Contracts.Admin.AdminGridExportRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        PostForBytesAsync($"{resource}/export", request, accessToken, cancellationToken);

    /// <summary>Generic grid XLSX import by resource slug. Multipart
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

    // -- type-scoped bulk operations for Visitors and Others -----------------

    /// <summary>Soft-deletes one or many visitor accounts.</summary>
    public Task<ApiCallResult<AdminBulkDeleteResponse>> BulkDeleteVisitorsAsync(
        AdminBulkDeleteRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkDeleteResponse>(
            HttpMethod.Post, "visitors/bulk-delete",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Soft-deletes one or many Other accounts.</summary>
    public Task<ApiCallResult<AdminBulkDeleteResponse>> BulkDeleteOthersAsync(
        AdminBulkDeleteRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkDeleteResponse>(
            HttpMethod.Post, "others/bulk-delete",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Duplicates an existing visitor account.</summary>
    public Task<ApiCallResult<AdminCreateUserResponse>> DuplicateVisitorAsync(
        AdminDuplicateUserRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCreateUserResponse>(
            HttpMethod.Post, "visitors/duplicate",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Duplicates an existing Other account.</summary>
    public Task<ApiCallResult<AdminCreateUserResponse>> DuplicateOtherAsync(
        AdminDuplicateUserRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminCreateUserResponse>(
            HttpMethod.Post, "others/duplicate",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Exports visitor accounts to an XLSX workbook.</summary>
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

    /// <summary>Exports Other accounts to an XLSX workbook.</summary>
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

    /// <summary>Bulk-creates visitor accounts from an XLSX workbook upload.</summary>
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

    /// <summary>Bulk-creates Other accounts from an XLSX workbook upload.</summary>
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

    // -- approval workflow (Admin / Other / Visitor) -------------------------

    /// <summary>Approves a pending Admin.</summary>
    public Task<ApiCallResult<bool>> ApproveAdminAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"admins/{subjectId}/approve",
            JsonContent.Create(new { }, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Rejects a pending Admin with a free-text reason.</summary>
    public Task<ApiCallResult<bool>> RejectAdminAsync(
        Guid subjectId, AdminRejectRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"admins/{subjectId}/reject",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Approves a pending Other.</summary>
    public Task<ApiCallResult<bool>> ApproveOtherAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"others/{subjectId}/approve",
            JsonContent.Create(new { }, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Rejects a pending Other with a free-text reason.</summary>
    public Task<ApiCallResult<bool>> RejectOtherAsync(
        Guid subjectId, AdminRejectRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"others/{subjectId}/reject",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Approves a pending Visitor. An optional
    /// <paramref name="profileTypeId"/> sets the visitor's tier as part of
    /// the approval; null is sent as a null property and leaves the tier
    /// unchanged.</summary>
    public Task<ApiCallResult<bool>> ApproveVisitorAsync(
        Guid subjectId, string accessToken, Guid? profileTypeId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"visitors/{subjectId}/approve",
            JsonContent.Create(new { ProfileTypeId = profileTypeId }, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Rejects a pending Visitor with a free-text reason.</summary>
    public Task<ApiCallResult<bool>> RejectVisitorAsync(
        Guid subjectId, AdminRejectRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"visitors/{subjectId}/reject",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Edit a visitor (email, display name, tier).</summary>
    public Task<ApiCallResult<bool>> UpdateVisitorAsync(
        Guid id, AdminUpdateVisitorRequest body, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Put, $"visitors/{id}",
            JsonContent.Create(body, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Edit a partner (Other) account.</summary>
    public Task<ApiCallResult<bool>> UpdateOtherAsync(
        Guid id, AdminUpdateOtherRequest body, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Put, $"others/{id}",
            JsonContent.Create(body, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Flips an account's type between Visitor and Other by
    /// reassigning it to a profile type in the opposite scope.</summary>
    public Task<ApiCallResult<bool>> ChangeAccountTypeAsync(
        Guid id, AdminChangeAccountTypeRequest body, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"accounts/{id}/change-type",
            JsonContent.Create(body, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Bulk-approve a batch of pending visitors.
    /// Up to 500 ids per request; per-subject failures are reported in
    /// <see cref="AdminBulkApprovalResponse.Failures"/>.</summary>
    public Task<ApiCallResult<AdminBulkApprovalResponse>> BulkApproveVisitorsAsync(
        AdminBulkApprovalRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkApprovalResponse>(
            HttpMethod.Post, "visitors/bulk-approve",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Bulk-approve a batch of pending Other-tier users.</summary>
    public Task<ApiCallResult<AdminBulkApprovalResponse>> BulkApproveOthersAsync(
        AdminBulkApprovalRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkApprovalResponse>(
            HttpMethod.Post, "others/bulk-approve",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Bulk-approve a batch of pending admins.</summary>
    public Task<ApiCallResult<AdminBulkApprovalResponse>> BulkApproveAdminsAsync(
        AdminBulkApprovalRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkApprovalResponse>(
            HttpMethod.Post, "admins/bulk-approve",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Bulk-reject a batch of pending visitors with a shared
    /// reason. Per-subject failures are reported in
    /// <see cref="AdminBulkRejectResponse.Failures"/>.</summary>
    public Task<ApiCallResult<AdminBulkRejectResponse>> BulkRejectVisitorsAsync(
        AdminBulkRejectRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkRejectResponse>(
            HttpMethod.Post, "visitors/bulk-reject",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Bulk-reject a batch of pending Other-tier users.</summary>
    public Task<ApiCallResult<AdminBulkRejectResponse>> BulkRejectOthersAsync(
        AdminBulkRejectRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkRejectResponse>(
            HttpMethod.Post, "others/bulk-reject",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Bulk-reject a batch of pending admins.</summary>
    public Task<ApiCallResult<AdminBulkRejectResponse>> BulkRejectAdminsAsync(
        AdminBulkRejectRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkRejectResponse>(
            HttpMethod.Post, "admins/bulk-reject",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One page of pending-approval Admins. Replaces
    /// <c>ListPendingStaffAsync</c>.</summary>
    public Task<ApiCallResult<GridPage<AdminPendingUserSummary>>> ListPendingAdminsAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminPendingUserSummary>>(
            HttpMethod.Post, "admins/pending/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One page of pending-approval Others.</summary>
    public Task<ApiCallResult<GridPage<AdminPendingUserSummary>>> ListPendingOthersAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminPendingUserSummary>>(
            HttpMethod.Post, "others/pending/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>One page of pending-approval Visitors.</summary>
    public Task<ApiCallResult<GridPage<AdminPendingUserSummary>>> ListPendingVisitorsAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminPendingUserSummary>>(
            HttpMethod.Post, "visitors/pending/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Scoped read of a pending Visitor's full profile.
    /// Returns 404 (not 403) for unknown / approved / wrong-type ids; the
    /// CP approve / reject flow renders this body in a preview modal.</summary>
    public Task<ApiCallResult<PendingProfileResponse>> GetPendingVisitorProfileAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<PendingProfileResponse>(
            HttpMethod.Get, $"visitors/{subjectId}/profile-for-approval",
            content: null,
            accessToken, cancellationToken);

    /// <summary>Scoped read of a pending Other's full profile.
    /// Twin of <see cref="GetPendingVisitorProfileAsync"/>.</summary>
    public Task<ApiCallResult<PendingProfileResponse>> GetPendingOtherProfileAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<PendingProfileResponse>(
            HttpMethod.Get, $"others/{subjectId}/profile-for-approval",
            content: null,
            accessToken, cancellationToken);

    // -- walk-in registration desk -------------------------------------------

    /// <summary>On-site walk-in visitor registration. Auto-approves
    /// and returns the minted QR id for the badge handover.</summary>
    public Task<ApiCallResult<AdminWalkInRegistrationResponse>> RegisterVisitorOnSiteAsync(
        AdminWalkInRegistrationRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminWalkInRegistrationResponse>(
            HttpMethod.Post, "visitors/register-onsite",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Bulk-generate placeholder badges by profile type + count.</summary>
    public Task<ApiCallResult<AdminBulkGenerateBadgesResponse>> BulkGenerateBadgesAsync(
        AdminBulkGenerateBadgesRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminBulkGenerateBadgesResponse>(
            HttpMethod.Post, "visitors/bulk-generate",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>The server-paged list of persisted bulk-badge batches.</summary>
    public Task<ApiCallResult<GridPage<AdminBadgeBatchSummary>>> ListBadgeBatchesAsync(
        GridQuery query,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<AdminBadgeBatchSummary>>(
            HttpMethod.Post, "visitors/badge-batches/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Re-email a batch's QR pack to an organiser.</summary>
    public Task<ApiCallResult<AdminReEmailBadgeBatchResponse>> ReEmailBadgeBatchAsync(
        AdminReEmailBadgeBatchRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminReEmailBadgeBatchResponse>(
            HttpMethod.Post, "visitors/badge-batches/re-email",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Revoke a batch (disable its accounts + mark it inactive).</summary>
    public Task<ApiCallResult<AdminRevokeBadgeBatchResponse>> RevokeBadgeBatchAsync(
        AdminRevokeBadgeBatchRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminRevokeBadgeBatchResponse>(
            HttpMethod.Post, "visitors/badge-batches/revoke",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>On-site walk-in Other registration.</summary>
    public Task<ApiCallResult<AdminWalkInRegistrationResponse>> RegisterOtherOnSiteAsync(
        AdminWalkInRegistrationRequest request,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminWalkInRegistrationResponse>(
            HttpMethod.Post, "others/register-onsite",
            JsonContent.Create(request, options: JsonOptions),
            accessToken, cancellationToken);

    // -- broadened admin profile read ----------------------------------------

    /// <summary>Full visitor profile, any state. 404 for unknown
    /// id or wrong UserType.</summary>
    public Task<ApiCallResult<AdminUserProfileView>> GetVisitorProfileAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminUserProfileView>(
            HttpMethod.Get, $"visitors/{subjectId}/profile",
            content: null,
            accessToken, cancellationToken);

    /// <summary>Full Other profile, any state.</summary>
    public Task<ApiCallResult<AdminUserProfileView>> GetOtherProfileAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminUserProfileView>(
            HttpMethod.Get, $"others/{subjectId}/profile",
            content: null,
            accessToken, cancellationToken);

    /// <summary>Print-bag station: resolve a QR id to the
    /// walk-in badge response so the page can render and reprint.</summary>
    public Task<ApiCallResult<AdminWalkInRegistrationResponse>> LookupByQrIdAsync(
        string qrId, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<AdminWalkInRegistrationResponse>(
            HttpMethod.Get, $"qr-lookup/{Uri.EscapeDataString(qrId)}",
            content: null,
            accessToken, cancellationToken);

    // -- admin ID-document upload + read -------------------------------------

    /// <summary>Admin-side upload of a visitor's ID-document image.</summary>
    public Task<ApiCallResult<bool>> UploadVisitorIdDocumentAsync(
        Guid subjectId, byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default) =>
        UploadIdDocumentAsync(
            $"visitors/{subjectId}/id-document",
            content, contentType, fileName, accessToken, cancellationToken);

    /// <summary>Admin-side upload of an Other account's ID-document image.</summary>
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

    /// <summary>Admin-side upload of a visitor's VVIP/VIP welcome
    /// photo (موج). Multipart "file"; the API returns a plain bool envelope.</summary>
    public Task<ApiCallResult<bool>> UploadVisitorVipPhotoAsync(
        Guid subjectId, byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default) =>
        UploadIdDocumentAsync(
            $"visitors/{subjectId}/vip-photo",
            content, contentType, fileName, accessToken, cancellationToken);

    /// <summary>Admin-side upload of a visitor's profile photo (avatar).</summary>
    public Task<ApiCallResult<AvatarResponse>> UploadVisitorAvatarAsync(
        Guid subjectId, byte[] content, string contentType, string fileName,
        string accessToken, CancellationToken cancellationToken = default) =>
        UploadAvatarAsync(
            $"visitors/{subjectId}/avatar",
            content, contentType, fileName, accessToken, cancellationToken);

    /// <summary>Admin-side upload of an Other account's profile photo (avatar).</summary>
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

    /// <summary>Admin-side streamed read of a visitor's ID-document image.</summary>
    public Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchVisitorIdDocumentAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        FetchIdDocumentAsync($"visitors/{subjectId}/id-document", accessToken, cancellationToken);

    /// <summary>Admin-side streamed read of an Other account's ID-document image.</summary>
    public Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchOtherIdDocumentAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        FetchIdDocumentAsync($"others/{subjectId}/id-document", accessToken, cancellationToken);

    /// <summary>Admin streamed read of a visitor's profile photo (avatar).
    /// Reuses the generic byte-fetch helper below.</summary>
    public Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchVisitorAvatarAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        FetchIdDocumentAsync($"visitors/{subjectId}/avatar", accessToken, cancellationToken);

    /// <summary>Admin streamed read of an Other account's profile photo (avatar).</summary>
    public Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchOtherAvatarAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        FetchIdDocumentAsync($"others/{subjectId}/avatar", accessToken, cancellationToken);

    /// <summary>Admin streamed read of an admin account's profile photo
    /// (avatar) for the Admins-list thumbnail. Gated by Admins.View.</summary>
    public Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchAdminAvatarAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        FetchIdDocumentAsync($"admins/{subjectId}/avatar", accessToken, cancellationToken);

    /// <summary>Admin streamed read of a visitor's VVIP/VIP welcome
    /// photo (موج). Reuses the generic byte-fetch helper.</summary>
    public Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchVisitorVipPhotoAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        FetchIdDocumentAsync($"visitors/{subjectId}/vip-photo", accessToken, cancellationToken);

    /// <summary>The VVIP/VIP welcome roster (موج) as JSON.</summary>
    public Task<ApiCallResult<IReadOnlyList<VipRosterRow>>> GetVipRosterAsync(
        string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<VipRosterRow>>(
            HttpMethod.Get, "visitors/vip/roster", content: null,
            accessToken, cancellationToken);

    /// <summary>One page of the VVIP/VIP roster for the CP export grid.</summary>
    public Task<ApiCallResult<GridPage<VipRosterRow>>> ListVipRosterAsync(
        GridQuery query, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<GridPage<VipRosterRow>>(
            HttpMethod.Post, "visitors/vip/roster/list",
            JsonContent.Create(query, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Download the VVIP/VIP welcome roster (موج) as a
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

    /// <summary>ProfileTypes filtered by UserType — for the create-page picker.</summary>
    public Task<ApiCallResult<IReadOnlyList<AdminProfileTypeSummary>>> ListProfileTypesAsync(
        string userType, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<AdminProfileTypeSummary>>(
            HttpMethod.Get, $"profile-types?userType={Uri.EscapeDataString(userType)}",
            content: null,
            accessToken, cancellationToken);
}
