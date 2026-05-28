using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Logs;

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

    /// <summary>Approves a pending Visitor (P4).</summary>
    public Task<ApiCallResult<bool>> ApproveVisitorAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"visitors/{subjectId}/approve",
            JsonContent.Create(new { }, options: JsonOptions),
            accessToken, cancellationToken);

    /// <summary>Rejects a pending Visitor with a free-text reason (P4).</summary>
    public Task<ApiCallResult<bool>> RejectVisitorAsync(
        Guid subjectId, AdminRejectRequest request, string accessToken,
        CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post, $"visitors/{subjectId}/reject",
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

    /// <summary>D-129 — admin-side streamed read of a visitor's ID-document image.</summary>
    public Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchVisitorIdDocumentAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        FetchIdDocumentAsync($"visitors/{subjectId}/id-document", accessToken, cancellationToken);

    /// <summary>D-129 — admin-side streamed read of an Other account's ID-document image.</summary>
    public Task<(int StatusCode, string? ContentType, byte[] Bytes)> FetchOtherIdDocumentAsync(
        Guid subjectId, string accessToken, CancellationToken cancellationToken = default) =>
        FetchIdDocumentAsync($"others/{subjectId}/id-document", accessToken, cancellationToken);

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

    private async Task<ApiCallResult<T>> SendAsync<T>(
        HttpMethod method, string path, HttpContent? content,
        string accessToken, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, BasePath + path) { Content = content };
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

    private static ApiResult<T> TransportFailure<T>(string message, string messageArabic) =>
        ApiResult<T>.Fail(new ApiError
        {
            Code = ErrorCodes.InternalError,
            Message = message,
            MessageArabic = messageArabic,
        });
}
