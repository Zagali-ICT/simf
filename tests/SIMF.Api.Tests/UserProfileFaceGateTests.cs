using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>Re-enables the C7 (D-371) human-face gate the base factory
/// switches off, so this class runs the REAL offline ONNX detector.</summary>
public sealed class FaceGateApiFactory : SimfApiFactory
{
    public FaceGateApiFactory()
    {
        Environment.SetEnvironmentVariable("FaceDetection__Enabled", "true");
    }
}

/// <summary>
/// C7 (D-371) — the server-side human-face gate on
/// <c>POST /app/account/user-profile/id-image</c>, exercised against the
/// real FaceAiSharp ONNX model: an image with no face is rejected with
/// <c>VISITOR_ID_IMAGE_NO_FACE</c>. The positive (real face → 200) path is
/// covered by the Wave-1 live E2E run with a real photo — no face asset is
/// committed to the repo; the disabled pass-through is covered implicitly
/// by <see cref="UserProfileTests"/>' round-trip test (1x1 PNG, gate off).
/// </summary>
public sealed class UserProfileFaceGateTests : IClassFixture<FaceGateApiFactory>
{
    private const string Path = "/api/v1/app/account/user-profile/id-image";

    private readonly FaceGateApiFactory _factory;
    private readonly HttpClient _client;

    public UserProfileFaceGateTests(FaceGateApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Detector_reports_no_face_for_a_faceless_valid_image()
    {
        // Service-level: a properly valid 64x64 solid-colour PNG runs
        // through the REAL ONNX detector and yields no face.
        var svc = _factory.Services
            .GetRequiredService<SIMF.Application.IdentityAccess.IFaceDetectionService>();

        Assert.False(await svc.ContainsHumanFaceAsync(ValidFacelessPng()));
    }

    [Fact]
    public async Task Self_service_id_upload_accepts_a_faceless_document()
    {
        // Two-photo split (D-431-follow-up) — the self-service ID upload is now
        // a DOCUMENT from the gallery (national-ID / Iqama / passport scan), so
        // it NO LONGER face-gates: a valid faceless image is accepted even with
        // the real ONNX detector enabled. The live face requirement moved to the
        // separate FACE photo (avatar), captured via the client liveness flow.
        var token = await CreateUserAndSignInAsync();

        var png = ValidFacelessPng();

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "File", "no-face.png");

        using var upload = new HttpRequestMessage(HttpMethod.Post, Path)
        {
            Content = form,
        };
        upload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(upload);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 (the ID document path no longer face-gates), got {(int)response.StatusCode}: {raw}");
    }

    [Fact]
    public async Task Admin_walk_in_upload_with_no_detectable_face_is_rejected_with_NO_FACE()
    {
        // Parity with the self-service gate: the admin walk-in id-document
        // upload must also reject a faceless image, because the operator's
        // device runs no on-device pre-check — the server is the only face
        // authority on this path.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var visitorId = await CreatePendingVisitorAsync();

        var png = ValidFacelessPng();
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        // The admin endpoint reads the field via Files.GetFile("file").
        form.Add(file, "file", "no-face.png");

        using var upload = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/admin/visitors/{visitorId}/id-document")
        {
            Content = form,
        };
        upload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(upload);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected 400, got {(int)response.StatusCode}: {raw}");
        var body = System.Text.Json.JsonSerializer.Deserialize<ApiResult<object>>(
            raw,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            })!;
        Assert.Equal(ErrorCodes.VisitorIdImageNoFace, body.Error!.Code);
    }

    [Fact]
    public async Task Admin_others_upload_with_no_detectable_face_is_rejected_with_NO_FACE()
    {
        // Parity guard for the OTHER desk: it is a distinct endpoint with its
        // own permission (Others.Edit) that shares the same base face gate, so
        // this pins the invariant that a future refactor cannot silently drop
        // the gate from one desk.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectId = await CreatePendingVisitorAsync();

        var png = ValidFacelessPng();
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "no-face.png");

        using var upload = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/admin/others/{subjectId}/id-document")
        {
            Content = form,
        };
        upload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(upload);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected 400, got {(int)response.StatusCode}: {raw}");
        var body = System.Text.Json.JsonSerializer.Deserialize<ApiResult<object>>(
            raw,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            })!;
        Assert.Equal(ErrorCodes.VisitorIdImageNoFace, body.Error!.Code);
    }

    /// <summary>A valid, decodable 64x64 solid-colour PNG — passes the
    /// magic-byte gate and the decoder, carries no face. (The 1x1 fixture
    /// used by the round-trip test has a corrupt IDAT CRC, which the
    /// byte-level storage path never noticed but a real decode rejects.)</summary>
    private static byte[] ValidFacelessPng()
    {
        using var image = new SixLabors.ImageSharp.Image<
            SixLabors.ImageSharp.PixelFormats.Rgb24>(64, 64);
        using var stream = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, stream);
        return stream.ToArray();
    }

    private async Task<string> CreateUserAndSignInAsync()
    {
        var email = $"face-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Face Gate Test",
                AccountState = AccountState.Approved,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            appDb.UserProfiles.Add(new SIMF.Domain.Profiles.UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CreatedAt = SimfClock.Now,
            });
            await appDb.SaveChangesAsync();
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"admin-face-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AppRoles.Administrator))
            {
                await roles.CreateAsync(new SimfRole { Name = AppRoles.Administrator });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Face Gate Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }
        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private async Task<Guid> CreatePendingVisitorAsync()
    {
        var email = $"visitor-face-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            DisplayName = "Pending Visitor",
            AccountState = AccountState.PendingApproval,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.UserProfiles.Add(new SIMF.Domain.Profiles.UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
        return user.Id;
    }
}
