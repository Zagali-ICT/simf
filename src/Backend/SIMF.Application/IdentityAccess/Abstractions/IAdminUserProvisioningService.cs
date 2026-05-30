using SIMF.Common;
using SIMF.Contracts.Authentication;

using SIMF.Common.Enums;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Admin-driven user provisioning for the three <c>UserType</c> families:
/// create, list, list-pending, and duplicate. R2 — D-075: split out of
/// <c>IAdminAccountService</c> per Architecture SEV-1.2.
/// </summary>
public interface IAdminUserProvisioningService
{
    // -- Admin family (UserType = Admin) -------------------------------------

    /// <summary>Creates a new Admin (P7c — replaces <c>CreateStaffAsync</c>).</summary>
    Task<AdminCreateUserResponse> CreateAdminAsync(
        Guid actorUserId,
        AdminCreateAdminRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>One page of Admin-typed accounts (P7c — replaces <c>ListStaffAsync</c>).</summary>
    Task<GridPage<AdminUserSummary>> ListAdminsAsync(
        GridQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>One page of pending-approval Admins (P7c).</summary>
    Task<GridPage<AdminPendingUserSummary>> ListPendingAdminsAsync(
        GridQuery query,
        CancellationToken cancellationToken = default);

    // -- Other family (UserType = Other) -------------------------------------

    /// <summary>Creates a new Other (P7c — new).</summary>
    Task<AdminCreateUserResponse> CreateOtherAsync(
        Guid actorUserId,
        AdminCreateOtherRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>One page of Other-typed accounts (P7c — new).</summary>
    Task<GridPage<AdminUserSummary>> ListOthersAsync(
        GridQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>One page of pending-approval Others (P7c — new).</summary>
    Task<GridPage<AdminPendingUserSummary>> ListPendingOthersAsync(
        GridQuery query,
        CancellationToken cancellationToken = default);

    // -- Visitor family (UserType = Visitor) ---------------------------------

    /// <summary>Creates a new Visitor (P3 — P7c added optional ProfileTypeId).</summary>
    Task<AdminCreateUserResponse> CreateVisitorAsync(
        Guid actorUserId,
        AdminCreateVisitorRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>One page of Visitor-typed accounts (P3 — P7c rekeyed off UserType).</summary>
    Task<GridPage<AdminUserSummary>> ListVisitorsAsync(
        GridQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>One page of pending-approval Visitors (P4).</summary>
    Task<GridPage<AdminPendingUserSummary>> ListPendingVisitorsAsync(
        GridQuery query,
        CancellationToken cancellationToken = default);

    // -- Duplicate (single-user create with a different source) --------------

    /// <summary>
    /// Creates a new user as a copy of an existing one — same display name,
    /// same UserType + RBAC roles, no password, fresh 7-day invite email
    /// (D-044 b).
    /// </summary>
    Task<AdminCreateUserResponse> DuplicateUserAsync(
        Guid actorUserId,
        AdminDuplicateUserRequest request,
        CancellationToken cancellationToken = default);

    // -- D-127 — walk-in / desk registration --------------------------------

    /// <summary>
    /// On-site walk-in registration for a Visitor or Other (drives the
    /// `kind` parameter). Single transaction:
    /// <list type="bullet">
    ///   <item>Creates the SimfUser with <c>AccountState = Approved</c>
    ///     (no pending queue — staff already verified the person in
    ///     hand).</item>
    ///   <item>Creates the UserProfile with every form field, links the
    ///     picked Interests, sets the ProfileTypeId, mints the QR id.</item>
    ///   <item>Audits the registration (<c>Admin.WalkInRegistered</c>).</item>
    /// </list>
    /// Email is optional; when missing the implementation synthesizes
    /// <c>walkin-{guid}@simf.local</c> so ASP.NET Core Identity stays
    /// valid. Phone (Saudi or International, depending on
    /// <see cref="AdminWalkInRegistrationRequest.IsSaudi"/>) is the
    /// soft uniqueness key.
    /// </summary>
    /// <summary>D-186 review-pass: <paramref name="expectedIsVisitor"/> is
    /// the audience-vs-partner desk guard — the Visitors desk endpoint
    /// passes <c>true</c>, the Others desk endpoint passes <c>false</c>.
    /// A mismatch rejects with <c>AdminProfileTypeInvalid</c> instead of
    /// silently routing the account to the wrong queue. Null leaves the
    /// guard off (legacy callers).</summary>
    Task<AdminWalkInRegistrationResponse> RegisterOnSiteAsync(
        Guid actorUserId,
        SIMF.Common.Enums.UserType kind,
        AdminWalkInRegistrationRequest request,
        CancellationToken cancellationToken = default,
        bool? expectedIsVisitor = null);
}
