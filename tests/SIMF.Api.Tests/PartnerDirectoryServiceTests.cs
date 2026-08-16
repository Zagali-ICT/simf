// Build #13 — "Meet People Like You" partner directory
// (GET /api/v1/app/networking/partner-directory, policy RequireApprovedAccount).
// Verifies the deduped union of curated Speakers / Sponsors / Booth companies +
// the opted-in "Other"-type accounts, and the CP switch that empties it.
//
// Shared-DB rules: this class's tests run sequentially, but other classes run in
// parallel against the SAME database, so every assertion is scoped to rows this
// class seeds (matched BY ID) — never an exact list count. Only the flag-off test
// asserts emptiness, and it restores the shared switch to true in a finally.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Networking;
using SIMF.Domain.Exhibition;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Organization;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Domain.Sponsors;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Meetings)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class PartnerDirectoryServiceTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public PartnerDirectoryServiceTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Curated_active_speaker_appears_as_kind_speaker()
    {
        Guid speakerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var speaker = new Speaker
            {
                Id = Guid.NewGuid(),
                Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = $"PD Speaker {Guid.NewGuid().ToString("N")[..8]}",
                NameArabic = "متحدّث",
                DisplayOrder = 0,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            db.Speakers.Add(speaker);
            await db.SaveChangesAsync();
            speakerId = speaker.Id;
        }

        var token = await SeedApprovedCallerTokenAsync();
        var body = await GetDirectoryAsync(token);

        Assert.Contains(body.Entries,
            e => e.Id == speakerId && e.Kind == PartnerDirectoryKind.Speaker);
    }

    [Fact]
    public async Task Curated_active_sponsor_appears_as_kind_sponsor()
    {
        Guid sponsorId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var sponsor = new Sponsor
            {
                Id = Guid.NewGuid(),
                Name = $"PD Sponsor {Guid.NewGuid().ToString("N")[..8]}",
                NameArabic = "راعٍ",
                Tier = SponsorTier.Gold,
                DisplayOrder = 0,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            db.Sponsors.Add(sponsor);
            await db.SaveChangesAsync();
            sponsorId = sponsor.Id;
        }

        var token = await SeedApprovedCallerTokenAsync();
        var body = await GetDirectoryAsync(token);

        Assert.Contains(body.Entries,
            e => e.Id == sponsorId && e.Kind == PartnerDirectoryKind.Sponsor);
    }

    // LogoRelativePath on a directory entry is a PRESENCE SENTINEL: the app only
    // tests it for null and then builds the asset URL from the id itself. It is
    // pinned here because the field it used to be read from is now permanently
    // null — nothing in the compiler or the rest of the suite would notice every
    // sponsor quietly dropping to an initials tile.
    [Fact]
    public async Task Sponsor_entry_carries_a_logo_sentinel_only_once_a_logo_is_stored()
    {
        Guid sponsorId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var sponsor = new Sponsor
            {
                Id = Guid.NewGuid(),
                Name = $"PD Logo {Guid.NewGuid().ToString("N")[..8]}",
                NameArabic = "راعٍ بشعار",
                Tier = SponsorTier.Gold,
                DisplayOrder = 0,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            db.Sponsors.Add(sponsor);
            await db.SaveChangesAsync();
            sponsorId = sponsor.Id;
        }

        var token = await SeedApprovedCallerTokenAsync();

        var before = await GetDirectoryAsync(token);
        Assert.Null(before.Entries.Single(e => e.Id == sponsorId).LogoRelativePath);

        using (var scope = _factory.Services.CreateScope())
        {
            var assets = scope.ServiceProvider
                .GetRequiredService<SIMF.Application.Assets.Abstractions.IAssetService>();
            await assets.SetExternalLinkAsync(
                Guid.Empty, AssetCategory.SponsorLogo, sponsorId, AssetKind.Image,
                "https://cdn.example.com/sponsor-logo.png");
        }

        var after = await GetDirectoryAsync(token);
        Assert.Equal(
            $"/app/assets/SponsorLogo/{sponsorId}/image",
            after.Entries.Single(e => e.Id == sponsorId).LogoRelativePath);
    }

    [Fact]
    public async Task Booth_with_linked_active_exhibitor_appears_with_exhibitor_name()
    {
        var exhibitorName = $"PD Exhibitor {Guid.NewGuid().ToString("N")[..8]}";
        Guid boothId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var exhibitorId = Guid.NewGuid();
            db.Set<Exhibitor>().Add(new Exhibitor
            {
                Id = exhibitorId,
                Name = exhibitorName,
                NameArabic = "عارض",
                IsActive = true,
                CreatedAt = SimfClock.Now,
            });
            var booth = new Booth
            {
                Id = Guid.NewGuid(),
                Code = "B-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = "Booth",
                NameArabic = "جناح",
                ExhibitorId = exhibitorId,
                Sector = "Defense",
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            db.Set<Booth>().Add(booth);
            await db.SaveChangesAsync();
            boothId = booth.Id;
        }

        var token = await SeedApprovedCallerTokenAsync();
        var body = await GetDirectoryAsync(token);

        // Kind + id + the name resolved from the LINKED exhibitor (not the booth name).
        Assert.Contains(body.Entries,
            e => e.Id == boothId
                && e.Kind == PartnerDirectoryKind.Booth
                && e.Name == exhibitorName);
    }

    [Fact]
    public async Task Company_that_is_both_a_sponsor_and_a_booth_is_deduped_to_the_sponsor()
    {
        // Same company registered BOTH as a Sponsor and as a booth Exhibitor. Owner
        // decision (2026-07-22): it must appear ONCE — as the Sponsor (kind=sponsor).
        // The shared Contact directory is gone (fields inlined); the sole de-dup key
        // is now a case-insensitive, trimmed match of the sponsor display name against
        // the booth exhibitor name, so both are seeded with the SAME name.
        var companyName = $"PD Dual Co {Guid.NewGuid().ToString("N")[..8]}";
        Guid sponsorId;
        Guid boothId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

            var sponsor = new Sponsor
            {
                Id = Guid.NewGuid(),
                Name = companyName,
                NameArabic = "راعٍ مزدوج",
                Tier = SponsorTier.Gold,
                DisplayOrder = 0,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            db.Sponsors.Add(sponsor);

            var exhibitorId = Guid.NewGuid();
            db.Set<Exhibitor>().Add(new Exhibitor
            {
                Id = exhibitorId,
                // Same English display name as the sponsor — collapses to the Sponsor.
                Name = companyName,
                NameArabic = "عارض مزدوج",
                IsActive = true,
                CreatedAt = SimfClock.Now,
            });
            var booth = new Booth
            {
                Id = Guid.NewGuid(),
                Code = "B-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = "Booth",
                NameArabic = "جناح",
                ExhibitorId = exhibitorId,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            db.Set<Booth>().Add(booth);

            await db.SaveChangesAsync();
            sponsorId = sponsor.Id;
            boothId = booth.Id;
        }

        var token = await SeedApprovedCallerTokenAsync();
        var body = await GetDirectoryAsync(token);

        // The Sponsor row is present...
        Assert.Contains(body.Entries,
            e => e.Id == sponsorId && e.Kind == PartnerDirectoryKind.Sponsor);
        // ...and the duplicate booth-company row is dropped (dedup by normalized name).
        Assert.DoesNotContain(body.Entries,
            e => e.Id == boothId && e.Kind == PartnerDirectoryKind.Booth);
    }

    [Fact]
    public async Task Booth_company_matching_a_sponsor_by_name_is_deduped_to_the_sponsor()
    {
        // No shared Contact — the only overlap is the company name. The
        // case-insensitive, trimmed name fallback still collapses it to the Sponsor.
        var companyName = $"PD NameDup Co {Guid.NewGuid().ToString("N")[..8]}";
        Guid sponsorId;
        Guid boothId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

            var sponsor = new Sponsor
            {
                Id = Guid.NewGuid(),
                Name = companyName,
                NameArabic = "راعٍ",
                Tier = SponsorTier.Gold,
                DisplayOrder = 0,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            db.Sponsors.Add(sponsor);

            var exhibitorId = Guid.NewGuid();
            db.Set<Exhibitor>().Add(new Exhibitor
            {
                Id = exhibitorId,
                // Same English name, different case — must still match on the fallback.
                Name = companyName.ToUpperInvariant(),
                NameArabic = "عارض",
                IsActive = true,
                CreatedAt = SimfClock.Now,
            });
            var booth = new Booth
            {
                Id = Guid.NewGuid(),
                Code = "B-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = "Booth",
                NameArabic = "جناح",
                ExhibitorId = exhibitorId,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            db.Set<Booth>().Add(booth);

            await db.SaveChangesAsync();
            sponsorId = sponsor.Id;
            boothId = booth.Id;
        }

        var token = await SeedApprovedCallerTokenAsync();
        var body = await GetDirectoryAsync(token);

        Assert.Contains(body.Entries,
            e => e.Id == sponsorId && e.Kind == PartnerDirectoryKind.Sponsor);
        Assert.DoesNotContain(body.Entries,
            e => e.Id == boothId && e.Kind == PartnerDirectoryKind.Booth);
    }

    [Fact]
    public async Task Opted_in_other_type_account_appears_as_kind_person()
    {
        var otherTypeId = await SeedProfileTypeAsync(isForVisitor: false);
        var profileId = await SeedAccountWithProfileAsync(
            otherTypeId, showInMeetLikeYou: true);

        var token = await SeedApprovedCallerTokenAsync();
        var body = await GetDirectoryAsync(token);

        // Id is the user-profile id; kind=person.
        Assert.Contains(body.Entries,
            e => e.Id == profileId && e.Kind == PartnerDirectoryKind.Person);
    }

    [Fact]
    public async Task Opted_in_visitor_type_account_never_appears()
    {
        // A visitor profile type (IsForVisitor=true) is a Normal/VIP attendee and
        // must never surface, even opted in.
        var visitorTypeId = await SeedProfileTypeAsync(isForVisitor: true);
        var profileId = await SeedAccountWithProfileAsync(
            visitorTypeId, showInMeetLikeYou: true);

        var token = await SeedApprovedCallerTokenAsync();
        var body = await GetDirectoryAsync(token);

        Assert.DoesNotContain(body.Entries, e => e.Id == profileId);
    }

    [Fact]
    public async Task Other_type_account_that_opted_out_does_not_appear()
    {
        var otherTypeId = await SeedProfileTypeAsync(isForVisitor: false);
        var profileId = await SeedAccountWithProfileAsync(
            otherTypeId, showInMeetLikeYou: false);

        var token = await SeedApprovedCallerTokenAsync();
        var body = await GetDirectoryAsync(token);

        Assert.DoesNotContain(body.Entries, e => e.Id == profileId);
    }

    [Fact]
    public async Task Other_type_hidden_from_meet_people_never_appears_even_when_opted_in()
    {
        // D-760: the admin master switch on the profile TYPE
        // (ShowInPartnerDirectory=false) hides ALL its accounts from the
        // directory, regardless of the per-user ShowInMeetLikeYou opt-in.
        var hiddenTypeId = await SeedProfileTypeAsync(
            isForVisitor: false, showInPartnerDirectory: false);
        var profileId = await SeedAccountWithProfileAsync(
            hiddenTypeId, showInMeetLikeYou: true);

        var token = await SeedApprovedCallerTokenAsync();
        var body = await GetDirectoryAsync(token);

        Assert.DoesNotContain(body.Entries, e => e.Id == profileId);
    }

    [Fact]
    public async Task Person_linked_to_a_curated_speaker_is_deduped_to_the_speaker()
    {
        var otherTypeId = await SeedProfileTypeAsync(isForVisitor: false);
        var profileId = await SeedAccountWithProfileAsync(
            otherTypeId, showInMeetLikeYou: true);

        // A curated Speaker linked to that same opted-in profile.
        Guid speakerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var speaker = new Speaker
            {
                Id = Guid.NewGuid(),
                Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = $"PD Linked Speaker {Guid.NewGuid().ToString("N")[..8]}",
                NameArabic = "متحدّث",
                DisplayOrder = 0,
                IsActive = true,
                CreatedAt = SimfClock.Now,
                UserProfileId = profileId,
            };
            db.Speakers.Add(speaker);
            await db.SaveChangesAsync();
            speakerId = speaker.Id;
        }

        var token = await SeedApprovedCallerTokenAsync();
        var body = await GetDirectoryAsync(token);

        // Curated entity wins: the linked speaker is present...
        Assert.Contains(body.Entries,
            e => e.Id == speakerId && e.Kind == PartnerDirectoryKind.Speaker);
        // ...and the opted-in person for that profile is dropped (no duplicate row).
        Assert.DoesNotContain(body.Entries,
            e => e.Id == profileId && e.Kind == PartnerDirectoryKind.Person);
    }

    [Fact]
    public async Task Directory_is_empty_when_the_cp_switch_is_off()
    {
        // Self-contained + serialized within this class. PartnerDirectoryEnabled is a
        // shared singleton, so it MUST be restored to true in the finally so the other
        // (presence-asserting) tests still see a populated directory.
        var token = await SeedApprovedCallerTokenAsync();
        try
        {
            await SetPartnerDirectoryEnabledAsync(false);

            var body = await GetDirectoryAsync(token);
            Assert.Empty(body.Entries);
        }
        finally
        {
            await SetPartnerDirectoryEnabledAsync(true);
        }
    }

    // -- Helpers --------------------------------------------------------------

    private async Task SetPartnerDirectoryEnabledAsync(bool enabled)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await db.OrganizationProfile
            .SingleAsync(p => p.Id == OrganizationProfile.SingletonId);
        profile.PartnerDirectoryEnabled = enabled;
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedProfileTypeAsync(
        bool isForVisitor, bool showInPartnerDirectory = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = $"PD-Type-{Guid.NewGuid():N}",
            NameArabic = "نوع",
            PageColor = "#3B82F6",
            IsForVisitor = isForVisitor,
            ShowInPartnerDirectory = showInPartnerDirectory,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.ProfileTypes.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    // Creates an Approved, non-Admin (Visitor UserType) Identity account and its
    // UserProfile on the given profile type with the given opt-in flag. Returns the
    // UserProfile.Id (the directory's person-entry id).
    private async Task<Guid> SeedAccountWithProfileAsync(
        Guid profileTypeId, bool showInMeetLikeYou)
    {
        var email = $"pd-person-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "PD Person",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ProfileTypeId = profileTypeId,
            Name = "PD Person",
            NameArabic = "شخص",
            PlaceOfBirth = "Riyadh",
            NationalityId = 0,
            ShowInMeetLikeYou = showInMeetLikeYou,
            CreatedAt = SimfClock.Now,
        };
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();
        return profile.Id;
    }

    // The directory is the same for every approved caller, so the caller only needs
    // to be an Approved account (no profile required).
    private async Task<string> SeedApprovedCallerTokenAsync()
    {
        var email = $"pd-caller-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "PD Caller",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var envelope = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return envelope.Data!.Tokens!.AccessToken;
    }

    private async Task<PartnerDirectoryResponse> GetDirectoryAsync(string token)
    {
        var response = await GetAuthAsync(
            "/api/v1/app/networking/partner-directory", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<PartnerDirectoryResponse>>())!.Data!;
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
