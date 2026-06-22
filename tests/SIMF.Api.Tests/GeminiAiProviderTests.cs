// Tests: Google Gemini (Generative Language API) provider.
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SIMF.Application.Ai.Abstractions;
using SIMF.Common;
using SIMF.Infrastructure.Ai;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class GeminiAiProviderTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Captured { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Captured = request;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static GeminiAiProvider Build(StubHandler handler, GeminiOptions gemini) =>
        new(new HttpClient(handler),
            Options.Create(new AiOptions { Gemini = gemini }),
            NullLogger<GeminiAiProvider>.Instance);

    private static AiProviderCall Call() => new(
        Model: string.Empty,
        SystemPrompt: "You are a helper.",
        UserPrompt: "Summarise this.",
        Temperature: 0.7,
        MaxOutputTokens: 0);

    [Fact]
    public async Task An_empty_api_key_is_not_configured_503()
    {
        var provider = Build(
            new StubHandler(HttpStatusCode.OK, "{}"),
            new GeminiOptions { ApiKey = "" });

        var ex = await Assert.ThrowsAsync<ApiException>(() => provider.CallAsync(Call()));
        Assert.Equal(ErrorCodes.AiProviderNotConfigured, ex.Code);
        Assert.Equal(503, ex.StatusCode);
    }

    [Fact]
    public async Task A_success_parses_the_text_plus_usage_and_sends_the_gemini_wire_contract()
    {
        const string body =
            "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Hi there\"}]}}]," +
            "\"usageMetadata\":{\"promptTokenCount\":5,\"candidatesTokenCount\":3}}";
        var handler = new StubHandler(HttpStatusCode.OK, body);
        var provider = Build(handler, new GeminiOptions { ApiKey = "gm-test" });

        var result = await provider.CallAsync(Call());

        Assert.Equal("Hi there", result.OutputText);
        Assert.Equal(5, result.TokensInput);
        Assert.Equal(3, result.TokensOutput);
        // Gemini wire contract: x-goog-api-key header (key not in the URL),
        // {model}:generateContent on the Generative Language API path.
        Assert.True(handler.Captured!.Headers.Contains("x-goog-api-key"));
        Assert.Contains("/v1beta/models/", handler.Captured!.RequestUri!.AbsoluteUri);
        Assert.EndsWith(":generateContent", handler.Captured!.RequestUri!.AbsoluteUri);
        // The key never rides the query string.
        Assert.DoesNotContain("gm-test", handler.Captured!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task An_http_failure_maps_to_provider_failed()
    {
        var provider = Build(
            new StubHandler(HttpStatusCode.BadRequest, "{\"error\":\"bad request\"}"),
            new GeminiOptions { ApiKey = "gm-test" });

        var ex = await Assert.ThrowsAsync<ApiException>(() => provider.CallAsync(Call()));
        Assert.Equal(ErrorCodes.AiProviderFailed, ex.Code);
    }
}
