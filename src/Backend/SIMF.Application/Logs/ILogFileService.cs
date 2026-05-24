// Tests: SIMF.Api.Tests/LogsEndpointsTests.cs (todo).
using SIMF.Contracts.Logs;

namespace SIMF.Application.Logs;

/// <summary>
/// Read-only access to the per-project log files written under
/// <c>{Storage:LogDirectory}</c> (P6). Every method is path-traversal safe —
/// only the project + file name pair is accepted from the caller.
/// </summary>
public interface ILogFileService
{
    /// <summary>Lists every project sub-directory and the files inside.</summary>
    Task<LogListResponse> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the last <paramref name="lineCount"/> lines of one file. Used by
    /// the CP page for the real-time tail. Returns <c>null</c> when the project
    /// or file is missing.
    /// </summary>
    Task<LogTailResponse?> TailAsync(
        string project,
        string fileName,
        int lineCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the file as a shared read stream (other writers must keep
    /// appending). The caller owns the stream's lifetime. Returns
    /// <c>null</c> when the project or file is missing.
    /// </summary>
    Stream? OpenRead(string project, string fileName);
}
