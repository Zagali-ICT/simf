using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ProgrammeDaysAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private bool _busy;
    private string? _error;

    // The <input type="date"> value mirror — the yyyy-MM-dd text the browser
    // exchanges, round-tripped against the DateOnly on the form model.
    private string _dateText = string.Empty;

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.Date = Initial.Date;
            _model.Title = Initial.Title;
            _model.TitleArabic = Initial.TitleArabic;
            _model.DisplayOrder = Initial.DisplayOrder;
            _model.IsActive = Initial.IsActive;
        }
        _dateText = _model.Date == default
            ? string.Empty
            : _model.Date.ToString("yyyy-MM-dd");
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        if (string.IsNullOrWhiteSpace(_model.Title) || _model.Title.Length > 128
            || string.IsNullOrWhiteSpace(_model.TitleArabic) || _model.TitleArabic.Length > 128)
        {
            _error = L["Admin.ProgrammeDays.Required"]; return;
        }
        if (_model.Date == default)
        {
            _error = L["Admin.ProgrammeDays.DateRequired"]; return;
        }

        _busy = true;
        try
        {
            ApiResult<AdminProgrammeDayDetail>? envelope;
            if (!IsEdit)
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminProgrammeDayDetail>>(
                    "simfAccount.postJson", "/account/api/admin/programme-days",
                    new AdminCreateProgrammeDayRequest
                    {
                        Date = _model.Date,
                        Title = _model.Title.Trim(),
                        TitleArabic = _model.TitleArabic.Trim(),
                        DisplayOrder = _model.DisplayOrder,
                    });
            }
            else
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminProgrammeDayDetail>>(
                    "simfAccount.putJson", $"/account/api/admin/programme-days/{Initial!.Id}",
                    new AdminUpdateProgrammeDayRequest
                    {
                        Date = _model.Date,
                        Title = _model.Title.Trim(),
                        TitleArabic = _model.TitleArabic.Trim(),
                        DisplayOrder = _model.DisplayOrder,
                        IsActive = _model.IsActive,
                    });
            }

            if (envelope is { Success: true, Data: not null })
            {
                await OnSuccess.InvokeAsync(envelope.Data);
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.ProgrammeDays.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.ProgrammeDays.LoadFailed"];
        }
        finally { _busy = false; }
    }

    private void OnDateChanged(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        if (string.IsNullOrWhiteSpace(raw)
            || !DateOnly.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            return;
        }
        _model.Date = parsed;
        _dateText = parsed.ToString("yyyy-MM-dd");
    }

    private void OnDisplayOrderChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var n) && n >= 0) _model.DisplayOrder = n;
    }

    private sealed class Model
    {
        public DateOnly Date { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TitleArabic { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
