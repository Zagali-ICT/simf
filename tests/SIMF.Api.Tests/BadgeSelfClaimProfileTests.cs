// #10 phase 4 (register defect `#10-phase4`) — badge self-claim must capture the
// claimer's profile data. Before the fix BadgeActivationCompleteRequest carried
// only QrId/Code/NewPassword/ConfirmPassword and CompleteActivationAsync never
// touched the placeholder UserProfile, so a claimed badge kept its GENERATED name
// ("VIP #3") and NationalityId = 0 forever.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Common;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the profile-capture half of badge self-claim
/// (<c>POST /app/auth/badge-activation/complete</c>). Sibling of
/// <see cref="BadgeAuthTests"/>, which covers the password/email half.
/// </summary>
public sealed class BadgeSelfClaimProfileTests : IClassFixture<SimfApiFactory>
{
    private const string Password = "Zx9#mKp2!";
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public BadgeSelfClaimProfileTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Self_claim_fills_the_placeholder_name_nationality_and_interests()
    {
        var badge = await CreatePlaceholderBadgeAsync();
        var country = await SeedCountryAsync();
        var (firstInterest, secondInterest) = await SeedInterestsAsync(active: true);

        var code = await StartActivationAsync(badge);

        var complete = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/complete",
            new BadgeActivationCompleteRequest
            {
                QrId = badge.QrId,
                Code = code,
                NewPassword = Password,
                ConfirmPassword = Password,
                EnglishName = "Khalid Al Otaibi",
                ArabicName = "خالد العتيبي",
                NationalityCode = country.Code,
                InterestIds = [firstInterest, secondInterest],
            });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await appDb.UserProfiles
            .Include(p => p.Interests)
            .SingleAsync(p => p.UserId == badge.UserId);

        Assert.Equal("Khalid Al Otaibi", profile.Name);
        Assert.Equal("خالد العتيبي", profile.NameArabic);
        Assert.Equal(country.Id, profile.NationalityId);
        Assert.Equal(
            new[] { firstInterest, secondInterest }.OrderBy(id => id),
            profile.Interests.Select(i => i.Id).OrderBy(id => id));

