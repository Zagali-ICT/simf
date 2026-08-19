// Tests: OpenAI chat-completions provider — the status it answers with when the
// vendor call fails. The sibling Anthropic / Gemini providers carry the same
// branch and the same cover.
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SIMF.Application.Ai.Abstractions;
using SIMF.Common;
using SIMF.Infrastructure.Ai;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Ai)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class OpenAiProviderTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }

    private static OpenAiProvider Build(StubHandler handler, OpenAiOptions openAi) =>
        new(new HttpClient(handler),
            Options.Create(new AiOptions { OpenAi = openAi }),
            NullLogger<OpenAiProvider>.Instance);

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
            new OpenAiOptions { ApiKey = "" });

        var ex = await Assert.ThrowsAsync<ApiException>(() => provider.CallAsync(Call()));
        Assert.Equal(ErrorCodes.AiProviderNotConfigured, ex.Code);
        Assert.Equal(503, ex.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Every_upstream_status_surfaces_as_bad_gateway(HttpStatusCode upstream)
    {
        // The provider used to relay the vendor's status, so a revoked key
        // answered a correctly-authenticated caller with 401 and a stale model
        // name answered a POST with 404 — every status-based consumer then
        // mis-triaged a vendor outage as an event on SIMF's own surface.
        var provider = Build(
            new StubHandler(upstream, "{\"error\":\"upstream\"}"),
            new OpenAiOptions { ApiKey = "sk-test-key" });

        var ex = await Assert.ThrowsAsync<ApiException>(() => provider.CallAsync(Call()));
        Assert.Equal(ErrorCodes.AiProviderFailed, ex.Code);
        Assert.Equal(502, ex.StatusCode);
    }
}
