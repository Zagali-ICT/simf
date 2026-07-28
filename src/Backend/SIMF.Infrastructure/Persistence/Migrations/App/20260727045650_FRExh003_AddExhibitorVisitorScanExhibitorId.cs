using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <summary>FR-EXH-003 — one nullable <c>ExhibitorId</c> column on
    /// <c>ExhibitorVisitorScans</c> (+ its Restrict FK to <c>Exhibitors</c> and a
    /// composite index), so a captured lead belongs to the BOOTH rather than to the
    /// one officer who scanned it. The column and the FK are ADDITIVE; no column is
    /// renamed or dropped and no shipped JSON field changes. The one non-additive
    /// step is the index swap below, which the ownership change forces.
    ///
    /// <para><b>Uniqueness moves with the ownership.</b> D-611 made
    /// <c>(ExhibitorUserId, VisitorUserId)</c> unique over the ACTIVE rows, encoding
    /// "a lead belongs to the officer who scanned it". Under the booth model that is
    /// wrong twice: it does not stop two officers of one booth double-capturing a
    /// visitor, and it REJECTS the legitimate case of an officer who transfers to
    /// another booth and captures the same visitor for that one (the old booth
    /// rightly keeps its lead, so a second row must exist) — which surfaced as a 500
    /// on scan. It is demoted to a plain index, still serving the legacy fallback
    /// lookup, and a filtered unique <c>(ExhibitorId, VisitorUserId)</c> takes its
    /// place. Rows the old rule allowed to fork — two officers of one booth each
    /// holding their own capture of the same visitor — are collapsed first (newest
    /// kept, the rest soft-deleted) so the new index can build.</para>
    ///
    /// <para><b>Existing rows.</b> The column is backfilled from the capturing
    /// user's ACTIVE <c>ExhibitorMembership</c> (the oldest one, matching the tie
    /// break <c>ExhibitorVisitorService.EnsureExhibitorAsync</c> uses), so a booth
    /// whose officers are still members inherits every lead they took. A row whose
    /// capturer has no active membership at migration time — an officer already
    /// dropped from their booth, or a capture taken under a rule that no longer
    /// admits its capturer — has NO booth to resolve, so it is deliberately LEFT
    /// NULL rather than guessed at. A null row stays visible to its capturer alone
    /// (the person-scoped fallback the service keeps), and the first re-scan of that
    /// visitor adopts it into the scanning officer's booth.</para></summary>
    public partial class FRExh003_AddExhibitorVisitorScanExhibitorId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExhibitorId",
                table: "ExhibitorVisitorScans",
                type: "uniqueidentifier",
                nullable: true);

            // Backfill BEFORE the FK is created, so a stale membership pointing at a
            // deleted exhibitor would fail here loudly rather than leave the table
            // inconsistent behind an unvalidated constraint.
            migrationBuilder.Sql("""
                UPDATE scan
                SET scan.ExhibitorId = membership.ExhibitorId
                FROM ExhibitorVisitorScans AS scan
                CROSS APPLY (
                    SELECT TOP (1) m.ExhibitorId
                    FROM ExhibitorMemberships AS m
                    WHERE m.UserId = scan.ExhibitorUserId
                      AND m.IsActive = 1
                    ORDER BY m.CreatedAt, m.Id
                ) AS membership
                WHERE scan.ExhibitorId IS NULL;
                """);

            // Collapse the leads the OLD uniqueness allowed to fork. Uniqueness
            // used to be per (officer, visitor), so two officers of one booth
            // could each hold their own ACTIVE capture of the same visitor;
            // backfilling puts both on that booth, and the per-booth unique index
            // below would then fail to build. Keep the freshest row (the one whose
            // note was written last) and SOFT-delete the others — the same
            // treatment a removed lead gets, so nothing is destroyed and the
            // consent trail survives.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT scan.Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY scan.ExhibitorId, scan.VisitorUserId
                               ORDER BY COALESCE(scan.UpdatedAt, scan.CreatedAt) DESC,
                                        scan.CreatedAt DESC,
                                        scan.Id DESC) AS rn
                    FROM ExhibitorVisitorScans AS scan
                    WHERE scan.IsActive = 1
                      AND scan.ExhibitorId IS NOT NULL
                )
                UPDATE scan
                SET scan.IsActive = 0,
                    scan.DeletedAt = SYSDATETIMEOFFSET()
                FROM ExhibitorVisitorScans AS scan
                INNER JOIN ranked ON ranked.Id = scan.Id
                WHERE ranked.rn > 1;
                """);

            // The uniqueness invariant moves WITH the ownership. D-611 made
            // (ExhibitorUserId, VisitorUserId) unique over the active rows, which
            // encoded "a lead belongs to the officer who scanned it". Under the
            // booth model that is wrong twice over: it does not stop two officers
            // of one booth double-capturing a visitor (the booth index does), and
            // it REJECTS the legitimate case of an officer who transfers to
            // another booth and captures the same visitor for that one — the old
            // booth rightly keeps its lead, so a second row has to exist. It is
            // demoted to a plain index; it still serves the legacy fallback
            // lookup (ExhibitorId IS NULL, ExhibitorUserId).
            migrationBuilder.DropIndex(
                name: "IX_ExhibitorVisitorScans_ExhibitorUserId_VisitorUserId",
                table: "ExhibitorVisitorScans");

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitorVisitorScans_ExhibitorUserId_VisitorUserId",
                table: "ExhibitorVisitorScans",
                columns: new[] { "ExhibitorUserId", "VisitorUserId" });

            // One ACTIVE capture per (BOOTH, visitor). Legacy un-backfilled rows
            // are excluded — they are never created any more, and two of them for
            // one officer + visitor could only come from data that predates the
            // column.
            migrationBuilder.CreateIndex(
                name: "IX_ExhibitorVisitorScans_ExhibitorId_VisitorUserId",
                table: "ExhibitorVisitorScans",
                columns: new[] { "ExhibitorId", "VisitorUserId" },
                unique: true,
                filter: "[IsActive] = 1 AND [ExhibitorId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ExhibitorVisitorScans_Exhibitors_ExhibitorId",
                table: "ExhibitorVisitorScans",
                column: "ExhibitorId",
                principalTable: "Exhibitors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExhibitorVisitorScans_Exhibitors_ExhibitorId",
                table: "ExhibitorVisitorScans");

            migrationBuilder.DropIndex(
                name: "IX_ExhibitorVisitorScans_ExhibitorId_VisitorUserId",
                table: "ExhibitorVisitorScans");

            migrationBuilder.DropColumn(
                name: "ExhibitorId",
                table: "ExhibitorVisitorScans");

            // Restore the D-611 person-scoped unique index.
            migrationBuilder.DropIndex(
                name: "IX_ExhibitorVisitorScans_ExhibitorUserId_VisitorUserId",
                table: "ExhibitorVisitorScans");

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitorVisitorScans_ExhibitorUserId_VisitorUserId",
                table: "ExhibitorVisitorScans",
                columns: new[] { "ExhibitorUserId", "VisitorUserId" },
                unique: true,
                filter: "[IsActive] = 1");
        }
    }
}