        // The generated account display name is promoted too — otherwise the app
        // greets the holder as "VIP #3" forever.
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = await users.FindByIdAsync(badge.UserId.ToString());
        Assert.Equal("Khalid Al Otaibi", user!.DisplayName);
    }

    [Fact]
    public async Task Self_claim_without_profile_fields_still_activates_and_leaves_the_placeholder()
    {
        // Append-only wire rule (D-219): a client that sends none of the new fields
        // must behave exactly as it did before — activation succeeds, placeholder
        // untouched.
        var badge = await CreatePlaceholderBadgeAsync();
        var code = await StartActivationAsync(badge);

        var complete = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/complete",
            new BadgeActivationCompleteRequest
            {
                QrId = badge.QrId,
                Code = code,
                NewPassword = Password,
                ConfirmPassword = Password,
            });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await appDb.UserProfiles.SingleAsync(p => p.UserId == badge.UserId);
        Assert.Equal(badge.PlaceholderName, profile.Name);
        Assert.Equal(0, profile.NationalityId);
    }

    [Fact]
    public async Task Unknown_nationality_code_is_rejected_and_the_badge_stays_unactivated()
    {
        var badge = await CreatePlaceholderBadgeAsync();
        var code = await StartActivationAsync(badge);

        var complete = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/complete",
            new BadgeActivationCompleteRequest
            {
                QrId = badge.QrId,
                Code = code,
                NewPassword = Password,
                ConfirmPassword = Password,
                EnglishName = "Khalid Al Otaibi",
                NationalityCode = "ZZ",
            });
        Assert.Equal(HttpStatusCode.BadRequest, complete.StatusCode);
        var body = (await complete.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ProfileNationalityUnknown, body.Error!.Code);

        // Fail-before-any-write: the password was never set, so the holder can retry.
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = await users.FindByIdAsync(badge.UserId.ToString());
        Assert.False(await users.HasPasswordAsync(user!));

        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await appDb.UserProfiles.SingleAsync(p => p.UserId == badge.UserId);
        Assert.Equal(badge.PlaceholderName, profile.Name);
    }

    [Fact]
    public async Task Deactivated_interest_is_rejected()
    {
        var badge = await CreatePlaceholderBadgeAsync();
        var country = await SeedCountryAsync();
        var (retired, _) = await SeedInterestsAsync(active: false);
        var code = await StartActivationAsync(badge);

        var complete = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/complete",
            new BadgeActivationCompleteRequest
            {
                QrId = badge.QrId,
                Code = code,
                NewPassword = Password,
                ConfirmPassword = Password,
                EnglishName = "Khalid Al Otaibi",
                NationalityCode = country.Code,
                InterestIds = [retired],
            });
        Assert.Equal(HttpStatusCode.BadRequest, complete.StatusCode);
        var body = (await complete.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.InterestInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task More_than_ten_interests_is_rejected_by_the_validator()
    {
        var badge = await CreatePlaceholderBadgeAsync();
        var code = await StartActivationAsync(badge);

        var complete = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/complete",
            new BadgeActivationCompleteRequest
            {
                QrId = badge.QrId,
                Code = code,
                NewPassword = Password,
                ConfirmPassword = Password,
                InterestIds = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToList(),
            });
        Assert.Equal(HttpStatusCode.BadRequest, complete.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private sealed record PlaceholderBadge(Guid UserId, string QrId, string PlaceholderName);

    /// <summary>Creates the exact shape a bulk badge run mints: an Approved,
    /// passwordless account with a placeholder <c>@simf.local</c> email, a GENERATED
    /// display name, and a profile stub with <c>NationalityId = 0</c> and no
    /// interests.</summary>
    private async Task<PlaceholderBadge> CreatePlaceholderBadgeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var placeholderName = $"VIP #{Random.Shared.Next(1000, 9999)}";
        var user = new SimfUser
        {
            UserName = $"badge-{Guid.NewGuid():N}@simf.local",
            Email = $"badge-{Guid.NewGuid():N}@simf.local",
            EmailConfirmed = false,
            DisplayName = placeholderName,
            UserType = UserType.Visitor,
            AccountState = AccountState.Approved,
        };
        var create = await users.CreateAsync(user);
        Assert.True(create.Succeeded, string.Join(";", create.Errors.Select(e => e.Description)));

        var qrId = NewQrId();
        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = placeholderName,
            NameArabic = placeholderName,
            NationalityId = 0,
            QrId = qrId,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
        return new PlaceholderBadge(user.Id, qrId, placeholderName);
    }

    private async Task<Country> SeedCountryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        // Reuse a seeded country when one is already present; the lookup is shared
        // across the whole fixture DB and its Id is manually assigned (not IDENTITY).
        var existing = await appDb.Countries
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IsActive);
        if (existing is not null) { return existing; }

        var country = new Country
        {
            Id = 682,
            Code = "SA",
            Name = "Saudi Arabia",
            NameArabic = "المملكة العربية السعودية",
            PhonePrefix = "+966",
            DisplayOrder = 10,
            IsActive = true,
        };
        appDb.Countries.Add(country);
        await appDb.SaveChangesAsync();
        return country;
    }

    private async Task<(Guid First, Guid Second)> SeedInterestsAsync(bool active)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var first = new UserInterest
        {
            Id = Guid.NewGuid(),
            Name = $"Maritime security {Guid.NewGuid():N}",
            NameArabic = "الأمن البحري",
            DisplayOrder = 1,
            IsActive = active,
            CreatedAt = SimfClock.Now,
        };
        var second = new UserInterest
        {
            Id = Guid.NewGuid(),
            Name = $"Port logistics {Guid.NewGuid():N}",
            NameArabic = "الخدمات اللوجستية للموانئ",
            DisplayOrder = 2,
            IsActive = active,
            CreatedAt = SimfClock.Now,
        };
        appDb.Interests.AddRange(first, second);
        await appDb.SaveChangesAsync();
        return (first.Id, second.Id);
    }

    /// <summary>Starts activation with a real email and returns the emailed code.</summary>
    private async Task<string> StartActivationAsync(PlaceholderBadge badge)
    {
        var start = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-activation/start",
            new BadgeActivationStartRequest
            {
                QrId = badge.QrId,
                Email = $"claimed-{Guid.NewGuid():N}@example.com",
            });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var hash = await idDb.AccountCodes
            .Where(c => c.UserId == badge.UserId
                && c.Purpose == AccountCodePurpose.BadgeActivationOtp
                && c.ConsumedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.CodeHash)
            .FirstOrDefaultAsync();
        Assert.NotNull(hash);
        return AuthFlow.RecoverPlaintextCode(hash!);
    }

    private static string NewQrId()
    {
        var guid = Guid.NewGuid().ToByteArray();
        var chars = new char[12];
        for (var i = 0; i < 12; i++)
        {
            chars[i] = Alphabet[guid[i] % Alphabet.Length];
        }
        return new string(chars);
    }
}
