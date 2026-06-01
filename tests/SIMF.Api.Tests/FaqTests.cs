using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Faq;
using SIMF.Domain.IdentityAccess;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>P2.1 (D-211) — integration tests for the two-level FAQ admin CRUD
/// (groups → entries).</summary>
public sealed class FaqTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public FaqTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_can_create_list_and_get_a_group()
    {
        var token = await CreateAdminAsync();
        var name = $"Registration-{Guid.NewGuid():N}".Substring(0, 18);

        var create = await PostAuthAsync("/api/v1/admin/faq/groups",
            new CreateFaqGroupRequest { NameEn = name, NameAr = "التسجيل", DisplayOrder = 1 }, token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<ApiResult<AdminFaqGroupSummary>>())!.Data!;
        Assert.True(created.IsActive);
        Assert.Equal(0, created.EntryCount);

        var list = await PostAuthAsync("/api/v1/admin/faq/groups/list",
            new GridQuery { Top = 200 }, token);
        var page = (await list.Content.ReadFromJsonAsync<ApiResult<GridPage<AdminFaqGroupSummary>>>())!.Data!;
        Assert.Contains(page.Items, g => g.Id == created.Id);

        var get = await GetAuthAsync($"/api/v1/admin/faq/groups/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Admin_can_add_entries_under_a_group_and_count_reflects_them()
    {
        var token = await CreateAdminAsync();
        var groupId = await CreateGroupAsync(token);

        var addEntry = await PostAuthAsync("/api/v1/admin/faq/entries",
            new CreateFaqEntryRequest
            {
                FaqGroupId = groupId,
                QuestionEn = "How do I register?",
                QuestionAr = "كيف أسجل؟",
                AnswerEn = "Use the sign-up page.",
                AnswerAr = "استخدم صفحة التسجيل.",
                DisplayOrder = 1,
            }, token);
        Assert.Equal(HttpStatusCode.OK, addEntry.StatusCode);

        var list = await PostAuthAsync($"/api/v1/admin/faq/groups/{groupId}/entries/list",
            new GridQuery { Top = 200 }, token);
        var page = (await list.Content.ReadFromJsonAsync<ApiResult<GridPage<AdminFaqEntrySummary>>>())!.Data!;
        Assert.Single(page.Items);
        Assert.Equal("How do I register?", page.Items[0].QuestionEn);

        // The group's live entry count now reflects the entry.
        var get = await GetAuthAsync($"/api/v1/admin/faq/groups/{groupId}", token);
        var group = (await get.Content.ReadFromJsonAsync<ApiResult<AdminFaqGroupSummary>>())!.Data!;
        Assert.Equal(1, group.EntryCount);
    }

    [Fact]
    public async Task Deactivating_a_group_hides_it()
    {
        var token = await CreateAdminAsync();
        var groupId = await CreateGroupAsync(token);

        var del = await DeleteAuthAsync($"/api/v1/admin/faq/groups/{groupId}", token);
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        var get = await GetAuthAsync($"/api/v1/admin/faq/groups/{groupId}", token);
        var group = (await get.Content.ReadFromJsonAsync<ApiResult<AdminFaqGroupSummary>>())!.Data!;
        Assert.False(group.IsActive);
    }

    [Fact]
    public async Task Creating_an_entry_under_an_unknown_group_returns_404()
    {
        var token = await CreateAdminAsync();
        var response = await PostAuthAsync("/api/v1/admin/faq/entries",
            new CreateFaqEntryRequest
            {
                FaqGroupId = Guid.NewGuid(),
                QuestionEn = "Q", QuestionAr = "س",
                AnswerEn = "A", AnswerAr = "ج", DisplayOrder = 0,
            }, token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_group_with_a_blank_name_returns_400()
    {
        var token = await CreateAdminAsync();
        var response = await PostAuthAsync("/api/v1/admin/faq/groups",
            new CreateFaqGroupRequest { NameEn = "", NameAr = "", DisplayOrder = 0 }, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- Helpers ---------------------------------------------------------------

    private async Task<Guid> CreateGroupAsync(string token)
    {
        var response = await PostAuthAsync("/api/v1/admin/faq/groups",
            new CreateFaqGroupRequest
            {
                NameEn = $"Group-{Guid.NewGuid():N}".Substring(0, 14),
                NameAr = "مجموعة",
                DisplayOrder = 0,
            }, token);
        var created = (await response.Content.ReadFromJsonAsync<ApiResult<AdminFaqGroupSummary>>())!.Data!;
        return created.Id;
    }

    private async Task<string> CreateAdminAsync()
    {
        var email = $"faq-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "FAQ Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password, Audience = SignInAudience.Cp });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private async Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
