// Tests: D-473 (#10) — delegates (وفد) + bulk-generate placeholder badges.
//        D-751 (#10) — bulk-badge organiser email delivery (ZIP of QR PNGs).
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Common;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Organisations;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-473 (#10) — a delegate is a normal visitor with <c>IsDelegate</c> set and an
/// invited country; plus the bulk-generate of placeholder badges by profile type.
/// </summary>
public sealed class DelegatesAndBulkBadgesTests : IClassFixture<BulkBadgeEmailApiFactory>
{
    private readonly BulkBadgeEmailApiFactory _factory;
    private readonly HttpClient _client;

    public DelegatesAndBulkBadgesTests(BulkBadgeEmailApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Walk_in_delegate_with_an_invited_country_succeeds_and_flags_the_profile()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await VisitorProfileTypeAsync();
        var organisationId = await OrganisationIdAsync();
        await SetCountryInvitedAsync("SA", invited: true);

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildRequest(profileTypeId, organisationId, "SA", isDelegate: true),
            admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!;

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await appDb.UserProfiles.SingleAsync(p => p.UserId == body.Data!.UserId);
        Assert.True(profile.IsDelegate);
    }

    [Fact]
    public async Task Walk_in_delegate_with_a_non_invited_country_is_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await VisitorProfileTypeAsync();
        var organisationId = await OrganisationIdAsync();
        await SetCountryInvitedAsync("US", invited: false);

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildRequest(profileTypeId, organisationId, "US", isDelegate: true),
            admin);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!;
        Assert.Equal(ErrorCodes.DelegateCountryNotInvited, body.Error!.Code);
    }

    [Fact]
    public async Task A_non_delegate_walk_in_is_not_constrained_to_invited_countries()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await VisitorProfileTypeAsync();
        var organisationId = await OrganisationIdAsync();
        await SetCountryInvitedAsync("US", invited: false);

        // A plain (non-delegate) visitor from a non-invited country is fine.
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildRequest(profileTypeId, organisationId, "US", isDelegate: false),
            admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Bulk_generate_creates_badges_per_type_with_qr_and_the_delegate_flag()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        // A dedicated profile type so the assertion sees only this batch's badges.
        var profileTypeId = await FreshVisitorProfileTypeAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                IsDelegate = true,
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = profileTypeId, Count = 3 },
                },
            },
            admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminBulkGenerateBadgesResponse>>())!;
        Assert.Equal(3, body.Data!.Created);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var badges = await appDb.UserProfiles
            .Where(p => p.ProfileTypeId == profileTypeId)
            .ToListAsync();
        Assert.Equal(3, badges.Count);
        Assert.All(badges, b => Assert.True(b.IsDelegate));
        // Every generated badge is Approved with a minted QR (ready to hand out).
        Assert.All(badges, b => Assert.False(string.IsNullOrEmpty(b.QrId)));
    }

    [Fact]
    public async Task Bulk_generate_rejects_an_empty_request_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest { Batches = new List<BulkBadgeBatch>() },
            admin);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bulk_generate_with_an_invalid_second_batch_writes_zero_accounts_and_400s()
    {
        // Fix #6 (data-integrity): every batch's profile type is validated BEFORE
        // any account is created, so an invalid later batch is a clean 400 with
        // nothing persisted — no partial write under a failure envelope.
        var admin = await CreateAdministratorAndSignInAsync();
        var validProfileTypeId = await FreshVisitorProfileTypeAsync();
        var missingProfileTypeId = Guid.NewGuid(); // no such profile type exists

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                IsDelegate = false,
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = validProfileTypeId, Count = 3 },
                    new() { ProfileTypeId = missingProfileTypeId, Count = 1 },
                },
            },
            admin);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminBulkGenerateBadgesResponse>>())!;
        Assert.Equal(ErrorCodes.AdminProfileTypeInvalid, body.Error!.Code);

        // The valid first batch must NOT have been committed — zero badges written.
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var badges = await appDb.UserProfiles
            .Where(p => p.ProfileTypeId == validProfileTypeId)
            .ToListAsync();
        Assert.Empty(badges);
    }

    [Fact]
    public async Task Bulk_generate_with_a_recipient_email_enqueues_one_zip_of_all_badge_pngs()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        // Two fresh types so the ZIP must carry the SUM of both counts (2 + 3 = 5).
        var typeA = await FreshVisitorProfileTypeAsync();
        var typeB = await FreshVisitorProfileTypeAsync();
        var recipient = $"organiser-{Guid.NewGuid():N}@simf.test";

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                IsDelegate = false,
                RecipientEmail = recipient,
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = typeA, Count = 2 },
                    new() { ProfileTypeId = typeB, Count = 3 },
                },
            },
            admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminBulkGenerateBadgesResponse>>())!;
        Assert.Equal(5, body.Data!.Created);
        Assert.True(body.Data!.EmailQueued);

        // Exactly one message to this (unique) recipient, carrying one ZIP whose
        // entries are one PNG per generated badge (sum of the batch counts).
        var messages = _factory.Emails.Messages.Where(m => m.To == recipient).ToList();
        var message = Assert.Single(messages);
        var attachment = Assert.Single(message.Attachments!);
        Assert.Equal("application/zip", attachment.ContentType);
        Assert.StartsWith("badges-", attachment.FileName);
        Assert.EndsWith(".zip", attachment.FileName);

        using var zip = new ZipArchive(new MemoryStream(attachment.Content), ZipArchiveMode.Read);
        Assert.Equal(5, zip.Entries.Count);
        Assert.All(zip.Entries, entry => Assert.EndsWith(".png", entry.Name));
        foreach (var entry in zip.Entries)
        {
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            // Each entry is a real, non-empty PNG (magic bytes 89 50 4E 47).
            Assert.True(bytes.Length > 8);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes.Take(4).ToArray());
        }
    }

    [Fact]
    public async Task Bulk_generate_with_an_invalid_recipient_email_is_400_and_writes_zero_accounts()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var typeId = await FreshVisitorProfileTypeAsync();
        var attachmentEmailsBefore =
            _factory.Emails.Messages.Count(m => m.Attachments is { Count: > 0 });

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                IsDelegate = false,
                RecipientEmail = "not-an-email",
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = typeId, Count = 3 },
                },
            },
            admin);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminBulkGenerateBadgesResponse>>())!;
        Assert.Equal(ErrorCodes.ValidationFailed, body.Error!.Code);

        // A 4xx must have no side effects — zero badges written, no email enqueued.
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var badges = await appDb.UserProfiles.Where(p => p.ProfileTypeId == typeId).ToListAsync();
        Assert.Empty(badges);
        Assert.Equal(attachmentEmailsBefore,
            _factory.Emails.Messages.Count(m => m.Attachments is { Count: > 0 }));
    }

    [Fact]
    public async Task Bulk_generate_without_a_recipient_email_enqueues_nothing()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var typeId = await FreshVisitorProfileTypeAsync();
        var attachmentEmailsBefore =
            _factory.Emails.Messages.Count(m => m.Attachments is { Count: > 0 });

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-generate",
            new AdminBulkGenerateBadgesRequest
            {
                IsDelegate = false,
                Batches = new List<BulkBadgeBatch>
                {
                    new() { ProfileTypeId = typeId, Count = 2 },
                },
            },
            admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminBulkGenerateBadgesResponse>>())!;
        Assert.Equal(2, body.Data!.Created);
        Assert.False(body.Data!.EmailQueued);
        // Back-compat: no attachment-bearing email was enqueued by this path.
        Assert.Equal(attachmentEmailsBefore,
            _factory.Emails.Messages.Count(m => m.Attachments is { Count: > 0 }));
    }

    // -- helpers --------------------------------------------------------------

    private static AdminWalkInRegistrationRequest BuildRequest(
        Guid profileTypeId, Guid organisationId, string nationalityCode, bool isDelegate)
    {
        var isSaudi = nationalityCode == "SA";
        return new AdminWalkInRegistrationRequest
        {
            DisplayName = "Delegate Subject",
            ArabicName = "عضو وفد",
            EnglishName = "Delegate Member",
            ProfileTypeId = profileTypeId,
            NationalityCode = nationalityCode,
            DateOfBirth = new DateOnly(1990, 1, 1),
            PlaceOfBirth = "Riyadh",
            IsSaudi = isSaudi,
            NationalId = isSaudi ? "1101798278" : null,
            PassportNumber = isSaudi ? null : "P1234567",
            SaudiMobile = "+966500000002",
            OrganisationId = organisationId,
            IsDelegate = isDelegate,
        };
    }

    private async Task SetCountryInvitedAsync(string code, bool invited)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var country = await appDb.Countries.FirstOrDefaultAsync(c => c.Code == code);
        if (country is null)
        {
            country = new Country
            {
                Id = code == "SA" ? 682 : 840,
                Code = code, Name = code, NameArabic = code,
                IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
            };
            appDb.Countries.Add(country);
        }
        country.IsActive = true;
        country.IsInvited = invited;
        await appDb.SaveChangesAsync();
    }

    private async Task<Guid> OrganisationIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var existing = await appDb.Organisations.FirstOrDefaultAsync(o => o.IsActive);
        if (existing is not null) return existing.Id;
        var fresh = new Organisation
        {
            Id = Guid.NewGuid(),
            NameArabic = "جهة اختبار",
            Name = "Test Organisation",
            CommercialRegistration = $"CR{Guid.NewGuid():N}"[..12],
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.Organisations.Add(fresh);
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    private async Task<Guid> VisitorProfileTypeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var seeded = await appDb.ProfileTypes
            .FirstOrDefaultAsync(p => p.IsForVisitor && p.IsActive);
        if (seeded is not null) return seeded.Id;
        var fresh = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = "Visitor — DelegateTestSeed",
            NameArabic = "زائر — اختبار",
            PageColor = "#3B82F6",
            IsForVisitor = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.ProfileTypes.Add(fresh);
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    private async Task<Guid> FreshVisitorProfileTypeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var fresh = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = $"Delegate Bulk {Guid.NewGuid():N}"[..24],
            NameArabic = "وفد — اختبار",
            PageColor = "#3B82F6",
            IsForVisitor = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.ProfileTypes.Add(fresh);
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"delegate-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Delegate Test Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email, Password = AuthFlow.Password, Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
