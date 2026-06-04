namespace SIMF.Infrastructure.Media;

/// <summary>D-199 — options for <see cref="FilesystemMediaImageStorage"/>,
/// mirroring <c>AvatarStorageOptions</c>.</summary>
public sealed class MediaImageStorageOptions
{
    public const string SectionName = "MediaImageStorage";

    /// <summary>Filesystem root under which media image files are written.</summary>
    public string RootPath { get; set; } = "App_Data/media";
}
