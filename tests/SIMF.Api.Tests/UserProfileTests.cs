using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the user self-service profile endpoints
/// (decisions D-046 b, P8 — D-049; renamed from
/// <c>VisitorProfileTests</c>). Each test signs in a freshly-created
/// Approved user so the QR id is present and the actor can hit the
/// profile endpoints.
/// </summary>
public sealed class UserProfileTests : IClassFixture<SimfApiFactory>
{
    private const string Path = "/api/v1/account/user-profile";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public UserProfileTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GET_returns_an_empty_response_with_the_QR_when_no_profile_saved_yet()
    {
        var token = await CreateUserAndSignInAsync();

        var response = await GetAuthAsync(Path, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = (await response.Content.ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!;
        Assert.True(body.Success);
        Assert.Empty(body.Data!.ArabicName);
        Assert.Empty(body.Data.EnglishName);
        Assert.False(body.Data.HasIdImage);
        Assert.False(string.IsNullOrEmpty(body.Data.QrId),
            "QR id is minted on Approved (D-046 a) and surfaced here even with no profile row.");
    }

    [Fact]
    public async Task POST_upsert_creates_a_profile_and_a_second_call_updates_it()
    {
        var token = await CreateUserAndSignInAsync();

        var first = await PostAuthAsync(Path,
            await ValidSaudiRequestAsync("Ahmad", "أحمد"), token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = (await first.Content.ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!;
        Assert.Equal("Ahmad", firstBody.Data!.EnglishName);
        Assert.Equal("أحمد", firstBody.Data.ArabicName);
        Assert.Equal("SA", firstBody.Data.NationalityCode);

        var second = await PostAuthAsync(Path,
            await ValidSaudiRequestAsync("Ahmed", "أحمد محمد"), token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = (await second.Content.ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!;
        Assert.Equal("Ahmed", secondBody.Data!.EnglishName);
        Assert.Equal("أحمد محمد", secondBody.Data.ArabicName);
    }

    [Fact]
    public async Task POST_rejects_an_unknown_nationality_code()
    {
        var token = await CreateUserAndSignInAsync();

        var request = await ValidSaudiRequestAsync();
        request.NationalityCode = "ZZ";   // not in the curated list

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_requires_a_national_id_for_a_Saudi_user()
    {
        var token = await CreateUserAndSignInAsync();

        var request = await ValidSaudiRequestAsync();
        request.NationalId = null;

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_rejects_a_Saudi_national_id_that_does_not_start_with_1()
    {
        // Saudi national IDs are 10 digits starting with 1 (P5 hardening).
        var token = await CreateUserAndSignInAsync();

        var request = await ValidSaudiRequestAsync();
        request.NationalId = "2234567890";   // 10 digits but starts with 2

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_accepts_a_valid_Iqama_starting_with_2_and_rejects_one_starting_with_1()
    {
        // Iqama numbers are 10 digits starting with 2 (P5 hardening).
        var token = await CreateUserAndSignInAsync();

        var validIqama = await ValidSaudiRequestAsync();
        validIqama.IsSaudi = false;
        validIqama.NationalId = null;
        validIqama.NationalityCode = "AE";
        validIqama.IqamaNumber = "2345678901";

        var ok = await PostAuthAsync(Path, validIqama, token);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var invalidIqama = await ValidSaudiRequestAsync();
        invalidIqama.IsSaudi = false;
        invalidIqama.NationalId = null;
        invalidIqama.NationalityCode = "AE";
        invalidIqama.IqamaNumber = "1234567890";   // starts with 1 — not an Iqama

        var bad = await PostAuthAsync(Path, invalidIqama, token);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task POST_requires_either_Iqama_or_Passport_for_a_non_Saudi_user()
    {
        var token = await CreateUserAndSignInAsync();

        var request = await ValidSaudiRequestAsync();
        request.IsSaudi = false;
        request.NationalId = null;
        request.IqamaNumber = null;
        request.PassportNumber = null;
        request.NationalityCode = "AE";

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_accepts_a_permissive_international_phone()
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.InternationalMobile = "+44-7700900123";  // UK shape

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ID_image_round_trips_through_encrypted_storage()
    {
        var token = await CreateUserAndSignInAsync();

        // A tiny 1x1 PNG — the smallest valid PNG that passes the magic-byte gate.
        var png = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
            0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41,
            0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
            0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
            0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
            0x42, 0x60, 0x82,
        };

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "File", "id.png");

        using var upload = new HttpRequestMessage(HttpMethod.Post, Path + "/id-image")
        {
            Content = form,
        };
        upload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var uploadResponse = await _client.SendAsync(upload);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        // Fetch back — the bytes must match plaintext.
        using var fetch = new HttpRequestMessage(HttpMethod.Get, Path + "/id-image");
        fetch.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var fetchResponse = await _client.SendAsync(fetch);
        Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);
        Assert.Equal("image/png", fetchResponse.Content.Headers.ContentType?.MediaType);
        var decrypted = await fetchResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(png, decrypted);
    }

    [Fact]
    public async Task ID_image_rejects_a_file_that_is_not_a_real_image()
    {
        var token = await CreateUserAndSignInAsync();

        var fakePng = new byte[] { 0x00, 0x01, 0x02, 0x03 };  // wrong magic bytes
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(fakePng);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "File", "fake.png");

        using var request = new HttpRequestMessage(HttpMethod.Post, Path + "/id-image")
        {
            Content = form,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ID_image_GET_returns_404_when_no_image_set()
    {
        var token = await CreateUserAndSignInAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, Path + "/id-image");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GET_countries_contains_Saudi_Arabia()
    {
        var token = await CreateUserAndSignInAsync();
        var response = await GetAuthAsync(Path + "/countries", token);

        var body = (await response.Content.ReadFromJsonAsync<ApiResult<CountryListResponse>>())!;
        Assert.Contains(body.Data!.Countries, c => c.Code == "SA");
    }

    [Fact]
    public async Task Encrypted_file_on_disk_does_not_contain_the_plaintext_bytes()
    {
        var token = await CreateUserAndSignInAsync();
        var actorId = await GetActorIdAsync(token);

        // Use a distinctive plaintext we can grep the file bytes for.
        var marker = System.Text.Encoding.UTF8.GetBytes("SIMF-PLAINTEXT-MARKER-2026");
        var image = new byte[3 + marker.Length];
        image[0] = 0xFF; image[1] = 0xD8; image[2] = 0xFF;     // JPEG magic
        Array.Copy(marker, 0, image, 3, marker.Length);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(image);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "File", "id.jpg");
        using var upload = new HttpRequestMessage(HttpMethod.Post, Path + "/id-image")
        {
            Content = form,
        };
        upload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var uploadResponse = await _client.SendAsync(upload);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        // Find the file on disk and confirm the marker is NOT there.
        var filePath = System.IO.Path.Combine(
            _factory.UserIdDocumentStorageDirectory, $"{actorId:N}.bin");
        Assert.True(File.Exists(filePath), $"Encrypted file expected at {filePath}");
        var diskBytes = await File.ReadAllBytesAsync(filePath);
        Assert.DoesNotContain(IndexOfSequence(diskBytes, marker),
            new[] { 0 });  // -1 means not found
    }

    // -- P9 interests ----------------------------------------------------------

    [Fact]
    public async Task POST_requires_at_least_one_interest()
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.InterestIds = new List<Guid>();   // empty — validator rejects

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_rejects_more_than_ten_interests()
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        // Eleven random ids — the validator gates before the service.
        request.InterestIds = Enumerable.Range(0, 11)
            .Select(_ => Guid.NewGuid())
            .ToList();

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_rejects_an_unknown_interest_id()
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.InterestIds = new List<Guid> { Guid.NewGuid() };   // never seeded

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.InterestInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task POST_rejects_a_deactivated_interest_id()
    {
        var token = await CreateUserAndSignInAsync();

        // Seed an interest then deactivate it directly.
        Guid deactivatedId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var interest = new Interest
            {
                Id = Guid.NewGuid(),
                Name = $"Deactivated {Guid.NewGuid():N}",
                NameArabic = "اهتمام معطّل",
                DisplayOrder = 0,
                IsActive = false,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Interests.Add(interest);
            await db.SaveChangesAsync();
            deactivatedId = interest.Id;
        }

        var request = await ValidSaudiRequestAsync();
        request.InterestIds = new List<Guid> { deactivatedId };

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.InterestInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task POST_round_trips_picked_interests()
    {
        var token = await CreateUserAndSignInAsync();

        // Seed three interests; pick two.
        Guid one, two, three;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var rows = Enumerable.Range(0, 3)
                .Select(i => new Interest
                {
                    Id = Guid.NewGuid(),
                    Name = $"Pick {i} {Guid.NewGuid():N}",
                    NameArabic = $"اختيار {i}",
                    DisplayOrder = i,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                })
                .ToList();
            db.Interests.AddRange(rows);
            await db.SaveChangesAsync();
            one = rows[0].Id; two = rows[1].Id; three = rows[2].Id;
        }

        var request = await ValidSaudiRequestAsync();
        request.InterestIds = new List<Guid> { one, two };

        var first = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = (await first.Content.ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!;
        Assert.Equal(2, firstBody.Data!.InterestIds.Count);
        Assert.Contains(one, firstBody.Data.InterestIds);
        Assert.Contains(two, firstBody.Data.InterestIds);

        // Second upsert — replace { one, two } with { two, three }; the
        // diff-then-save in the service removes `one` and adds `three`.
        request.InterestIds = new List<Guid> { two, three };
        var second = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = (await second.Content.ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!;
        Assert.Equal(2, secondBody.Data!.InterestIds.Count);
        Assert.DoesNotContain(one, secondBody.Data.InterestIds);
        Assert.Contains(two, secondBody.Data.InterestIds);
        Assert.Contains(three, secondBody.Data.InterestIds);
    }

    // -- Helpers ---------------------------------------------------------------

    private async Task<UpsertUserProfileRequest> ValidSaudiRequestAsync(
        string englishName = "Test User",
        string arabicName = "مستخدم اختبار")
    {
        var interestId = await SeedInterestAsync();
        return new UpsertUserProfileRequest
        {
            InterestIds = new List<Guid> { interestId },
            ArabicName = arabicName,
            EnglishName = englishName,
            NationalityCode = "SA",
            DateOfBirth = new DateOnly(1990, 1, 1),
            PlaceOfBirth = "Riyadh",
            IsSaudi = true,
            NationalId = "1234567890",
        };
    }

    /// <summary>Creates one active <see cref="Interest"/> directly via the
    /// DbContext (the admin endpoint path is exercised in
    /// <see cref="InterestTests"/>). Returns the id so the test can pin
    /// it into the upsert request — the P9 validator requires 1-10.</summary>
    private async Task<Guid> SeedInterestAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var interest = new Interest
        {
            Id = Guid.NewGuid(),
            Name = $"Test Interest {Guid.NewGuid():N}",
            NameArabic = "اهتمام اختبار",
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Interests.Add(interest);
        await db.SaveChangesAsync();
        return interest.Id;
    }

    private async Task<string> CreateUserAndSignInAsync()
    {
        var email = $"up-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Profile Test",
                AccountState = AccountState.Approved,
                QrId = $"TEST{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private async Task<Guid> GetActorIdAsync(string token)
    {
        // Parse the JWT's sub claim — much simpler than another DB roundtrip.
        var middle = token.Split('.')[1];
        var padded = middle.PadRight(middle.Length + (4 - middle.Length % 4) % 4, '=');
        var json = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/')));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }

    private async Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private static int IndexOfSequence(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length) return -1;
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}
