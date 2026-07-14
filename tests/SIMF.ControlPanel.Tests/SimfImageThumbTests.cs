// Regression tests for the shared SimfImageThumb broken-image fallback (D-357).
// The fallback reveals the placeholder icon on a failed load via a TWO-WAY
// onload/onerror class toggle: onerror ADDS `simf-img-thumb--broken`, onload
// REMOVES it. The onload half is the fix for the review-caught bug — without it a
// no-row-key grid that reuses the same DOM node keeps a stale "broken" class from a
// prior 404 and suppresses the next row's valid image. bUnit cannot fire real image
// load events, so this pins the markup contract: dropping either handler or the
// fallback element would fail the build (the live behaviour is covered by E2E).
using Bunit;
using SIMF.Components.Forms;

namespace SIMF.ControlPanel.Tests;

public sealed class SimfImageThumbTests : CpComponentTestBase
{
    [Fact]
    public void With_a_url_it_renders_the_image_with_the_two_way_broken_toggle_and_fallback()
    {
        var cut = RenderComponent<SimfImageThumb>(p => p
            .Add(x => x.Src, "/account/api/admin/admins/x/avatar")
            .Add(x => x.Alt, "Demo Admin"));

        var img = cut.Find("img.simf-img-thumb__img");
        // onerror ADDS the broken class; onload REMOVES it (clears a stale broken
        // state when a no-key grid reuses the node for a valid next row).
        Assert.Contains("classList.add('simf-img-thumb--broken')", img.GetAttribute("onerror"));
        Assert.Contains("classList.remove('simf-img-thumb--broken')", img.GetAttribute("onload"));
        // The placeholder is rendered alongside the image (revealed by CSS only when broken).
        Assert.Contains("simf-img-thumb__fallback", cut.Markup);
    }

    [Fact]
    public void Without_a_url_it_renders_only_the_placeholder_and_no_image()
    {
        var cut = RenderComponent<SimfImageThumb>();

        Assert.Empty(cut.FindAll("img"));
        Assert.Contains("simf-img-thumb__placeholder", cut.Markup);
    }
}
