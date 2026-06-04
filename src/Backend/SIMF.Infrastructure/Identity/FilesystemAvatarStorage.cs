// Tests: SIMF.Api.Tests/ProfileEndpointsTests.cs (integration), via a temp directory.
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Abstractions;
using SIMF.Common.Options;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// File-system implementation of <see cref="IAvatarStorage"/>. Avatars live in
/// the configured base directory as <c>{userId}.{ext}</c>. Writes are atomic
/// (temp-file + move) so a partial write never replaces a previous good file.
/// The filename is derived only from server-controlled values (the user id
/// + a fixed extension from the validated content type), never the
/// client-supplied <c>FileName</c> — so path traversal is impossible by
/// construction (D-039).
/// </summary>
internal sealed class FilesystemAvatarStorage : IAvatarStorage
{
    private readonly string _baseDirectory;
    private readonly ILogger<FilesystemAvatarStorage> _logger;

    public FilesystemAvatarStorage(
        IOptions<StorageOptions> options,
        ILogger<FilesystemAvatarStorage> logger)
    {
        // R1 — D-074: typed options replace the raw IConfiguration[…] read.
        // The required-key check stays here (rather than ValidateOnStart) so
        // the failure mode and message are identical to the pre-R1 shape.
        var configured = options.Value.AvatarBase;
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "Configuration value 'Storage:AvatarBase' is required but was not found.");
        }
        _baseDirectory = Path.GetFullPath(configured);
        Directory.CreateDirectory(_baseDirectory);
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

        // A previous avatar may have used a different extension — remove every
        // file the user owns so only the new one survives.
        DeleteAnyAvatarFor(userId);

        // Atomic write: a partial write never replaces a previous file.
        var temp = fullPath + ".tmp";
        await File.WriteAllBytesAsync(temp, content, cancellationToken);
        File.Move(temp, fullPath, overwrite: true);

        _logger.LogInformation(
            "Avatar saved at {Path} ({Bytes} bytes, {ContentType})",
            relativePath, content.Length, contentType);
        return relativePath;
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSafe(relativePath);
        if (fullPath is not null && File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Avatar deleted at {Path}", relativePath);
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
    /// Resolves a relative path to an absolute one and confirms it stays
    /// within the base directory. Returns null if the input would escape
    /// the base — defence in depth, since the caller already controls the
    /// path; this is the last line against a future caller passing
    /// untrusted data.
    /// </summary>
    private string? ResolveSafe(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        var full = Path.GetFullPath(Path.Combine(_baseDirectory, relativePath));
        if (!full.StartsWith(_baseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Rejected avatar path {Path} — outside the storage base directory.",
                relativePath);
            return null;
        }
        return full;
    }

    private void DeleteAnyAvatarFor(Guid userId)
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
