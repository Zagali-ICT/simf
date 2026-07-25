using SIMF.Contracts.Organization;

namespace SIMF.Application.Configuration.Abstractions;

/// <summary>D-495 — the public, cached read-path over the singleton Organization
/// Profile. Returns the projected public response plus the <c>Last-Modified</c>
/// revalidation token (the row's last-write instant). Read-only; admin writes go
/// through <see cref="IOrganizationProfileAdminService"/>, which calls
/// <see cref="Invalidate"/> on every change.</summary>
public interface IOrganizationProfileReadService
{
    Task<OrganizationProfileSnapshot> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Drop the cached snapshot so the next read reloads from the DB.</summary>
    void Invalidate();
}

/// <summary>The cached profile + the instant it last changed (the conditional-GET
/// <c>Last-Modified</c> token, truncated to the second by the endpoint).</summary>
public sealed record OrganizationProfileSnapshot(
    OrganizationProfileResponse Profile,
    DateTimeOffset LastModified);
