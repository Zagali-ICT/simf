// Tests: SIMF.Api.Tests/WalkInRegistrationTests.cs (avatar + vip-photo round-trips)
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// D-430 follow-up — the shared base for the user-keyed filesystem image stores
/// (the account avatar and the VVIP/VIP welcome photo). One file per user as
/// <c>{userId}.{ext}</c>; writes are atomic (temp-file + move) so a partial write
/// never replaces a previous good file; the filename is derived only from
/// server-controlled values (the user id + a fixed extension from the validated
/// content type), never the client name — so path traversal is impossible by
/// construction (D-039). Subclasses differ only in the configured base directory
/// (and the log label); collapsing the two near-identical implementations keeps
/// the path-safety + atomic-write logic in exactly one place.
/// </summary>
internal abstract class FilesystemImageStore
{
    private readonly string _baseDirectory;
    private readonly string _label;
    private readonly ILogger _logger;

    protected FilesystemImageStore(string baseDirectory, string label, ILogger logger)
    {
        _baseDirectory = Path.GetFullPath(baseDirectory);
        Directory.CreateDirectory(_baseDirectory);
        _label = label;
        _logger = logger;
    }

    public async Task<string> SaveAsync(
        Guid userId,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var extension = ExtensionFor(contentType);
        var relativePath = $"{userId:N}{extension}";
        var fullPath = Path.Combine(_baseDirectory, relativePath);

        // A previous image may have used a different extension — remove every
        // file the user owns so only the new one survives.
        DeleteAnyFor(userId);

        // Atomic write: a partial write never replaces a previous file.
        var temp = fullPath + ".tmp";
        await File.WriteAllBytesAsync(temp, content, cancellationToken);
        File.Move(temp, fullPath, overwrite: true);

        _logger.LogInformation(
            "{Label} saved at {Path} ({Bytes} bytes, {ContentType})",
            _label, relativePath, content.Length, contentType);
        return relativePath;
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafe(relativePath);
        if (fullPath is not null && File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("{Label} deleted at {Path}", _label, relativePath);
        }
        return Task.CompletedTask;
    }

    public Task<AvatarRead?> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafe(relativePath);
        if (fullPath is null || !File.Exists(fullPath))
        {
            return Task.FromResult<AvatarRead?>(null);
        }

        var contentType = ContentTypeFor(Path.GetExtension(fullPath));
        if (contentType is null)
        {
            return Task.FromResult<AvatarRead?>(null);
        }

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult<AvatarRead?>(new AvatarRead(stream, contentType));
    }

    /// <summary>
    /// Resolves a relative path to an absolute one and confirms it stays within
    /// the base directory. Returns null if the input would escape the base —
    /// defence in depth, since the caller already controls the path.
    /// </summary>
    private string? ResolveSafe(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        var full = Path.GetFullPath(Path.Combine(_baseDirectory, relativePath));
        if (!full.StartsWith(_baseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Rejected {Label} path {Path} — outside the storage base directory.",
                _label, relativePath);
            return null;
        }
        return full;
    }

    private void DeleteAnyFor(Guid userId)
    {
        var prefix = userId.ToString("N");
        foreach (var existing in Directory.EnumerateFiles(_baseDirectory, prefix + ".*"))
        {
            File.Delete(existing);
        }
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/webp" => ".webp",
        _ => throw new ArgumentOutOfRangeException(nameof(contentType),
            contentType, "Unsupported content type — validation should have rejected it."),
    };

    private static string? ContentTypeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        _ => null,
    };
}
