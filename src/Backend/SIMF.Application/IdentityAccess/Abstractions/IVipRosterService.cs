using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Reads the VVIP/VIP welcome roster for the موج (Mawj)
/// integration and renders it as a downloadable CSV / Excel file. The roster is
/// a cross-database projection: VVIP/VIP profiles on SIMF_App joined on read
/// with their owners on SIMF_Identity. Read-only — the export flows one way
/// (us → the technical teams), there is no Mawj import.
/// </summary>
public interface IVipRosterService
{
    /// <summary>The full VVIP/VIP roster, newest tier first. This is the JSON
    /// API the Mawj integration consumes.</summary>
    Task<IReadOnlyList<VipRosterRow>> GetRosterAsync(
        CancellationToken cancellationToken = default);

    /// <summary>One page of the roster for the CP export grid (SimfDataGrid).
    /// The roster is small (dozens of VIPs), so search / sort / filter / paging
    /// are applied in memory over <see cref="GetRosterAsync"/>.</summary>
    Task<GridPage<VipRosterRow>> GetRosterPageAsync(
        GridQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Renders the roster as a downloadable file in the requested
    /// format. Returns the bytes, the MIME content-type, and a suggested
    /// file name (with extension).</summary>
    Task<VipRosterFile> ExportAsync(
        VipRosterExportFormat format,
        CancellationToken cancellationToken = default);
}

/// <summary>The export formats the روster page offers.</summary>
public enum VipRosterExportFormat
{
    Csv = 0,
    Xlsx = 1,
}

/// <summary>A rendered roster file ready to stream back.</summary>
public sealed record VipRosterFile(byte[] Content, string ContentType, string FileName);
