// D-249 — App Screen 14 My-Area dashboard (Page_014): identity card + counters
// + today's merged schedule, plus the calendar.ics / contact-card.vcf exports.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Account;
using SIMF.Contracts.Authentication;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Organisations;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class MyAreaDashboardTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public MyAreaDashboardTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Dashboard_returns_identity_counters_and_todays_schedule()
    {
        var (token, userId) = await CreateApprovedVisitorAsync();
        var qrId = await SeedFullDatasetAsync(userId);

        var response = await GetAuthAsync("/api/v1/app/account/dashboard", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<MyAreaDashboard>>())!;
        Assert.True(body.Success);
        var data = body.Data!;

        // Identity card (UserProfile + ProfileType).
        Assert.Equal("سعد العتيبي", data.Identity.FullNameAr);
        Assert.Equal("Saad Alotaibi", data.Identity.FullNameEn);
        Assert.Equal(qrId, data.Identity.QrId);
        Assert.StartsWith("VVIP", data.Identity.TierNameEn!);
        Assert.Equal("#FFD700", data.Identity.PageColor);

        // Counters: 1 held booking; 1 meeting (the business meeting — the
        // session-scoped MeetingRequest feature was removed in D-278).
        Assert.Equal(1, data.Counters.BookedSessionsCount);
        Assert.Equal(1, data.Counters.MeetingsCount);

        // Today's schedule: the booked session + the business meeting (seeded "now").
        Assert.Equal(2, data.TodaySchedule.Count);
        Assert.Single(data.TodaySchedule, i => i.Kind == "Session");
        Assert.Equal(1, data.TodaySchedule.Count(i => i.Kind == "Meeting"));
    }

    [Fact]
    public async Task Dashboard_is_empty_for_a_visitor_with_no_bookings_or_meetings()
    {
        var (token, _) = await CreateApprovedVisitorAsync();

        var response = await GetAuthAsync("/api/v1/app/account/dashboard", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<ApiResult<MyAreaDashboard>>())!.Data!;

        Assert.Equal(0, data.Counters.BookedSessionsCount);
        Assert.Equal(0, data.Counters.MeetingsCount);
        Assert.Empty(data.TodaySchedule);
    }

    [Fact]
    public async Task Open_seating_join_appears_in_the_schedule_and_count()
    {
        // D-485 regression — a general-admission (OpenSeating) join must show in
        // the user's booked-sessions count + today's schedule, like a seat booking.
        var (token, userId) = await CreateApprovedVisitorAsync();
        var now = DateTimeOffset.UtcNow;
        using (var scope = _factory.Services.CreateScope())
        {
            var app = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var hall = new Hall
            {
                Id = Guid.NewGuid(),
                Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = "Open Hall", NameArabic = "قاعة مفتوحة",
                Capacity = 100,
                SeatSelectionMode = SeatSelectionMode.OpenSeating,
                IsActive = true, CreatedAt = now,
            };
            app.Halls.Add(hall);
            var session = new Session
            {
                Id = Guid.NewGuid(),
                Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Title = "Plenary", TitleArabic = "الجلسة العامة",
                HallId = hall.Id,
                StartUtc = now, EndUtc = now.AddHours(1),
                IsActive = true, CreatedAt = now,
            };
            app.Sessions.Add(session);
            app.SeatReservations.Add(new SeatReservation
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                RowLabel = null, SeatNumber = null,
                Kind = SeatReservationKind.OpenSeating,
                ReservedForUserId = userId,
                CreatedByUserId = userId,
                Status = BookingStatus.Pending,
                ReleasedAt = null,
                CreatedAt = now,
            });
            await app.SaveChangesAsync();
        }

        var response = await GetAuthAsync("/api/v1/app/account/dashboard", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<ApiResult<MyAreaDashboard>>())!.Data!;

        Assert.Equal(1, data.Counters.BookedSessionsCount);
        Assert.Single(data.TodaySchedule, i => i.Kind == "Session");
    }

    [Fact]
    public async Task Calendar_ics_returns_an_RFC5545_document_with_an_event_per_item()
    {
        var (token, userId) = await CreateApprovedVisitorAsync();
        await SeedFullDatasetAsync(userId);

        var response = await GetAuthAsync("/api/v1/app/account/calendar.ics", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/calendar", response.Content.Headers.ContentType?.MediaType);
        var ics = await response.Content.ReadAsStringAsync();

        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("END:VCALENDAR", ics);
        // One booked session + one business meeting = 2 VEVENTs (the session-scoped
        // MeetingRequest was removed in D-278).
        Assert.Equal(2, CountOccurrences(ics, "BEGIN:VEVENT"));
        Assert.Contains("SUMMARY:Keynote", ics);
    }

    [Fact]
    public async Task Contact_card_vcf_has_the_arabic_name_and_phones_and_omits_the_qr_id()
    {
        var (token, userId) = await CreateApprovedVisitorAsync();
        var qrId = await SeedFullDatasetAsync(userId);

        var response = await GetAuthAsync("/api/v1/app/account/contact-card.vcf", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/vcard", response.Content.Headers.ContentType?.MediaType);
        var vcf = await response.Content.ReadAsStringAsync();

        Assert.Contains("BEGIN:VCARD", vcf);
        // D-470 — the Arabic name leads (requirement #8 "Name ar"); this vCard is
        // encoded in a camera-readable QR.
        Assert.Contains("FN:سعد العتيبي", vcf);
        Assert.Contains("TITLE:Captain", vcf);
        Assert.Contains("ORG:Royal Saudi Naval Forces", vcf);
        // The mobile numbers become TEL lines ("phones").
        Assert.Contains("TEL;TYPE=CELL:+966500112233", vcf);
        Assert.Contains("TEL;TYPE=CELL:+447700900123", vcf);
        // The gate/lead QrId is NOT leaked into a QR anyone's camera can read.
        Assert.DoesNotContain(qrId, vcf);
    }

    [Fact]
    public async Task Dashboard_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/v1/app/account/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_for_a_not_yet_approved_account_is_forbidden()
    {
        // A verified-but-not-approved visitor holds a valid token but fails the
        // RequireApprovedAccount policy.
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await GetAuthAsync("/api/v1/app/account/dashboard", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<(string Token, Guid UserId)> CreateApprovedVisitorAsync()
    {
        var email = $"ma-visitor-{Guid.NewGuid():N}@simf.test";
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
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        // D-373 — registration enables 2FA; this auth plumbing needs the
        // direct-token path (the admin-disabled scenario).
        AuthFlow.DisableTwoFactor(_factory, email);
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var token = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;

        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var userId = identity.Users.Single(u => u.Email == email).Id;
        return (token, userId);
    }

    /// <summary>
    /// Seeds a profile (tier + organisation), a held seat booking, an accepted
    /// speaker meeting, and a confirmed business meeting — all for the visitor,
    /// all scheduled "now" (today's AST window) so they land on the dashboard.
    /// Returns the seeded (system-unique) QR id so the caller can assert on it.
    /// </summary>
    private async Task<string> SeedFullDatasetAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var app = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        // QrId is unique system-wide and ProfileType names may be too — make both
        // unique per test so repeated seeding does not collide.
        var qrId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Main Hall", NameArabic = "القاعة الرئيسية",
            Capacity = 100, IsActive = true, CreatedAt = now,
        };
        app.Halls.Add(hall);

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Keynote", TitleArabic = "الكلمة الرئيسية",
            HallId = hall.Id,
            StartUtc = now, EndUtc = now.AddHours(1),
            IsActive = true, CreatedAt = now,
        };
        app.Sessions.Add(session);

        var profileType = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = "VVIP " + Guid.NewGuid().ToString("N")[..4], NameArabic = "كبار الشخصيات",
            PageColor = "#FFD700",
            IsForVisitor = true,
            MobileAppRole = MobileAppRole.None, IsActive = true, CreatedAt = now,
        };
        app.ProfileTypes.Add(profileType);

        var organisation = new Organisation
        {
            Id = Guid.NewGuid(),
            NameArabic = "القوات البحرية الملكية السعودية",
            Name = "Royal Saudi Naval Forces",
            IsActive = true, CreatedAt = now,
        };
        app.Organisations.Add(organisation);

        app.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProfileTypeId = profileType.Id,
            OrganisationId = organisation.Id,
            NameArabic = "سعد العتيبي",
            Name = "Saad Alotaibi",
            JobTitle = "Captain",
            QrId = qrId,
            NationalityId = 682,
            PlaceOfBirth = "Riyadh",
            Gender = Gender.Male,
            SaudiMobile = "+966500112233",
            InternationalMobile = "+447700900123",
            CreatedAt = now,
        });

        // Held booking (counter 1 + a Session schedule item).
        app.SeatReservations.Add(new SeatReservation
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            RowLabel = "A", SeatNumber = 1,
            Kind = SeatReservationKind.UserBooking,
            ReservedForUserId = userId,
            CreatedByUserId = userId,
            Status = BookingStatus.Approved,
            ReleasedAt = null,
            CreatedAt = now,
        });

        // Confirmed business meeting at a table (the only Meeting item now —
        // the session-scoped MeetingRequest feature was removed in D-278).
        var table = new MeetingTable
        {
            Id = Guid.NewGuid(),
            HallId = hall.Id,
            Code = "T-01", Capacity = 4, IsActive = true, CreatedAt = now,
        };
        app.MeetingTables.Add(table);

        var meeting = new BusinessMeeting
        {
            Id = Guid.NewGuid(),
            MeetingTableId = table.Id,
            MeetingType = BusinessMeetingType.B2C,
            StartUtc = now, EndUtc = now.AddMinutes(30),
            Status = BusinessMeetingStatus.Confirmed,
            Notes = "Partnership discussion",
            ScheduledByUserId = userId,
            CreatedAt = now,
        };
        meeting.Participants.Add(new BusinessMeetingParticipant
        {
            Id = Guid.NewGuid(),
            BusinessMeetingId = meeting.Id,
            Kind = MeetingPartyKind.Visitor,
            VisitorUserId = userId,
            DisplayNameSnapshot = "Saad Alotaibi",
            CreatedAt = now,
        });
        app.BusinessMeetings.Add(meeting);

        await app.SaveChangesAsync();
        return qrId;
    }

    private async Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
