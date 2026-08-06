using SIMF.Common;

namespace SIMF.Contracts.Admin;

/// <summary>
/// Request body for a generic grid Excel export. Mirrors the per-user
/// <c>AdminExportUsersRequest</c> shape so the CP grid can post one body for
/// every resource: the selected row ids, or — when none are selected — the
/// grid query whose full (capped) result set to export.
/// </summary>
public sealed class AdminGridExportRequest
{
    /// <summary>The row ids to export. Empty means "all matching the query".</summary>
    public IList<Guid> Ids { get; set; } = new List<Guid>();

    /// <summary>The grid query whose result set to export (used only when <see cref="Ids"/> is empty).</summary>
    public GridQuery? Query { get; set; }
}

/// <summary>
/// Per-row outcome summary of a generic grid Excel import. Mirrors the
/// per-user <c>AdminImportUsersResponse</c> but adds <see cref="Updated"/> for
/// resources whose import upserts (create-or-update) rather than insert-only.
/// </summary>
public sealed record AdminGridImportResult(
    int Created,
    int Updated,
    int Skipped,
    IReadOnlyList<GridImportRowError> Errors);

/// <summary>One failed row in a generic grid import. <see cref="Key"/>
/// is the human-recognisable value the importer echoes back (e.g. the row's
/// name or code) so the admin can find the offending row; null when unknown.</summary>
public sealed record GridImportRowError(int Row, string? Key, string Reason);
