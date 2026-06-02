namespace SIMF.Infrastructure.Programme;

/// <summary>P2.3 — D-228: options for <see cref="FilesystemSpeakerPresentationStorage"/>,
/// mirroring <c>MediaImageStorageOptions</c>.</summary>
public sealed class SpeakerPresentationStorageOptions
{
    public const string SectionName = "SpeakerPresentationStorage";

    /// <summary>Filesystem root under which presentation files are written.</summary>
    public string RootPath { get; set; } = "App_Data/presentations";
}
