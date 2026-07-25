using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Programme;

namespace SIMF.Web.Components.Pages;

// Website — D-717 (item 7, FDS-013 §15 GAP-3) — the public speaker double-opt-in
// landing page. The email Approve/Reject link opens this (a GET preview — no state
// change, safe against link prefetch); the speaker then clicks Confirm, which POSTs
// and applies the decision. Anonymous — the opaque token in the query is the only
// credential. Markup lives in MeetingConfirm.razor.
public partial class MeetingConfirm
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private SimfPublicClient Api { get; set; } = default!;

    [Parameter]
    [SupplyParameterFromQuery(Name = "token")]
    public string? Token { get; set; }

    private bool _loading = true;
    private bool _submitting;
    private MeetingActionPreview? _preview;
    private MeetingActionOutcome? _outcome;

    protected override async Task OnInitializedAsync()
    {
        if (!string.IsNullOrWhiteSpace(Token))
        {
            _preview = await Api.GetMeetingActionAsync(Token!);
        }
        _loading = false;
    }

    private async Task ConfirmAsync()
    {
        if (_submitting || _preview is null || string.IsNullOrWhiteSpace(Token))
        {
            return;
        }
        _submitting = true;
        try
        {
            _outcome = await Api.ConfirmMeetingActionAsync(Token!);
            // Raced / expired / already used between preview and confirm → fall
            // through to the neutral "no longer valid" state.
            if (_outcome is null)
            {
                _preview = null;
            }
        }
        finally { _submitting = false; }
    }

    // R10 (D-767) — show the slot on the Saudi wall clock (AST = UTC+3, no DST) with the
    // weekday, matching the D-765 relabel of the CP from UTC to Saudi time; the stored
    // instants stay UTC. Invariant culture keeps the digits/format stable across AR/EN.
    private string FormatSlot(MeetingActionPreview preview) =>
        preview.SlotStartUtc is { } start && preview.SlotEndUtc is { } end
            ? L["Meeting.Confirm.SlotSaudi",
                start.FormatSaudi("ddd, yyyy-MM-dd hh:mm tt"),
                end.FormatSaudi("hh:mm tt")]
            : L["Meeting.Confirm.TBD"];
}
