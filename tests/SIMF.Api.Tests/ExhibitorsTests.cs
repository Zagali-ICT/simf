// D-357 — regression coverage for the Admins/Exhibitors-list company-logo thumbnail.
// An exhibitor owns no logo of its own; the grid resolves the ONE-HOP
// Exhibitor→Contact and reports the linked Contact's active CompanyLogo via
// AdminExhibitorSummary.HasLogo (+ the resolved ContactId). This is a distinct
// projection from the Booth two-hop (AdminBoothsTests) and the Contact own-id path
// (ContactsTests), so it needs its own guard. Also satisfies the // Tests: header on
// AdminExhibitorService.cs.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Exhibitors;
using SIMF.Domain.Contacts;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.Files;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ExhibitorsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ExhibitorsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Exhibitor_list_reports_HasLogo_from_the_linked_contacts_company_logo()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var marker = $"EX{Guid.NewGuid():N}"[..10];

        // Three distinct paths: linked contact WITH a logo, linked contact WITHOUT a
        // logo, and NO contact at all (the resolve is null-safe, not an inner join
        // that would hide the row).
        var withLogo = await SeedExhibitorWithContactAsync($"{marker}-with", withLogo: true);
        var noLogo = await SeedExhibitorWithContactAsync($"{marker}-nolg", withLogo: false);
        var noContact = await SeedExhibitorAsync($"{marker}-none");

        var rows = await ListExhibitorsAsync(token);
        var rowWith = rows.Single(e => e.Id == withLogo.ExhibitorId);
        var rowNoLogo = rows.Single(e => e.Id == noLogo.ExhibitorId);
        var rowNoContact = rows.Single(e => e.Id == noContact);

        // Linked contact has an active CompanyLogo → HasLogo true + the contact resolved.
        Assert.True(rowWith.HasLogo);
        Assert.Equal(withLogo.ContactId, rowWith.ContactId);
        // Linked contact but no logo → initials fallback (HasLogo false), contact still resolved.
        Assert.False(rowNoLogo.HasLogo);
        Assert.Equal(noLogo.ContactId, rowNoLogo.ContactId);
        // No contact at all → still lists, with no resolved contact / logo.
        Assert.Null(rowNoContact.ContactId);
        Assert.False(rowNoContact.HasLogo);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<IReadOnlyList<AdminExhibitorSummary>> ListExhibitorsAsync(string token)
    {
        var list = await PostAuthAsync(
            "/api/v1/admin/exhibitors/list", new GridQuery { Top = 200 }, token);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        return (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminExhibitorSummary>>>())!.Data!.Items;
    }

    // Seeds a Contact (optionally with an active CompanyLogo asset) and an Exhibitor
    // linked to it; returns both ids so the test can assert the resolved ContactId.
    private async Task<(Guid ExhibitorId, Guid ContactId)> SeedExhibitorWithContactAsync(
        string nameEn, bool withLogo)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var contactId = Guid.NewGuid();
        appDb.Contacts.Add(new Contact
        {
            Id = contactId,
            Name = "Logo Co",
            NameArabic = "جهة الشعار",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        if (withLogo)
        {
            appDb.Set<StoredFile>().Add(new StoredFile
            {
                Id = Guid.NewGuid(),
                Service = FileService.CompanyLogo,
                OwnerEntityId = contactId,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
        var exhibitorId = Guid.NewGuid();
        appDb.Exhibitors.Add(new Exhibitor
        {
            Id = exhibitorId,
            Name = nameEn,
            NameArabic = "عارض",
            ContactId = contactId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await appDb.SaveChangesAsync();
        return (exhibitorId, contactId);
    }

    // Seeds an active exhibitor with NO linked contact.
    private async Task<Guid> SeedExhibitorAsync(string nameEn)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var exhibitorId = Guid.NewGuid();
        appDb.Exhibitors.Add(new Exhibitor
        {
            Id = exhibitorId,
            Name = nameEn,
            NameArabic = "عارض بلا جهة",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await appDb.SaveChangesAsync();
        return exhibitorId;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"exhibitor-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Exhibitor Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        var sign = await _client.PostAsJsonAsync(
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

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
