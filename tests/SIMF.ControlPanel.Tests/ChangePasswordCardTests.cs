// D-206 — behavior tests for the shared ChangePasswordCard. It backs both the
// Profile change-password section (current field shown) and the forced-change
// sign-in popup (current field hidden — the sign-in ticket already proved it).
// The component is endpoint-agnostic: it raises OnSubmit with the entered
// values and the host performs the API call, so these tests pin the field
// visibility and the callback payload.
using Bunit;
using SIMF.ControlPanel.Components;

namespace SIMF.ControlPanel.Tests;

public sealed class ChangePasswordCardTests : CpComponentTestBase
{
    [Fact]
    public void Shows_the_current_password_field_for_the_profile_form()
    {
        var cut = RenderComponent<ChangePasswordCard>(parameters => parameters
            .Add(card => card.ShowCurrentField, true));

        var labels = cut.FindAll(".simf-field__label").Select(e => e.TextContent.Trim()).ToList();
        Assert.Contains("Profile.Password.Current", labels);
        Assert.Contains("Profile.Password.New", labels);
        Assert.Contains("Profile.Password.Confirm", labels);
    }

    [Fact]
    public void Hides_the_current_password_field_for_the_forced_change_popup()
    {
        var cut = RenderComponent<ChangePasswordCard>(parameters => parameters
            .Add(card => card.ShowCurrentField, false));

        var labels = cut.FindAll(".simf-field__label").Select(e => e.TextContent.Trim()).ToList();
        Assert.DoesNotContain("Profile.Password.Current", labels);
        Assert.Contains("Profile.Password.New", labels);
        Assert.Contains("Profile.Password.Confirm", labels);
    }

    [Fact]
    public void Submitting_raises_OnSubmit_with_the_entered_values()
    {
        ChangePasswordCard.Model? submitted = null;
        var cut = RenderComponent<ChangePasswordCard>(parameters => parameters
            .Add(card => card.ShowCurrentField, false)
            .Add(card => card.OnSubmit, (ChangePasswordCard.Model model) => submitted = model));

        // ShowCurrentField=false → the only inputs are [0] New and [1] Confirm.
        // Re-query before each change — a change re-renders and invalidates the
        // previously found elements (bUnit's UnknownEventHandlerId guard).
        cut.FindAll("input.simf-field__input")[0].Change("N3wPassw0rd!");
        cut.FindAll("input.simf-field__input")[1].Change("N3wPassw0rd!");
        cut.Find("form").Submit();

        Assert.NotNull(submitted);
        Assert.Equal("N3wPassw0rd!", submitted!.NewPassword);
        Assert.Equal("N3wPassw0rd!", submitted.ConfirmPassword);
        Assert.Equal(string.Empty, submitted.CurrentPassword);
    }
}
