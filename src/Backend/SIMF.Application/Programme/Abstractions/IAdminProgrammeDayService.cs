using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.Programme.Abstractions;

/// <summary>D-452 — admin CRUD over the <c>ProgrammeDay</c> rows (date + bilingual
/// title; the logo rides the D-357 asset pipeline). Mirrors
/// <see cref="IAdminSessionCategoryService"/>.</summary>
public interface IAdminProgrammeDayService
{
    /// <summary>One page of programme-day summaries for the admin grid.</summary>
    Task<GridPage<AdminProgrammeDaySummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>One programme day by id, or null when it is missing.</summary>
    Task<AdminProgrammeDayDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new programme day and returns its full detail.</summary>
    Task<AdminProgrammeDayDetail> CreateAsync(
        Guid actorUserId, AdminCreateProgrammeDayRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Updates an existing programme day and returns its full detail.</summary>
    Task<AdminProgrammeDayDetail> UpdateAsync(
        Guid actorUserId, Guid id, AdminUpdateProgrammeDayRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes (deactivates) a programme day.</summary>
    Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default);
}
