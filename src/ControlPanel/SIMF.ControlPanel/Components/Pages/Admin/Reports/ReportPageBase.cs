using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Reporting;

namespace SIMF.ControlPanel.Components.Pages.Admin.Reports;

/// <summary>
/// The shared behaviour of every report page: hold the query, load a page,
/// re-load when the grid or the date range changes, and trigger the export.
///
/// <para>Each report page supplies only what actually differs — its API slug,
/// its caption, and its grid columns in markup. Without this base the pages
/// would be three copies of the same twelve methods, which is exactly the
/// duplication the shared-widget rule exists to prevent.</para>
/// </summary>
/// <typeparam name="TRow">The report's row contract.</typeparam>
public abstract class ReportPageBase<TRow> : ComponentBase
{
    [Inject] protected IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;

    /// <summary>The report's route segment, e.g. <c>attendance</c>. Drives both
    /// the list and the export BFF routes.</summary>
    protected abstract string Slug { get; }

    protected ReportQuery Query { get; private set; } = new();
    protected GridPage<TRow>? Page { get; private set; }
    protected IReadOnlyList<ReportTotal> Totals { get; private set; } = [];
    protected bool Loading { get; private set; }
    protected string? Error { get; private set; }

    protected DateOnly? From
    {
        get => Query.From;
        set => Query.From = value;
    }

    protected DateOnly? To
    {
        get => Query.To;
        set => Query.To = value;
    }

    protected override async Task OnInitializedAsync() => await LoadAsync();

    /// <summary>Re-reads the current page. Any transport or authorisation failure
    /// surfaces as a message rather than an empty grid, so "no rows" always means
    /// no rows.</summary>
    protected async Task LoadAsync()
    {
        Loading = true;
        Error = null;
        StateHasChanged();

        try
        {
            var env = await JS.InvokeAsync<ApiResult<ReportPage<TRow>>>(
                "simfAccount.postJson", $"/account/api/admin/reports/{Slug}/list", Query);

            if (env is { Success: true, Data: not null })
            {
                Page = new GridPage<TRow>
                {
                    Items = env.Data.Rows,
                    Total = env.Data.Total,
                    Skip = env.Data.Skip,
                    Top = env.Data.Top,
                };
                Totals = env.Data.Totals;
            }
            else
            {
                Error = env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Reports.LoadFailed"].Value;
            }
        }
        finally
        {
            Loading = false;
            StateHasChanged();
        }
    }

    /// <summary>The grid changed page, sort or filter.</summary>
    protected async Task OnQueryChangedAsync(GridQuery grid)
    {
        Query.Grid = grid;
        await LoadAsync();
    }

    /// <summary>A new date range was applied. Paging resets: staying on page 7 of
    /// a different period would show an empty grid for no visible reason.</summary>
    protected async Task OnRangeAppliedAsync()
    {
        Query.Grid.Skip = 0;
        await LoadAsync();
    }

    /// <summary>Downloads the whole filtered report, not just this page.</summary>
    protected async Task ExportAsync() =>
        await JS.InvokeVoidAsync(
            "simfAccount.downloadXlsx", $"/account/api/admin/reports/{Slug}/export", Query);

    /// <summary>Resolves a total's resource key. The API returns keys, not
    /// sentences, so the figures localise with the rest of the page.</summary>
    protected string TotalLabel(ReportTotal total) => L[total.LabelKey];

    /// <summary>
    /// Turns an enum member name the API sent into a readable, localised label.
    ///
    /// <para>Report rows carry enums as <c>someEnum.ToString()</c> — the raw
    /// PascalCase member. Left alone, a gate report shows an operator
    /// "HolderNotApproved" and "CheckIn"; a ratings report shows "PerSession";
    /// and on the ARABIC page every one of those columns stays in English,
    /// which is the bigger problem since Arabic is the primary language.</para>
    ///
    /// <para><paramref name="group"/> is the namespace, normally the enum type
    /// name. The partners report is the exception: its Tier column flattens
    /// <c>ExhibitorTier</c> and <c>SponsorTier</c> into one string, so both
    /// resolve under <c>PartnerTier</c>.</para>
    ///
    /// <para>The API and the XLSX exports keep sending the CODE, deliberately.
    /// An export is a data file people filter and pivot on, where a stable
    /// identifier is worth more than prose; the screen is where words belong.
    /// Keeping the copy only here also means one source for it.</para>
    ///
    /// <para>An unknown member renders AS ITSELF rather than as a missing
    /// resource key — printing "Admin.Reports.Enum.X.Y" into a grid would be
    /// worse than the bare name. That is a safety net, not the plan:
    /// <c>ReportEnumLabelTests</c> fails the build when any member of a rendered
    /// enum has no label in either language.</para>
    /// </summary>
    protected string EnumLabel(string group, string? member)
    {
        if (string.IsNullOrWhiteSpace(member))
        {
            return string.Empty;
        }

        var label = L[$"Admin.Reports.Enum.{group}.{member}"];
        return label.ResourceNotFound ? member : label.Value;
    }

    protected string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"].Value, current, total);

    protected string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"].Value, skip + 1, skip + taken, total);
}
