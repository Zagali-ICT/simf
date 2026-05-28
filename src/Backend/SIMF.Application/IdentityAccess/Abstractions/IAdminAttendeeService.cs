using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// D-134 Sprint A — read-only roster of every event attendee. Joins
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
    /// name). Default sort: newest CreatedAt first.</summary>
    Task<GridPage<AdminAttendeeSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default);
}
