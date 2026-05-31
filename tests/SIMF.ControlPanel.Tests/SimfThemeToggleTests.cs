// Regression test for the shared SimfThemeToggle. simfTheme.toggle returns the
// new theme NAME (a string, like setNamed/current) — not a bool. The component
// previously read it as InvokeAsync<bool>, which threw a JSException (and tore
// down the Blazor circuit) trying to deserialise "light"/"dark" into a bool.
// This drives a string return and asserts the toggle updates its pressed state.
using Bunit;
using SIMF.Components.Controls;

namespace SIMF.ControlPanel.Tests;

public sealed class SimfThemeToggleTests : CpComponentTestBase
{
    [Fact]
    public void Toggle_reads_the_string_theme_name_and_flips_to_dark()
    {
        // Loose mode → simfTheme.isDark (first-render bool) defaults to false.
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<string>("simfTheme.toggle", _ => true).SetResult("dark");

        var cut = RenderComponent<SimfThemeToggle>();
        cut.Find("button").Click();

        // No JSException (the bug), and the ARIA toggle state reflects dark.
        cut.WaitForAssertion(() =>
            Assert.Equal("true", cut.Find("button").GetAttribute("aria-pressed")));
    }
}
