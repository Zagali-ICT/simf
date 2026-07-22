// Behaviour-pin for the 2026-07-22 professional redesign of the walk-in
// registration form (WalkInRegistrationForm). The long record is now grouped
// into numbered SimfFormSection cards wrapped in ONE disabled-able fieldset, and
// the native select / date / file inputs use SimfSelect / SimfDatePicker /
// SimfFileUpload. These tests pin that structure so a later edit cannot silently
// regress the house field-shell pattern.
using Bunit;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class WalkInRegistrationFormTests : CpComponentTestBase
{
    private IRenderedComponent<WalkInRegistrationForm> Render()
    {
        // OnInitializedAsync fans out several simfAccount.get/postJson loads;
        // Loose mode returns null envelopes, which the form tolerates (empty
        // pickers + the offline Saudi-region fallback), so it still renders.
        JSInterop.Mode = JSRuntimeMode.Loose;
        return RenderComponent<WalkInRegistrationForm>();
    }

    [Fact]
    public void Groups_the_record_into_numbered_form_section_cards()
    {
        var cut = Render();
        // The house numbered-section card pattern (SpeakersAddEdit parity).
        Assert.NotEmpty(cut.FindAll("section.simf-form-section"));
        Assert.Contains("Admin.WalkIn.Section.Identity", cut.Markup);
        Assert.Contains("Admin.WalkIn.Section.Contact", cut.Markup);
        Assert.Contains("Admin.WalkIn.Section.IdDocument", cut.Markup);
    }

    [Fact]
    public void Wraps_the_body_in_one_fieldset_that_is_enabled_at_rest()
    {
        var cut = Render();
        var fieldset = cut.Find("fieldset.simf-walkin__fields");
        // Not busy at rest → the submit-time lockout attribute is absent.
        Assert.False(fieldset.HasAttribute("disabled"));
    }

    [Fact]
    public void Uses_the_shared_field_shell_components()
    {
        var cut = Render();
        // SimfDatePicker (DOB), SimfSelect (gender + Saudi birth-region),
        // SimfFileUpload (ID document + photo) — the redesigned controls.
        Assert.NotEmpty(cut.FindAll("input.simf-field__date"));
        Assert.NotEmpty(cut.FindAll("select.simf-field__select"));
        Assert.NotEmpty(cut.FindAll("input.simf-fileupload__input"));
    }
}
