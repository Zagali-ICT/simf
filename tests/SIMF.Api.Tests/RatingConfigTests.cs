// Admin rating-configuration CRUD (types → groups → questions) + the
// permission gates, the system-type delete-block, and required-question
// enforcement on the app submit path. Mirrors FaqTests.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Feedback;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class RatingConfigTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public RatingConfigTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_can_crud_a_type_group_and_question()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var code = $"exh-{Guid.NewGuid():N}".Substring(0, 12);

        // Create a type.
        var typeResp = await PostAuthAsync("/api/v1/admin/ratings/types",
            new CreateRatingTypeRequest
            {
                Code = code, Name = "Exhibition", NameArabic = "المعرض",
                Scope = RatingScope.Global, HasOverallStars = true, AllowComment = true,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, typeResp.StatusCode);
        var type = (await typeResp.Content.ReadFromJsonAsync<ApiResult<AdminRatingTypeSummary>>())!.Data!;
        Assert.False(type.IsSystem);

        // Create a group under it.
        var groupResp = await PostAuthAsync("/api/v1/admin/ratings/groups",
            new CreateRatingQuestionGroupRequest
            {
                RatingTypeId = type.Id, Name = "Staff", NameArabic = "الطاقم",
            }, admin);
        Assert.Equal(HttpStatusCode.OK, groupResp.StatusCode);
        var group = (await groupResp.Content.ReadFromJsonAsync<ApiResult<AdminRatingQuestionGroupSummary>>())!.Data!;

        // Create a question in the group.
        var qResp = await PostAuthAsync("/api/v1/admin/ratings/questions",
            new CreateRatingQuestionRequest
            {
                RatingTypeId = type.Id, RatingQuestionGroupId = group.Id,
                Text = "Helpfulness", TextArabic = "المساعدة", IsRequired = false,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, qResp.StatusCode);
        var question = (await qResp.Content.ReadFromJsonAsync<ApiResult<AdminRatingQuestionSummary>>())!.Data!;
        Assert.Equal(group.Id, question.RatingQuestionGroupId);

        // The type list now shows the child counts.
        var listResp = await PostAuthAsync("/api/v1/admin/ratings/types/list",
            new GridQuery { Top = 200 }, admin);
        var page = (await listResp.Content.ReadFromJsonAsync<ApiResult<GridPage<AdminRatingTypeSummary>>>())!.Data!;
        var mine = page.Items.Single(t => t.Id == type.Id);
        Assert.Equal(1, mine.GroupCount);
        Assert.Equal(1, mine.QuestionCount);

        // Soft-delete the question.
        var delResp = await DeleteAuthAsync($"/api/v1/admin/ratings/questions/{question.Id}", admin);
        Assert.Equal(HttpStatusCode.OK, delResp.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_built_in_system_type_is_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();

        // Resolve the seeded "App" system type id.
        var listResp = await PostAuthAsync("/api/v1/admin/ratings/types/list",
            new GridQuery { Top = 200 }, admin);
        var page = (await listResp.Content.ReadFromJsonAsync<ApiResult<GridPage<AdminRatingTypeSummary>>>())!.Data!;
        var appType = page.Items.Single(t => t.Code == "App");
        Assert.True(appType.IsSystem);

        var delResp = await DeleteAuthAsync($"/api/v1/admin/ratings/types/{appType.Id}", admin);
        Assert.Equal(HttpStatusCode.BadRequest, delResp.StatusCode);
    }

    [Fact]
    public async Task Duplicate_type_code_is_409()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var code = $"dup-{Guid.NewGuid():N}".Substring(0, 12);
        var req = new CreateRatingTypeRequest
        {
            Code = code, Name = "Dup", NameArabic = "مكرر", Scope = RatingScope.Global,
        };

        Assert.Equal(HttpStatusCode.OK,
            (await PostAuthAsync("/api/v1/admin/ratings/types", req, admin)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await PostAuthAsync("/api/v1/admin/ratings/types", req, admin)).StatusCode);
    }

    [Fact]
    public async Task Non_admin_cannot_create_a_type()
    {
        var visitor = await SignInApprovedVisitorAsync();
        var resp = await PostAuthAsync("/api/v1/admin/ratings/types",
            new CreateRatingTypeRequest { Code = "x", Name = "X", NameArabic = "س" }, visitor);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Required_question_must_be_answered_to_submit()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var code = $"req-{Guid.NewGuid():N}".Substring(0, 12);

        var type = (await (await PostAuthAsync("/api/v1/admin/ratings/types",
            new CreateRatingTypeRequest
            {
                Code = code, Name = "ReqTest", NameArabic = "اختبار",
                Scope = RatingScope.Global, HasOverallStars = true, AllowComment = false,
            }, admin)).Content.ReadFromJsonAsync<ApiResult<AdminRatingTypeSummary>>())!.Data!;

        var question = (await (await PostAuthAsync("/api/v1/admin/ratings/questions",
            new CreateRatingQuestionRequest
            {
                RatingTypeId = type.Id, Text = "Must answer", TextArabic = "إلزامي",
                IsRequired = true,
            }, admin)).Content.ReadFromJsonAsync<ApiResult<AdminRatingQuestionSummary>>())!.Data!;

        var visitor = await SignInApprovedVisitorAsync();

        // Missing the required answer → 400.
        var missing = await PostAuthAsync("/api/v1/app/feedback/submit",
            new SubmitRatingRequest { RatingTypeId = type.Id, OverallStars = 5 }, visitor);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        // With the required answer → 200.
        var ok = await PostAuthAsync("/api/v1/app/feedback/submit",
            new SubmitRatingRequest
            {
                RatingTypeId = type.Id, OverallStars = 5,
                Answers = new List<RatingAnswerInput> { new(question.Id, 4) },
            }, visitor);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<string> SignInApprovedVisitorAsync()
    {
        var email = $"rc-visitor-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync("/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = AuthFlow.Password, ConfirmPassword = AuthFlow.Password });
        await _client.PostAsJsonAsync("/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        AuthFlow.DisableTwoFactor(_factory, email);
        var sign = await _client.PostAsJsonAsync("/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var token = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
        // Owner 2026-07-19 — ratings now require attendance; mark the visitor as having
        // attended the event so the rating-submit test still runs.
        await RatingAttendance.SeedEventAttendanceAsync(
            _factory, await RatingAttendance.UserIdAsync(_factory, email));
        return token;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"rc-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "RC Admin", AccountState = AccountState.Approved, UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }
        var sign = await _client.PostAsJsonAsync("/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password, Audience = SignInAudience.Cp });
        return (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
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
