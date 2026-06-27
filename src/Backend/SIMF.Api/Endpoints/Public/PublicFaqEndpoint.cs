// Tests: SIMF.Api.Tests/FaqTests.cs
using FastEndpoints;
using SIMF.Application.Faq.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Faq;

namespace SIMF.Api.Endpoints.Public;

/// <summary>Public, anonymous read of the FAQ for the app's الأسئلة الشائعة
/// accordion (Figma 1388:7567). Projects only active groups/entries (ordered
/// server-side) over the D-211 FAQ tables — no schema change. Anonymous like the
/// other public content reads (organization profile, sessions, archive).</summary>
public sealed class GetPublicFaqEndpoint(IPublicFaqService service)
    : EndpointWithoutRequest<ApiResult<IReadOnlyList<PublicFaqGroup>>>
{
    public override void Configure()
    {
        Get("/app/faq");
        AllowAnonymous();
        Tags("Public");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(
            ApiResult<IReadOnlyList<PublicFaqGroup>>.Ok(await service.GetAsync(ct)), ct);
}
