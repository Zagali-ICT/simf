// Session Detail (Figma 5991-85840) — the ANONYMOUS public download route
// GET /api/v1/app/sessions/{sessionId}/downloads/{presentationId}. Unlike the
// signed-in /app/presentations/{id}/file route, this one is AllowAnonymous and
// its authorisation IS the session scope: the presentation must belong to the
// given session. These tests pin that boundary (the security crux of the
// public-downloads decision, 2026-07-15) — anonymous success + the swap-ids 404.
using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Files.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class PublicSessionDownloadTests : IClassFixture<SimfApiFactory>
{
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4 session download");

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public PublicSessionDownloadTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Anonymous_download_streams_the_file_for_its_own_session()
    {
        var (sessionId, presentationId, fileName) = await SeedPresentationAsync();

        // No Authorization header — the route is anonymous by design.
        var response = await _client.GetAsync(
            $"/api/v1/app/sessions/{sessionId}/downloads/{presentationId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PdfBytes, await response.Content.ReadAsByteArrayAsync());
        // Served as an attachment carrying the original file name.
        var disposition = response.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition);
        Assert.Equal("attachment", disposition!.DispositionType);
        Assert.Contains(Uri.EscapeDataString(fileName), disposition.ToString());
    }

    [Fact]
    public async Task A_presentation_from_another_session_is_not_reachable()
    {
        // The security crux: session scope IS the authorisation. Requesting a real
        // presentation under a DIFFERENT (also real) session's id must 404 — you
        // cannot read another session's file by swapping the session id.
        var (_, presentationId, _) = await SeedPresentationAsync();
        var (otherSessionId, _, _) = await SeedPresentationAsync();

        var response = await _client.GetAsync(
            $"/api/v1/app/sessions/{otherSessionId}/downloads/{presentationId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_presentation_returns_404()
    {
        var (sessionId, _, _) = await SeedPresentationAsync();

        var response = await _client.GetAsync(
            $"/api/v1/app/sessions/{sessionId}/downloads/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_soft_deleted_presentation_is_not_reachable()
    {
        var (sessionId, presentationId, _) = await SeedPresentationAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var app = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var row = app.SpeakerPresentations.Single(p => p.Id == presentationId);
            row.IsActive = false;
            await app.SaveChangesAsync();
        }

        var response = await _client.GetAsync(
            $"/api/v1/app/sessions/{sessionId}/downloads/{presentationId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Seeds an active hall + session + speaker + one presentation deck in the
    // unified StoredFile store; returns the session + presentation ids + file name.
    private async Task<(Guid SessionId, Guid PresentationId, string FileName)> SeedPresentationAsync()
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
            Start = now, End = now.AddHours(1),
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
        return (session.Id, presentationId, fileName);
    }
}
