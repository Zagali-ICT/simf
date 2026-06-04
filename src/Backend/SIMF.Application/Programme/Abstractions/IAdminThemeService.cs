using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.Programme.Abstractions;

/// <summary>
/// D-134 Sprint B — admin CRUD over <see cref="SIMF.Domain.Programme.Theme"/>.
/// SIMF-FDS-004 §5.1. Themes are the top-level grouping the agenda uses;
/// Sessions reference a theme via <c>ThemeId</c> in Sprint B's later
/// commits.
/// </summary>
public interface IAdminThemeService
{
    /// <summary>One page of the admin grid. Search matches Code + Name +
    /// NameArabic case-insensitively. Default sort: DisplayOrder asc,
    /// then Name.</summary>
    Task<GridPage<AdminThemeSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>One theme by id (full detail).</summary>
    Task<AdminThemeDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a theme. <c>Code</c> must be unique
    /// (case-insensitive). Throws <c>ThemeCodeDuplicate</c> (409) on clash.</summary>
    Task<AdminThemeDetail> CreateAsync(
        Guid actorUserId,
        AdminCreateThemeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Updates Code / Name / NameArabic / Description{,Arabic} /
    /// DisplayOrder / PageColor / IsActive.</summary>
    Task<AdminThemeDetail> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateThemeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes the theme (sets <c>IsActive = false</c>).
    /// Idempotent on already-inactive rows. Sessions table will get an
    /// in-use guard in Sprint B's Sessions commit; for now the deactivate
    /// is unconditional.</summary>
    Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default);
}
