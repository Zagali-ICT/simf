using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Contracts.Email;

namespace SIMF.ControlPanel.Components.Pages.Admin;

// D-735 — the email-template list. Mirrors the AiPrompts house pattern
// (SimfDataGrid + CrudShell + JS-proxy transport), minus Add / Delete: the
// template set is a fixed six, so the only row action is Edit.
public partial class EmailTemplatesList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private const string PageKey = "email-templates";

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminEmailTemplateSummary> _page = new();
    private bool _loading;
    private Toast? _toast;

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private bool _editing;
    private AdminEmailTemplateDetail? _target;

    private bool FormOpen => _editing;
    private bool GridHidden => FormOpen && _presentation == CrudPresentation.Page;

    protected override async Task OnInitializedAsync()
    {
        _presentation = await Prefs.GetPresentationAsync(PageKey);
        await LoadAsync();
    }

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        await LoadAsync();
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    private static string FormatUpdatedAt(DateTime? updatedAt) =>
        updatedAt is { } value
            ? value.FormatSaudi("dd-MM-yyyy hh:mm tt")
            : "—";

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminEmailTemplateSummary>>>(
                "simfAccount.postJson", "/account/api/admin/email/templates/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.EmailTemplates.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    // Load the full detail (subject / bodies / defaults / tokens) for the
    // selected type and open the editor. The route segment is the enum name.
    private async Task OnEditAsync(AdminEmailTemplateSummary row)
    {
        _toast = null;
        var env = await JS.InvokeAsync<ApiResult<AdminEmailTemplateDetail>>(
            "simfAccount.getJson", $"/account/api/admin/email/templates/{row.Type}");
        if (env is { Success: true, Data: not null })
        {
            _target = env.Data;
            _editing = true;
        }
        else
        {
            _toast = new Toast("error",
                env?.Error?.MessageForCurrentCulture()
                ?? L["Admin.EmailTemplates.LoadFailed"]);
        }
    }

    private void CloseForm()
    {
        _editing = false;
        _target = null;
    }

    private async Task OnSavedAsync(AdminEmailTemplateDetail saved)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.EmailTemplates.Saved"]);
        await LoadAsync();
    }

    // Reset happens in-editor (the shell stays open, showing the defaults);
    // refresh the grid so the "Customised" column reflects the change.
    private async Task OnResetAsync(AdminEmailTemplateDetail reset)
    {
        _toast = new Toast("success", L["Admin.EmailTemplates.Reset.Done"]);
        await LoadAsync();
    }
}
