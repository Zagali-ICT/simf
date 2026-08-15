using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SIMF.Common;
using SIMF.Contracts.Archive;
using SIMF.Contracts.Cms;
using SIMF.Contracts.Media;
using SIMF.Contracts.PublicRelations;
using SIMF.Contracts.Sponsors;

namespace SIMF.ApiClient.Tests;

/// <summary>Tests for the anonymous public-read client used by the Website's
/// /content/site landing proxy (D — landing dynamic content).</summary>
public sealed class SimfPublicClientTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetNews_requests_the_paged_route_and_returns_the_payload()
    {
        HttpRequestMessage? captured = null;
        var page = new PublicNewsPage(
            new[]
            {
                new PublicNewsListItem(
                    Guid.NewGuid(), "Title", "عنوان", "x", "س", "C", "ف", null,
                    DateTime.UnixEpoch),
            },
            Total: 1, Page: 2, PageSize: 5);
        var client = Client(req => { captured = req; return Ok(ApiResult<PublicNewsPage>.Ok(page)); });

        var result = await client.GetNewsAsync(2, 5);

        Assert.Equal("/api/v1/app/news?page=2&pageSize=5", captured!.RequestUri!.PathAndQuery);
        Assert.Single(result!.Items);
    }

    [Fact]
    public async Task GetArchive_requests_the_archive_route()
    {
        HttpRequestMessage? captured = null;
        var archive = new PublicArchive(Array.Empty<PublicArchiveEdition>());
        var client = Client(req => { captured = req; return Ok(ApiResult<PublicArchive>.Ok(archive)); });

        var result = await client.GetArchiveAsync();

        Assert.Equal("/api/v1/app/archive", captured!.RequestUri!.PathAndQuery);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetMedia_appends_album_skip_and_top()
    {
        HttpRequestMessage? captured = null;
        var media = new PublicMediaPage(Array.Empty<PublicMediaItem>(), 0, 0, 10);
        var client = Client(req => { captured = req; return Ok(ApiResult<PublicMediaPage>.Ok(media)); });

        await client.GetMediaAsync("الافتتاح", 5, 10);

        var query = captured!.RequestUri!.PathAndQuery;
        Assert.StartsWith("/api/v1/app/media?skip=5&top=10&album=", query);
    }

    [Fact]
    public async Task GetContentBatch_posts_the_keys_to_the_batch_route()
    {
        HttpRequestMessage? captured = null;
        var batch = new PublicContentBlockBatch(new Dictionary<string, PublicContentBlock>());
        var client = Client(req => { captured = req; return Ok(ApiResult<PublicContentBlockBatch>.Ok(batch)); });

        await client.GetContentBatchAsync(new[] { "hero.tagline" });

        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("/api/v1/app/content/batch", captured.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task A_failed_envelope_yields_null()
    {
        var client = Client(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                ApiResult<PublicSponsors>.Fail(new ApiError
                {
                    Code = ErrorCodes.InternalError,
                    Message = "nope",
                    MessageArabic = "لا",
                }),
                options: Json),
        });

        Assert.Null(await client.GetSponsorsAsync());
    }

    [Fact]
    public async Task A_transport_failure_yields_null()
    {
        var client = new SimfPublicClient(new HttpClient(new ThrowingHandler())
        {
            BaseAddress = new Uri("http://localhost/"),
        });

        Assert.Null(await client.GetMediaPartnersAsync());
    }

    [Fact]
    public async Task FetchMediaImage_returns_bytes_and_content_type_on_success()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var client = Client(_ =>
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        var (status, contentType, body, _) = await client.FetchMediaImageAsync(Guid.NewGuid());

        Assert.Equal(200, status);
        Assert.Equal("image/png", contentType);
        Assert.Equal(4, body.Length);
    }

    [Fact]
    public async Task FetchMediaImage_maps_a_404_to_empty()
    {
        var client = Client(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var (status, contentType, body, _) = await client.FetchMediaImageAsync(Guid.NewGuid());

        Assert.Equal(404, status);
        Assert.Null(contentType);
        Assert.Empty(body);
    }

    [Fact]
    public async Task FetchAssetImage_returns_bytes_and_targets_the_category_owner_route()
    {
        var bytes = new byte[] { 9, 8, 7 };
        HttpRequestMessage? seen = null;
        var client = Client(req =>
        {
            seen = req;
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/webp");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        var owner = Guid.NewGuid();
        var (status, contentType, body, _) = await client.FetchAssetImageAsync("SpeakerPhoto", owner);

        Assert.Equal(200, status);
        Assert.Equal("image/webp", contentType);
        Assert.Equal(3, body.Length);
        Assert.Contains($"assets/SpeakerPhoto/{owner}/image", seen!.RequestUri!.ToString());
    }

    [Fact]
    public async Task FetchAssetImage_maps_a_404_to_empty()
    {
        var client = Client(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var (status, contentType, body, _) = await client.FetchAssetImageAsync("SpeakerPhoto", Guid.NewGuid());

        Assert.Equal(404, status);
        Assert.Null(contentType);
        Assert.Empty(body);
    }

    // The security fix. An external-link asset 302s to a URL a Control Panel
    // editor supplied. Following it server-side pulled that host's bytes AND its
    // Content-Type back onto the Website's origin, so an editor holding a single
    // per-category asset permission could serve `text/html` + script from
    // web.simrsnf.com. The redirect must be REPORTED, never followed, and no
    // bytes may come back with it.
    [Fact]
    public async Task FetchAssetImage_reports_a_redirect_and_never_follows_it()
    {
        var requests = new List<Uri>();
        var client = Client(req =>
        {
            requests.Add(req.RequestUri!);
            var redirect = new HttpResponseMessage(HttpStatusCode.Found);
            redirect.Headers.Location = new Uri("https://attacker.example/payload");
            return redirect;
        });

        var image = await client.FetchAssetImageAsync("SpeakerPhoto", Guid.NewGuid());

        Assert.Equal(302, image.StatusCode);
        Assert.Equal("https://attacker.example/payload", image.RedirectLocation);
        Assert.Null(image.ContentType);
        Assert.Empty(image.Bytes);

        // One request only: the external host was never contacted, which is also
        // what closes the SSRF (a public DNS name resolving to an internal
        // address would otherwise have been fetched from the Website tier).
        Assert.Single(requests);
        Assert.DoesNotContain("attacker.example", requests[0].ToString());
    }

    // A 3xx with no Location cannot be honoured, and passing a bare 302
    // downstream would strand the browser.
    [Fact]
    public async Task FetchAssetImage_maps_a_redirect_without_a_location_to_502()
    {
        var client = Client(_ => new HttpResponseMessage(HttpStatusCode.Found));

        var image = await client.FetchAssetImageAsync("SpeakerPhoto", Guid.NewGuid());

        Assert.Equal(502, image.StatusCode);
        Assert.Null(image.RedirectLocation);
        Assert.Empty(image.Bytes);
    }

    private static HttpResponseMessage Ok<T>(ApiResult<T> envelope) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(envelope, options: Json) };

    private static SimfPublicClient Client(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new HttpClient(new StubHandler(responder)) { BaseAddress = new Uri("http://localhost/") });

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("The connection was refused.");
    }
}
