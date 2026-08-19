// Pure unit cover for the two workbook-ingest guards, no host and no database:
// the government-organisation reader's row cap (its two sibling importers both
// had one and it did not), and the generic grid importer's decompressed-size
// pre-check (the endpoint gate bounds the COMPRESSED upload only, and an xlsx
// is a zip of xml).
using System.IO.Compression;
using ClosedXML.Excel;
using SIMF.Common;
using SIMF.Infrastructure.Excel;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Reporting)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class ExcelImportGuardTests
{
    // -- Organisation reader row cap -----------------------------------------

    /// <summary>A workbook whose LAST used row is <paramref name="lastRow"/>.
    /// Only the header and that one row carry values, so the cap can be probed
    /// without materialising thousands of cells.</summary>
    private static MemoryStream WorkbookEndingAtRow(int lastRow)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Organisations");
        sheet.Cell(1, 1).Value = "NameAr";
        sheet.Cell(lastRow, 1).Value = "شركة";

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Organisation_reader_rejects_a_workbook_past_the_row_cap()
    {
        var reader = new ClosedXmlOrganisationReader();
        using var stream = WorkbookEndingAtRow(ClosedXmlOrganisationReader.MaxImportRows + 2);

        var ex = Assert.Throws<DataValidationException>(() => reader.Read(stream));

        Assert.Contains(
            ClosedXmlOrganisationReader.MaxImportRows.ToString(),
            ex.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Organisation_reader_accepts_a_workbook_exactly_at_the_row_cap()
    {
        var reader = new ClosedXmlOrganisationReader();
        using var stream = WorkbookEndingAtRow(ClosedXmlOrganisationReader.MaxImportRows + 1);

        var rows = reader.Read(stream);

        // Only the last row carries a value; the blank ones in between are skipped.
        Assert.Single(rows);
    }

    // -- Grid importer decompressed-size pre-check ---------------------------

    [Fact]
    public void Grid_importer_rejects_a_workbook_that_declares_a_huge_expansion()
    {
        // A few hundred KB of compressed zeros that declare ~256 MB uncompressed:
        // the shape of an upload that passes the endpoint's 5 MB size gate and
        // the zip-magic pre-check, and then allocates the workbook object model
        // before any row is counted.
        var bomb = ZipDeclaringUncompressedBytes(256L * 1024 * 1024);
        var importer = new ClosedXmlGridExcelImporter();

        var ex = Assert.Throws<DataValidationException>(
            () => importer.Parse(bomb, "Sheet1", ["Name"], maxRows: 5));

        Assert.Contains("too large", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Grid_importer_still_reads_an_ordinary_workbook()
    {
        // The guard must not cost a legitimate import anything.
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(1, 1).Value = "Name";
        sheet.Cell(2, 1).Value = "Riyadh";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var parsed = new ClosedXmlGridExcelImporter()
            .Parse(stream.ToArray(), "Sheet1", ["Name"], maxRows: 5);

        Assert.Single(parsed.Rows);
        Assert.Equal("Riyadh", parsed.Rows[0].Cells["Name"]);
    }

    private static byte[] ZipDeclaringUncompressedBytes(long total)
    {
        var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("xl/sharedStrings.xml", CompressionLevel.Fastest);
            using var writer = entry.Open();
            var chunk = new byte[1024 * 1024];
            for (long written = 0; written < total; written += chunk.Length)
            {
                writer.Write(chunk, 0, chunk.Length);
            }
        }

        return output.ToArray();
    }
}
