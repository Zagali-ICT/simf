// Tests: SIMF.Infrastructure.Ai.AiService.ResolveModelForCall (D-484 follow-up)
using SIMF.Common.Enums;
using SIMF.Infrastructure.Ai;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>D-484 follow-up — the seeder pins every prompt to the sentinel
/// <c>Model="echo"</c>. When routing redirects an Echo-default prompt to a real
/// provider, that literal must NOT be sent to the vendor API (it would 404);
/// instead the model is blanked so the provider substitutes its own DefaultModel.
/// This pins that decision so a naive go-live (flip DefaultProvider + set a key)
/// actually works without editing each prompt's Model.</summary>
[Trait(TestAreas.TraitName, TestAreas.Ai)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class AiServiceModelResolutionTests
{
    [Theory]
    // Real provider + the "echo" sentinel (any case) or blank -> blank, so the
    // provider falls back to its configured DefaultModel.
    [InlineData("echo", AiProvider.Anthropic, "")]
    [InlineData("ECHO", AiProvider.Gemini, "")]
    [InlineData("", AiProvider.OpenAi, "")]
    [InlineData("   ", AiProvider.Anthropic, "")]
    // Real provider + a concrete pinned model -> honoured as-is.
    [InlineData("claude-haiku-4-5-20251001", AiProvider.Anthropic, "claude-haiku-4-5-20251001")]
    [InlineData("gpt-4o-mini", AiProvider.OpenAi, "gpt-4o-mini")]
    // Echo provider keeps whatever it was given (incl. "echo") so its
    // "[echo:echo]" label — which existing tests assert — is unchanged.
    [InlineData("echo", AiProvider.Echo, "echo")]
    [InlineData("custom", AiProvider.Echo, "custom")]
    public void ResolveModelForCall_blanks_echo_only_for_real_providers(
        string promptModel, AiProvider effectiveProvider, string expected)
    {
        Assert.Equal(expected, AiService.ResolveModelForCall(promptModel, effectiveProvider));
    }
}
