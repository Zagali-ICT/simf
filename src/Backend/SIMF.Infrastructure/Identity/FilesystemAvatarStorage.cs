// Tests: SIMF.Api.Tests/ProfileEndpointsTests.cs (integration), via a temp directory.
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Abstractions;
using SIMF.Common.Options;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// File-system implementation of <see cref="IAvatarStorage"/> (D-039). The
/// avatar-specific concern is only the base directory; the keyed-file storage +
/// atomic-write + path-safety logic lives in <see cref="FilesystemImageStore"/>
/// (D-430 follow-up).
/// </summary>
internal sealed class FilesystemAvatarStorage : FilesystemImageStore, IAvatarStorage
{
    public FilesystemAvatarStorage(
        IOptions<StorageOptions> options,
        ILogger<FilesystemAvatarStorage> logger)
        : base(ResolveBaseDirectory(options.Value), "Avatar", logger)
    {
    }

    // R1 — D-074: typed options replace the raw IConfiguration[…] read. The
    // required-key check stays here so the failure mode + message are identical
    // to the pre-R1 shape.
    private static string ResolveBaseDirectory(StorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AvatarBase))
        {
            throw new InvalidOperationException(
                "Configuration value 'Storage:AvatarBase' is required but was not found.");
        }
        return options.AvatarBase;
    }
}
