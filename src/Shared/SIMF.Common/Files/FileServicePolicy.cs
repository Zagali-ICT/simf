// Tests: SIMF.Api.Tests/Files/FileServicePolicyTests.cs (every FileService is
//        mapped to a deliberate, reviewed policy — default-deny guard).
using SIMF.Common.Enums;

namespace SIMF.Common.Files;

/// <summary>How the single download-by-GUID endpoint authorizes a file,
/// derived from the file's own <see cref="FileService"/> (never the URL), so a
/// guessed/leaked GUID for a private file is still rejected.</summary>
public enum FileAccessClass
{
    /// <summary>Anyone, including anonymous (logos, gallery, news, banners).</summary>
    Public = 0,

    /// <summary>Any approved signed-in account (presentations, recordings).</summary>
    Authenticated = 1,

    /// <summary>An admin holding <see cref="FileServicePolicy.AdminPermission"/> (VIP photo).</summary>
    Admin = 2,

    /// <summary>The owning user (self) OR an admin holding
    /// <see cref="FileServicePolicy.AdminPermission"/> (avatar, ID document).</summary>
    OwnerOrAdmin = 3,
}

/// <summary>The immutable policy a <see cref="FileService"/> resolves to:
/// the single source of every per-file protection (classification, authorization,
/// encryption-at-rest default, the upload allow-list, retention, deletability and
/// the owner-entity family). Consumed by both the upload pipeline and the
/// download endpoint. This object is also the file/PII classification register
/// (SAMA A1-3 / NCA ECC 2-7-2).</summary>
/// <param name="Service">The category this policy governs.</param>
/// <param name="Tier">The persisted data-classification tier.</param>
/// <param name="Access">Who may download a file of this service.</param>
/// <param name="AdminPermission">The permission code an admin needs for the
/// <see cref="FileAccessClass.Admin"/> / <see cref="FileAccessClass.OwnerOrAdmin"/>
/// branch; null for <see cref="FileAccessClass.Public"/> / <see cref="FileAccessClass.Authenticated"/>.</param>
/// <param name="UploadPermission">The permission code the generic upload / link
/// endpoints require for this service, on top of the flat <c>Files.Upload</c> that
/// opens the endpoint at all. Never null: one flat upload code across every service
/// is what let an admin who may replace a sponsor logo plant a file on any other
/// service. Reuses the owning module's existing write code (the same pairing
/// <c>AssetPermissionRegistry</c> uses per category).</param>
/// <param name="DeletePermission">The permission code <c>DELETE /files/{id}</c>
/// requires for this service, on top of the flat <c>Files.Delete</c>. Never null,
/// for the same reason.</param>
/// <param name="EncryptAtRest">Server-set encryption default; clients cannot downgrade it.</param>
/// <param name="AllowedTypes">The media kinds accepted on upload for this service.</param>
/// <param name="OwnerEntityType">The polymorphic owner family.</param>
/// <param name="OwnerRequired">When true, an upload MUST carry a (server-derived)
/// owner id and the download owner-check can never silently fall through to admin.</param>
/// <param name="Retention">Retention period from which RetainUntil is computed;
/// null = indefinite. (The concrete schedule is still an open owner decision.)</param>
/// <param name="DeletableDefault">Default for StoredFile.IsDeletable.</param>
/// <param name="DedicatedUploadRoute">When set, the generic upload / link endpoints
/// REFUSE this service and name this route instead. It is set for exactly the
/// services whose owner is a person: their dedicated endpoints derive the owner id
/// from the authenticated subject, whereas the generic endpoint takes it from a
/// client form field — which is an IDOR, since the caller chooses whose profile the
/// file lands on. Null = the generic endpoint may serve this service.</param>
public sealed record FileServicePolicy(
    FileService Service,
    FileSensitivityTier Tier,
    FileAccessClass Access,
    string? AdminPermission,
    string UploadPermission,
    string DeletePermission,
    bool EncryptAtRest,
    IReadOnlySet<FileType> AllowedTypes,
    FileOwnerEntityType OwnerEntityType,
    bool OwnerRequired,
    TimeSpan? Retention,
    bool DeletableDefault,
    string? DedicatedUploadRoute = null);

