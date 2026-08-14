// Tests: SIMF.Api.Tests/OrganizationHeroVideoTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Configuration;

/// <summary>Turns the profile's <c>BackgroundVideoFileId</c> back into the URL the
/// clients expect, which is the one part of the media programme where a
/// download-by-id redirect would not do.
///
/// <para>Both clients decide how to play a hero by <b>inspecting the string</b>:
/// the website picks a YouTube iframe over a <c>&lt;video&gt;</c> tag, and the app
/// refuses to mount the player at all unless the last path segment is
/// <c>.mp4</c> or <c>.m3u8</c>. So an uploaded video resolves to its dedicated
/// <c>.mp4</c> route — the suffix is load-bearing — and a pasted link resolves to
/// itself, untouched.</para>
///
/// <para>The absolute origin comes from <c>PublicApiBaseUrl</c> when configured,
/// and from the incoming request otherwise. That is the same order the upload
/// endpoint used when it composed this URL at write time; keeping it means a
/// deployment that worked before still works, and a direct dev call still plays.
/// It is resolved per request rather than cached, because a host baked into a
/// five-minute shared cache entry would be wrong for everyone behind a different
/// origin.</para></summary>
internal sealed class HeroVideoUrlResolver(
    SimfAppDbContext dbContext,
    IPublicApiOriginProvider origin)
{
    public async Task<string?> ResolveAsync(Guid? fileId, CancellationToken cancellationToken)
    {
        if (fileId is not { } id)
        {
            return null;
        }

        var file = await dbContext.StoredFiles.AsNoTracking()
            .Where(f => f.Id == id && f.IsActive)
            .Select(f => new { f.SourceType, f.ExternalUrl })
            .FirstOrDefaultAsync(cancellationToken);

        if (file is null)
        {
            return null;
        }

        return file.SourceType == FileSourceType.ExternalLink
            ? file.ExternalUrl
            : ComposeServedUrl();
    }

    private string? ComposeServedUrl()
    {
        var baseUrl = origin.GetApiBaseUrl();
        return string.IsNullOrEmpty(baseUrl)
            ? null
            : baseUrl.TrimEnd('/') + OrganizationHeroVideoRoute.StreamRoute;
    }
}
