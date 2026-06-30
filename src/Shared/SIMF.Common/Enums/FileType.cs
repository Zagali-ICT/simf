namespace SIMF.Common.Enums;

/// <summary>D-568 — the media kind of a <c>StoredFile</c>. Drives the
/// per-type upload allow-list (extension + magic-byte signature + size cap)
/// enforced once on the single upload path. This is the centralized file
/// store's "Type" (Img, PDF, …).
///
/// <para>Persisted as an int; <b>append-only</b> — never rename or reorder an
/// existing value.</para></summary>
public enum FileType
{
    /// <summary>PNG / JPEG / WebP raster image.</summary>
    Image = 0,

    /// <summary>PDF document.</summary>
    Pdf = 1,

    /// <summary>Uploaded video (mp4 / webm / mov / m4v / ogg), range-streamed.</summary>
    Video = 2,

    /// <summary>Office / document file (doc, docx, ppt, pptx, txt).</summary>
    Document = 3,

    /// <summary>Spreadsheet (xls, xlsx, csv).</summary>
    Spreadsheet = 4,

    /// <summary>Any other allowed binary not covered above.</summary>
    Other = 5,
}
