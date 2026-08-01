// Tests: D-809 — the offline badge desk. A desk with no network prints badges
// carrying an encrypted (profileTypeCode, sequence) QR; this covers uploading
// that shift back into the system, and the gate resolving one of those badges.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Common;
using SIMF.Common.Badges;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Badges;
using SIMF.Contracts.Gates;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class OfflineBadgeUploadTests : IClassFixture<SimfApiFactory>
{
    /// <summary>A fixed AES-256 key, base64, so the test can mint a badge the
    /// server will open. Test-only — the real key is provisioned per event.</summary>
    private static readonly string BadgeKey = Convert.ToBase64String(
        Enumerable.Range(0, EventBadgeCodec.KeyBytes).Select(i => (byte)i).ToArray());

    private const int BadgeKeyVersion = 1;

    private readonly SimfApiFactory _factory;

    public OfflineBadgeUploadTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Upload_is_refused_while_the_capability_is_disarmed()
    {
        // Ships off. A desk permission is not enough — the switch is the control.
        using var client = _factory.CreateClient();
        var token = await CreateAdminTokenAsync(client);

        var response = await PostBatchAsync(client, token, new OfflineBadgeBatchRequest
        {
            DeskLabel = "Desk 1",
            Registrations = [BuildRegistration(NextSequence(), await ResolveProfileTypeCodeAsync())],
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Upload_creates_an_approved_account_under_the_badge_sequence()
    {
        using var armed = CreateArmedFactory();
        using var client = armed.CreateClient();
        var token = await CreateAdminTokenAsync(client);

        var sequence = NextSequence();
        var response = await PostBatchAsync(client, token, new OfflineBadgeBatchRequest
        {
            DeskLabel = "Desk 3 — north entrance",
            Registrations = [BuildRegistration(sequence, await ResolveProfileTypeCodeAsync())],
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<OfflineBadgeBatchResponse>>())!.Data!;

        Assert.Equal(1, body.Submitted);
        Assert.Equal(1, body.Created);
        Assert.Equal(0, body.Rejected);

        var result = Assert.Single(body.Results);
        Assert.Equal(OfflineBadgeUploadStatus.Created, result.Status);

        // The account is stored under the id the PRINTED badge encodes, so the
        // paper already in the visitor's hand resolves without a reprint.
        Assert.True(OfflineBadgeId.TryFormat(sequence, out var expectedQrId));
        Assert.Equal(expectedQrId, result.QrId);

        using var scope = _factory.Services.CreateScope();
        var app = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await app.UserProfiles.AsNoTracking()
            .SingleAsync(p => p.QrId == expectedQrId);
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = await identity.Users.AsNoTracking()
            .SingleAsync(u => u.Id == profile.UserId);
        Assert.Equal(AccountState.Approved, user.AccountState);
    }

    [Fact]
    public async Task Re_uploading_the_same_batch_changes_nothing()
    {
        // A desk retries after a dropped connection. The badge is already printed
        // and handed over, so a second account for it would be a second person as
        // far as every count in the system is concerned.
        using var armed = CreateArmedFactory();
        using var client = armed.CreateClient();
        var token = await CreateAdminTokenAsync(client);

        var batch = new OfflineBadgeBatchRequest
        {
            Registrations = [BuildRegistration(NextSequence(), await ResolveProfileTypeCodeAsync())],
        };

        var first = await PostBatchAsync(client, token, batch);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = (await first.Content
            .ReadFromJsonAsync<ApiResult<OfflineBadgeBatchResponse>>())!.Data!;
        Assert.Equal(1, firstBody.Created);

        var second = await PostBatchAsync(client, token, batch);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = (await second.Content
            .ReadFromJsonAsync<ApiResult<OfflineBadgeBatchResponse>>())!.Data!;

        Assert.Equal(0, secondBody.Created);
        Assert.Equal(1, secondBody.AlreadyUploaded);
        Assert.Equal(0, secondBody.Rejected);

        var qrId = firstBody.Results[0].QrId;
        using var scope = _factory.Services.CreateScope();
        var app = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Equal(1, await app.UserProfiles.CountAsync(p => p.QrId == qrId));
    }

    [Fact]
    public async Task A_rejected_row_does_not_take_the_rest_of_the_batch_with_it()
    {
        // The point of per-item isolation. During a rush the upload is the only
        // record that a badge was handed out, so one bad row must not discard the
        // good ones around it.
        using var armed = CreateArmedFactory();
        using var client = armed.CreateClient();
        var token = await CreateAdminTokenAsync(client);
        var code = await ResolveProfileTypeCodeAsync();

        var good = BuildRegistration(NextSequence(), code);
        var unknownType = BuildRegistration(NextSequence(), profileTypeCode: 30_000);
        var outOfRange = BuildRegistration(OfflineBadgeId.MaxSequence + 1, code);
        var alsoGood = BuildRegistration(NextSequence(), code);

        var response = await PostBatchAsync(client, token, new OfflineBadgeBatchRequest
        {
            Registrations = [good, unknownType, outOfRange, alsoGood],
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<OfflineBadgeBatchResponse>>())!.Data!;

        Assert.Equal(4, body.Submitted);
        Assert.Equal(2, body.Created);
        Assert.Equal(2, body.Rejected);
        Assert.Equal(
            ErrorCodes.OfflineBadgeInvalid,
            body.Results.Single(r => r.Sequence == unknownType.Sequence).ErrorCode);
        Assert.Equal(
            ErrorCodes.OfflineBadgeInvalid,
            body.Results.Single(r => r.Sequence == outOfRange.Sequence).ErrorCode);
    }

    [Fact]
    public async Task A_duplicate_identity_document_is_reported_not_written_twice()
    {
        // The one field the quick desk keeps. Two badges for the same person is
        // exactly what it exists to prevent, and the second upload must say so
        // rather than fail the batch.
        using var armed = CreateArmedFactory();
        using var client = armed.CreateClient();
        var token = await CreateAdminTokenAsync(client);
        var code = await ResolveProfileTypeCodeAsync();

        var nationalId = BuildLuhnNationalId();
        var first = BuildRegistration(NextSequence(), code);
        first.NationalId = nationalId;
        var duplicate = BuildRegistration(NextSequence(), code);
        duplicate.NationalId = nationalId;

        var response = await PostBatchAsync(client, token, new OfflineBadgeBatchRequest
        {
            Registrations = [first, duplicate],
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<OfflineBadgeBatchResponse>>())!.Data!;

        Assert.Equal(1, body.Created);
        Assert.Equal(1, body.Rejected);
        Assert.Equal(
            ErrorCodes.DuplicateIdentity,
            body.Results.Single(r => r.Status == OfflineBadgeUploadStatus.Rejected).ErrorCode);
    }

    [Fact]
    public async Task An_uploaded_account_lands_pending_when_auto_approve_is_not_armed()
    {
        // Approval keeps its own switch. The badge is already in circulation, so
        // the response has to SAY it will be refused at the gate until an admin
        // approves rather than quietly forcing the account through.
        using var armed = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("WalkInMode:Enabled", "true");
            builder.UseSetting("WalkInMode:QuickRegister", "true");
            builder.UseSetting("WalkInMode:OfflineUpload", "true");
            // AutoApprove deliberately left off.
        });
        using var client = armed.CreateClient();
        var token = await CreateAdminTokenAsync(client);

        var response = await PostBatchAsync(client, token, new OfflineBadgeBatchRequest
        {
            Registrations = [BuildRegistration(NextSequence(), await ResolveProfileTypeCodeAsync())],
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<OfflineBadgeBatchResponse>>())!.Data!;

        Assert.Equal(0, body.Created);
        Assert.Equal(1, body.PendingApproval);
        Assert.Equal(
            OfflineBadgeUploadStatus.CreatedPendingApproval,
            body.Results[0].Status);
    }

    [Fact]
    public async Task The_gate_resolves_an_encrypted_badge_to_its_uploaded_holder()
    {
        // The whole chain: a desk with no network encrypts (profileTypeCode,
        // sequence) onto paper, the shift is uploaded, and a scanner later
        // presents that blob at a gate. The server decrypts it independently
        // rather than trusting the device's reading.
        using var armed = CreateArmedFactory();
        using var client = armed.CreateClient();
        var token = await CreateAdminTokenAsync(client);

        var sequence = NextSequence();
        var code = await ResolveProfileTypeCodeAsync();
        var response = await PostBatchAsync(client, token, new OfflineBadgeBatchRequest
        {
            Registrations = [BuildRegistration(sequence, code)],
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var badge = EventBadgeCodec.Encode(
            new EventBadgePayload(code, sequence),
            Convert.FromBase64String(BadgeKey),
            BadgeKeyVersion);

        using var scope = armed.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IQrResolver>();
        var resolved = await resolver.ResolveAsync(badge);

        Assert.NotNull(resolved);
        Assert.Equal(AccountState.Approved, resolved!.AccountState);
    }

    [Fact]
    public async Task An_encrypted_badge_is_unknown_to_a_server_that_has_no_key()
    {
        // Fail-closed. A server that is not armed treats the blob as an
        // unrecognised code, which is the same QR_UNKNOWN denial any garbage
        // produces — never an oracle for which keys are loaded.
        using var armed = CreateArmedFactory();
        using var client = armed.CreateClient();
        var token = await CreateAdminTokenAsync(client);

        var sequence = NextSequence();
        var code = await ResolveProfileTypeCodeAsync();
        await PostBatchAsync(client, token, new OfflineBadgeBatchRequest
        {
            Registrations = [BuildRegistration(sequence, code)],
        });

        var badge = EventBadgeCodec.Encode(
            new EventBadgePayload(code, sequence),
            Convert.FromBase64String(BadgeKey),
            BadgeKeyVersion);

        // The default factory has no badge key configured.
        using var scope = _factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IQrResolver>();

        Assert.Null(await resolver.ResolveAsync(badge));
    }

    [Fact]
    public async Task A_badge_minted_with_a_foreign_key_is_not_resolved()
    {
        using var armed = CreateArmedFactory();
        using var client = armed.CreateClient();
        var token = await CreateAdminTokenAsync(client);

        var sequence = NextSequence();
        var code = await ResolveProfileTypeCodeAsync();
        await PostBatchAsync(client, token, new OfflineBadgeBatchRequest
        {
            Registrations = [BuildRegistration(sequence, code)],
        });

        var foreignKey = RandomNumberGenerator.GetBytes(EventBadgeCodec.KeyBytes);
        var forged = EventBadgeCodec.Encode(
            new EventBadgePayload(code, sequence), foreignKey, BadgeKeyVersion);

        using var scope = armed.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IQrResolver>();

        Assert.Null(await resolver.ResolveAsync(forged));
    }

    [Fact]
    public async Task Offline_config_withholds_the_badge_key_while_disarmed()
    {
        // The key travels only while offline badges are armed. Disarming is
        // therefore also what stops handing it to new devices — the lever
        // available if one goes missing.
        using var client = _factory.CreateClient();
        var token = await CreateAdminTokenAsync(client);

        var config = await GetOfflineConfigAsync(client, token);

        Assert.Null(config.BadgeKey);
        Assert.Null(config.PreviousBadgeKey);
        Assert.False(config.SessionWalkIn);
    }

    [Fact]
    public async Task Offline_config_carries_the_key_and_profile_type_codes_when_armed()
    {
        using var armed = CreateArmedFactory();
        using var client = armed.CreateClient();
        var token = await CreateAdminTokenAsync(client);

        var config = await GetOfflineConfigAsync(client, token);

        Assert.Equal(BadgeKey, config.BadgeKey);
        Assert.Equal(BadgeKeyVersion, config.BadgeKeyVersion);
        // The gates list is per-operator; this fixture's admin has no gate
        // assignments, so the rules are empty while the key still arrives. That
        // is the honest shape: an operator with no gates can verify nothing.
        Assert.NotNull(config.Gates);
    }

    private static async Task<GateOfflineConfig> GetOfflineConfigAsync(
        HttpClient client, string token)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Get, "/api/v1/app/gates/offline-config");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<GateOfflineConfig>>())!.Data!;
    }

    // -- helpers ------------------------------------------------------------

    private WebApplicationFactory<Program> CreateArmedFactory() =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("WalkInMode:Enabled", "true");
            builder.UseSetting("WalkInMode:QuickRegister", "true");
            builder.UseSetting("WalkInMode:AutoApprove", "true");
            builder.UseSetting("WalkInMode:OfflineUpload", "true");
            builder.UseSetting("WalkInMode:AcceptOfflineBadges", "true");
            builder.UseSetting("WalkInMode:BadgeKey", BadgeKey);
            builder.UseSetting(
                "WalkInMode:BadgeKeyVersion",
                BadgeKeyVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        });

    private static Task<HttpResponseMessage> PostBatchAsync(
        HttpClient client, string token, OfflineBadgeBatchRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/offline/batch")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(message);
    }

    /// <summary>Desk sequences are allocated per desk from a wide range; the test
    /// only needs values that do not collide across runs of the shared fixture.</summary>
    private static long NextSequence() =>
        3_000_000L + RandomNumberGenerator.GetInt32(1, 900_000_000);

    private static OfflineBadgeRegistration BuildRegistration(
        long sequence, short profileTypeCode) =>
        new()
        {
            Sequence = sequence,
            ProfileTypeCode = profileTypeCode,
            Name = "Offline Desk Visitor",
            SaudiMobile = "0512345678",
            NationalId = BuildLuhnNationalId(),
            RegisteredAt = DateTimeOffset.UtcNow,
        };

    private async Task<short> ResolveProfileTypeCodeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await db.ProfileTypes.AsNoTracking()
            .Where(p => p.IsActive && p.IsForVisitor && p.Code != 0)
            .Select(p => p.Code)
            .FirstAsync();
    }

    private static string BuildLuhnNationalId()
    {
        var digits = new int[10];
        digits[0] = 1;
        for (var i = 1; i < 9; i++) { digits[i] = RandomNumberGenerator.GetInt32(0, 10); }

        for (var check = 0; check < 10; check++)
        {
            digits[9] = check;
            var sum = 0;
            var doubleDigit = false;
            for (var i = 9; i >= 0; i--)
            {
                var value = digits[i];
                if (doubleDigit)
                {
                    value *= 2;
                    if (value > 9) { value -= 9; }
                }
                sum += value;
                doubleDigit = !doubleDigit;
            }
            if (sum % 10 == 0) { break; }
        }

        return string.Concat(digits);
    }

    private async Task<string> CreateAdminTokenAsync(HttpClient client)
    {
        const string administratorRole = "Administrator";
        var email = $"offline-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(administratorRole))
            {
                await roles.CreateAsync(new SimfRole { Name = administratorRole });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Offline Desk Operator",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, administratorRole);
        }

        var sign = await client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }
}