/// <summary>The per-<see cref="FileService"/> policy registry. The single
/// source of truth for file authorization (read AND write), encryption and the
/// upload allow-list.
/// <see cref="Resolve"/> hard-fails on an unmapped service (default-deny), and the
/// guard test asserts every enum value is mapped to a deliberate, reviewed policy
/// — mirroring <c>AssetPermissionRegistryTests</c>, so a new <see cref="FileService"/>
/// cannot ship without a reviewed access decision.</summary>
public static class FileServicePolicies
{
    private static readonly IReadOnlySet<FileType> ImageOnly = new HashSet<FileType> { FileType.Image };
    private static readonly IReadOnlySet<FileType> ImageOrPdf = new HashSet<FileType> { FileType.Image, FileType.Pdf };
    private static readonly IReadOnlySet<FileType> Documents = new HashSet<FileType> { FileType.Pdf, FileType.Document };
    private static readonly IReadOnlySet<FileType> Videos = new HashSet<FileType> { FileType.Video };

    // The admin permission that gates an admin viewing an attendee's private file.
    private const string AttendeeView = PermissionCatalog.Visitors.View;

    // The write gate for the three personal-data services. Their generic upload is
    // refused outright (see DedicatedUploadRoute), so this is the delete gate.
    private const string PersonalWrite = PermissionCatalog.Files.ManagePersonal;

    /// <summary>The routes that own the personal-file writes. Each derives the
    /// owner from the authenticated subject or from an admin route the caller is
    /// already gated on, which is exactly what the generic endpoint's client-chosen
    /// owner id cannot do.</summary>
    private const string AvatarRoutes =
        "POST /api/v1/app/account/avatar (self) or POST /api/v1/admin/{visitors|others}/{id}/avatar";

    private const string IdDocumentRoutes =
        "POST /api/v1/app/account/user-profile/id-image (self) or POST /api/v1/admin/{visitors|others}/{id}/id-document";

    private const string VipPhotoRoute = "POST /api/v1/admin/visitors/{id}/vip-photo";

