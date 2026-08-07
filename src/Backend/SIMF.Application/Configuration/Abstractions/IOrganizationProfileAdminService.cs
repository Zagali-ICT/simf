using SIMF.Contracts.Admin;
using SIMF.Contracts.Organization;

namespace SIMF.Application.Configuration.Abstractions;

/// <summary>The admin write-path for the singleton Organization Profile.
/// A single full-document upsert: updates the scalar branding fields and reconciles
/// the about-items + details child lists by id (update existing / insert new /
/// soft-delete removed). Every change touches the row's <c>UpdatedAt</c>, audits the
/// actor, and invalidates the public read cache.</summary>
public interface IOrganizationProfileAdminService
{
    /// <summary>The full profile (fresh from the DB, including child-row ids) for
    /// the CP editor.</summary>
    Task<OrganizationProfileResponse> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Apply the full-document upsert on behalf of <paramref name="actorUserId"/>.</summary>
    Task UpdateAsync(
        AdminUpdateOrganizationProfileRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
