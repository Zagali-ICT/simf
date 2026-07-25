namespace SIMF.Common.Enums;

/// <summary>D-568 — which entity family a <c>StoredFile</c> belongs to. With
/// <c>StoredFile.OwnerEntityId</c> (a bare logical Guid, no DB FK — D-157) it
/// forms the polymorphic back-link the owning row resolves on read, and it
/// scopes the owner-or-admin authorization check on the download endpoint.
///
/// <para>Persisted as an int; <b>append-only</b>. <see cref="None"/> is the
/// non-null default for a file with no owning entity.</para></summary>
public enum FileOwnerEntityType
{
    /// <summary>No owning entity (e.g. a standalone upload).</summary>
    None = 0,

    /// <summary>SimfUser.Id (Identity DB) — avatar / ID-document / VIP photo.</summary>
    UserProfile = 1,

    Speaker = 2,
    Session = 3,
    SpeakerPresentation = 4,
    MediaItem = 5,
    News = 6,
    Sponsor = 7,
    MediaPartner = 8,
    Contact = 9,
    ArchiveEdition = 10,
    ProgrammeDay = 11,
    OrganizationProfile = 12,
    Banner = 13,
    Booth = 14,
    Exhibitor = 15,
}
