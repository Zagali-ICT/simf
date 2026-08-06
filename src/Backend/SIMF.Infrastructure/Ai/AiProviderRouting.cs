// Tests: SIMF.Api.Tests/AiProviderRoutingTests.cs
using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Ai;

/// <summary>Resolves which provider actually serves a prompt. Each prompt
/// carries its own <see cref="AiProvider"/>, but a prompt left on the offline
/// <see cref="AiProvider.Echo"/> default is redirected to the operator's
/// configured <see cref="AiOptions.DefaultProvider"/> when one is set — this is
/// the long-documented "redirect Echo to a real backend without editing every
/// prompt" behaviour (<see cref="AiOptions.DefaultProvider"/>), which was declared
/// but never wired until now. A prompt pinned to a concrete provider is always
/// honoured as-is.</summary>
internal static class AiProviderRouting
{
    public static AiProvider Effective(AiProvider promptProvider, AiProvider defaultProvider) =>
        promptProvider == AiProvider.Echo && defaultProvider != AiProvider.Echo
            ? defaultProvider
            : promptProvider;
}
