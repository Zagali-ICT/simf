// BUG-024 — a successful exhibitor badge scan must ALSO email the captured lead
// to the exhibitor's own account address (the owner's "send to exhibitor email"
// requirement). Regression cover: exactly one message per NEW capture, none for
// a duplicate scan, and the message carries the lead fields (never the national
// ID or the raw badge QR id) with the scan time on the Saudi wall clock (D-219).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Email;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Contacts;
using SIMF.Contracts.Exhibitors;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.Organisations;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ExhibitorLeadEmailTests : IClassFixture<ExhibitorLeadEmailApiFactory>
{
    // The sign-up flow also mails this address (verification + welcome), so the
    // lead-capture assertions match on the catalogue subject as well.
    private const string LeadSubjectPrefix = "SIMF visitor captured at your booth";

    private readonly ExhibitorLeadEmailApiFactory _factory;
    private readonly HttpClient _client;

    public ExhibitorLeadEmailTests(ExhibitorLeadEmailApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Scan_emails_the_lead_to_the_exhibitor_once_and_not_again_on_a_duplicate()
    {
        var exhibitor = await CreateApprovedUserAsync();
        await SeedExhibitorProfileAsync(exhibitor.UserId, "Acme Booth", "جناح أكمي");

        var visitor = await CreateApprovedUserAsync();
        var qrId = $"LEAD{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        await SeedVisitorWithQrAsync(
            visitor.UserId, qrId, "Sara Al-Otaibi", "سارة العتيبي",
            "Operations Manager", "مدير العمليات", "Red Sea Shipping", "الشحن البحري الأحمر");

        var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = qrId, Note = "Booth A3 follow-up" },
            exhibitor.AccessToken);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);

        // Exactly one lead message, to the exhibitor's OWN account address.
        var message = Assert.Single(LeadEmailsTo(exhibitor.Email));
        Assert.Contains("Sara Al-Otaibi", message.Subject, StringComparison.Ordinal);
        Assert.Contains("Sara Al-Otaibi", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("سارة العتيبي", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Operations Manager", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Red Sea Shipping", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("الشحن البحري الأحمر", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Booth A3 follow-up", message.HtmlBody, StringComparison.Ordinal);
        // Bilingual: the EN block, the rule, then the RTL AR block.
        Assert.Contains("dir=\"rtl\"", message.HtmlBody, StringComparison.Ordinal);
        // D-219 — the scan time is the Saudi wall clock (12-hour AM/PM), never a zoned stamp.
        Assert.Contains(
            _factory.Time.SimfNow().FormatSaudi(), message.HtmlBody, StringComparison.Ordinal);
        // The raw badge QR id is never in the message.
        Assert.DoesNotContain(qrId, message.HtmlBody, StringComparison.OrdinalIgnoreCase);

        // A duplicate scan is idempotent — still one capture row, and NO second email.
        var again = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = qrId, Note = "Booth A3 follow-up (again)" },
            exhibitor.AccessToken);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Single(LeadEmailsTo(exhibitor.Email));

        var list = await GetAuthAsync("/api/v1/app/exhibitor/visitors", exhibitor.AccessToken);
        var rows = (await list.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<ExhibitorVisitorRow>>>())!.Data!;
        Assert.Single(rows);
    }

    [Fact]
    public async Task A_failed_scan_emails_nothing()
    {
        var exhibitor = await CreateApprovedUserAsync();
        await SeedExhibitorProfileAsync(exhibitor.UserId, "Empty Booth", "جناح فارغ");

        var scan = await PostAuthAsync("/api/v1/app/exhibitor/visitors/scan",
            new ScanVisitorBadgeRequest { QrId = $"NOSUCH{Guid.NewGuid():N}"[..14] },
            exhibitor.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, scan.StatusCode);

        Assert.Empty(LeadEmailsTo(exhibitor.Email));
    }

    private List<EmailMessage> LeadEmailsTo(string recipient) =>
        _factory.Emails.Messages
            .Where(m => m.To == recipient
                && m.Subject.StartsWith(LeadSubjectPrefix, StringComparison.Ordinal))
            .ToList();

    // -- seeding ---------------------------------------------------------------

    private async Task SeedExhibitorProfileAsync(Guid userId, string name, string nameArabic)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var type = await appDb.ProfileTypes
            .FirstAsync(profileType => profileType.Name == "Exhibitor" && profileType.IsActive);
        var countryId = await appDb.Countries.AsNoTracking()
            .Where(c => c.IsActive).Select(c => c.Id).FirstAsync();
        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            NameArabic = nameArabic,
            ProfileTypeId = type.Id,
            NationalityId = countryId,
            PlaceOfBirth = string.Empty,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        });

        // MERGE (BUG-024 + DEF-EXH-006): these tests were authored against the older
        // rule, where the Exhibitor profile type alone authorised a scan. The security
        // hardening now also requires the officer to belong to a LIVE exhibitor, so
        // that dropping them from a booth revokes their scanning tools. Without a
        // membership the caller is correctly 403'd — so the fixture, not the rule,
        // was what needed updating.
        var exhibitor = new Exhibitor
        {
            Id = Guid.NewGuid(),
            Name = name,
            NameArabic = nameArabic,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.Exhibitors.Add(exhibitor);
        appDb.ExhibitorMemberships.Add(new ExhibitorMembership
        {
            Id = Guid.NewGuid(),
            ExhibitorId = exhibitor.Id,
            UserId = userId,
            ContactName = name,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
    }

    private async Task SeedVisitorWithQrAsync(
        Guid userId, string qrId, string name, string nameArabic,
        string jobTitle, string jobTitleArabic, string organisation, string organisationArabic)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var countryId = await appDb.Countries.AsNoTracking()
            .Where(c => c.IsActive).Select(c => c.Id).FirstAsync();
        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = organisation,
            NameArabic = organisationArabic,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.Organisations.Add(org);
        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            NameArabic = nameArabic,
            JobTitle = jobTitle,
            JobTitleArabic = jobTitleArabic,
            OrganisationId = org.Id,
            QrId = qrId,
            NationalityId = countryId,
            PlaceOfBirth = string.Empty,
            IsActive = true,
            CreatedAt = SimfClock.Now,
            // A badge exists only for an admitted attendee, and admission is
            // read on the profile rather than the account.
            AdmissionState = AccountState.Approved,
        });
        await appDb.SaveChangesAsync();
    }

    private async Task<(string AccessToken, Guid UserId, string Email)> CreateApprovedUserAsync()
    {
        var email = $"lead-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync("/api/v1/app/auth/sign-up",
            new SignUpRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                ConfirmPassword = AuthFlow.Password,
            });
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
        var token = (await sign.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
        return (token, UserIdFromToken(token), email);
    }

    private static Guid UserIdFromToken(string accessToken)
    {
        var payload = accessToken.Split('.')[1];
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }
        var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
        using var doc = System.Text.Json.JsonDocument.Parse(
            System.Text.Encoding.UTF8.GetString(bytes));
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