    private static readonly IReadOnlyDictionary<FileService, FileServicePolicy> Map =
        new Dictionary<FileService, FileServicePolicy>
        {
            // ── Owner-scoped, encrypted (Confidential / Secret) ──────────────
            // The three services whose owner is a PERSON. The generic upload is
            // closed for all three: it takes the owner id from a client form field,
            // so a caller could plant or replace a personal file on anyone's
            // profile. The dedicated routes named below derive the owner instead.
            [FileService.Avatar] = new(
                FileService.Avatar, FileSensitivityTier.Confidential, FileAccessClass.OwnerOrAdmin,
                AttendeeView, UploadPermission: PersonalWrite, DeletePermission: PersonalWrite,
                EncryptAtRest: true, ImageOnly, FileOwnerEntityType.UserProfile,
                OwnerRequired: true, Retention: null, DeletableDefault: true,
                DedicatedUploadRoute: AvatarRoutes),

            [FileService.IdDocument] = new(
                FileService.IdDocument, FileSensitivityTier.Secret, FileAccessClass.OwnerOrAdmin,
                AttendeeView, UploadPermission: PersonalWrite, DeletePermission: PersonalWrite,
                EncryptAtRest: true, ImageOrPdf, FileOwnerEntityType.UserProfile,
                OwnerRequired: true, Retention: null, DeletableDefault: false,
                DedicatedUploadRoute: IdDocumentRoutes),

            [FileService.VipPhoto] = new(
                FileService.VipPhoto, FileSensitivityTier.Confidential, FileAccessClass.Admin,
                AttendeeView, UploadPermission: PersonalWrite, DeletePermission: PersonalWrite,
                EncryptAtRest: true, ImageOnly, FileOwnerEntityType.UserProfile,
                OwnerRequired: true, Retention: null, DeletableDefault: true,
                DedicatedUploadRoute: VipPhotoRoute),

            // ── Authenticated, encrypted (Internal) ──────────────────────────
            [FileService.SpeakerPresentation] = new(
                FileService.SpeakerPresentation, FileSensitivityTier.Internal, FileAccessClass.Authenticated,
                AdminPermission: null,
                UploadPermission: PermissionCatalog.Speakers.Edit,
                DeletePermission: PermissionCatalog.Speakers.Edit,
                EncryptAtRest: true, Documents, FileOwnerEntityType.SpeakerPresentation,
                OwnerRequired: false, Retention: null, DeletableDefault: true),

            // EncryptAtRest:false: a conference recording is
            // Internal-tier (not PII), and Range/seek streaming (HTTP 206) needs a
            // SEEKABLE plaintext file — AES-GCM is not seekable. This matches the
            // posture of the legacy plaintext recording store it replaces (no
            // security regression) and is streamed to disk (never buffered whole).
            [FileService.SessionRecording] = new(
                FileService.SessionRecording, FileSensitivityTier.Internal, FileAccessClass.Authenticated,
                AdminPermission: null,
                UploadPermission: PermissionCatalog.Sessions.Publish,
                DeletePermission: PermissionCatalog.Sessions.Publish,
                EncryptAtRest: false, Videos, FileOwnerEntityType.Session,
                OwnerRequired: false, Retention: null, DeletableDefault: true),

            // ── Public video (plaintext, seekable) ───────────────────────────
            // The landing/home hero background video. Public-tier (shown to
            // anonymous guests on the app home + the website landing) and, like a
            // recording, EncryptAtRest:false so it stays seekable for Range/HTTP-206
            // streaming (AES-GCM is not seekable). It is public branding content,
            // never PII — the same posture as the public logos, only larger and
            // range-served through its own dedicated .mp4 route.
            [FileService.OrganizationHeroVideo] = new(
                FileService.OrganizationHeroVideo, FileSensitivityTier.Public, FileAccessClass.Public,
                AdminPermission: null,
                UploadPermission: PermissionCatalog.OrganizationProfile.Manage,
                DeletePermission: PermissionCatalog.OrganizationProfile.Manage,
                EncryptAtRest: false, Videos, FileOwnerEntityType.OrganizationProfile,
                OwnerRequired: false, Retention: null, DeletableDefault: true),

            // ── Externally hosted feeds (link rows; SIMF stores no bytes) ────
            // Public and typed Videos, so the link validator holds them to the
            // players' own classifier rule. The row exists so a feed carries a
            // media type, a policy, a tier and an owner — none of which the free
            // text columns these replaced could carry.
            [FileService.SessionLiveStream] = PublicVideoLink(
                FileService.SessionLiveStream, FileOwnerEntityType.Session, PermissionCatalog.Sessions.Edit),
            [FileService.SessionSignLanguage] = PublicVideoLink(
                FileService.SessionSignLanguage, FileOwnerEntityType.Session, PermissionCatalog.Sessions.Edit),
            [FileService.SessionSummaryVideo] = PublicVideoLink(
                FileService.SessionSummaryVideo, FileOwnerEntityType.Session, PermissionCatalog.SessionSummaries.Edit),
            [FileService.MediaGalleryVideo] = PublicVideoLink(
                FileService.MediaGalleryVideo, FileOwnerEntityType.MediaItem, PermissionCatalog.Media.Edit),
            [FileService.OrganizationLiveStream] = PublicVideoLink(
                FileService.OrganizationLiveStream, FileOwnerEntityType.OrganizationProfile,
                PermissionCatalog.OrganizationProfile.Manage),
            [FileService.ArchiveGalleryVideo] = PublicVideoLink(
                FileService.ArchiveGalleryVideo, FileOwnerEntityType.ArchiveMediaItem, PermissionCatalog.Archive.Edit),

            // ── Public images (plaintext) ────────────────────────────────────
            // Each write gate is the code that already gates the owning module's
            // own edit endpoint — the same pairing AssetPermissionRegistry uses,
            // so one file cannot be written through two different permissions.
            [FileService.MediaGalleryImage] = PublicImage(
                FileService.MediaGalleryImage, FileOwnerEntityType.MediaItem, PermissionCatalog.Media.Edit),
            [FileService.SpeakerPhoto] = PublicImage(
                FileService.SpeakerPhoto, FileOwnerEntityType.Speaker, PermissionCatalog.Speakers.Edit),
            [FileService.NewsImage] = PublicImage(
                FileService.NewsImage, FileOwnerEntityType.News, PermissionCatalog.News.Edit),
            [FileService.SponsorLogo] = PublicImage(
                FileService.SponsorLogo, FileOwnerEntityType.Sponsor, PermissionCatalog.Sponsors.Edit),
            [FileService.MediaPartnerLogo] = PublicImage(
                FileService.MediaPartnerLogo, FileOwnerEntityType.MediaPartner, PermissionCatalog.MediaPartners.Edit),
            [FileService.OrganizationLogo] = PublicImage(
                FileService.OrganizationLogo, FileOwnerEntityType.OrganizationProfile,
                PermissionCatalog.OrganizationProfile.Manage),
            [FileService.ArchiveCover] = PublicImage(
                FileService.ArchiveCover, FileOwnerEntityType.ArchiveEdition, PermissionCatalog.Archive.Edit),
            [FileService.ProgrammeDayImage] = PublicImage(
                FileService.ProgrammeDayImage, FileOwnerEntityType.ProgrammeDay, PermissionCatalog.ProgrammeDays.Edit),
            [FileService.Banner] = PublicImage(
                FileService.Banner, FileOwnerEntityType.Banner, PermissionCatalog.Banners.Edit),
            [FileService.BoothLogo] = PublicImage(
                FileService.BoothLogo, FileOwnerEntityType.Booth, PermissionCatalog.Booths.Edit),
            [FileService.ExhibitorLogo] = PublicImage(
                FileService.ExhibitorLogo, FileOwnerEntityType.Exhibitor, PermissionCatalog.Exhibitors.Edit),
            [FileService.ArchivePastSpeakerPhoto] = PublicImage(
                FileService.ArchivePastSpeakerPhoto, FileOwnerEntityType.ArchivePastSpeaker,
                PermissionCatalog.Archive.Edit),
            [FileService.ArchiveGalleryImage] = PublicImage(
                FileService.ArchiveGalleryImage, FileOwnerEntityType.ArchiveMediaItem, PermissionCatalog.Archive.Edit),
        };

