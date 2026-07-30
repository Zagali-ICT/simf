using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class BannersAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private string _displayOrderInput = "0";
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.TitleEn = Initial.Title;
            _model.TitleAr = Initial.TitleArabic;
            _model.BodyEn = Initial.Body;
            _model.BodyAr = Initial.BodyArabic;
            _model.ImageUrl = Initial.ImageUrl ?? string.Empty;
            _model.LinkUrl = Initial.LinkUrl ?? string.Empty;
            _model.Start = Initial.Start.ToSaudi().ToString("yyyy-MM-ddTHH:mm");
            _model.End = Initial.End.ToSaudi().ToString("yyyy-MM-ddTHH:mm");
            _model.IsActive = Initial.IsActive;
            _displayOrderInput = Initial.DisplayOrder.ToString();
        }
        else
        {
            var now = DateTimeOffset.UtcNow.ToSaudi();
            _model.Start = now.ToString("yyyy-MM-ddTHH:mm");
            _model.End = now.AddDays(1).ToString("yyyy-MM-ddTHH:mm");
        }
        _editContext = new EditContext(_model);
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        if (string.IsNullOrWhiteSpace(_model.TitleEn)
            || string.IsNullOrWhiteSpace(_model.TitleAr))
        {
            _error = L["Admin.Banners.Required"]; return;
        }
        if (!DateTime.TryParse(_model.Start, out var startParsed)
            || !DateTime.TryParse(_model.End, out var endParsed))
        {
            _error = L["Admin.Banners.Required"]; return;
        }
        if (!int.TryParse(_displayOrderInput, out var order) || order < 0)
        {
            order = 0;
        }

        var start = SaudiTime.FromSaudiWallClock(startParsed);
        var end = SaudiTime.FromSaudiWallClock(endParsed);

        _busy = true;
        try
        {
            ApiResult<AdminBannerDetail>? envelope;
            if (!IsEdit)
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminBannerDetail>>(
                    "simfAccount.postJson", "/account/api/admin/banners",
                    new CreateBannerRequest
                    {
                        Title = _model.TitleEn.Trim(),
                        TitleArabic = _model.TitleAr.Trim(),
                        Body = _model.BodyEn.Trim(),
                        BodyArabic = _model.BodyAr.Trim(),
                        ImageUrl = NullIfBlank(_model.ImageUrl),
                        LinkUrl = NullIfBlank(_model.LinkUrl),
                        Start = start,
                        End = end,
                        DisplayOrder = order,
                    });
            }
            else
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminBannerDetail>>(
                    "simfAccount.putJson", $"/account/api/admin/banners/{Initial!.Id}",
                    new UpdateBannerRequest
                    {
                        Title = _model.TitleEn.Trim(),
                        TitleArabic = _model.TitleAr.Trim(),
                        Body = _model.BodyEn.Trim(),
                        BodyArabic = _model.BodyAr.Trim(),
                        ImageUrl = NullIfBlank(_model.ImageUrl),
                        LinkUrl = NullIfBlank(_model.LinkUrl),
                        Start = start,
                        End = end,
                        DisplayOrder = order,
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
                    ?? L["Admin.Banners.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Banners.LoadFailed"];
        }
        finally { _busy = false; }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class Model
    {
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string BodyEn { get; set; } = string.Empty;
        public string BodyAr { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string LinkUrl { get; set; } = string.Empty;
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
