// Tests: SIMF.Api.Tests/OrganizationHeroVideoTests.cs
using FastEndpoints;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Application.Files.Abstractions;
using SIMF.Common;

namespace SIMF.Api.Endpoints.Public;

/// <summary>D-768 — anonymously range-stream the singleton Organization Profile's
/// uploaded hero background video (public branding content shown on the app home +
/// the website landing hero). A FIXED singleton route ending <c>.mp4</c> — so the
/// app/website hero accept-gate passes and no file GUID is enumerable — resolves the
/// one active hero-video <c>StoredFile</c> and serves its bytes. 404 when none is
/// uploaded. <c>enableRangeProcessing</c> lets the player seek (HTTP 206) without
/// buffering the whole video in memory.</summary>
public sealed class OrganizationHeroVideoStreamEndpoint(
    IOrganizationHeroVideoService heroVideo, IFileService files)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get(OrganizationHeroVideoRoute.StreamRoute);
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var pointer = await heroVideo.GetActivePointerAsync(ct);
        if (pointer is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // OrganizationHeroVideo is EncryptAtRest:false, so the stored file opens as a
        // seekable plaintext stream — the player can Range-seek (HTTP 206) without the
        // whole video ever being buffered.
        var file = await files.OpenReadStreamAsync(pointer.FileId, ct);
        if (file is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // The video is opened directly by a <video>/player URL, so stop MIME-sniffing
        // (the content-type was validated to video/* on upload); nosniff blocks a
        // polyglot file from being interpreted as HTML in the api origin.
        HttpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // Public branding content on a fixed route: allow a short shared cache (matches
        // the public-file download endpoint). A replace propagates within the window;
        // the admin changes the hero rarely, so this trades a brief staleness for not
        // re-streaming a large file from disk on every anonymous hit.
        HttpContext.Response.Headers.CacheControl = "public, max-age=300";

        // Send.StreamAsync disposes the stream after sending. enableRangeProcessing
        // lets the player seek and resume; a large MP4 is never buffered whole.
        await Send.StreamAsync(
            file.Content,
            fileName: pointer.FileName,
            fileLengthBytes: file.Length,
            contentType: pointer.ContentType,
            enableRangeProcessing: true,
            cancellation: ct);
    }
}
