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

    [Theory]
    [InlineData("aria-haspopup=\"dialog\"")]
    [InlineData("aria-expanded=")]
    [InlineData("aria-controls=")]
    [InlineData("OnTriggerKeyDown")]
    [InlineData("OnDropdownKeyDown")]
    [InlineData("simf-bell__backdrop")]
    public void SimfNotificationBell_renders_required_a11y_attribute(string token)
    {
        var source = ReadSource("src/Shared/SIMF.Components/Controls/SimfNotificationBell.razor");
        Assert.Contains(token, source);
    }

    // ----------------------------------------------------------------------
    // H3 — state-banner pages each carry a <main aria-labelledby=…>
    // pointing at their own <h1>.
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("src/ControlPanel/SIMF.ControlPanel/Components/Pages/Auth/PendingApproval.razor",
                "auth-pending-title")]
    [InlineData("src/ControlPanel/SIMF.ControlPanel/Components/Pages/Auth/Rejected.razor",
                "auth-rejected-title")]
    [InlineData("src/Website/SIMF.Web/Components/Pages/Account/PendingApproval.razor",
                "account-pending-title")]
    [InlineData("src/Website/SIMF.Web/Components/Pages/Account/Rejected.razor",
                "account-rejected-title")]
    public void State_banner_page_has_main_landmark_with_matching_aria_labelledby(
        string relativePath, string anchorId)
    {
        var source = ReadSource(relativePath);
        Assert.Contains($"<main", source);
        Assert.Contains($"aria-labelledby=\"{anchorId}\"", source);
        Assert.Contains($"id=\"{anchorId}\"", source);
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
