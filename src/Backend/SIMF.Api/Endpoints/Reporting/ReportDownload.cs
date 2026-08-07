using Microsoft.AspNetCore.Http;
using SIMF.Common;

namespace SIMF.Api.Endpoints.Reporting;

/// <summary>
/// The one place the reporting exports agree on their content type and file
/// name, so a new report cannot ship with a subtly different download header.
/// </summary>
internal static class ReportDownload
{
    /// <summary>The XLSX media type.</summary>
    public const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// The download file name, stamped in <b>Saudi local time</b>. An operator
    /// exporting at 1am Riyadh should not find a file dated the previous day.
    /// The slug is a fixed literal per report, never user input, so
    /// there is nothing here to sanitise.
    /// </summary>
    public static string FileName(string slug) =>
        $"simf-{slug}-{SimfClock.Now.FormatSaudi("yyyyMMdd-HHmmss")}.xlsx";

    /// <summary>Sets the attachment header for a report download.</summary>
    public static void SetAttachmentHeader(HttpContext http, string slug) =>
        http.Response.Headers.ContentDisposition =
            $"attachment; filename=\"{FileName(slug)}\"";
}
