using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Support;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>Integration tests for the "تواصل معنا / Contact us" inquiries
/// (Figma 1388:7567): the public anonymous submit + the CP inbox list/handled
/// toggle.</summary>
[Trait(TestAreas.TraitName, TestAreas.Content)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class ContactInquiryTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ContactInquiryTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Anonymous_can_submit_and_admin_sees_it_then_marks_handled()
    {
        var name = $"Sara-{Guid.NewGuid():N}".Substring(0, 12);
        var submit = await _client.PostAsJsonAsync("/api/v1/app/contact-inquiry",
            new SubmitContactInquiryRequest
            {
                Name = name,
                Email = "sara@example.com",
                Message = "Hello, I have a question about registration.",
            });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

        var token = await CreateAdminAsync();
        var list = await PostAuthAsync("/api/v1/admin/contact-inquiries/list",
            new GridQuery { Top = 200 }, token);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminContactInquiryRow>>>())!.Data!;
        var row = page.Items.Single(r => r.Name == name);
        Assert.False(row.IsHandled);
        Assert.Equal("sara@example.com", row.Email);

        var handled = await PostAuthAsync(
            $"/api/v1/admin/contact-inquiries/{row.Id}/handled",
            new { handled = true }, token);
        Assert.Equal(HttpStatusCode.OK, handled.StatusCode);

        var list2 = await PostAuthAsync("/api/v1/admin/contact-inquiries/list",
            new GridQuery { Top = 200 }, token);
        var page2 = (await list2.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminContactInquiryRow>>>())!.Data!;
        Assert.True(page2.Items.Single(r => r.Id == row.Id).IsHandled);
    }

    [Fact]
    public async Task Submit_with_a_blank_name_or_bad_email_returns_400()
    {
        var blankName = await _client.PostAsJsonAsync("/api/v1/app/contact-inquiry",
            new SubmitContactInquiryRequest
            {
                Name = "", Email = "a@b.com", Message = "Hi there, a message.",
            });
        Assert.Equal(HttpStatusCode.BadRequest, blankName.StatusCode);

        var badEmail = await _client.PostAsJsonAsync("/api/v1/app/contact-inquiry",
            new SubmitContactInquiryRequest
            {
                Name = "Sara", Email = "not-an-email", Message = "Hi there, a message.",
            });
        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);
    }

    [Fact]
    public async Task Inbox_list_rejects_an_anonymous_caller()
    {
        var anon = await _client.PostAsJsonAsync(
            "/api/v1/admin/contact-inquiries/list", new GridQuery { Top = 10 });
        Assert.True(anon.StatusCode is HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden);
    }

    // -- Helpers (mirror FaqTests) ---------------------------------------------

    private async Task<string> CreateAdminAsync()
    {
        var email = $"contact-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Contact Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }
        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private async Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
