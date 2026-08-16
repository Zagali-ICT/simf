using SIMF.Common.Enums;
using SIMF.Common.Files;
using Xunit;

namespace SIMF.Api.Tests.Files;

/// <summary>D-568 — guard: every <see cref="FileService"/> must resolve to a
/// deliberate, reviewed <see cref="FileServicePolicy"/> (default-deny), and the
/// security invariants of that policy hold — so a new service cannot ship without
/// an explicit access + encryption decision. Mirrors <c>AssetPermissionRegistryTests</c>.</summary>
public sealed class FileServicePolicyTests
{
    /// <summary>The public services that serve video rather than images. Every
    /// other public service must be image-only, and enumerating the exceptions
    /// here is what forces a future public video to be a deliberate decision
    /// rather than an accident.
    ///
    /// <para>The hero background is stored bytes, range-served through its own
    /// dedicated .mp4 route. The rest store no bytes at all: they are external
    /// link rows, and their URL is handed to the clients verbatim because both
    /// classify a feed by reading the string.</para></summary>
    private static readonly HashSet<FileService> PublicVideoServices =
    [
        FileService.OrganizationHeroVideo,
        FileService.SessionLiveStream,
        FileService.SessionSignLanguage,
        FileService.SessionSummaryVideo,
        FileService.MediaGalleryVideo,
        FileService.OrganizationLiveStream,
        FileService.ArchiveGalleryVideo,
    ];

    [Fact]
    public void Every_file_service_resolves_to_a_policy_for_itself()
    {
        foreach (var service in Enum.GetValues<FileService>())
        {
            var policy = FileServicePolicies.Resolve(service); // throws if unmapped
            Assert.Equal(service, policy.Service);
            Assert.NotEmpty(policy.AllowedTypes);
        }
    }

    [Fact]
    public void Mapped_policies_cover_the_whole_enum()
    {
        Assert.Equal(Enum.GetValues<FileService>().Length, FileServicePolicies.All.Count);
    }

    [Fact]
    public void Resolving_an_unmapped_service_is_a_hard_deny()
    {
        Assert.Throws<InvalidOperationException>(() => FileServicePolicies.Resolve((FileService)9999));
    }

    [Fact]
    public void Admin_and_owner_gated_services_always_name_an_admin_permission()
    {
        // The IDOR invariant: any non-anonymous, non-"authenticated" access class
        // must carry a concrete admin permission, never a null that would widen access.
        foreach (var policy in FileServicePolicies.All)
        {
            if (policy.Access is FileAccessClass.Admin or FileAccessClass.OwnerOrAdmin)
            {
                Assert.False(string.IsNullOrWhiteSpace(policy.AdminPermission),
                    $"{policy.Service} ({policy.Access}) has no admin permission.");
            }
        }
    }

    [Fact]
    public void Public_and_authenticated_services_do_not_carry_an_admin_permission()
    {
        foreach (var policy in FileServicePolicies.All)
        {
            if (policy.Access is FileAccessClass.Public or FileAccessClass.Authenticated)
            {
                Assert.Null(policy.AdminPermission);
            }
        }
    }

    [Fact]
    public void Owner_scoped_services_are_encrypted_and_classified_above_internal()
    {
        // Avatar / IdDocument / VipPhoto hold personal data: encrypted-at-rest and
        // at least Confidential.
        foreach (var policy in FileServicePolicies.All)
        {
            if (policy.OwnerRequired)
            {
                Assert.True(policy.EncryptAtRest, $"{policy.Service} is owner-scoped but not encrypted.");
                Assert.True(policy.Tier >= FileSensitivityTier.Confidential,
                    $"{policy.Service} is owner-scoped but classified below Confidential.");
                Assert.NotEqual(FileOwnerEntityType.None, policy.OwnerEntityType);
            }
        }
    }

    [Fact]
    public void Public_services_are_plaintext_anyone_can_read()
    {
        foreach (var policy in FileServicePolicies.All)
        {
            if (policy.Access == FileAccessClass.Public)
            {
                Assert.False(policy.EncryptAtRest, $"{policy.Service} is public but encrypted.");
                Assert.Equal(FileSensitivityTier.Public, policy.Tier);

                var expected = PublicVideoServices.Contains(policy.Service)
                    ? new HashSet<FileType> { FileType.Video }
                    : new HashSet<FileType> { FileType.Image };
                Assert.Equal(expected, policy.AllowedTypes);
            }
        }
    }

    [Theory]
    [InlineData(FileService.Avatar, FileAccessClass.OwnerOrAdmin)]
    [InlineData(FileService.IdDocument, FileAccessClass.OwnerOrAdmin)]
    [InlineData(FileService.VipPhoto, FileAccessClass.Admin)]
    [InlineData(FileService.SpeakerPresentation, FileAccessClass.Authenticated)]
    [InlineData(FileService.SessionRecording, FileAccessClass.Authenticated)]
    [InlineData(FileService.SpeakerPhoto, FileAccessClass.Public)]
    [InlineData(FileService.SponsorLogo, FileAccessClass.Public)]
    public void Key_services_keep_their_reviewed_access_class(FileService service, FileAccessClass expected)
    {
        Assert.Equal(expected, FileServicePolicies.Resolve(service).Access);
    }

    [Fact]
    public void The_id_document_is_secret_and_not_deletable_by_default()
    {
        var policy = FileServicePolicies.Resolve(FileService.IdDocument);

        Assert.Equal(FileSensitivityTier.Secret, policy.Tier);
        Assert.False(policy.DeletableDefault);
    }
}
