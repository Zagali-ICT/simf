namespace SIMF.Common.Enums;

/// <summary>The business category of a <c>StoredFile</c>: the single
/// dimension that drives access control, encryption-at-rest, the upload
/// allow-list, retention and disposal (resolved through
/// <c>FileServicePolicy</c>). This is the centralized file store's "Service"
/// (it supersedes <see cref="AssetCategory"/> + the seven bespoke stores).
///
/// <para>Persisted as an int; <b>append-only</b> — never rename or reorder an
/// existing value, per the enum-stability rule. A new category takes the next
/// free integer.</para></summary>
public enum FileService
{
    /// <summary>Account profile picture (owner = SimfUser.Id). Owner-or-admin read; encrypted.</summary>
    Avatar = 0,

    /// <summary>National-ID / passport document image (owner = SimfUser.Id).
    /// Owner-or-admin read, no-store, every access audited; encrypted.</summary>
    IdDocument = 1,

    /// <summary>VVIP / VIP welcome photo (owner = SimfUser.Id). Admin read; encrypted.</summary>
    VipPhoto = 2,

    /// <summary>Media-gallery image (owner = MediaItem.Id). Public read.</summary>
    MediaGalleryImage = 3,

    /// <summary>Speaker profile photo (owner = Speaker.Id). Public read.</summary>
    SpeakerPhoto = 4,

    /// <summary>Speaker presentation file — PDF / slides (owner = SpeakerPresentation.Id).
    /// Authenticated read; served as an attachment; encrypted.</summary>
    SpeakerPresentation = 5,

    /// <summary>Recorded session video (owner = Session.Id). Authenticated read,
    /// range-streamed; encrypted.</summary>
    SessionRecording = 6,

    /// <summary>News article image (owner = News.Id). Public read.</summary>
    NewsImage = 7,

    /// <summary>Sponsor logo (owner = Sponsor.Id). Public read.</summary>
    SponsorLogo = 8,

    /// <summary>Media-partner logo (owner = MediaPartner.Id). Public read.</summary>
    MediaPartnerLogo = 9,

    /// <summary>Company / contact-directory logo (owner = Contact.Id). Public read.</summary>
    CompanyLogo = 10,

    /// <summary>Organization-profile logo (owner = OrganizationProfile.SingletonId). Public read.</summary>
    OrganizationLogo = 11,

    /// <summary>Archive-edition cover image (owner = ArchiveEdition.Id). Public read.</summary>
    ArchiveCover = 12,

    /// <summary>Programme-day banner image (owner = ProgrammeDay.Id). Public read.</summary>
    ProgrammeDayImage = 13,

    /// <summary>Time-windowed home banner (owner = Banner.Id). Public read.</summary>
    Banner = 14,

    /// <summary>Exhibition booth logo (owner = Booth.Id). Public read.</summary>
    BoothLogo = 15,

    /// <summary>Exhibitor company logo (owner = Exhibitor.Id). Public read.</summary>
    ExhibitorLogo = 16,

    /// <summary>Home/landing hero background video
    /// (owner = OrganizationProfile.SingletonId). Public read, range-streamed;
    /// plaintext (kept seekable for HTTP 206, like <see cref="SessionRecording"/>).</summary>
    OrganizationHeroVideo = 17,
}
