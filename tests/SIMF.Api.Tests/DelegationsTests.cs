// D-174 (gap doc G11, Mockup page 21) — Delegations admin CRUD + public list.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Delegations;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class DelegationsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public DelegationsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_creates_delegation_and_public_endpoint_returns_it()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/delegations",
            new CreateDelegationRequest
            {
                Name = "Royal Navy Delegation",
                NameArabic = "وفد البحرية الملكية",
                MemberCount = 8,
                IsPriority = true,
                IsInternational = true,
                DisplayOrder = 0,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminDelegationSummary>>())!.Data!;
        Assert.True(created.IsPriority);

        var publicList = await _client.GetAsync("/api/v1/app/delegations");
        Assert.Equal(HttpStatusCode.OK, publicList.StatusCode);
        var body = (await publicList.Content
            .ReadFromJsonAsync<ApiResult<PublicDelegations>>())!.Data!;
        Assert.Contains(body.Items, d => d.Id == created.Id);
    }

    [Fact]
    public async Task Public_list_orders_priority_first()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        await PostAuthAsync(
            "/api/v1/admin/delegations",
            new CreateDelegationRequest
            {
                Name = "Regular A", NameArabic = "عادي أ",
                MemberCount = 3, DisplayOrder = 0,
                IsPriority = false, IsInternational = false,
            }, admin);
        await PostAuthAsync(
            "/api/v1/admin/delegations",
            new CreateDelegationRequest
            {
                Name = "Priority B", NameArabic = "أولوية ب",
                MemberCount = 5, DisplayOrder = 0,
                IsPriority = true, IsInternational = false,
            }, admin);

        var list = await _client.GetAsync("/api/v1/app/delegations");
        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicDelegations>>())!.Data!;
        var priorityIndex = -1;
        var regularIndex = -1;
        for (var i = 0; i < body.Items.Count; i++)
        {
            if (body.Items[i].Name == "Priority B") priorityIndex = i;
            if (body.Items[i].Name == "Regular A") regularIndex = i;
        }
        Assert.True(priorityIndex >= 0 && regularIndex >= 0);
        Assert.True(priorityIndex < regularIndex,
            "Priority delegation must precede non-priority in the public list.");
    }

    [Fact]
    public async Task Deactivated_delegation_drops_off_public_list()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/delegations",
            new CreateDelegationRequest
            {
                Name = "Soon Gone", NameArabic = "سيختفي قريباً",
                MemberCount = 2, DisplayOrder = 0,
            }, admin);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminDelegationSummary>>())!.Data!;

        await DeleteAuthAsync($"/api/v1/admin/delegations/{created.Id}", admin);

        var list = await _client.GetAsync("/api/v1/app/delegations");
        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicDelegations>>())!.Data!;
        Assert.DoesNotContain(body.Items, d => d.Id == created.Id);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var visitor = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync(
            "/api/v1/admin/delegations",
            new CreateDelegationRequest
            {
                Name = "x", NameArabic = "س", MemberCount = 1,
            }, visitor.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"deleg-admin-{Guid.NewGuid():N}@simf.test";
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
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Deleg Admin",
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
                Email = email, Password = AuthFlow.Password,
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
