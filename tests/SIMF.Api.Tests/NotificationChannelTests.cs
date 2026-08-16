// `sms-whatsapp-channels` — the notification dispatcher had NO channel
// abstraction: it hard-coded "write the in-app row, then enqueue an email", so the
// one abstraction the gap report credited did not generalise past email. These
// tests pin the seam: the dispatcher runs registered INotificationChannel
// implementations in Order, honours ShouldHandle, and keeps the D-713 dedup guard
// in front of ALL of them.
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Notifications;
using SIMF.Common.Enums;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class NotificationChannelTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public NotificationChannelTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public void Both_shipped_channels_are_registered_in_a_deterministic_order()
    {
        using var scope = _factory.Services.CreateScope();
        var channels = scope.ServiceProvider
            .GetServices<INotificationChannel>()
            .OrderBy(channel => channel.Order)
            .ToList();

        // In-app first, so no outbound transport's failure can cost the user the
        // in-app record; email second. SMS / WhatsApp are deliberately absent —
        // they are blocked on a procured gateway (owner-action).
        Assert.Equal(new[] { "in-app", "email" }, channels.Select(c => c.Name));
        Assert.True(channels[0].Order < channels[1].Order);
    }

    [Fact]
    public void In_app_channel_handles_every_request_email_channel_only_opt_ins()
    {
        using var scope = _factory.Services.CreateScope();
        var channels = scope.ServiceProvider
            .GetServices<INotificationChannel>()
            .ToDictionary(channel => channel.Name);

        var withoutEmail = Request(sendEmail: false);
        var withEmail = Request(sendEmail: true);

        Assert.True(channels["in-app"].ShouldHandle(withoutEmail));
        Assert.True(channels["in-app"].ShouldHandle(withEmail));

        Assert.False(channels["email"].ShouldHandle(withoutEmail));
        Assert.True(channels["email"].ShouldHandle(withEmail));
    }

    [Fact]
    public async Task Dispatcher_writes_the_in_app_row_through_the_channel()
    {
        // Behaviour parity: routing through the seam must still produce exactly the
        // row the inline code produced.
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
        var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

        // A real account, not a fabricated id: Notifications.UserId carries a real
        // FK to AspNetUsers, so a Guid.NewGuid() recipient fails the INSERT.
        var userId = await CreateRecipientAsync();
        var relatedId = Guid.NewGuid();
        await dispatcher.DispatchAsync(new NotificationRequest
        {
            UserId = userId,
            Kind = NotificationKind.SessionReminder,
            Title = "Session starting soon",
            TitleArabic = "تبدأ الجلسة قريباً",
            Body = "See you there.",
            BodyArabic = "نراك هناك.",
            RelatedEntityType = "Session",
            RelatedEntityId = relatedId,
            SendEmail = false,
        });

        Assert.True(await repository.ExistsForUserAsync(
            userId, NotificationKind.SessionReminder, relatedId));
    }

    [Fact]
    public async Task Dedup_guard_still_short_circuits_every_channel()
    {
        // D-713 — the guard is dispatch-wide policy, not a channel concern, so a
        // second identical dispatch must produce no second row.
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
        var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

        var userId = await CreateRecipientAsync();
        var relatedId = Guid.NewGuid();
        NotificationRequest Prompt() => new()
        {
            UserId = userId,
            Kind = NotificationKind.SessionNotAttended,
            Title = "The session has started",
            TitleArabic = "بدأت الجلسة",
            Body = "You have not arrived yet.",
            BodyArabic = "لم تصل بعد.",
            RelatedEntityType = "Session",
            RelatedEntityId = relatedId,
            DeduplicateByRelatedEntity = true,
            SendEmail = false,
        };

        await dispatcher.DispatchAsync(Prompt());
        await dispatcher.DispatchAsync(Prompt());

        var count = await CountAsync(userId, NotificationKind.SessionNotAttended, relatedId);
        Assert.Equal(1, count);
    }

    private static NotificationRequest Request(bool sendEmail) => new()
    {
        UserId = Guid.NewGuid(),
        Kind = NotificationKind.SessionReminder,
        Title = "t",
        TitleArabic = "ت",
        Body = "b",
        BodyArabic = "ب",
        SendEmail = sendEmail,
    };

    private async Task<int> CountAsync(Guid userId, NotificationKind kind, Guid relatedId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        return await db.Notifications.CountAsync(n =>
            n.UserId == userId && n.Kind == kind && n.RelatedEntityId == relatedId);
    }

    /// <summary>A real recipient account. Notifications live on the Identity
    /// context with a real <c>FK_Notifications_AspNetUsers_UserId</c>, so a
    /// fabricated <c>Guid.NewGuid()</c> recipient fails the INSERT rather than
    /// exercising the dispatcher. Mirrors the helper in NotificationTests.</summary>
    private async Task<Guid> CreateRecipientAsync()
    {
        var email = $"channel-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Channel Recipient",
            AccountState = AccountState.Approved,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }
}
