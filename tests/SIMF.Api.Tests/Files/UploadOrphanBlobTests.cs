// The centralized upload writes the blob to disk and THEN inserts the StoredFile
// row. Anything that fails that insert - a client abort mid-upload, a SQL timeout
// under event-day load, a throw from the audit interceptors on the same
// SaveChanges - used to leave the bytes on disk with no row pointing at them.
// Nothing sweeps those: the retention purge only clears token and idempotency
// rows, and every delete path starts from a StoredFile row, so an encrypted
// ID-document scan could sit in the store indefinitely, invisible to the
// erasure path.
//
// The seam is the upload scanner: it is the last thing the pipeline calls before
// the bytes are written, and a scoped one shares the request's SimfAppDbContext,
// so it can add a row that fails the CK_StoredFiles_SizeBytes check constraint
// and poison exactly this unit of work - the same class of failure a deadlock or
// a dropped connection raises.
using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Abstractions;
using SIMF.Common.Enums;
using SIMF.Domain.Files;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests.Files;

public sealed class UploadCommitFailingApiFactory : SimfApiFactory
{
    /// <summary>Off while the database is seeded (which also stores files); the
    /// test turns it on for the one upload under test.</summary>
    public bool PoisonEnabled { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        var factory = this;
        builder.ConfigureServices(services =>
        {
            // Last registration wins for GetRequiredService<IUploadScanner>.
            services.AddScoped<IUploadScanner>(serviceProvider =>
                new CommitPoisoningScanner(
                    serviceProvider.GetRequiredService<SimfAppDbContext>(),
                    () => factory.PoisonEnabled));
        });
    }

    private sealed class CommitPoisoningScanner(
        SimfAppDbContext appDb, Func<bool> poisonEnabled) : IUploadScanner
    {
        public Task<UploadScanResult> ScanAsync(
            byte[] content, string fileName, CancellationToken cancellationToken = default)
        {
            if (poisonEnabled())
            {
                // SizeBytes = 0 breaks CK_StoredFiles_SizeBytes, so the upload's
                // own SaveChanges throws after its bytes are already on disk.
                appDb.StoredFiles.Add(new StoredFile
                {
                    Id = Guid.NewGuid(),
                    Service = FileService.SpeakerPhoto,
                    FileType = FileType.Image,
                    SourceType = FileSourceType.Upload,
                    SizeBytes = 0,
                });
            }

            return Task.FromResult(UploadScanResult.Clean);
        }
    }
}

[Trait(TestAreas.TraitName, TestAreas.Files)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class UploadOrphanBlobTests : IClassFixture<UploadCommitFailingApiFactory>
{
    private const string AdministratorRole = "Administrator";

    // Minimal 1x1 PNG.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private readonly UploadCommitFailingApiFactory _factory;
    private readonly HttpClient _client;

    public UploadOrphanBlobTests(UploadCommitFailingApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_failed_commit_leaves_no_bytes_behind()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var before = StoredBlobCount();

        _factory.PoisonEnabled = true;
        HttpResponseMessage response;
        try
        {
            response = await UploadSpeakerPhotoAsync(Guid.NewGuid(), token);
        }
        finally
        {
            _factory.PoisonEnabled = false;
        }

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        // The whole point: the write happened, so without the compensation this
        // count is one higher and nothing will ever clear it.
        Assert.Equal(before, StoredBlobCount());
    }

    [Fact]
    public async Task A_successful_upload_still_stores_its_bytes()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var before = StoredBlobCount();

        var response = await UploadSpeakerPhotoAsync(Guid.NewGuid(), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before + 1, StoredBlobCount());
    }

    private int StoredBlobCount() =>
        Directory.Exists(_factory.FileStorageDirectory)
            ? Directory.GetFiles(
                _factory.FileStorageDirectory, "*", SearchOption.AllDirectories).Length
            : 0;

    private Task<HttpResponseMessage> UploadSpeakerPhotoAsync(Guid owner, string token)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "File", "p.png");

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/admin/assets/SpeakerPhoto/{owner}/image?kind=Image")
        {
            Content = form,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"orphan-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Orphan Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }
}
