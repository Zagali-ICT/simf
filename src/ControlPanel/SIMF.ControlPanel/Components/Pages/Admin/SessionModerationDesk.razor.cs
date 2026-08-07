using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Sessions;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class SessionModerationDesk
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public Guid SessionId { get; set; }

    private record Toast(string Variant, string Message);

    private IReadOnlyList<SessionQuestionModeratorRow> _rows =
        Array.Empty<SessionQuestionModeratorRow>();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>(
                "simfAccount.getJson",
                $"/account/api/sessions/{SessionId}/questions/moderate");
            if (env is { Success: true, Data: not null })
            {
                _rows = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionModeration.Loading"]);
            }
        }
        finally { _loading = false; }
    }

    private async Task SetHiddenAsync(SessionQuestionModeratorRow row, bool isHidden)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<SessionQuestionModeratorRow>>(
                "simfAccount.putJson",
                $"/account/api/sessions/{SessionId}/questions/{row.Id}/hide",
                new SetQuestionHiddenRequest { IsHidden = isHidden });
            if (env is { Success: true })
            {
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionModeration.Loading"]);
            }
        }
        finally { _busy = false; }
    }

    private async Task PushAsync(SessionQuestionModeratorRow row)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<SessionQuestionModeratorRow>>(
                "simfAccount.putJson",
                $"/account/api/sessions/{SessionId}/questions/{row.Id}/push",
                new { });
            if (env is { Success: true })
            {
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionModeration.Loading"]);
            }
        }
        finally { _busy = false; }
    }
}
