using Xunit;

namespace SIMF.ControlPanel.Tests;

/// <summary>
/// H17 — D-072: lightweight regression net for the H3, H6, H9 markup
/// contracts (notification bell ARIA, state-banner `&lt;main&gt;`
/// landmarks, skip-link).
///
/// <para>A full bUnit harness would prove runtime behaviour (focus
/// jumps, Escape closes, etc.) but pulls in `IJSRuntime` +
/// `NavigationManager` + `AuthenticationStateProvider` + auth state
/// mocks for the state-banner pages and a JS-interop mock for the
/// bell — significant scaffolding. This test instead asserts the
/// markup attributes are present in the .razor sources. It will not
/// catch a runtime breakage that keeps the attribute but removes the
/// JS hook; it WILL catch the common regression the reviewer
/// flagged: a future commit drops the attribute outright.</para>
/// </summary>
public sealed class AccessibilityMarkupTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    // ----------------------------------------------------------------------
    // H3 — bell ARIA contract.
    // ----------------------------------------------------------------------

    // H22 — D-081 changed the bell from role="dialog" + aria-haspopup="dialog"
    // to the disclosure pattern + aria-haspopup="true" and dropped the
    // role="dialog" entirely (Tab now flows naturally; focus returns to
    // the trigger on close via _triggerEl.FocusAsync). The contract
    // tokens were updated accordingly.
    [Theory]
    [InlineData("aria-haspopup=\"true\"")]
    [InlineData("aria-expanded=")]
    [InlineData("aria-controls=")]
    [InlineData("OnTriggerKeyDown")]
    [InlineData("OnDropdownKeyDown")]
    [InlineData("simf-bell__backdrop")]
    [InlineData("_triggerEl.FocusAsync")]
    public void SimfNotificationBell_renders_required_a11y_attribute(string token)
    {
        var source = ReadSource("src/Shared/SIMF.Components/Controls/SimfNotificationBell.razor");
        Assert.Contains(token, source);
    }

    // ----------------------------------------------------------------------
    // H3 — state-banner pages each carry a labelled landmark / region
    // pointing at their own <h1>. H22 — D-081 demoted the Website pages
    // from <main> to <section aria-labelledby> because the new Website
    // MainLayout wraps @Body in <main> (nested <main> would be invalid
    // HTML). The CP pages keep <main> because the CpShellLayout doesn't
    // wrap pages in <main> (SimfAppShell does provide one, but at the
    // shell level — the page-level <main> is allowed because the
    // CpShellLayout sits between, breaking the nesting).
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("src/ControlPanel/SIMF.ControlPanel/Components/Pages/Auth/PendingApproval.razor",
                "auth-pending-title")]
    [InlineData("src/ControlPanel/SIMF.ControlPanel/Components/Pages/Auth/Rejected.razor",
                "auth-rejected-title")]
    public void Cp_state_banner_page_has_main_landmark_with_matching_aria_labelledby(
        string relativePath, string anchorId)
    {
        var source = ReadSource(relativePath);
        Assert.Contains($"<main", source);
        Assert.Contains($"aria-labelledby=\"{anchorId}\"", source);
        Assert.Contains($"id=\"{anchorId}\"", source);
    }

    [Theory]
    [InlineData("src/Website/SIMF.Web/Components/Pages/Account/PendingApproval.razor",
                "account-pending-title")]
    [InlineData("src/Website/SIMF.Web/Components/Pages/Account/Rejected.razor",
                "account-rejected-title")]
    public void Website_state_banner_page_uses_section_inside_layout_main(
        string relativePath, string anchorId)
    {
        // H22 — D-081: page uses <section aria-labelledby> because the
        // layout's <main> is the outer landmark. The section keeps the
        // labelled-region semantic.
        var source = ReadSource(relativePath);
        Assert.Contains("<section", source);
        Assert.Contains($"aria-labelledby=\"{anchorId}\"", source);
        Assert.Contains($"id=\"{anchorId}\"", source);
    }

    [Fact]
    public void Website_main_layout_wraps_body_in_main_landmark()
    {
        // H22 — D-081: the Website's bare MainLayout (was just @Body)
        // now wraps content in <main id="main-content" tabindex="-1">
        // so every Website signed-in page gets a landmark.
        var source = ReadSource("src/Website/SIMF.Web/Components/Layout/MainLayout.razor");
        Assert.Contains("<main", source);
        Assert.Contains("id=\"main-content\"", source);
    }

    // ----------------------------------------------------------------------
    // H9 — skip-link target wired on the CP shell.
    // ----------------------------------------------------------------------

    [Fact]
    public void SimfAppShell_renders_a_skip_link_to_main()
    {
        var source = ReadSource("src/Shared/SIMF.Components/Layout/SimfAppShell.razor");
        Assert.Contains("simf-skip-link", source);
        Assert.Contains("href=\"#simf-main\"", source);
        Assert.Contains("id=\"simf-main\"", source);
    }

    // ----------------------------------------------------------------------
    // H6 — Website App.razor renders culture-aware lang/dir.
    // ----------------------------------------------------------------------

    [Fact]
    public void Website_App_razor_renders_culture_aware_html_lang_dir()
    {
        var source = ReadSource("src/Website/SIMF.Web/Components/App.razor");
        Assert.Contains("CultureInfo.CurrentUICulture.TwoLetterISOLanguageName", source);
        Assert.Contains("TextInfo.IsRightToLeft", source);
    }

    // ----------------------------------------------------------------------
    // H13 — file-input pickers have programmatic labels.
    // ----------------------------------------------------------------------

    [Fact]
    public void Profile_avatar_picker_has_label_and_describedby()
    {
        var source = ReadSource("src/ControlPanel/SIMF.ControlPanel/Components/Pages/Account/Profile.razor");
        Assert.Contains("<label for=\"avatar-input\"", source);
        Assert.Contains("aria-describedby=\"avatar-input-hint\"", source);
        Assert.Contains("id=\"avatar-input-hint\"", source);
    }

    [Fact]
    public void Admin_users_import_picker_has_aria_label()
    {
        var source = ReadSource("src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/UsersList.razor");
        Assert.Contains("id=\"users-import-input\"", source);
        Assert.Contains("aria-label=", source);
    }

    // ----------------------------------------------------------------------

    private static string ReadSource(string relativePath)
    {
        var absolute = Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(absolute), $"Expected source file not found: {absolute}");
        return File.ReadAllText(absolute);
    }

    private static string FindRepoRoot()
    {
        // Walk up from the test bin/Debug/net10.0 directory until we find SIMF.slnx.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SIMF.slnx")))
        {
            dir = dir.Parent;
        }
        if (dir is null)
        {
            throw new InvalidOperationException(
                "Could not locate the SIMF repo root from " + AppContext.BaseDirectory);
        }
        return dir.FullName;
    }
}
