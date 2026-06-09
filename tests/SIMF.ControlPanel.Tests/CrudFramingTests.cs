// D-353 — bUnit tests for the centralized CRUD framing primitives:
// CrudShell (frames a form as a dialog or a full page) and SimfConfirm
// (the must-decide confirmation dialog). These live in SIMF.Components but
// are rendered through the CP test harness for the shared service set.
using Bunit;
using SIMF.Components.Forms;

namespace SIMF.ControlPanel.Tests;

public sealed class CrudFramingTests : CpComponentTestBase
{
    public CrudFramingTests()
    {
        // CrudDialogFrame / SimfConfirm wrap SimfModal, whose OnAfterRenderAsync
        // calls simfModal.bind over JS interop — loose mode no-ops it.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Shell_renders_dialog_frame_when_presentation_is_dialog()
    {
        var cut = RenderComponent<CrudShell>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.Presentation, CrudPresentation.Dialog)
            .Add(p => p.Title, "Edit thing")
            .AddChildContent("<div class=\"body-marker\">form</div>"));

        // The dialog presentation renders the modal chrome…
        Assert.NotEmpty(cut.FindAll(".simf-modal"));
        Assert.Empty(cut.FindAll(".simf-crudpage"));
        // …with the hosted form inside it.
        Assert.NotEmpty(cut.FindAll(".body-marker"));
    }

    [Fact]
    public void Shell_renders_page_frame_when_presentation_is_page()
    {
        var cut = RenderComponent<CrudShell>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.Presentation, CrudPresentation.Page)
            .Add(p => p.Title, "Edit thing")
            .AddChildContent("<div class=\"body-marker\">form</div>"));

        // The page presentation renders the in-flow panel, not a modal.
        Assert.NotEmpty(cut.FindAll(".simf-crudpage"));
        Assert.Empty(cut.FindAll(".simf-modal"));
        Assert.NotEmpty(cut.FindAll(".body-marker"));
    }

    [Fact]
    public void Shell_renders_nothing_when_closed()
    {
        var cut = RenderComponent<CrudShell>(parameters => parameters
            .Add(p => p.Open, false)
            .Add(p => p.Presentation, CrudPresentation.Page)
            .AddChildContent("<div class=\"body-marker\">form</div>"));

        Assert.Empty(cut.FindAll(".simf-modal"));
        Assert.Empty(cut.FindAll(".simf-crudpage"));
        Assert.Empty(cut.FindAll(".body-marker"));
    }

    [Fact]
    public void Confirm_invokes_OnConfirm_when_the_confirm_button_is_clicked()
    {
        var confirmed = false;
        var cancelled = false;

        var cut = RenderComponent<SimfConfirm>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.Title, "Delete?")
            .Add(p => p.Message, "This cannot be undone.")
            .Add(p => p.ConfirmLabel, "Delete")
            .Add(p => p.CancelLabel, "Cancel")
            .Add(p => p.Danger, true)
            .Add(p => p.OnConfirm, () => confirmed = true)
            .Add(p => p.OnCancel, () => cancelled = true));

        // The danger styling proves the destructive variant is applied.
        var confirmButton = cut.Find(".simf-modal__footer .simf-button--danger");
        confirmButton.Click();

        Assert.True(confirmed);
        Assert.False(cancelled);
    }

    [Fact]
    public void Confirm_invokes_OnCancel_when_the_cancel_button_is_clicked()
    {
        var confirmed = false;
        var cancelled = false;

        var cut = RenderComponent<SimfConfirm>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.Title, "Delete?")
            .Add(p => p.Message, "This cannot be undone.")
            .Add(p => p.OnConfirm, () => confirmed = true)
            .Add(p => p.OnCancel, () => cancelled = true));

        cut.Find(".simf-modal__footer .simf-button--secondary").Click();

        Assert.True(cancelled);
        Assert.False(confirmed);
    }
}
