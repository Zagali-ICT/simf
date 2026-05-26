using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// H25 — D-085: end-to-end visitor lifecycle test (Sprint 1 §3.5).
/// Drives the full chain in one <see cref="Fact"/> so a cross-hop seam
/// regression (e.g. an Approved visitor cannot sign in on the App
/// audience because of a stamp/claim drift) is caught at the boundary
/// between any two hops, not lost in the gap between two per-hop
/// tests.
///
/// <para>The chain:</para>
/// <list type="number">
///   <item>Sign up (visitor) → <see cref="AccountState.Registered"/>.</item>
///   <item>Verify email → <see cref="AccountState.EmailVerified"/>.</item>
///   <item>Sign in (Web audience, no 2FA) — gets tokens for the
///         profile-upsert call.</item>
///   <item>Upsert profile (with one Interest) — H2 (D-057) auto-transitions
///         to <see cref="AccountState.PendingApproval"/> and revokes the
///         existing refresh token.</item>
///   <item>Admin approves → <see cref="AccountState.Approved"/> +
///         <c>QrId</c> minted.</item>
///   <item>Visitor signs in again, this time on the App audience — the
///         new JWT carries <c>account_state=Approved</c> and
///         <c>user_type=Visitor</c>.</item>
/// </list>
/// </summary>
public sealed class VisitorLifecycleTests : IClassFixture<SimfApiFactory>
{
    private const string Password = "Passw0rd!";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public VisitorLifecycleTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Visitor_lifecycle_signup_through_approved_app_sign_in()
    {
        var email = $"lifecycle-{Guid.NewGuid():N}@simf.test";

        // ----------------------------------------------------------------
        // 1. Sign up → Registered.
        // ----------------------------------------------------------------
        var signUp = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-up",
            new SignUpRequest { Email = email, Password = Password, ConfirmPassword = Password });
        Assert.Equal(HttpStatusCode.Created, signUp.StatusCode);
        Assert.Equal(AccountState.Registered, GetAccountState(email));

        // ----------------------------------------------------------------
        // 2. Verify email → EmailVerified.
        // ----------------------------------------------------------------
        var verifyCode = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification);
        var verify = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest { Email = email, Code = verifyCode });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        Assert.Equal(AccountState.EmailVerified, GetAccountState(email));

        // ----------------------------------------------------------------
        // 3. Sign in (Web audience, default no-2FA) — get tokens for
        //    the profile-upsert call.
        // ----------------------------------------------------------------
        var firstSignIn = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest { Email = email, Password = Password, Audience = SignInAudience.Web });
        Assert.Equal(HttpStatusCode.OK, firstSignIn.StatusCode);
        var firstBody = (await firstSignIn.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        Assert.False(firstBody.Data!.MfaRequired);
        Assert.NotNull(firstBody.Data.Tokens);
        var emailVerifiedAccessToken = firstBody.Data.Tokens!.AccessToken;

        // The JWT carries account_state=EmailVerified at this hop.
        Assert.Equal("EmailVerified", JwtClaim(emailVerifiedAccessToken, "account_state"));
        Assert.Equal("Visitor", JwtClaim(emailVerifiedAccessToken, "user_type"));

        // ----------------------------------------------------------------
        // 4. Seed an Interest + upsert profile → H2 (D-057) auto-transitions
        //    to PendingApproval and revokes the visitor's refresh token.
        // ----------------------------------------------------------------
        var interestId = await SeedInterestAsync();
        var upsertRequest = new HttpRequestMessage(
            HttpMethod.Post, "/api/v1/account/user-profile")
        {
            Content = JsonContent.Create(new UpsertUserProfileRequest
            {
                InterestIds = new List<Guid> { interestId },
                ArabicName = "زائر اختبار",
                EnglishName = "Lifecycle Visitor",
                NationalityCode = "SA",
                DateOfBirth = new DateOnly(1990, 1, 1),
                PlaceOfBirth = "Riyadh",
                IsSaudi = true,
                NationalId = "1234567890",
            }),
        };
        upsertRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", emailVerifiedAccessToken);
        var upsert = await _client.SendAsync(upsertRequest);
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);
        Assert.Equal(AccountState.PendingApproval, GetAccountState(email));

        // ----------------------------------------------------------------
        // 5. Admin approves → Approved + QrId minted.
        // ----------------------------------------------------------------
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectId = await FindUserIdAsync(email);
        var approve = await PostAuthAsync(
            $"/api/v1/admin/visitors/{subjectId}/approve", new { }, adminToken);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var (state, qrId) = GetAccountStateAndQr(email);
        Assert.Equal(AccountState.Approved, state);
        Assert.False(string.IsNullOrEmpty(qrId));

        // ----------------------------------------------------------------
        // 6. Visitor signs in on the App audience — the new JWT must carry
        //    account_state=Approved and user_type=Visitor.
        // ----------------------------------------------------------------
        var finalSignIn = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest
            {
                Email = email, Password = Password, Audience = SignInAudience.App,
            });
        Assert.Equal(HttpStatusCode.OK, finalSignIn.StatusCode);
        var finalBody = (await finalSignIn.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        Assert.False(finalBody.Data!.MfaRequired);
        Assert.NotNull(finalBody.Data.Tokens);

        Assert.Equal("Approved", JwtClaim(finalBody.Data.Tokens!.AccessToken, "account_state"));
        Assert.Equal("Visitor", JwtClaim(finalBody.Data.Tokens.AccessToken, "user_type"));
    }

    // ----------------------------------------------------------------------

    private AccountState GetAccountState(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        return db.Users.Single(u => u.Email == email).AccountState;
    }

    private (AccountState State, string? QrId) GetAccountStateAndQr(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = db.Users.Single(u => u.Email == email);
        // D-106: QR lives on UserProfile.
        var qr = db.UserProfiles
            .Where(p => p.UserId == user.Id)
            .Select(p => p.QrId)
            .SingleOrDefault();
        return (user.AccountState, qr);
    }

    private async Task<Guid> FindUserIdAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        return (await db.Users.SingleAsync(u => u.Email == email)).Id;
    }

    private async Task<Guid> SeedInterestAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var interest = new Interest
        {
            Id = Guid.NewGuid(),
            Name = $"Lifecycle interest {Guid.NewGuid():N}",
            NameArabic = "اهتمام دورة الحياة",
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Interests.Add(interest);
        await db.SaveChangesAsync();
        return interest.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var adminEmail = $"lifecycle-admin-{Guid.NewGuid():N}@simf.test";
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
                UserName = adminEmail, Email = adminEmail, EmailConfirmed = true,
                DisplayName = "Lifecycle Test Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }
        var signIn = await _client.PostAsJsonAsync("/api/v1/auth/sign-in",
            new SignInRequest
            {
                Email = adminEmail, Password = Password, Audience = SignInAudience.Cp,
            });
        var body = (await signIn.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private async Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) { request.Content = JsonContent.Create(body); }
        return await _client.SendAsync(request);
    }

    private static string? JwtClaim(string accessToken, string claimType)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return jwt.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
    }
}
