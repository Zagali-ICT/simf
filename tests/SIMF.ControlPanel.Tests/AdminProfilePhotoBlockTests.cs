// D-727 (owner item 5) — render tests for the shared AdminProfilePhotoBlock used
// to surface the staff / visitor profile photo (avatar) across the Others /
// Visitors view + pending-review pages.
using Bunit;
using SIMF.ControlPanel.Components;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class AdminProfilePhotoBlockTests : CpComponentTestBase
{
    [Fact]
    public void Renders_the_photo_image_with_its_heading_and_src()
    {
        const string src = "/account/api/admin/others/abc/avatar?v=1";

        var cut = RenderComponent<AdminProfilePhotoBlock>(parameters => parameters
            .Add(p => p.Heading, "Profile photo")
            .Add(p => p.Src, src));

        Assert.Contains("Profile photo", cut.Find("h4").TextContent);
        var img = cut.Find("img");
        Assert.Equal(src, img.GetAttribute("src"));
        // Alt mirrors the heading so the photo is labelled for assistive tech.
        Assert.Equal("Profile photo", img.GetAttribute("alt"));
    }
}