    private static FileServicePolicy PublicImage(
        FileService service, FileOwnerEntityType owner, string writePermission) =>
        new(service, FileSensitivityTier.Public, FileAccessClass.Public,
            AdminPermission: null, UploadPermission: writePermission, DeletePermission: writePermission,
            EncryptAtRest: false, ImageOnly, owner,
            OwnerRequired: false, Retention: null, DeletableDefault: true);

    /// <summary>A feed somebody else hosts. Same public posture as an image, typed
    /// Videos so the external-link validator applies the players' classifier rule
    /// (a YouTube id, or a direct .mp4 / .m3u8 stream) rather than accepting any
    /// https URL. EncryptAtRest is moot — there are no bytes to encrypt.</summary>
    /// <para><c>OwnerRequired</c> stays false, as it is for every other public
    /// service. In this registry that flag means "scoped to a private owner" —
    /// the guard test reads it as implying encryption and a Confidential tier —
    /// and a public feed is neither. The owner is supplied on every write
    /// regardless; requiring it here would assert something untrue about who the
    /// file belongs to.</para>
    private static FileServicePolicy PublicVideoLink(
        FileService service, FileOwnerEntityType owner, string writePermission) =>
        new(service, FileSensitivityTier.Public, FileAccessClass.Public,
            AdminPermission: null, UploadPermission: writePermission, DeletePermission: writePermission,
            EncryptAtRest: false, Videos, owner,
            OwnerRequired: false, Retention: null, DeletableDefault: true);

    /// <summary>Resolves the policy for a file's service. Throws on an unmapped
    /// service — a hard deny, never a silent default. The guard test guarantees
    /// every <see cref="FileService"/> value is present.</summary>
    public static FileServicePolicy Resolve(FileService service) =>
        Map.TryGetValue(service, out var policy)
            ? policy
            : throw new InvalidOperationException(
                $"No FileServicePolicy mapped for '{service}'. Every FileService must have a "
                + "deliberate, reviewed access policy (default-deny).");

    /// <summary>All mapped services — for the guard test and the classification register.</summary>
    public static IReadOnlyCollection<FileServicePolicy> All => (IReadOnlyCollection<FileServicePolicy>)Map.Values;
}
