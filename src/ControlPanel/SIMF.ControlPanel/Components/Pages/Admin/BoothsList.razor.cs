using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Exhibition;
using SIMF.Contracts.Exhibitors;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class BoothsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private enum FormKind { None, AddEdit, ViewDelete }

    private const string PageKey = "booths";

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminBoothSummary> _page = new();
    private bool _loading;
    private Toast? _toast;

    // Halls + exhibitors cached at mount so the grid's Hall + Exhibitor columns
    // can resolve a name (the summary carries only the ids). The Add/Edit form
    // loads its own copies for the pickers. Bounded → one Top=500 round-trip each.
    private List<AdminHallSummary> _halls = new();
    private List<AdminExhibitorSummary> _exhibitors = new();

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private FormKind _form = FormKind.None;
    private bool _isEdit;
    private bool _isDelete;

    // The booth's own logo thumbnail URL — the booth's BoothLogo asset (owner = the
    // booth, the same logo the app renders), or null so SimfIdentityCell shows an
    // initials tile (never a broken image).
    private static string? LogoImageUrl(AdminBoothSummary row) =>
        row.HasBoothLogo
            ? CpAssetUrls.AdminImage(nameof(AssetCategory.BoothLogo), row.Id)
            : null;
    private AdminBoothDetail? _target;
    private CrudGridExcel? _excel;

    private bool FormOpen => _form != FormKind.None;
    private bool GridHidden => FormOpen && _presentation == CrudPresentation.Page;

    private string FormTitle => _form switch
    {
        FormKind.AddEdit => _isEdit
            ? L["Admin.Booths.Edit.Title"]
            : L["Admin.Booths.Add.Title"],
        FormKind.ViewDelete => _isDelete
            ? L["Admin.Booths.Delete.Title"]
            : L["Admin.Booths.Details.Title"],
        _ => string.Empty,
    };

    protected override async Task OnInitializedAsync()
    {
        _presentation = await Prefs.GetPresentationAsync(PageKey);
        // Flip the grid into its loading state from the very first paint:
        // OnInitializedAsync first yields inside LoadHallsAsync, so without this
        // the in-progress frame would render with _loading == false and the
        // spinner would never show on first load. LoadAsync's finally clears it.
        _loading = true;
        await LoadHallsAsync();
        await LoadExhibitorsAsync();
        await LoadAsync();
    }

    private async Task LoadHallsAsync()
    {
        var env = await JS.InvokeAsync<ApiResult<GridPage<AdminHallSummary>>>(
            "simfAccount.postJson", "/account/api/admin/halls/list",
            new GridQuery { Top = 500 });
        if (env is { Success: true, Data: not null })
        {
            _halls = env.Data.Items.Where(h => h.IsActive)
                .OrderBy(h => h.Name).ToList();
        }
        else
        {
            _toast = new Toast("error",
                env?.Error?.MessageForCurrentCulture()
                ?? L["Admin.Booths.HallsLoadFailed"]);
        }
    }

    private async Task LoadExhibitorsAsync()
    {
        var env = await JS.InvokeAsync<ApiResult<GridPage<AdminExhibitorSummary>>>(
            "simfAccount.postJson", "/account/api/admin/exhibitors/list",
            new GridQuery { Top = 500 });
        if (env is { Success: true, Data: not null })
        {
            _exhibitors = env.Data.Items.Where(c => c.IsActive)
                .OrderBy(c => c.NameEn).ToList();
        }
        else
        {
            _toast = new Toast("error",
                env?.Error?.MessageForCurrentCulture()
                ?? L["Admin.Booths.ExhibitorsLoadFailed"]);
        }
    }

    private string ExhibitorName(Guid? exhibitorId)
    {
        if (exhibitorId is null) return "—";
        var exhibitor = _exhibitors.FirstOrDefault(c => c.Id == exhibitorId.Value);
        return exhibitor is null ? "—" : exhibitor.NameEn;
    }

    private string HallName(Guid? hallId)
    {
        if (hallId is null) return "—";
        var hall = _halls.FirstOrDefault(h => h.Id == hallId.Value);
        return hall is null ? "—" : hall.Name;
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

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminBoothSummary>>>(
                "simfAccount.postJson",
                "/account/api/admin/booths/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Booths.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private Task OnAddAsync()
    {
        _toast = null;
        _form = FormKind.AddEdit;
        _isEdit = false;
        _target = null;
        return Task.CompletedTask;
    }

    private async Task OnEditAsync(AdminBoothSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.AddEdit;
        _isEdit = true;
        _target = detail;
    }

    private async Task OnDetailsAsync(AdminBoothSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = false;
        _target = detail;
    }

    private async Task OnDeleteAsync(AdminBoothSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = true;
        _target = detail;
    }

    // Edit / View / Delete all work against the full detail — the grid summary
    // omits the bilingual sector/description, the officer fields, the map
    // position and the optional Contact link, so editing from a summary-only
    // form would wipe them. Returns null and surfaces a toast on failure.
    private async Task<AdminBoothDetail?> LoadDetailAsync(Guid id)
    {
        _toast = null;
        var env = await JS.InvokeAsync<ApiResult<AdminBoothDetail>>(
            "simfAccount.getJson", $"/account/api/admin/booths/{id}");
        if (env is { Success: true, Data: not null })
        {
            return env.Data;
        }
        _toast = new Toast("error",
            env?.Error?.MessageForCurrentCulture()
            ?? L["Admin.Booths.LoadFailed"]);
        return null;
    }

    private void CloseForm()
    {
        _form = FormKind.None;
        _target = null;
    }

    // Excel export/import wired to the reusable CrudGridExcel component.
    private Task OnExportAsync(IReadOnlyList<AdminBoothSummary> selected) =>
        _excel!.ExportAsync(selected.Select(row => row.Id).ToList(), _query);

    private Task OnImportAsync() => _excel!.TriggerImportAsync();

    private async Task OnImportedAsync()
    {
        _toast = new Toast("success", L["Grid.Import.Done"]);
        await LoadAsync();
    }

    private void OnExcelError(string message) => _toast = new Toast("error", message);

    private async Task OnSavedAsync(AdminBoothDetail saved)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.Booths.Saved"]);
        await LoadAsync();
    }

    private async Task OnDeletedAsync(AdminBoothDetail deleted)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.Booths.Deleted"]);
        await LoadAsync();
    }
}
