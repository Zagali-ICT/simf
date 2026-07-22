using Microsoft.AspNetCore.Components;
using SIMF.ApiClient;
using SIMF.Web.Content;

namespace SIMF.Web.Components.Pages;

// Code-behind for the public "/partners" page. The sponsors band reads the live
// roster from the anonymous public API (the same SponsorsFeed the landing band
// uses) and renders it with the shared <SponsorsMarquee>, so the two never
// drift. The government-partners band stays static (Landing.PartnerLogos).
public partial class Partners
{
    [Inject] private SimfPublicClient Api { get; set; } = default!;

    public IReadOnlyList<SponsorCard> SponsorItems { get; private set; } = [];

    protected override async Task OnInitializedAsync() =>
        SponsorItems = await SponsorsFeed.LoadAsync(Api);
}
