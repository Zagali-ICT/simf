using SIMF.Domain.Common;

namespace SIMF.Domain.Archive;

/// <summary>D-199 — a previous SIMF edition shown on the public Archive /
/// Past Editions screen (Mockup screen 24) and managed from the Control
/// Panel. One row per forum year. The public list is additionally gated by
/// the archive-visibility operations toggle (D-166): when that toggle is
/// off, the public endpoint returns an empty list regardless of how many
/// active editions exist.</summary>
public class ArchiveEdition:BaseAuditEntity
{ 

    /// <summary>Forum year (e.g. 2023). Unique across active + inactive rows
    /// — one edition per calendar year. Used as the natural display key
    /// ("SIMF 2023") and the default sort.</summary>
    public int Year { get; set; }

    /// <summary>English edition title (e.g. "SIMF 2023"). 1–200 chars.</summary>
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Arabic edition title. 1–200 chars.</summary>
    public string TitleAr { get; set; } = string.Empty;

    /// <summary>Optional English summary / theme line
    /// (e.g. "The third edition — Securing Tomorrow's Seas"). ≤ 1024 chars.</summary>
    public string? SummaryEn { get; set; }

    /// <summary>Optional Arabic summary / theme line. ≤ 1024 chars.</summary>
    public string? SummaryAr { get; set; }

    /// <summary>Reported attendee count for the edition (≥ 0).</summary>
    public int Attendees { get; set; }

    /// <summary>Reported session count for the edition (≥ 0).</summary>
    public int Sessions { get; set; }

    /// <summary>Reported speaker count for the edition (≥ 0).</summary>
    public int Speakers { get; set; }

    /// <summary>Optional cover image relative path under the media root
    /// (e.g. "archive/simf2023.png"). ≤ 512 chars.</summary>
    public string? CoverImageRelativePath { get; set; }

    /// <summary>§9 (Mockup screen 24-01 "تفاصيل النسخة") — the edition's
    /// place/venue (e.g. "الرياض · واجهة الرياض"). Optional, ≤ 256 chars.</summary>
    public string? LocationEn { get; set; }
    public string? LocationAr { get; set; }

    /// <summary>§9 (screen 24-01) — a human date label for the edition
    /// (e.g. "نوفمبر 2024 · 3 أيام" / "November 2024 · 3 days"); distinct from
    /// the numeric <see cref="Year"/>. Optional, ≤ 128 chars.</summary>
    public string? DateLabelEn { get; set; }
    public string? DateLabelAr { get; set; }
}
// Deferred (entity TODO): the rich per-edition lists — gallery, programme/session
// titles, past-speaker names, sponsors — are not yet modelled; the 24-01 detail
// surfaces title/summary/location/date/counts/cover for now (§9 / D-273).