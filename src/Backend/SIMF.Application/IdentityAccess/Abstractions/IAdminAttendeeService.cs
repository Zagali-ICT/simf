using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Read-only roster of every event attendee. Joins
/// <c>SimfUser</c> + <c>UserProfile</c> + <c>ProfileType</c>. Filters by
/// UserType (Visitor / Other — default both; Admin excluded),
/// ProfileTypeId, AccountState, and free-text search across email +
/// display name. **Path 2 — no schema change.**
/// </summary>
public interface IAdminAttendeeService
{
    /// <summary>One page of the attendees grid. <see cref="GridQuery.Filters"/>
    /// keys: <c>userType</c> (Visitor|Other|All, default All),
    /// <c>profileTypeId</c> (Guid), <c>accountState</c> (AccountState enum
    /// name), <c>from</c> + <c>to</c> (ISO-8601, on CreatedAt). Default sort:
    /// newest CreatedAt first.</summary>
    Task<GridPage<AdminAttendeeSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>P1.6 — XLSX of the filtered roster (same
    /// <see cref="GridQuery.Filters"/> as <see cref="ListAsync"/>, bounded to a
    /// safe row cap). Writes an <c>Admin.AttendeesExported</c> audit row.</summary>
    Task<byte[]> ExportAsync(
        Guid actorUserId, GridQuery query, CancellationToken cancellationToken = default);
}
