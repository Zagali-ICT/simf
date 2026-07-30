using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;

namespace SIMF.ControlPanel;

/// <summary>§6.16 (F-U5-005) — the one place that knows how a failed Excel
/// export is reported.
///
/// <para><c>simfAccount.downloadXlsx</c> returns <c>null</c> once the browser
/// download has started, and an <see cref="ApiResult{T}"/> failure envelope when
/// it has not. It used to return silently on any non-OK status, so a failed
/// Export was indistinguishable from a dead button on every list page that has
/// one — the admin clicked, no file arrived, and nothing on the page changed.
/// </para>
///
/// <para>Pages keep their own page-local <c>Toast</c> record, so this returns
/// the localized message to show rather than the toast itself:
/// <code>
/// var error = await JS.ExportXlsxAsync(url, request, L);
/// if (error is not null) _toast = new Toast("error", error);
/// </code></para></summary>
public static class CpExport
{
    /// <summary>POSTs the export request and starts the download. Returns null
    /// when the download started, or the localized failure message when it did
    /// not — the API's own bilingual error where it sent one.</summary>
    public static async Task<string?> ExportXlsxAsync(
        this IJSRuntime js,
        string url,
        object request,
        IStringLocalizer<Strings> l)
    {
        var envelope = await js.InvokeAsync<ApiResult<bool>?>(
            "simfAccount.downloadXlsx", url, request);
        if (envelope is not { Success: false })
        {
            return null;
        }
        return envelope.Error?.MessageForCurrentCulture() ?? l["Grid.Export.Failed"].Value;
    }
}
