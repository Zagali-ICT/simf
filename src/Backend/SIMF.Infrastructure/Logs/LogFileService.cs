// Tests: SIMF.Api.Tests/LogFileServiceTests.cs (the tail region matches a
//        whole-file read across the block boundary, with and without a trailing
//        newline, and never splits a multi-byte character).
using Microsoft.Extensions.Options;
using SIMF.Application.Logs;
using SIMF.Contracts.Logs;
using SIMF.Common.Options;

namespace SIMF.Infrastructure.Logs;

/// <summary>
/// Default implementation of <see cref="ILogFileService"/> — reads the file
/// tree under <c>{Storage:LogDirectory}</c>. All file-system access is
/// validated against the root directory to prevent path traversal
/// (any <c>..</c> or absolute path in a supplied <c>project</c> or
/// <c>fileName</c> is rejected by treating only the bare leaf name).
/// </summary>
public sealed class LogFileService : ILogFileService
{
    private const int MaxTailLines = 5_000;

    // The backwards scan reads whole blocks; a file this size or smaller is read
    // from the start, which is what the whole-file path always did.
    private const int TailBlockSize = 64 * 1024;

    private readonly string _rootDirectory;

    public LogFileService(IOptions<StorageOptions> options)
    {
        // Typed options replace the raw IConfiguration[…] read.
        // LogDirectory defaults to "logs" in StorageOptions so the same
        // fallback applies when the config key is unset.
        var configured = options.Value.LogDirectory;
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
                    LastModified = info.LastWriteTimeUtc,
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
    /// Keeps the last <paramref name="count"/> lines in a ring buffer, decoding
    /// only the tail region rather than the whole file. A production rolling log
    /// reaches hundreds of MB before it rolls, and reading it from byte zero
    /// allocated every line in it to keep the last few hundred — on a request an
    /// admin can repeat with every click. <see cref="FindTailStartAsync"/> locates
    /// the region; the forward read over it is unchanged, so decoding and CRLF
    /// handling behave exactly as before.
    /// </summary>
    private static async Task<List<string>> ReadLastLinesAsync(
        FileStream stream,
        int count,
        CancellationToken cancellationToken)
    {
        var start = await FindTailStartAsync(stream, count, cancellationToken);
        stream.Seek(start, SeekOrigin.Begin);

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

    /// <summary>
    /// The byte offset to start decoding from: just past the
    /// (<paramref name="count"/> + 1)-th newline counted back from the end, or 0
    /// when the file holds fewer lines than that. One extra boundary is counted
    /// because the final line need not carry a trailing newline; over-reading by
    /// a line is harmless, since the ring buffer trims to
    /// <paramref name="count"/> anyway.
    ///
    /// <para>Scans backwards in fixed blocks counting the newline BYTE. In UTF-8
    /// no continuation byte can equal 0x0A, so an offset taken immediately after
    /// one is always a character boundary and never splits a multi-byte
    /// sequence. A file no larger than one block is read whole, as before.</para>
    /// </summary>
    private static async Task<long> FindTailStartAsync(
        FileStream stream,
        int count,
        CancellationToken cancellationToken)
    {
        // Read Length once: the file is opened FileShare.ReadWrite and Serilog is
        // still appending to it, so re-reading it mid-scan would move the target.
        var length = stream.Length;
        if (length <= TailBlockSize)
        {
            return 0;
        }

        var block = new byte[TailBlockSize];
        var newlines = 0;
        var position = length;

        while (position > 0)
        {
            var blockLength = (int)Math.Min(TailBlockSize, position);
            position -= blockLength;
            stream.Seek(position, SeekOrigin.Begin);
            await stream.ReadExactlyAsync(block.AsMemory(0, blockLength), cancellationToken);

            for (var index = blockLength - 1; index >= 0; index--)
            {
                if (block[index] != (byte)'\n')
                {
                    continue;
                }
                newlines++;
                if (newlines > count)
                {
                    return position + index + 1;
                }
            }
        }

        return 0;
    }
}
