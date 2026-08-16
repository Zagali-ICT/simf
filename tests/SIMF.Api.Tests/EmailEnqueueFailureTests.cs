using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// H10 — D-065: when the IEmailQueue.Enqueue side-effect throws AFTER
/// the matching code row is already persisted, the service must write
/// a distinct <c>Email.EnqueueFailed</c> audit row so SOC sees the gap
/// even though the surrounding request audits Success. The throwing
/// queue is registered in a sibling factory so the rest of the suite
/// keeps the FakeEmailSender + null queue setup.
/// </summary>
public sealed class ThrowingEmailQueueApiFactory : SimfApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailQueue>();
            services.AddSingleton<IEmailQueue, ThrowingEmailQueue>();
        });
    }

    private sealed class ThrowingEmailQueue : IEmailQueue
    {
        public void Enqueue(EmailMessage message) =>
            throw new InvalidOperationException("Test: enqueue is unavailable");

        public int PendingCount => 0;
    }
}

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class EmailEnqueueFailureTests : IClassFixture<ThrowingEmailQueueApiFactory>
{
    private readonly ThrowingEmailQueueApiFactory _factory;
    private readonly HttpClient _client;

    public EmailEnqueueFailureTests(ThrowingEmailQueueApiFactory factory)
    {
        _factory = factory;
        factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ForgotPassword_writes_an_Email_EnqueueFailed_audit_row_when_the_queue_throws()
    {
        // Seed a user the forgot-password endpoint will find.
        var email = $"enq-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            await users.CreateAsync(
                new SimfUser
                {
                    UserName = email, Email = email, EmailConfirmed = true,
                    DisplayName = "Enqueue Test", AccountState = AccountState.Approved,
                },
                "Zx9#mKp2!");
        }

        // The endpoint must NOT throw to the caller; the always-200 contract
        // (no enumeration oracle) is preserved.
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Both audit rows are present: the distinct enqueue-failure row,
        // and the existing ForgotPassword.Requested (Success) row.
        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Contains(db.OperationLog,
            entry => entry.EventType == AuditEvents.EmailEnqueueFailed
                  && entry.SubjectEmail == email);
        Assert.Contains(db.OperationLog,
            entry => entry.EventType == AuditEvents.ForgotPasswordRequested
                  && entry.SubjectEmail == email
                  && entry.Outcome == AuditOutcome.Success);
    }

    [Fact]
    public async Task SignUp_writes_an_Email_EnqueueFailed_audit_row_when_the_queue_throws()
    {
        var email = $"enq-su-{Guid.NewGuid():N}@simf.test";

        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
            new SignUpRequest
            {
                Email = email, Password = "Zx9#mKp2!", ConfirmPassword = "Zx9#mKp2!",
            });
        // The endpoint still reports success — the user IS created.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Contains(db.OperationLog,
            entry => entry.EventType == AuditEvents.EmailEnqueueFailed
                  && entry.SubjectEmail == email);
    }
}
