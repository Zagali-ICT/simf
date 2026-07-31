// Tests: SIMF.Api.Tests/SpeakerPresentationsExcelTests.cs
// Integration tests for the D-356 speaker-presentations grid export. The grid is
// master-detail (one speaker's files), so the export carries the speaker id in
// Query.Filters["speakerId"]. Export-only: covers the export round-trip (ZIP magic)
// + the Export permission gate.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SpeakerPresentationsExcelTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SpeakerPresentationsExcelTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_returns_an_xlsx_workbook()
    {
        var (speakerId, sessionId) = await SeedSpeakerAndSessionAsync();
        var adminToken = await CreateAdministratorAndSignInAsync();

        // Seed one presentation row so the workbook has a data row.
        var uploaded = await UploadAsync(
            speakerId, sessionId, "deck.pdf", "application/pdf",
            new byte[] { 0x25, 0x50, 0x44, 0x46 }, adminToken); // "%PDF"
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);

        var response = await PostAuthAsync(
            "/api/v1/admin/speaker-presentations/export",
            new AdminGridExportRequest
            {
                Query = new GridQuery
                {
                    Top = 100,
                    Filters = new Dictionary<string, string>
                    {
                        ["speakerId"] = speakerId.ToString(),
                    },
                },
            },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        // .xlsx is a ZIP — first four bytes are the local-file header.
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
        Assert.Equal(0x03, bytes[2]);
        Assert.Equal(0x04, bytes[3]);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_from_export()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/speaker-presentations/export",
            new AdminGridExportRequest
            {
                Query = new GridQuery
                {
                    Top = 10,
                    Filters = new Dictionary<string, string>
                    {
                        ["speakerId"] = Guid.NewGuid().ToString(),
                    },
                },
            },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<(Guid SpeakerId, Guid SessionId)> SeedSpeakerAndSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall", NameArabic = "قاعة",
            Capacity = 50, IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SP-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Dr. Speaker", NameArabic = "د. متحدث",
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Speakers.Add(speaker);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Keynote", TitleArabic = "كلمة",
            HallId = hall.Id,
            Start = SimfClock.Now.AddHours(1),
            End = SimfClock.Now.AddHours(2),
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return (speaker.Id, session.Id);
    }

    private Task<HttpResponseMessage> UploadAsync(
        Guid speakerId, Guid sessionId, string fileName, string contentType,
        byte[] bytes, string token)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/admin/speakers/{speakerId}/presentations?sessionId={sessionId}")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"sp-xlsx-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "SP Excel Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
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
