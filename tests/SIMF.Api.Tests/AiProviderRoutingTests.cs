// Tests: D-484 — Echo→DefaultProvider redirect resolution.
using SIMF.Common.Enums;
using SIMF.Infrastructure.Ai;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AiProviderRoutingTests
{
    [Theory]
    // An Echo-default prompt is redirected to the configured DefaultProvider…
    [InlineData(AiProvider.Echo, AiProvider.Anthropic, AiProvider.Anthropic)]
    [InlineData(AiProvider.Echo, AiProvider.OpenAi, AiProvider.OpenAi)]
    // …but not when the default is itself Echo (offline dev/test default).
    [InlineData(AiProvider.Echo, AiProvider.Echo, AiProvider.Echo)]
    // A prompt pinned to a concrete provider is always honoured as-is.
    [InlineData(AiProvider.OpenAi, AiProvider.Anthropic, AiProvider.OpenAi)]
    [InlineData(AiProvider.Anthropic, AiProvider.Echo, AiProvider.Anthropic)]
    public void Effective_resolves_the_serving_provider(
        AiProvider promptProvider, AiProvider defaultProvider, AiProvider expected)
    {
        Assert.Equal(expected, AiProviderRouting.Effective(promptProvider, defaultProvider));
    }
}
