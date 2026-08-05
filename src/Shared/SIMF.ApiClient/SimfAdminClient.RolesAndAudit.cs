// Part of SimfAdminClient - see SimfAdminClient.cs for the transport core.
// roles, the operation log, the attendee roster
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
}
