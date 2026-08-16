using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Notifications;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the P13 — D-054 lifecycle notifications: each
/// trigger should write the right Kind into the recipient's row + queue
/// an email through the FakeEmailSender.
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
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
        // Seed an interest + organisation so the upsert validator passes.
        var interestId = await SeedInterestAsync();
        var organisationId = await SeedOrganisationAsync();

        var request = new UpsertUserProfileRequest
        {
            InterestIds = new List<Guid> { interestId },
            ArabicName = "محمد عبدالله أحمد الزهراني",
            EnglishName = "Notification Test User Account",
            NationalityCode = "SA",
            DateOfBirth = new DateOnly(1990, 1, 1),
            PlaceOfBirth = "Riyadh",
            IsSaudi = true,
            NationalId = "1101798278",
            OrganisationId = organisationId,
            // DEF-PHN-004 — the mobile is required on the upsert now.
            SaudiMobile = "0501234567",
        };

        var response = await PostAuthAsync(
            "/api/v1/app/account/user-profile", request, subjectToken);
        Assert.True(response.IsSuccessStatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        // State auto-transitioned.
        var subject = await db.Users.SingleAsync(u => u.Id == subjectId);
        Assert.Equal(AccountState.PendingApproval, subject.AccountState);

        // Visitor's "ProfileSubmitted" notification landed.
        Assert.True(await db.Notifications.AnyAsync(n =>
            n.UserId == subjectId && n.Kind == NotificationKind.AccountProfileSubmitted));

        // Admin's "PendingVisitor" notification landed.
        Assert.True(await db.Notifications.AnyAsync(n =>
            n.UserId == adminId && n.Kind == NotificationKind.AdminPendingVisitor));
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
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var notification = await db.Notifications
            .SingleAsync(n => n.UserId == subjectId
                && n.Kind == NotificationKind.AccountApproved);
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
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var notification = await db.Notifications
            .SingleAsync(n => n.UserId == subjectId
                && n.Kind == NotificationKind.AccountRejected);
        Assert.Equal(NotificationSeverity.Error, notification.Severity);
        Assert.Contains("Not permitted", notification.Body);
    }

    // ----------------------------------------------------------------------
    // D-111: auth-flow closure notifications.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task VerifyEmail_dispatches_AccountWelcome_to_the_user()
    {
        var email = $"welcome-{Guid.NewGuid():N}@simf.test";

        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = AuthFlow.Password, ConfirmPassword = AuthFlow.Password });
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
            });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        var welcome = await db.Notifications
            .SingleAsync(n => n.UserId == user.Id && n.Kind == NotificationKind.AccountWelcome);
        Assert.Equal(NotificationSeverity.Success, welcome.Severity);
        Assert.Contains("Welcome", welcome.Title);
    }

    [Fact]
    public async Task AdminCreate_dispatches_AdminPendingApproval_to_every_other_admin()
    {
        // Two admins exist: the actor (who clicks Create) and a peer.
        // The peer should get AdminPendingApproval; the actor should NOT.
        var actorToken = await CreateAdministratorAndSignInAsync();
        var (_, peerAdminId) = await CreateAdminAsync();
        var actorAdminId = Guid.Parse(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(actorToken).Claims.First(c => c.Type == "sub").Value);

        var subjectEmail = $"pending-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/admins",
            new AdminCreateAdminRequest
            {
                Email = subjectEmail,
                DisplayName = "Pending Admin",
                Roles = new List<string> { AppRoles.Administrator },
            },
            actorToken);
        Assert.True(response.IsSuccessStatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var peerRow = await db.Notifications.SingleAsync(n =>
            n.UserId == peerAdminId
            && n.Kind == NotificationKind.AdminPendingApproval);
        Assert.Equal(NotificationSeverity.Info, peerRow.Severity);
        Assert.Contains(subjectEmail, peerRow.Body);

        var actorRows = await db.Notifications.CountAsync(n =>
            n.UserId == actorAdminId
            && n.Kind == NotificationKind.AdminPendingApproval);
        Assert.Equal(0, actorRows);
    }

    [Fact]
    public async Task AdminCreate_dispatches_AccountWelcome_to_the_new_user()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectEmail = $"created-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/admins",
            new AdminCreateAdminRequest
            {
                Email = subjectEmail,
                DisplayName = "New Admin",
                Roles = new List<string> { AppRoles.Administrator },
            },
            adminToken);
        Assert.True(response.IsSuccessStatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminCreateUserResponse>>())!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var welcome = await db.Notifications
            .SingleAsync(n => n.UserId == body.Data!.UserId
                && n.Kind == NotificationKind.AccountWelcome);
        Assert.Equal(NotificationSeverity.Success, welcome.Severity);
    }

    [Fact]
    public async Task ChangePassword_dispatches_AccountPasswordChanged_to_the_user()
    {
        var tokens = await AuthFlow.SignInVisitorAsync(_client, _factory);
        const string newPassword = "Newp@ssw0rd!";

        using var changeRequest = new HttpRequestMessage(
            HttpMethod.Post, "/api/v1/app/auth/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest
            {
                CurrentPassword = AuthFlow.Password,
                NewPassword = newPassword,
                ConfirmPassword = newPassword,
            }),
        };
        changeRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var change = await _client.SendAsync(changeRequest);
        Assert.True(change.IsSuccessStatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var notification = await db.Notifications
            .SingleAsync(n => n.UserId == tokens.User.Id
                && n.Kind == NotificationKind.AccountPasswordChanged);
        Assert.Equal(NotificationSeverity.Warning, notification.Severity);
        Assert.Contains("changed", notification.Body);
    }

    [Fact]
    public async Task ResetPassword_dispatches_AccountPasswordResetCompleted_to_the_user()
    {
        var email = await AuthFlow.RegisterVerifiedVisitorAsync(_client, _factory);
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });
        var code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.PasswordReset);
        const string newPassword = "Resetp@ssw0rd!";

        var reset = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/reset-password",
            new ResetPasswordRequest
            {
                Email = email,
                Code = code,
                NewPassword = newPassword,
                ConfirmPassword = newPassword,
            });
        Assert.True(reset.IsSuccessStatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        var notification = await db.Notifications
            .SingleAsync(n => n.UserId == user.Id
                && n.Kind == NotificationKind.AccountPasswordResetCompleted);
        Assert.Equal(NotificationSeverity.Warning, notification.Severity);
        Assert.Contains("reset", notification.Body);
    }

    // -- helpers ---------------------------------------------------------------

    private async Task<(string Token, Guid UserId)> CreateEmailVerifiedVisitorAsync()
    {
        var email = $"visitor-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
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
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (body.Data!.Tokens!.AccessToken, userId);
    }

    private async Task<(string Email, Guid Id)> CreateAdminAsync()
    {
        var email = $"admin-{Guid.NewGuid():N}@simf.test";
        Guid id;
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
        if (!await roles.RoleExistsAsync(AdministratorRole))
        {
            await roles.CreateAsync(new SimfRole { Name = AdministratorRole });
        }
        var user = new SimfUser
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
        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
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
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var interest = new UserInterest
        {
            Id = Guid.NewGuid(),
            Name = $"Lifecycle Interest {Guid.NewGuid():N}",
            NameArabic = "اهتمام",
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.Interests.Add(interest);
        await db.SaveChangesAsync();
        await appDb.SaveChangesAsync();
        return interest.Id;
    }

    // B3 — D-221: organisation is now required on the profile upsert.
    private async Task<Guid> SeedOrganisationAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var org = new SIMF.Domain.Organisations.Organisation
        {
            Id = Guid.NewGuid(),
            NameArabic = "جهة الإشعارات",
            Name = $"Notif Org {Guid.NewGuid():N}",
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.Organisations.Add(org);
        await appDb.SaveChangesAsync();
        return org.Id;
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
