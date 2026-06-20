// Tests: D-484 — Anthropic (Claude) Messages-API provider.
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SIMF.Application.Ai.Abstractions;
using SIMF.Common;
using SIMF.Infrastructure.Ai;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AnthropicAiProviderTests
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

    private static AnthropicAiProvider Build(StubHandler handler, AnthropicOptions anthropic) =>
        new(new HttpClient(handler),
            Options.Create(new AiOptions { Anthropic = anthropic }),
            NullLogger<AnthropicAiProvider>.Instance);

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
            new AnthropicOptions { ApiKey = "" });

        var ex = await Assert.ThrowsAsync<ApiException>(() => provider.CallAsync(Call()));
        Assert.Equal(ErrorCodes.AiProviderNotConfigured, ex.Code);
        Assert.Equal(503, ex.StatusCode);
    }

    [Fact]
    public async Task A_success_parses_the_text_plus_usage_and_sends_the_anthropic_wire_contract()
    {
        const string body =
            "{\"content\":[{\"type\":\"text\",\"text\":\"Hi there\"}]," +
            "\"usage\":{\"input_tokens\":5,\"output_tokens\":3}}";
        var handler = new StubHandler(HttpStatusCode.OK, body);
        var provider = Build(handler, new AnthropicOptions { ApiKey = "sk-ant-test" });

        var result = await provider.CallAsync(Call());

        Assert.Equal("Hi there", result.OutputText);
        Assert.Equal(5, result.TokensInput);
        Assert.Equal(3, result.TokensOutput);
        // Anthropic wire contract: x-api-key + anthropic-version headers, /v1/messages.
        Assert.True(handler.Captured!.Headers.Contains("x-api-key"));
        Assert.True(handler.Captured!.Headers.Contains("anthropic-version"));
        Assert.EndsWith("/v1/messages", handler.Captured!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task An_http_failure_maps_to_provider_failed()
    {
        var provider = Build(
            new StubHandler(HttpStatusCode.BadRequest, "{\"error\":\"bad request\"}"),
            new AnthropicOptions { ApiKey = "sk-ant-test" });

        var ex = await Assert.ThrowsAsync<ApiException>(() => provider.CallAsync(Call()));
        Assert.Equal(ErrorCodes.AiProviderFailed, ex.Code);
    }
}
