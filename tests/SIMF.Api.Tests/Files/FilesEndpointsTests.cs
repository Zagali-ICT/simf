// D-568 — integration coverage for the centralized file endpoints:
// POST /api/v1/files (one upload), GET /api/v1/files/{id} (download-by-GUID) and
// DELETE /api/v1/files/{id} (soft-delete). Exercises the full StoredFileService
// pipeline: magic-byte detection, the per-service allow-list, encrypt-at-rest +
// envelope round-trip, the upload permission gate, and soft-delete.
//
// The owner-scoped services (Avatar, IdDocument, VipPhoto) are refused on the
// generic upload route — see SeedFileAsync — so the tests that need one of their
// files in place seed it in process and keep asserting the real behaviour
// (encryption, integrity, retention, erasure) through the endpoints.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Files.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Files;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests.Files;

public sealed class FilesEndpointsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    // Minimal 1x1 PNG (real magic bytes so DetectUpload classifies it as an image).
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private static readonly byte[] Pdf = "%PDF-1.4\n%fake pdf body for the type test\n"u8.ToArray();

    // Minimal ZIP local-file-header magic (PK\x03\x04). DetectUpload reads only the
    // 4-byte signature, so this is enough to exercise the OOXML branch.
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00];

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public FilesEndpointsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_public_image_then_anonymous_download_streams_the_bytes()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var owner = Guid.NewGuid();

        var resp = await UploadAsync(FileService.SpeakerPhoto, FileOwnerEntityType.Speaker, owner, Png, "image/png", "p.png", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var file = (await resp.Content.ReadFromJsonAsync<ApiResult<UploadedFileResponse>>())!.Data!;
        Assert.Equal(FileType.Image, file.FileType);
        Assert.False(file.IsEncrypted);
        Assert.Equal($"/api/v1/files/{file.Id}", file.Url);

        // Anonymous (no bearer) download returns the original bytes.
        var download = await _client.GetAsync(file.Url);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(Png, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task An_encrypted_avatar_round_trips_through_the_cipher()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var file = await SeedFileAsync(FileService.Avatar, Guid.NewGuid(), Png, "image/png", "a.png");
        Assert.True(file.IsEncrypted);

        // The admin (wildcard) downloads — the bytes decrypt back to the original.
        var download = await GetAuthAsync(file.Url, token);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(Png, await download.Content.ReadAsByteArrayAsync());

        // Anonymous is refused with a uniform 404 (private file, no oracle).
        var anon = await _client.GetAsync(file.Url);
        Assert.Equal(HttpStatusCode.NotFound, anon.StatusCode);
    }

    [Fact]
    public async Task An_unrecognized_payload_is_rejected_400()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var resp = await UploadAsync(
            FileService.SpeakerPhoto, FileOwnerEntityType.Speaker, Guid.NewGuid(),
            "not-an-image"u8.ToArray(), "text/plain", "x.txt", token);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task A_pdf_uploaded_to_an_image_only_service_is_rejected_400()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var resp = await UploadAsync(
            FileService.SpeakerPhoto, FileOwnerEntityType.Speaker, Guid.NewGuid(),
            Pdf, "application/pdf", "doc.pdf", token);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task A_docx_zip_is_stored_with_the_canonical_ooxml_mime_not_the_client_type()
    {
        // P3 (D-568 hardening) — a ZIP/OOXML upload is stored under the CANONICAL
        // Office MIME resolved from the .docx extension; the client's lie
        // ("application/zip") is never echoed onto the stored/served content-type.
        var token = await CreateAdministratorAndSignInAsync();

        var resp = await UploadAsync(
            FileService.SpeakerPresentation, FileOwnerEntityType.SpeakerPresentation, Guid.NewGuid(),
            Zip, "application/zip", "deck.docx", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var file = (await resp.Content.ReadFromJsonAsync<ApiResult<UploadedFileResponse>>())!.Data!;
        Assert.Equal(FileType.Document, file.FileType);

        var download = await GetAuthAsync(file.Url, token);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            download.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task A_zip_without_a_recognised_office_extension_is_rejected_400()
    {
        // P3 — a ZIP that is not a known OOXML type (.docx/.pptx/.xlsx) is not
        // stored as an opaque blob; it is rejected at the edge.
        var token = await CreateAdministratorAndSignInAsync();

        var resp = await UploadAsync(
            FileService.SpeakerPresentation, FileOwnerEntityType.SpeakerPresentation, Guid.NewGuid(),
            Zip, "application/zip", "archive.zip", token);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task A_tampered_confidential_file_fails_closed_on_download_404()
    {
        // P4 (D-568 hardening) — when the stored bytes no longer match the recorded
        // SHA-256 (tamper / corrupt decrypt), a Confidential+ download FAILS CLOSED:
        // uniform 404, never the unverified bytes. Simulated by mutating the
        // recorded hash so the on-read integrity check cannot match.
        var token = await CreateAdministratorAndSignInAsync();
        var file = await SeedFileAsync(FileService.Avatar, Guid.NewGuid(), Png, "image/png", "a.png");

        // A clean download works first (control).
        var ok = await GetAuthAsync(file.Url, token);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var row = await db.StoredFiles.FirstAsync(f => f.Id == file.Id);
            row.Sha256 = new string('0', 64);   // wrong hash → mismatch on next read
            await db.SaveChangesAsync();
        }

        var tampered = await GetAuthAsync(file.Url, token);
        Assert.Equal(HttpStatusCode.NotFound, tampered.StatusCode);
    }

    [Fact]
    public async Task The_stored_owner_family_is_forced_from_the_policy_not_the_client()
    {
        // P2 (D-568 hardening) — the client cannot set the owner family; the
        // service stamps it from the resolved policy (SpeakerPhoto → Speaker).
        // Shown on a content service because the owner-scoped ones this used to use
        // are refused on this route now; the rule is the same one, and the family it
        // forces is the very thing an over-post would try to change.
        var token = await CreateAdministratorAndSignInAsync();
        var owner = Guid.NewGuid();

        var form = BuildUpload(
            FileService.SpeakerPhoto, FileOwnerEntityType.Speaker, owner, Png, "image/png", "p.png");
        // Over-post the family a caller would rather the row carried. The request
        // DTO has no such property, so it is not bound — and the row must still
        // come out stamped Speaker, not UserProfile.
        form.Add(new StringContent(nameof(FileOwnerEntityType.UserProfile)), "OwnerEntityType");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var file = (await resp.Content.ReadFromJsonAsync<ApiResult<UploadedFileResponse>>())!.Data!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await db.StoredFiles.FirstAsync(f => f.Id == file.Id);
        Assert.Equal(FileOwnerEntityType.Speaker, row.OwnerEntityType);
        Assert.Equal(owner, row.OwnerEntityId);
    }

    [Fact]
    public async Task An_arabic_file_name_is_served_via_rfc5987_filename_star()
    {
        // P1 (D-568 hardening) — a non-ASCII (Arabic) download name is carried by
        // the RFC 5987 filename* param (UTF-8, percent-encoded), with an ASCII
        // fallback, so the Content-Disposition header stays valid.
        var token = await CreateAdministratorAndSignInAsync();

        var resp = await UploadAsync(
            FileService.SpeakerPresentation, FileOwnerEntityType.SpeakerPresentation, Guid.NewGuid(),
            Zip, "application/zip", "عرض.docx", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var file = (await resp.Content.ReadFromJsonAsync<ApiResult<UploadedFileResponse>>())!.Data!;

        var download = await GetAuthAsync(file.Url, token);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        // The client parses the RFC 5987 filename* param back to the decoded name.
        var disposition = download.Content.Headers.ContentDisposition!;
        Assert.Equal("عرض.docx", disposition.FileNameStar);
    }

    [Theory]
    [InlineData(FileService.Avatar)]
    [InlineData(FileService.IdDocument)]
    [InlineData(FileService.VipPhoto)]
    public async Task An_owner_scoped_service_is_refused_on_the_generic_upload_route(FileService service)
    {
        // The owner of these three is a PERSON, and this endpoint reads the owner id
        // from a form field — so holding any file-upload gate would have been enough
        // to plant a file on somebody else's profile. They are refused here outright
        // and written through their dedicated routes, which derive the owner from
        // the authenticated subject instead.
        var token = await CreateAdministratorAndSignInAsync();

        var resp = await UploadAsync(
            service, FileOwnerEntityType.UserProfile, Guid.NewGuid(),
            Png, "image/png", "a.png", token);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task An_owner_required_service_without_an_owner_is_rejected_400()
    {
        // Asserted against the upload pipeline rather than the endpoint, because
        // OwnerRequired is set on exactly the three owner-scoped services and all
        // three are now refused on the generic route (403 above) before this rule is
        // reached. Their dedicated routes derive the owner from the authenticated
        // subject, so no request can omit it either — the pipeline is the only place
        // the rule can still fire, and it is where every internal writer enters.
        using var scope = _factory.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IFileService>();

        var command = new UploadFileCommand(
            FileService.Avatar, OwnerEntityId: null, Png, "a.png", "image/png",
            SeedActorId, FailClosed: true);

        var error = await Assert.ThrowsAsync<ApiException>(() => files.UploadAsync(command));
        Assert.Equal(400, error.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, error.Code);
    }

    [Fact]
    public async Task Anonymous_upload_is_unauthorized()
    {
        var resp = await UploadAsync(
            FileService.SpeakerPhoto, FileOwnerEntityType.Speaker, Guid.NewGuid(),
            Png, "image/png", "p.png", token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task A_non_admin_upload_is_forbidden()
    {
        var visitor = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var resp = await UploadAsync(
            FileService.SpeakerPhoto, FileOwnerEntityType.Speaker, Guid.NewGuid(),
            Png, "image/png", "p.png", visitor.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Link_then_anonymous_download_302_redirects_to_the_url()
    {
        // P6 (D-568) — an external image link is 302-served (no bytes stored).
        var token = await CreateAdministratorAndSignInAsync();
        const string url = "https://cdn.example.com/sponsor/acme.png";

        var link = await LinkAsync(FileService.SponsorLogo, Guid.NewGuid(), url, token);
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);
        var file = (await link.Content.ReadFromJsonAsync<ApiResult<UploadedFileResponse>>())!.Data!;

        var noRedirect = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var download = await noRedirect.GetAsync(file.Url);
        Assert.Equal(HttpStatusCode.Redirect, download.StatusCode);
        Assert.Equal(url, download.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Link_upserts_the_owner_to_a_single_active_file()
    {
        // P6 — a second link for the same (service, owner) REPLACES the first
        // (one active file per owner), not appends.
        var token = await CreateAdministratorAndSignInAsync();
        var owner = Guid.NewGuid();

        var first = await LinkAsync(FileService.SponsorLogo, owner, "https://cdn.example.com/a.png", token);
        var firstId = (await first.Content.ReadFromJsonAsync<ApiResult<UploadedFileResponse>>())!.Data!.Id;
        var second = await LinkAsync(FileService.SponsorLogo, owner, "https://cdn.example.com/b.png", token);
        var secondId = (await second.Content.ReadFromJsonAsync<ApiResult<UploadedFileResponse>>())!.Data!.Id;
        Assert.Equal(firstId, secondId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var active = await db.StoredFiles.CountAsync(
            f => f.Service == FileService.SponsorLogo && f.OwnerEntityId == owner && f.IsActive);
        Assert.Equal(1, active);
        var row = await db.StoredFiles.FirstAsync(f => f.Id == secondId);
        Assert.Equal("https://cdn.example.com/b.png", row.ExternalUrl);
    }

    [Fact]
    public async Task Link_with_a_non_https_url_is_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var resp = await LinkAsync(
            FileService.SponsorLogo, Guid.NewGuid(), "http://cdn.example.com/a.png", token);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Link_with_a_localhost_url_is_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var resp = await LinkAsync(
            FileService.SponsorLogo, Guid.NewGuid(), "https://localhost/a.png", token);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_soft_deletes_and_a_later_download_is_404()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var upload = await UploadAsync(
            FileService.SpeakerPhoto, FileOwnerEntityType.Speaker, Guid.NewGuid(),
            Png, "image/png", "p.png", token);
        var file = (await upload.Content.ReadFromJsonAsync<ApiResult<UploadedFileResponse>>())!.Data!;

        var del = await DeleteAuthAsync($"/api/v1/files/{file.Id}", token);
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        var after = await _client.GetAsync(file.Url);
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_the_on_disk_bytes()
    {
        // P7 (D-568 hardening) — a soft-delete unlinks the stored blob, not just
        // the row, so a "deleted" file's bytes do not linger on disk.
        var token = await CreateAdministratorAndSignInAsync();
        var upload = await UploadAsync(
            FileService.SpeakerPhoto, FileOwnerEntityType.Speaker, Guid.NewGuid(),
            Png, "image/png", "p.png", token);
        var file = (await upload.Content.ReadFromJsonAsync<ApiResult<UploadedFileResponse>>())!.Data!;

        string storageKey;
        bool encrypted;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var row = await db.StoredFiles.FirstAsync(f => f.Id == file.Id);
            storageKey = row.StorageKey!;
            encrypted = row.IsEncrypted;
        }

        var del = await DeleteAuthAsync($"/api/v1/files/{file.Id}", token);
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<IFileStorageProvider>();
            Assert.Null(await storage.ReadAsync(storageKey, encrypted));
        }
    }

    [Fact]
    public async Task ForceDelete_secure_erases_the_bytes_even_under_a_retention_hold()
    {
        // P7 — PDPL right-to-erasure: an IdDocument is retention-held
        // (IsDeletable=false) so the ordinary delete is 409, but ForceDelete
        // securely destroys the bytes and stamps SecureDestroyed.
        var token = await CreateAdministratorAndSignInAsync();
        var file = await SeedFileAsync(FileService.IdDocument, Guid.NewGuid(), Png, "image/png", "id.png");

        var softDelete = await DeleteAuthAsync($"/api/v1/files/{file.Id}", token);
        Assert.Equal(HttpStatusCode.Conflict, softDelete.StatusCode);

        var force = await SendAuthAsync(HttpMethod.Delete, $"/api/v1/files/{file.Id}/force", token);
        Assert.Equal(HttpStatusCode.OK, force.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await db.StoredFiles.FirstAsync(f => f.Id == file.Id);
        Assert.NotNull(row.SecureDestroyed);
        Assert.False(row.IsActive);
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageProvider>();
        Assert.Null(await storage.ReadAsync(row.StorageKey!, row.IsEncrypted));
    }

    [Fact]
    public async Task ForceDelete_requires_the_force_delete_permission()
    {
        // A signed-in non-admin (no Files.ForceDelete) is forbidden.
        var token = await CreateAdministratorAndSignInAsync();
        var upload = await UploadAsync(
            FileService.SpeakerPhoto, FileOwnerEntityType.Speaker, Guid.NewGuid(),
            Png, "image/png", "p.png", token);
        var file = (await upload.Content.ReadFromJsonAsync<ApiResult<UploadedFileResponse>>())!.Data!;

        var visitor = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var resp = await SendAuthAsync(
            HttpMethod.Delete, $"/api/v1/files/{file.Id}/force", visitor.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Deleting_an_unknown_file_is_404()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var resp = await DeleteAuthAsync($"/api/v1/files/{Guid.NewGuid()}", token);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Downloading_an_unknown_file_is_404()
    {
        var resp = await _client.GetAsync($"/api/v1/files/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    // The owner-scoped services (Avatar, IdDocument, VipPhoto) are unreachable
    // through the generic POST /files: it takes both the service and the owner id
    // from the caller, so allowing them there let anyone holding Files.Upload plant
    // an identity document on any profile. The tests below assert STORAGE, CRYPTO,
    // INTEGRITY and RETENTION behaviour, and only ever used the upload to put a
    // file in place, so they seed the same way every internal writer does —
    // through IFileService, in process, where no endpoint gate applies because
    // there is no request.
    private async Task<StoredFileResult> SeedFileAsync(
        FileService service, Guid? ownerId, byte[] bytes, string contentType, string fileName)
    {
        using var scope = _factory.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IFileService>();
        return await files.UploadAsync(new UploadFileCommand(
            service, ownerId, bytes, fileName, contentType, SeedActorId, FailClosed: true));
    }

    private static readonly Guid SeedActorId = Guid.Parse("00000000-0000-0000-0000-0000000000A2");

    private static MultipartFormDataContent BuildUpload(
        FileService service, FileOwnerEntityType ownerType, Guid? ownerId,
        byte[] bytes, string contentType, string fileName)
    {
        // P2 (D-568) — the owner family is server-forced from the policy; the
        // client no longer sends OwnerEntityType (the `ownerType` arg documents
        // the expected family for the reader). Only the owner id rides the form.
        _ = ownerType;
        var form = new MultipartFormDataContent
        {
            { new StringContent(service.ToString()), "Service" },
        };
        if (ownerId is { } id)
        {
            form.Add(new StringContent(id.ToString()), "OwnerEntityId");
        }
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "File", fileName);
        return form;
    }

    private Task<HttpResponseMessage> UploadAsync(
        FileService service, FileOwnerEntityType ownerType, Guid? ownerId,
        byte[] bytes, string contentType, string fileName, string? token)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files")
        {
            Content = BuildUpload(service, ownerType, ownerId, bytes, contentType, fileName),
        };
        if (token is not null)
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return _client.SendAsync(req);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> SendAuthAsync(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> LinkAsync(
        FileService service, Guid? ownerId, string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files/link")
        {
            Content = JsonContent.Create(new { Service = service, OwnerEntityId = ownerId, Url = url }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"file-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "File Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }
}
