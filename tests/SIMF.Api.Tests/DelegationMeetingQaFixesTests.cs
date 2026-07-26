// Tests: the delegation-meeting QA batch —
//   B8  the TARGET delegation can DECLINE an approved meeting (there was no exit
//       but an admin cancel);
//   A31 the decline/cancel notice actually emails the requester (SendEmail was false
//       although the code comment promised "in-app + email too");
//   A32 a verbal Confirm from Pending now tells the TARGET delegation (the if/else
//       chain had no arm for that transition, so their first notice was the reminder);
//   A33 the outcome email carries real pre-rendered HTML (parties + topic + Saudi
//       local time), not the dispatcher's bare HtmlEncode'd paragraph;
//   A34 a meeting TABLE cannot be double-booked (the bind only checked the table was
//       active and in the hall).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SIMF.Application.Email;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Programme;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Common;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>A <see cref="SimfApiFactory"/> with a capturing e-mail queue, so the
/// A31 / A33 assertions can read the exact message the delegation flow enqueued
/// instead of racing the async background sender.</summary>
public sealed class DelegationEmailApiFactory : SimfApiFactory
{
    public FakeEmailQueue Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailQueue>();
            services.AddSingleton<IEmailQueue>(Emails);
        });
    }
}

public sealed class DelegationMeetingQaFixesTests
    : IClassFixture<DelegationEmailApiFactory>
{
    private readonly DelegationEmailApiFactory _factory;
    private readonly HttpClient _client;

    public DelegationMeetingQaFixesTests(DelegationEmailApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // -- B8: the target delegation can decline --------------------------------

    [Fact]
    public async Task B8_a_target_member_can_decline_an_awaiting_meeting()
    {
        var (_, requesterId) = await CreateDelegateAsync("XA", 9001);
        var (memberToken, _) = await CreateDelegateAsync("XB", 9002);
        var requestId = await SeedAwaitingRequestAsync(
            requesterId, "XA", "XB", withHall: true);

        var decline = await SendAuthAsync(
            HttpMethod.Post,
            $"/api/v1/app/delegation-meeting-requests/{requestId}/decline",
            memberToken, new { });
        Assert.Equal(HttpStatusCode.OK, decline.StatusCode);

        // The app caller never sees the requester's Identity login email.
        var body = (await decline.Content
            .ReadFromJsonAsync<ApiResult<AdminDelegationMeetingRequestDetail>>())!;
        Assert.True(body.Success);
        Assert.Null(body.Data!.RequesterEmail);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.DelegationMeetingRequests.SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.Rejected, req.Status);
        // The held hall slot is released so it frees up for another meeting.
        Assert.Null(req.HallId);
        Assert.Null(req.MeetingTableId);
        Assert.NotNull(req.RespondedAt);

        // The requester is told — in-app and (A31's rule) by e-mail.
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        Assert.True(await identity.Notifications.AnyAsync(
            n => n.UserId == requesterId && n.RelatedEntityId == requestId));
    }

    [Fact]
    public async Task B8_a_member_of_another_delegation_cannot_decline()
    {
        var (_, requesterId) = await CreateDelegateAsync("XC", 9003);
        await CreateDelegateAsync("XD", 9004);
        var (outsiderToken, _) = await CreateDelegateAsync("XE", 9005);
        var requestId = await SeedAwaitingRequestAsync(
            requesterId, "XC", "XD", withHall: false);

        var decline = await SendAuthAsync(
            HttpMethod.Post,
            $"/api/v1/app/delegation-meeting-requests/{requestId}/decline",
            outsiderToken, new { });
        Assert.Equal(HttpStatusCode.Forbidden, decline.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.DelegationMeetingRequests.SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.AwaitingSpeaker, req.Status);
    }

    [Fact]
    public async Task B8_declining_twice_is_a_conflict()
    {
        var (_, requesterId) = await CreateDelegateAsync("XF", 9006);
        var (memberToken, _) = await CreateDelegateAsync("XG", 9007);
        var requestId = await SeedAwaitingRequestAsync(
            requesterId, "XF", "XG", withHall: false);

        var url = $"/api/v1/app/delegation-meeting-requests/{requestId}/decline";
        Assert.Equal(HttpStatusCode.OK,
            (await SendAuthAsync(HttpMethod.Post, url, memberToken, new { })).StatusCode);

        var again = await SendAuthAsync(HttpMethod.Post, url, memberToken, new { });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var body = (await again.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AppRequestAlreadyResponded, body.Error!.Code);
    }

    [Fact]
    public async Task B8_declining_a_pending_meeting_is_a_conflict()
    {
        // Only an Approved (AwaitingSpeaker) meeting reaches the target delegation,
        // so a still-Pending one is not theirs to decline.
        var (_, requesterId) = await CreateDelegateAsync("XH", 9008);
        var (memberToken, _) = await CreateDelegateAsync("XI", 9009);
        var requestId = await SeedRequestAsync(
            requesterId, "XH", "XI", MeetingRequestStatus.Pending);

        var decline = await SendAuthAsync(
            HttpMethod.Post,
            $"/api/v1/app/delegation-meeting-requests/{requestId}/decline",
            memberToken, new { });
        Assert.Equal(HttpStatusCode.Conflict, decline.StatusCode);
    }

    // -- A31 + A33: the decline notice reaches the requester by e-mail --------

    [Fact]
    public async Task A31_A33_an_admin_decline_emails_the_requester_with_real_html()
    {
        var (requesterToken, requesterId, requesterEmail) =
            await CreateDelegateWithEmailAsync("XJ", 9010);
        await EnsureCountryAsync("XK", 9011, invited: true);
        var admin = await CreateAdministratorAndSignInAsync();

        var submit = await SendAuthAsync(
            HttpMethod.Post, "/api/v1/app/delegation-meeting-requests", requesterToken,
            new SubmitDelegationMeetingRequestRequest
            {
                TargetCountryCode = "XK", AttendeeCount = 5, Subject = "Decline email probe",
            });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var requestId = (await submit.Content
            .ReadFromJsonAsync<ApiResult<DelegationMeetingRequestSubmitted>>())!.Data!.Id;

        var respond = await SendAuthAsync(
            HttpMethod.Put,
            $"/api/v1/admin/delegation-meeting-requests/{requestId}/respond", admin,
            new RespondToDelegationMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Rejected,
                ResponseNote = "No slot available this year.",
            });
        Assert.Equal(HttpStatusCode.OK, respond.StatusCode);

        // A31 — the decline used to be in-app only (SendEmail = false).
        var email = Assert.Single(
            _factory.Emails.Messages,
            m => string.Equals(m.To, requesterEmail, StringComparison.OrdinalIgnoreCase)
                && m.Subject.Contains("declined", StringComparison.OrdinalIgnoreCase));
        // A33 — a real notice body, not the dispatcher's single HtmlEncode'd <p>.
        Assert.Contains("Topic:", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Decline email probe", email.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("No slot available this year.", email.HtmlBody, StringComparison.Ordinal);

        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        Assert.True(await identity.Notifications.AnyAsync(
            n => n.UserId == requesterId && n.RelatedEntityId == requestId));
    }

    // -- A32: a verbal Confirm from Pending tells the TARGET delegation -------

    [Fact]
    public async Task A32_a_verbal_confirm_from_pending_notifies_the_target_delegation()
    {
        var (requesterToken, _) = await CreateDelegateAsync("XL", 9012);
        var (_, targetMemberId) = await CreateDelegateAsync("XM", 9013);
        var admin = await CreateAdministratorAndSignInAsync();

        var submit = await SendAuthAsync(
            HttpMethod.Post, "/api/v1/app/delegation-meeting-requests", requesterToken,
            new SubmitDelegationMeetingRequestRequest
            {
                TargetCountryCode = "XM", AttendeeCount = 4, Subject = "Verbal confirm probe",
            });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var requestId = (await submit.Content
            .ReadFromJsonAsync<ApiResult<DelegationMeetingRequestSubmitted>>())!.Data!.Id;

        // Confirm straight from Pending — the admin already has the verbal agreement.
        var confirm = await SendAuthAsync(
            HttpMethod.Put,
            $"/api/v1/admin/delegation-meeting-requests/{requestId}/respond", admin,
            new RespondToDelegationMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted, VerbalConfirmed = true,
            });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        // Before A32 the chain had no arm for this transition, so the target member
        // received nothing at all.
        Assert.True(await identity.Notifications.AnyAsync(
            n => n.UserId == targetMemberId && n.RelatedEntityId == requestId));
    }

    // -- A34: a meeting table cannot be double-booked -------------------------

    [Fact]
    public async Task A34_binding_a_table_already_booked_at_that_time_is_a_conflict()
    {
        var fixtureSlot = new DateTimeOffset(2045, 7, 1, 9, 0, 0, TimeSpan.Zero);
        var (_, requesterId) = await CreateDelegateAsync("XN", 9014);
        await EnsureCountryAsync("XO", 9015, invited: true);
        var (otherToken, _) = await CreateDelegateAsync("XP", 9016);
        await EnsureCountryAsync("XQ", 9017, invited: true);
        var admin = await CreateAdministratorAndSignInAsync();

        var (hallB, tableId) = await SeedTableAlreadyBookedElsewhereAsync(
            requesterId, "XN", "XO", fixtureSlot);

        // A second, unrelated pair of delegations asks for a meeting in hall B.
        var submit = await SendAuthAsync(
            HttpMethod.Post, "/api/v1/app/delegation-meeting-requests", otherToken,
            new SubmitDelegationMeetingRequestRequest
            {
                TargetCountryCode = "XQ", AttendeeCount = 3, Subject = "Table clash probe",
            });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var requestId = (await submit.Content
            .ReadFromJsonAsync<ApiResult<DelegationMeetingRequestSubmitted>>())!.Data!.Id;

        // Same table, overlapping window -> 409. Hall B itself is free, so only the
        // new table guard can catch this.
        var clash = await SendAuthAsync(
            HttpMethod.Put,
            $"/api/v1/admin/delegation-meeting-requests/{requestId}/respond", admin,
            new RespondToDelegationMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                HallId = hallB,
                SlotStart = fixtureSlot,
                SlotEnd = fixtureSlot.AddHours(1),
                MeetingTableId = tableId,
            });
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        var body = (await clash.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DelegationMeetingRequestInvalid, body.Error!.Code);

        // The touching window (10:00-11:00 against a 09:00-10:00 booking) does NOT
        // collide — half-open overlap, the same rule the hall guards use.
        var touching = await SendAuthAsync(
            HttpMethod.Put,
            $"/api/v1/admin/delegation-meeting-requests/{requestId}/respond", admin,
            new RespondToDelegationMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                HallId = hallB,
                SlotStart = fixtureSlot.AddHours(1),
                SlotEnd = fixtureSlot.AddHours(2),
                MeetingTableId = tableId,
            });
        Assert.Equal(HttpStatusCode.OK, touching.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var bound = await db.DelegationMeetingRequests.SingleAsync(r => r.Id == requestId);
        Assert.Equal(tableId, bound.MeetingTableId);
        Assert.Equal(fixtureSlot.AddHours(1), bound.SlotStart);
    }

    // -- helpers --------------------------------------------------------------

    /// <summary>Builds the reachable table-double-book setup: table T lives in hall B
    /// today (an admin moved it there), but an older LIVE meeting in hall A still holds
    /// it for the fixture slot. Hall B has a fresh availability window covering that
    /// slot and no bookings of its own, so the hall-level guards see it as free.</summary>
    private async Task<(Guid HallId, Guid TableId)> SeedTableAlreadyBookedElsewhereAsync(
        Guid requesterUserId, string requestingCode, string targetCode,
        DateTimeOffset slotStart)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var hallA = NewHall("A" + suffix);
        var hallB = NewHall("B" + suffix);
        db.Halls.AddRange(hallA, hallB);

        // The table now belongs to hall B (so the bind's "table is in this hall" check
        // passes), while the older hall-A meeting still holds it.
        var table = new MeetingTable
        {
            Id = Guid.NewGuid(),
            HallId = hallB.Id,
            Code = "TB" + suffix,
            Capacity = 6, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.MeetingTables.Add(table);

        db.HallAvailabilityWindows.Add(new HallAvailabilityWindow
        {
            Id = Guid.NewGuid(),
            HallId = hallB.Id,
            Start = slotStart,
            End = slotStart.AddHours(2),
            SlotMinutes = 60,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        var requesting = await ResolveCountryIdAsync(db, requestingCode);
        var target = await ResolveCountryIdAsync(db, targetCode);
        db.DelegationMeetingRequests.Add(new DelegationMeetingRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = requesterUserId,
            RequestingCountryId = requesting,
            TargetCountryId = target,
            AttendeeCount = 2,
            Subject = "Holds the table",
            Status = MeetingRequestStatus.Accepted,
            HallId = hallA.Id,
            MeetingTableId = table.Id,
            SlotStart = slotStart,
            SlotEnd = slotStart.AddHours(1),
            RespondedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync();
        return (hallB.Id, table.Id);
    }

    private static Hall NewHall(string suffix) => new()
    {
        Id = Guid.NewGuid(),
        Code = "QH" + suffix,
        Name = "Meeting Hall", NameArabic = "قاعة",
        Purpose = HallPurpose.Meeting, Capacity = 12, IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private Task<Guid> SeedAwaitingRequestAsync(
        Guid requesterUserId, string requestingCode, string targetCode, bool withHall) =>
        SeedRequestAsync(
            requesterUserId, requestingCode, targetCode,
            MeetingRequestStatus.AwaitingSpeaker, withHall);

    private async Task<Guid> SeedRequestAsync(
        Guid requesterUserId, string requestingCode, string targetCode,
        MeetingRequestStatus status, bool withHall = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var requesting = await ResolveCountryIdAsync(db, requestingCode);
        var target = await ResolveCountryIdAsync(db, targetCode);

        Guid? hallId = null;
        Guid? tableId = null;
        if (withHall)
        {
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            var hall = NewHall("D" + suffix);
            db.Halls.Add(hall);
            var table = new MeetingTable
            {
                Id = Guid.NewGuid(),
                HallId = hall.Id,
                Code = "TD" + suffix,
                Capacity = 4, IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.MeetingTables.Add(table);
            hallId = hall.Id;
            tableId = table.Id;
        }

        var slotStart = new DateTimeOffset(2046, 8, 1, 9, 0, 0, TimeSpan.Zero);
        var req = new DelegationMeetingRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = requesterUserId,
            RequestingCountryId = requesting,
            TargetCountryId = target,
            AttendeeCount = 3,
            Subject = "QA probe",
            Status = status,
            HallId = hallId,
            MeetingTableId = tableId,
            SlotStart = hallId is null ? null : slotStart,
            SlotEnd = hallId is null ? null : slotStart.AddHours(1),
            RespondedAt = status == MeetingRequestStatus.Pending
                ? null : DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.DelegationMeetingRequests.Add(req);
        await db.SaveChangesAsync();
        return req.Id;
    }

    private static async Task<int> ResolveCountryIdAsync(SimfAppDbContext db, string code) =>
        (await db.Countries.AsNoTracking().SingleAsync(c => c.Code == code)).Id;

    private async Task<int> EnsureCountryAsync(string code, int id, bool invited)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var country = await db.Countries.FirstOrDefaultAsync(c => c.Code == code);
        if (country is null)
        {
            country = new Country
            {
                Id = id, Code = code, Name = code, NameArabic = code,
                IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Countries.Add(country);
        }
        country.IsActive = true;
        country.IsInvited = invited;
        await db.SaveChangesAsync();
        return country.Id;
    }

    private async Task<(string Token, Guid UserId)> CreateDelegateAsync(
        string countryCode, int countryId)
    {
        var (token, userId, _) = await CreateDelegateWithEmailAsync(countryCode, countryId);
        return (token, userId);
    }

    private async Task<(string Token, Guid UserId, string Email)> CreateDelegateWithEmailAsync(
        string countryCode, int countryId)
    {
        var nationalityId = await EnsureCountryAsync(countryCode, countryId, invited: true);
        var email = $"dqa-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "QA Delegate",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;

            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var type = await db.ProfileTypes.FirstOrDefaultAsync(p => p.IsForVisitor && p.IsActive);
            if (type is null)
            {
                type = new UserProfileType
                {
                    Id = Guid.NewGuid(),
                    Name = "Visitor — QaSeed", NameArabic = "زائر",
                    PageColor = "#3B82F6", IsForVisitor = true, IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                db.ProfileTypes.Add(type);
                await db.SaveChangesAsync();
            }
            db.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProfileTypeId = type.Id,
                Name = user.DisplayName, NameArabic = user.DisplayName,
                NationalityId = nationalityId,
                IsDelegate = true,
                AllowsDelegationMeeting = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var token = (await sign.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
        return (token, userId, email);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"dqa-admin-{Guid.NewGuid():N}@simf.test";
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
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "QA Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email, Password = AuthFlow.Password, Audience = SignInAudience.Cp,
            });
        return (await sign.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> SendAuthAsync<TBody>(
        HttpMethod method, string url, string token, TBody body)
        where TBody : class
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
