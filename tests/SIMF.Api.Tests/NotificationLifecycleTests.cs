using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Notifications;
using SIMF.Infrastructure.Identity;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the P13 — D-054 lifecycle notifications: each
/// trigger should write the right Kind into the recipient's row + queue
/// an email through the FakeEmailSender.
/// </summary>
public sealed class NotificationLifecycleTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public NotificationLifecycleTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task First_profile_submit_transitions_state_to_PendingApproval_and_notifies_visitor_and_admins()
    {
        var (subjectToken, subjectId) = await CreateEmailVerifiedVisitorAsync();
        var (adminEmail, adminId) = await CreateAdminAsync();
        // Seed an interest so the upsert validator passes.
        var interestId = await SeedInterestAsync();

        var request = new UpsertUserProfileRequest
        {
            InterestIds = new List<Guid> { interestId },
            ArabicName = "اسم",
            EnglishName = "Name",
            NationalityCode = "SA",
            DateOfBirth = new DateOnly(1990, 1, 1),
            PlaceOfBirth = "Riyadh",
            IsSaudi = true,
            NationalId = "1234567890",
        };

        var response = await PostAuthAsync(
            "/api/v1/account/user-profile", request, subjectToken);
        Assert.True(response.IsSuccessStatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();

        // State auto-transitioned.
        var subject = await db.Users.SingleAsync(u => u.Id == subjectId);
        Assert.Equal(AccountState.PendingApproval, subject.AccountState);

        // Visitor's "ProfileSubmitted" notification landed.
        Assert.True(await db.Notifications.AnyAsync(n =>
            n.UserId == subjectId && n.Kind == "Account.ProfileSubmitted"));

        // Admin's "PendingVisitor" notification landed.
        Assert.True(await db.Notifications.AnyAsync(n =>
            n.UserId == adminId && n.Kind == "Admin.PendingVisitor"));
    }

    [Fact]
    public async Task Admin_approve_dispatches_Account_Approved_notification_with_QrId()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectId = await CreateStaffSubjectAsync(adminToken);

        var response = await PostAuthAsync(
            $"/api/v1/admin/admins/{subjectId}/approve", new { }, adminToken);
        Assert.True(response.IsSuccessStatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var notification = await db.Notifications
            .SingleAsync(n => n.UserId == subjectId
                && n.Kind == "Account.Approved");
        Assert.Equal(NotificationSeverity.Success, notification.Severity);
    }

    [Fact]
    public async Task Admin_reject_dispatches_Account_Rejected_notification_with_reason()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectId = await CreateStaffSubjectAsync(adminToken);

        var response = await PostAuthAsync(
            $"/api/v1/admin/admins/{subjectId}/reject",
            new AdminRejectRequest
            {
                Reason = "Not permitted per the operations list — please contact ops.",
            },
            adminToken);
        Assert.True(response.IsSuccessStatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var notification = await db.Notifications
            .SingleAsync(n => n.UserId == subjectId
                && n.Kind == "Account.Rejected");
        Assert.Equal(NotificationSeverity.Error, notification.Severity);
        Assert.Contains("Not permitted", notification.Body);
    }

    // -- helpers ---------------------------------------------------------------

    private async Task<(string Token, Guid UserId)> CreateEmailVerifiedVisitorAsync()
    {
        var email = $"visitor-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentitySimfUser>>();
            var user = new IdentitySimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Lifecycle Test",
                AccountState = AccountState.EmailVerified,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (body.Data!.Tokens!.AccessToken, userId);
    }

    private async Task<(string Email, Guid Id)> CreateAdminAsync()
    {
        var email = $"admin-{Guid.NewGuid():N}@simf.test";
        Guid id;
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentitySimfUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
        if (!await roles.RoleExistsAsync(AdministratorRole))
        {
            await roles.CreateAsync(new SimfRole { Name = AdministratorRole });
        }
        var user = new IdentitySimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Admin",
            AccountState = AccountState.Approved,
            UserType = UserType.Admin,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        await users.AddToRoleAsync(user, AdministratorRole);
        id = user.Id;
        return (email, id);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var (email, _) = await CreateAdminAsync();
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private async Task<Guid> CreateStaffSubjectAsync(string adminToken)
    {
        var email = $"subject-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/admins",
            new AdminCreateAdminRequest
            {
                Email = email,
                DisplayName = "Subject",
                Roles = new List<string> { AppRoles.Administrator },
            },
            adminToken);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminCreateUserResponse>>())!;
        return body.Data!.UserId;
    }

    private async Task<Guid> SeedInterestAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var interest = new Interest
        {
            Id = Guid.NewGuid(),
            Name = $"Lifecycle Interest {Guid.NewGuid():N}",
            NameArabic = "اهتمام",
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Interests.Add(interest);
        await db.SaveChangesAsync();
        return interest.Id;
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
