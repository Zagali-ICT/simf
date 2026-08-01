namespace SIMF.Contracts.Logs;

/// <summary>
/// One project under {Storage:LogDirectory} — e.g. <c>SIMF.Api</c>,
/// <c>SIMF.ControlPanel</c>, <c>SIMF.Web</c>. The names mirror the
/// per-project subdirectories Serilog writes to (P6).
/// </summary>
public sealed record LogProject
{
    public required string Name { get; init; }
    public required int FileCount { get; init; }
}

/// <summary>One log file inside a project's folder.</summary>
public sealed record LogFileEntry
{
    /// <summary>The bare file name (e.g. <c>log-20260524.log</c>) — never a path.</summary>
    public required string FileName { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTime LastModified { get; init; }
}

/// <summary>
/// Response for <c>GET /api/v1/admin/logs/list</c> — every project's file list,
/// pre-sorted newest-first. One round-trip populates both CP dropdowns.
/// </summary>
public sealed record LogListResponse
{
    public required IReadOnlyList<LogProject> Projects { get; init; }
    public required IReadOnlyList<LogProjectFiles> Files { get; init; }
}

/// <summary>The files for one project, in <see cref="LogListResponse"/>.</summary>
public sealed record LogProjectFiles
{
    public required string Project { get; init; }
    public required IReadOnlyList<LogFileEntry> Files { get; init; }
}

/// <summary>
/// Response for <c>GET /api/v1/admin/logs/tail</c> — the last
/// <see cref="LineCount"/> lines of the requested file, plus the total file
/// size so the CP can show "256 KB" / "2.1 MB" next to the live tail.
/// </summary>
public sealed record LogTailResponse
{
    public required string Project { get; init; }
    public required string FileName { get; init; }
    public required long SizeBytes { get; init; }
    public required int LineCount { get; init; }
    public required string Content { get; init; }
}
