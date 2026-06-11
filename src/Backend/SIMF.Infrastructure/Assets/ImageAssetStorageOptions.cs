namespace SIMF.Infrastructure.Assets;

/// <summary>D-357 — options for <see cref="FilesystemImageAssetStorage"/>,
/// mirroring <c>MediaImageStorageOptions</c>. The default root is used when no
/// <c>ImageAssetStorage</c> config section is present.</summary>
public sealed class ImageAssetStorageOptions
{
    public const string SectionName = "ImageAssetStorage";

    /// <summary>Filesystem root under which unified media-asset files are written.</summary>
    public string RootPath { get; set; } = "App_Data/assets";
}
