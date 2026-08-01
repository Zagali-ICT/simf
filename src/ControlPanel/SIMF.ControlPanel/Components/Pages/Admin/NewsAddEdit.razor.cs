using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.PublicRelations;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class NewsAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private bool _busy;
    private string? _error;

    // The <input type="date"> value mirror — kept as the yyyy-MM-dd text the
    // browser exchanges so the field round-trips cleanly against the
    // DateTime on the form model.
    private string _publishedAtText = string.Empty;

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.Title = Initial.Title;
            _model.TitleArabic = Initial.TitleArabic;
            _model.Excerpt = Initial.Excerpt ?? string.Empty;
            _model.ExcerptArabic = Initial.ExcerptArabic ?? string.Empty;
            _model.Body = Initial.Body;
            _model.BodyArabic = Initial.BodyArabic;
            _model.Category = Initial.Category;
            _model.CategoryArabic = Initial.CategoryArabic;
            _model.ImageRelativePath = Initial.ImageRelativePath ?? string.Empty;
            _model.PublishedAt = Initial.PublishedAt;
            _model.DisplayOrder = Initial.DisplayOrder;
            _model.IsActive = Initial.IsActive;
        }
        _publishedAtText = _model.PublishedAt.ToString("yyyy-MM-dd");
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        // Required-field client guards (the server validator is authoritative;
        // these stop a no-op round-trip and give an immediate message).
        if (string.IsNullOrWhiteSpace(_model.Title)
            || string.IsNullOrWhiteSpace(_model.TitleArabic)
            || string.IsNullOrWhiteSpace(_model.Body)
            || string.IsNullOrWhiteSpace(_model.BodyArabic)
            || string.IsNullOrWhiteSpace(_model.Category)
            || string.IsNullOrWhiteSpace(_model.CategoryArabic))
        {
            _error = L["Admin.News.RequiredFields"];
            return;
        }

        _busy = true;
        try
        {
            var result = await SendAsync(
                JS,
                "/account/api/admin/news",
                $"/account/api/admin/news/{Initial?.Id}",
                new CreateNewsRequest
                {
                    Title = _model.Title,
                    TitleArabic = _model.TitleArabic,
                    Excerpt = NullIfBlank(_model.Excerpt),
                    ExcerptArabic = NullIfBlank(_model.ExcerptArabic),
                    Body = _model.Body,
                    BodyArabic = _model.BodyArabic,
                    Category = _model.Category,
                    CategoryArabic = _model.CategoryArabic,
                    ImageRelativePath = NullIfBlank(_model.ImageRelativePath),
                    PublishedAt = _model.PublishedAt,
                    DisplayOrder = _model.DisplayOrder,
                },
                new UpdateNewsRequest
                {
                    Title = _model.Title,
                    TitleArabic = _model.TitleArabic,
                    Excerpt = NullIfBlank(_model.Excerpt),
                    ExcerptArabic = NullIfBlank(_model.ExcerptArabic),
                    Body = _model.Body,
                    BodyArabic = _model.BodyArabic,
                    Category = _model.Category,
                    CategoryArabic = _model.CategoryArabic,
                    ImageRelativePath = NullIfBlank(_model.ImageRelativePath),
                    PublishedAt = _model.PublishedAt,
                    DisplayOrder = _model.DisplayOrder,
                    IsActive = _model.IsActive,
                });

            if (!result.Succeeded)
            {
                _error = result.ServerMessage ?? L["Admin.News.LoadFailed"];
            }
        }
        finally { _busy = false; }
    }

    private void OnPublishedAtChanged(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        // The date input carries no timezone and does not need one: what the
        // admin picks IS what gets stored. This flag pair is what keeps a naked
        // "2026-11-23" on that date whatever timezone the Control Panel runs in —
        // it is not a zone conversion, and a plain local-time parse here would make
        // the stored value depend on the server's location.
        if (string.IsNullOrWhiteSpace(raw)
            || !DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return;
        }
        _model.PublishedAt = parsed;
        _publishedAtText = parsed.ToString("yyyy-MM-dd");
    }

    private void OnDisplayOrderChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var n) && n >= 0) _model.DisplayOrder = n;
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed class Model
    {
        public string Title { get; set; } = string.Empty;
        public string TitleArabic { get; set; } = string.Empty;
        public string Excerpt { get; set; } = string.Empty;
        public string ExcerptArabic { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string BodyArabic { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string CategoryArabic { get; set; } = string.Empty;
        public string ImageRelativePath { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; } = SimfClock.Now;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
