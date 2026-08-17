// The typed pointer an owning row holds to its file — Sponsor.LogoFileId,
// MediaPartner.LogoFileId, News.ImageFileId, ArchiveEdition.CoverImageFileId,
// Banner.ImageFileId — must track that owner's active asset through every
// transition AssetService can perform.
//
// This file exists because nothing else would notice if it stopped. The serve
// path resolves an asset through StoredFile's polymorphic owner pair, so an
// upload renders correctly whether or not the pointer was ever written; the
// column could silently return to being decorative and every other test would
// still pass. These assertions are the reason it cannot.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Files;
using SIMF.Contracts.Assets;
using SIMF.Domain.Archive;
using SIMF.Domain.Cms;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.PublicRelations;
using SIMF.Domain.Sponsors;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Files)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class AssetOwnerPointerTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    // Minimal 1x1 PNG — the upload pipeline magic-byte checks the bytes, so a
    // synthetic array would be rejected before any pointer was written.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AssetOwnerPointerTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Uploading_a_sponsor_logo_points_the_sponsor_at_the_new_file()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var sponsorId = Guid.NewGuid();
        await SeedSponsorAsync(sponsorId);

        var resp = await UploadAsync("SponsorLogo", sponsorId, token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var fileId = await ActiveAssetIdAsync(sponsorId, token);
        var pointer = await ReadAsync(db => db.Sponsors
            .Where(x => x.Id == sponsorId).Select(x => x.LogoFileId).FirstAsync());
        Assert.Equal(fileId, pointer);
    }

    [Fact]
    public async Task Linking_a_news_image_points_the_article_at_the_link_row()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var newsId = Guid.NewGuid();
        await SeedNewsAsync(newsId);

        var resp = await PutAuthAsync(
            $"/api/v1/admin/assets/NewsImage/{newsId}/link",
            new SetAssetLinkRequest { Kind = AssetKind.Image, Url = "https://cdn.example.com/a.png" },
            token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var fileId = await ActiveAssetIdAsync(newsId, token);
        var pointer = await ReadAsync(db => db.News
            .Where(x => x.Id == newsId).Select(x => x.ImageFileId).FirstAsync());
        Assert.Equal(fileId, pointer);
    }

    [Fact]
    public async Task Replacing_a_partner_logo_moves_the_pointer_off_the_retired_file()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var partnerId = Guid.NewGuid();
        await SeedMediaPartnerAsync(partnerId);

        Assert.Equal(HttpStatusCode.OK, (await UploadAsync("MediaPartnerLogo", partnerId, token)).StatusCode);
        var first = await ActiveAssetIdAsync(partnerId, token);

        Assert.Equal(HttpStatusCode.OK, (await UploadAsync("MediaPartnerLogo", partnerId, token)).StatusCode);
        var second = await ActiveAssetIdAsync(partnerId, token);

        Assert.NotEqual(first, second);
        var pointer = await ReadAsync(db => db.MediaPartners
            .Where(x => x.Id == partnerId).Select(x => x.LogoFileId).FirstAsync());
        Assert.Equal(second, pointer);
    }

    [Fact]
    public async Task Deactivating_an_archive_cover_clears_the_edition_pointer()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var editionId = Guid.NewGuid();
        await SeedArchiveEditionAsync(editionId);

        Assert.Equal(HttpStatusCode.OK, (await UploadAsync("ArchiveCover", editionId, token)).StatusCode);
        var fileId = await ActiveAssetIdAsync(editionId, token);

        var resp = await DeleteAuthAsync($"/api/v1/admin/assets/item/{fileId}", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var pointer = await ReadAsync(db => db.ArchiveEditions
            .Where(x => x.Id == editionId).Select(x => x.CoverImageFileId).FirstAsync());
        Assert.Null(pointer);
    }

    [Fact]
    public async Task Restoring_a_banner_image_points_the_banner_back_at_it()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var bannerId = Guid.NewGuid();
        await SeedBannerAsync(bannerId);

        Assert.Equal(HttpStatusCode.OK, (await UploadAsync("Banner", bannerId, token)).StatusCode);
        var fileId = await ActiveAssetIdAsync(bannerId, token);

        Assert.Equal(HttpStatusCode.OK,
            (await DeleteAuthAsync($"/api/v1/admin/assets/item/{fileId}", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await PostAuthAsync($"/api/v1/admin/assets/item/{fileId}/restore", new { }, token)).StatusCode);

        var pointer = await ReadAsync(db => db.Banners
            .Where(x => x.Id == bannerId).Select(x => x.ImageFileId).FirstAsync());
        Assert.Equal(fileId, pointer);
    }

    // Deleting the file straight through the file store never reaches the asset
    // service, so this is the path where the pointer would be left naming a dead
    // file if the sync lived only there.
    [Fact]
    public async Task Deleting_the_file_directly_also_clears_the_owner_pointer()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var newsId = Guid.NewGuid();
        await SeedNewsAsync(newsId);

        Assert.Equal(HttpStatusCode.OK, (await UploadAsync("NewsImage", newsId, token)).StatusCode);
        var fileId = await ActiveAssetIdAsync(newsId, token);
        Assert.NotNull(fileId);

        var resp = await DeleteAuthAsync($"/api/v1/files/{fileId}", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var pointer = await ReadAsync(db => db.News
            .Where(x => x.Id == newsId).Select(x => x.ImageFileId).FirstAsync());
        Assert.Null(pointer);
    }

    // The archive's two child categories. These shipped with NO case in
    // OwnerPointerSync at all, so the upload succeeded, the file served, the
    // Control Panel preview rendered it - and the pointer stayed null, which is
    // the one thing the public read gates on. Every archive picture would have
    // been invisible in the app. Nothing failed: the serve path resolves by owner
    // pair, so only the pointer knew, and only these assertions ask it.
    [Fact]
    public async Task Uploading_a_past_speaker_photo_points_the_speaker_row_at_it()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var speakerId = await SeedArchivePastSpeakerAsync();

        var resp = await UploadAsync("ArchivePastSpeakerPhoto", speakerId, token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var fileId = await ActiveAssetIdAsync(speakerId, token);
        var pointer = await ReadAsync(db => db.ArchivePastSpeakers
            .Where(x => x.Id == speakerId).Select(x => x.PhotoFileId).FirstAsync());
        Assert.Equal(fileId, pointer);
    }

    [Fact]
    public async Task Uploading_a_gallery_image_points_the_gallery_row_at_it()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var itemId = await SeedArchiveMediaItemAsync();

        var resp = await UploadAsync("ArchiveGalleryImage", itemId, token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var fileId = await ActiveAssetIdAsync(itemId, token);
        var pointer = await ReadAsync(db => db.ArchiveMediaItems
            .Where(x => x.Id == itemId).Select(x => x.MediaFileId).FirstAsync());
        Assert.Equal(fileId, pointer);
    }

    // A speaker photo used to be the case with no pointer column at all, and this
    // test asserted only that the upload survived the fall-through. It has a
    // column now, so it asserts what the others do.
    [Fact]
    public async Task Uploading_a_speaker_photo_points_the_speaker_at_it()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var speakerId = Guid.NewGuid();
        await SeedSpeakerAsync(speakerId);

        var resp = await UploadAsync("SpeakerPhoto", speakerId, token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var fileId = await ActiveAssetIdAsync(speakerId, token);
        Assert.NotNull(fileId);

        var pointer = await ReadAsync(db => db.Speakers
            .Where(x => x.Id == speakerId).Select(x => x.PhotoFileId).FirstAsync());
        Assert.Equal(fileId, pointer);
    }

    /// <summary>The rule, rather than another example of it.
    ///
    /// <para>Every test above pins one service by hand, which is how three of them
    /// (BoothLogo, ExhibitorLogo, ProgrammeDayImage) had no test at all while their
    /// owners had no pointer column either - the gap in the tests and the gap in the
    /// schema were the same gap, and neither was visible from the other.</para>
    ///
    /// <para>This asserts the invariant instead: a service whose files belong to a
    /// real entity must have a column for that entity to point back with, and a case
    /// in OwnerPointerSync to keep it in step. A new service that skips either fails
    /// here rather than silently joining the reverse-linked minority.</para></summary>
    [Fact]
    public void Every_file_service_with_an_owner_has_a_pointer_column_and_a_sync_case()
    {
        var syncSource = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Backend", "SIMF.Infrastructure", "Files", "OwnerPointerSync.cs"));

        var missing = FileServicePolicies.All
            .Where(policy => policy.OwnerEntityType != FileOwnerEntityType.None)
            .Select(policy => policy.Service)
            .Distinct()
            .Where(service => !syncSource.Contains(
                $"case FileService.{service}:", StringComparison.Ordinal))
            .Select(service => service.ToString())
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // Exemptions. Each one is a service whose owning row DOES carry a pointer
        // column - the column is simply written by the service that owns the
        // workflow rather than by OwnerPointerSync, because these files are created
        // through their own endpoints and never through the generic asset pipeline.
        // Listed individually with where the write happens, so "not in the switch"
        // stays a deliberate statement rather than an oversight:
        //
        //   Avatar              SimfUser.AvatarFileId        AccountService
        //   IdDocument          UserProfile.IdImageFileId    UserProfileService
        //   VipPhoto            UserProfile.VipPhotoFileId   UserProfileService
        //   MediaGalleryImage   MediaItem.ImageFileId        AdminMediaService
        //   SessionRecording    Session.RecordingFileId      AdminSessionService
        //   SpeakerPresentation SpeakerPresentation.StoredFileId
        //                                                    AdminSpeakerPresentationService
        //
        // CompanyLogo is the only entry that is NOT a pointer maintained elsewhere:
        // there is no Company or Contact entity for it to point at, AssetService
        // .OwnerIsActiveAsync has no arm for it, and a public resolve always returns
        // null. It is a dead service pending removal, not a table awaiting a column.
        string[] maintainedByTheirOwnService =
        [
            nameof(FileService.Avatar),
            nameof(FileService.IdDocument),
            nameof(FileService.VipPhoto),
            nameof(FileService.MediaGalleryImage),
            nameof(FileService.SessionRecording),
            nameof(FileService.SpeakerPresentation),
            nameof(FileService.CompanyLogo),
        ];
        missing.RemoveAll(maintainedByTheirOwnService.Contains);

        Assert.True(
            missing.Count == 0,
            "These file services own files on behalf of a real entity but have no "
            + "case in OwnerPointerSync, so the owning row is never pointed at its "
            + "file and the link survives only as the store's polymorphic "
            + "OwnerEntityType/OwnerEntityId pair - which no foreign key constrains. "
            + "Add a Guid? XFileId column to the owning entity, an FK in its EF "
            + "configuration, and a case here. Offenders: "
            + string.Join(", ", missing));
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "SIMF.slnx")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return directory!.FullName;
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<T> ReadAsync<T>(Func<SimfAppDbContext, Task<T>> read)
    {
        using var scope = _factory.Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<SimfAppDbContext>());
    }

    private async Task WriteAsync(Action<SimfAppDbContext> add)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        add(db);
        await db.SaveChangesAsync();
    }

    private Task SeedSponsorAsync(Guid id) => WriteAsync(db => db.Sponsors.Add(new Sponsor
    {
        Id = id,
        Name = "Pointer Sponsor",
        NameArabic = "الراعي",
        IsActive = true,
    }));

    private Task SeedMediaPartnerAsync(Guid id) => WriteAsync(db => db.MediaPartners.Add(new MediaPartner
    {
        Id = id,
        Name = "Pointer Partner",
        NameArabic = "الشريك",
        IsActive = true,
    }));

    private Task SeedNewsAsync(Guid id) => WriteAsync(db => db.News.Add(new News
    {
        Id = id,
        Title = "Pointer Article",
        TitleArabic = "المقال",
        Body = "b",
        BodyArabic = "ب",
        Category = "NEWS",
        CategoryArabic = "أخبار",
        IsActive = true,
        PublishedAt = DateTime.UtcNow.AddDays(-1),
    }));

    // Year is uniquely indexed and the content seed already occupies the real
    // ones, so take the next free year rather than hashing into a range that
    // overlaps them.
    private async Task SeedArchiveEditionAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var year = await db.ArchiveEditions.AnyAsync()
            ? await db.ArchiveEditions.MaxAsync(x => x.Year) + 1
            : 2100;
        db.ArchiveEditions.Add(new ArchiveEdition
        {
            Id = id,
            Year = year,
            TitleEn = "Pointer Edition",
            TitleAr = "النسخة",
            IsActive = true,
        });
        await db.SaveChangesAsync();
    }

    private Task SeedBannerAsync(Guid id) => WriteAsync(db => db.Banners.Add(new Banner
    {
        Id = id,
        Title = "Pointer Banner",
        TitleArabic = "اللافتة",
        Body = "b",
        BodyArabic = "ب",
        Start = DateTime.UtcNow.AddDays(-1),
        End = DateTime.UtcNow.AddDays(1),
        IsActive = true,
    }));

    private Task SeedSpeakerAsync(Guid id) => WriteAsync(db => db.Speakers.Add(new SIMF.Domain.Programme.Speaker
    {
        Id = id,
        Code = id.ToString("N")[..16],
        Name = "Pointer Speaker",
        NameArabic = "المتحدث",
        IsActive = true,
    }));

    // Both archive children are snapshot rows of an edition, so the edition has
    // to exist first; neither carries an active flag of its own.
    private async Task<Guid> SeedArchivePastSpeakerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var edition = await NewEditionAsync(db);
        var speaker = new SIMF.Domain.Archive.ArchivePastSpeaker
        {
            Id = Guid.NewGuid(),
            ArchiveEditionId = edition.Id,
            NameEn = "Pointer Past Speaker",
            NameAr = "\u0645\u062a\u062d\u062f\u062b",
            DisplayOrder = 0,
        };
        db.ArchivePastSpeakers.Add(speaker);
        await db.SaveChangesAsync();
        return speaker.Id;
    }

    private async Task<Guid> SeedArchiveMediaItemAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var edition = await NewEditionAsync(db);
        var item = new SIMF.Domain.Archive.ArchiveMediaItem
        {
            Id = Guid.NewGuid(),
            ArchiveEditionId = edition.Id,
            Kind = SIMF.Common.Enums.ArchiveMediaKind.Image,
            CaptionEn = "Pointer gallery item",
            DisplayOrder = 0,
        };
        db.ArchiveMediaItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private static async Task<ArchiveEdition> NewEditionAsync(SimfAppDbContext db)
    {
        var year = await db.ArchiveEditions.AnyAsync()
            ? await db.ArchiveEditions.MaxAsync(x => x.Year) + 1
            : 2100;
        var edition = new ArchiveEdition
        {
            Id = Guid.NewGuid(),
            Year = year,
            TitleEn = "Pointer Edition",
            TitleAr = "\u0627\u0644\u0646\u0633\u062e\u0629",
            IsActive = true,
        };
        db.ArchiveEditions.Add(edition);
        await db.SaveChangesAsync();
        return edition;
    }

    private Task<HttpResponseMessage> UploadAsync(string category, Guid owner, string token)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "File", "logo.png");
        var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/admin/assets/{category}/{owner}/image?kind=Image") { Content = form };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req);
    }

    private async Task<Guid?> ActiveAssetIdAsync(Guid owner, string token)
    {
        var resp = await PostAuthAsync("/api/v1/admin/assets/list", new GridQuery { Top = 200 }, token);
        var page = (await resp.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminAssetSummary>>>())!.Data!;
        return page.Items.FirstOrDefault(a => a.OwnerId == owner && a.IsActive)?.Id;
    }

    // An administrator sign-in carries a second factor, so this goes through the
    // shared AuthFlow helper rather than posting credentials directly.
    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"pointer-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AdministratorRole))
            {
                await roles.CreateAsync(new SimfRole { Name = AdministratorRole });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Pointer Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req);
    }

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var req = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req);
    }

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req);
    }
}
