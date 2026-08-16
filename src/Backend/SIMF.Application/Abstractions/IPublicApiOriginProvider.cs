namespace SIMF.Application.Abstractions;

/// <summary>The absolute, public origin of this API — including its route prefix —
/// as the internet reaches it, for the one case that needs an absolute URL baked
/// into a response body rather than a relative route the client resolves itself.
///
/// <para>That case is the hero background video. Both clients decide whether they
/// can play it by <b>reading the string</b>: the app requires a last path segment
/// of <c>.mp4</c> or <c>.m3u8</c>, and the website picks a YouTube embed over a
/// <c>video</c> tag the same way. A relative route fails both tests, so the URL
/// has to be absolute by the time it leaves the server.</para>
///
/// <para>It is an abstraction rather than a direct <c>IHttpContextAccessor</c> use
/// because the composition happens in Infrastructure, which deliberately holds no
/// reference to ASP.NET Core. The API implements it from the configured public
/// base URL, falling back to the incoming request — the same order, and the same
/// precedence, the upload endpoint used when it composed this URL at write
/// time.</para></summary>
public interface IPublicApiOriginProvider
{
    /// <summary>The origin plus route prefix (e.g.
    /// <c>https://api.example.com/api/v1</c>), or null when neither configuration
    /// nor an in-flight request can supply one.</summary>
    string? GetApiBaseUrl();
}
