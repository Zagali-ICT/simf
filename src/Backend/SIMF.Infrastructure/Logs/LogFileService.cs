// Tests: SIMF.Api.Tests/LogsEndpointsTests.cs (todo).
using Microsoft.Extensions.Configuration;
using SIMF.Application.Logs;
using SIMF.Contracts.Logs;

namespace SIMF.Infrastructure.Logs;

/// <summary>
/// Default implementation of <see cref="ILogFileService"/> — reads the file
/// tree under <c>{Storage:LogDirectory}</c>. All file-system access is
/// validated against the root directory to prevent path traversal
/// (any <c>..</c> or absolute path in <paramref name="project"/> /
/// <paramref name="fileName"/> is rejected by treating only the bare leaf
/// name).
/// </summary>
public sealed class LogFileService : ILogFileService
{
    private const int MaxTailLines = 5_000;

    private readonly string _rootDirectory;

    public LogFileService(IConfiguration configuration)
    {
        var configured = configuration["Storage:LogDirectory"];
        var directory = string.IsNullOrWhiteSpace(configured) ? "logs" : configured;
        _rootDirectory = Path.GetFullPath(directory);
    }

    public Task<LogListResponse> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_rootDirectory))
        {
            return Task.FromResult(new LogListResponse
            {
                Projects = Array.Empty<LogProject>(),
                Files = Array.Empty<LogProjectFiles>(),
            });
        }

        var projects = new List<LogProject>();
        var files = new List<LogProjectFiles>();

        foreach (var directory in Directory.EnumerateDirectories(_rootDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var projectName = Path.GetFileName(directory);
            var entries = Directory
                .EnumerateFiles(directory, "*.log", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .Select(info => new LogFileEntry
                {
                    FileName = info.Name,
                    SizeBytes = info.Length,
                    LastModifiedUtc = info.LastWriteTimeUtc,
                })
                .ToList();

            projects.Add(new LogProject { Name = projectName, FileCount = entries.Count });
            files.Add(new LogProjectFiles { Project = projectName, Files = entries });
        }

        projects.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        files.Sort((a, b) => string.Compare(a.Project, b.Project, StringComparison.Ordinal));

        return Task.FromResult(new LogListResponse { Projects = projects, Files = files });
    }

    public async Task<LogTailResponse?> TailAsync(
        string project,
        string fileName,
        int lineCount,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolve(project, fileName, out var fullPath))
        {
            return null;
        }

        var info = new FileInfo(fullPath);
        var clampedCount = Math.Clamp(lineCount, 1, MaxTailLines);

        // Read the tail with FileShare.ReadWrite — Serilog is still appending.
        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        var lines = await ReadLastLinesAsync(stream, clampedCount, cancellationToken);

        return new LogTailResponse
        {
            Project = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? project,
            FileName = info.Name,
            SizeBytes = info.Length,
            LineCount = lines.Count,
            Content = string.Join('\n', lines),
        };
    }

    public Stream? OpenRead(string project, string fileName)
    {
        if (!TryResolve(project, fileName, out var fullPath))
        {
            return null;
        }

        return new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
    }

    /// <summary>
    /// Resolves <c>{root}/{project}/{fileName}</c> and verifies the result is
    /// inside the configured root. Strips any path component from the inputs;
    /// only the bare leaf name is honoured. This is the path-traversal guard.
    /// </summary>
    private bool TryResolve(string project, string fileName, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var safeProject = Path.GetFileName(project);
        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(safeProject) || string.IsNullOrEmpty(safeFileName))
        {
            return false;
        }
        if (!safeFileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(_rootDirectory, safeProject, safeFileName));
        var rootWithSeparator = _rootDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? _rootDirectory
            : _rootDirectory + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (!File.Exists(candidate))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    /// <summary>
    /// Streams the file and keeps the last <paramref name="count"/> lines in a
    /// ring buffer. Reads from the start (UTF-8 with a permissive decoder so a
    /// truncated multi-byte sequence at the file end never throws).
    /// </summary>
    private static async Task<List<string>> ReadLastLinesAsync(
        Stream stream,
        int count,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        var buffer = new Queue<string>(count);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (buffer.Count == count)
            {
                buffer.Dequeue();
            }
            buffer.Enqueue(line);
        }

        return buffer.ToList();
    }
}
