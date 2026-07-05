// Wave 2 — public session-presentations (Figma 1388:7621): GET /app/presentations
// (list) + GET /app/presentations/{id}/file (download), approved-account.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Files.Abstractions;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Programme;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class PublicPresentationsTests : IClassFixture<SimfApiFactory>
{
    // "%PDF-1.4" — a minimal valid PDF magic header (the bytes are opaque here).
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4 test");

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public PublicPresentationsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task List_returns_the_presentation_and_the_file_downloads()
    {
        var token = await CreateApprovedVisitorAsync();
        var (presentationId, fileName) = await SeedPresentationAsync();

        var listResponse = await GetAuthAsync("/api/v1/app/presentations", token);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = (await listResponse.Content
            .ReadFromJsonAsync<ApiResult<PublicPresentations>>())!.Data!;
        var item = page.Items.Single(p => p.Id == presentationId);
        Assert.Equal(fileName, item.FileName);
        Assert.Equal("Session", item.SessionTitle);
        Assert.Equal("Speaker One", item.SpeakerName);

        var fileResponse = await GetAuthAsync(
            $"/api/v1/app/presentations/{presentationId}/file", token);
        Assert.Equal(HttpStatusCode.OK, fileResponse.StatusCode);
        var bytes = await fileResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(PdfBytes, bytes);
    }

    [Fact]
    public async Task Download_an_unknown_presentation_returns_404()
    {
        var token = await CreateApprovedVisitorAsync();
        var response = await GetAuthAsync(
            $"/api/v1/app/presentations/{Guid.NewGuid()}/file", token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/v1/app/presentations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<(Guid PresentationId, string FileName)> SeedPresentationAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var app = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();

        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall", NameArabic = "قاعة",
            Capacity = 100, IsActive = true, CreatedAt = now,
        };
        app.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Session", TitleArabic = "جلسة",
            HallId = hall.Id,
            StartUtc = now, EndUtc = now.AddHours(1),
            IsActive = true, CreatedAt = now,
        };
        app.Sessions.Add(session);
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Speaker One", NameArabic = "المتحدث الأول",
            IsActive = true, CreatedAt = now,
        };
        app.Speakers.Add(speaker);

        var presentationId = Guid.NewGuid();
        const string fileName = "deck.pdf";
        var actorId = Guid.NewGuid();
        // D-568 (S6) — seed the bytes in the unified StoredFile store (owner = the
        // presentation); StoredFileName holds the bare-Guid pointer.
        var stored = await fileService.UploadAsync(
            new UploadFileCommand(
                FileService.SpeakerPresentation, presentationId, PdfBytes, fileName, "application/pdf",
                actorId, FailClosed: false),
            CancellationToken.None);
        app.SpeakerPresentations.Add(new SpeakerPresentation
        {
            Id = presentationId,
            SpeakerId = speaker.Id,
            SessionId = session.Id,
            FileName = fileName,
            StoredFileName = stored.Id.ToString(),
            ContentType = "application/pdf",
            SizeBytes = PdfBytes.Length,
            UploadedByUserId = actorId,
            IsActive = true,
            CreatedAt = now,
        });
        await app.SaveChangesAsync();
        return (presentationId, fileName);
    }

    private async Task<string> CreateApprovedVisitorAsync()
    {
        var email = $"pres-visitor-{Guid.NewGuid():N}@simf.test";
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
        AuthFlow.DisableTwoFactor(_factory, email);
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        return (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
    }

    private async Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
