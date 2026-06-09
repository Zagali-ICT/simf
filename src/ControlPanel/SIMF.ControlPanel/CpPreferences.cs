using Microsoft.JSInterop;
using SIMF.Components.Forms;

namespace SIMF.ControlPanel;

/// <summary>Per-user, per-page Control Panel UI preferences, persisted in the
/// browser's localStorage via the <c>simfPrefs</c> JS helper (D-353). Today it
/// stores the CRUD presentation mode (dialog vs full page); the per-page blob
/// is versioned so grid page-size and column widths can be added later without
/// breaking older saved values. Scoped per circuit (it depends on the circuit's
/// <see cref="IJSRuntime"/>). Pure browser storage — no API, no server state,
/// no schema (respects the D-110 freeze).</summary>
public sealed class CpPreferences
{
    // Every CP preference key starts with this so ClearAll can wipe exactly the
    // CP layout/display preferences (and nothing else, e.g. the theme key).
    private const string KeyPrefix = "simf.cp.prefs.";

    private readonly IJSRuntime _js;

    public CpPreferences(IJSRuntime js) => _js = js;

    /// <summary>Reads the saved presentation for a page; defaults to
    /// <see cref="CrudPresentation.Dialog"/> when nothing is stored (so the
    /// behaviour is unchanged until the user picks full-page).</summary>
    public async Task<CrudPresentation> GetPresentationAsync(
        string pageKey, CancellationToken cancellationToken = default)
    {
        var prefs = await ReadAsync(pageKey, cancellationToken);
        return string.Equals(prefs?.Presentation, "page", StringComparison.OrdinalIgnoreCase)
            ? CrudPresentation.Page
            : CrudPresentation.Dialog;
    }

    /// <summary>Saves the presentation choice for a page, preserving any other
    /// fields already stored in the page's blob.</summary>
    public async Task SetPresentationAsync(
        string pageKey, CrudPresentation value, CancellationToken cancellationToken = default)
    {
        var prefs = await ReadAsync(pageKey, cancellationToken) ?? new PagePrefs();
        prefs.Presentation = value == CrudPresentation.Page ? "page" : "dialog";
        await _js.InvokeVoidAsync("simfPrefs.set", cancellationToken, KeyPrefix + pageKey, prefs);
    }

    /// <summary>Clears every saved CP display/layout preference in this browser
    /// (the Profile "clear saved layout" action). Returns how many keys were
    /// removed.</summary>
    public async Task<int> ClearAllAsync(CancellationToken cancellationToken = default) =>
        await _js.InvokeAsync<int>("simfPrefs.clearAll", cancellationToken, KeyPrefix);

    private async Task<PagePrefs?> ReadAsync(string pageKey, CancellationToken cancellationToken) =>
        await _js.InvokeAsync<PagePrefs?>("simfPrefs.get", cancellationToken, KeyPrefix + pageKey);

    /// <summary>The persisted per-page preference blob. Versioned (<see cref="V"/>)
    /// so future additive fields (page size, column widths) can evolve the shape
    /// without misreading an older value.</summary>
    public sealed class PagePrefs
    {
        /// <summary>Schema version of this blob.</summary>
        public int V { get; set; } = 1;

        /// <summary>"dialog" or "page" — null when never set.</summary>
        public string? Presentation { get; set; }
    }
}
