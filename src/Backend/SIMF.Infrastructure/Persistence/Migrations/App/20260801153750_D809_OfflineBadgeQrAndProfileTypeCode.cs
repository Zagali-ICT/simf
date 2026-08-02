using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <summary>
    /// D-819 — the offline event badge.
    ///
    /// <para>Two changes, both required before a gate can validate a badge with
    /// no network:</para>
    ///
    /// <para><b>1. <c>GateScans.QrIdAtScan</c> widened nvarchar(32) -> (64).</b>
    /// The scan log stores the QR verbatim. An offline badge is an ENCRYPTED
    /// payload of roughly 54 characters rather than a bare 12-character serial,
    /// and the scanner deliberately sends the whole blob so the SERVER decrypts
    /// it independently instead of trusting the device's result — which keeps
    /// this append-only audit column exactly what was physically presented at
    /// the gate. This is a WIDENING, so every existing row is preserved
    /// untouched; the scaffolder's data-loss warning refers only to the Down
    /// migration, which narrows back. It is DDL, so the table's INSTEAD OF
    /// UPDATE/DELETE trigger (which protects rows, not schema) does not apply.</para>
    ///
    /// <para><b>2. <c>ProfileTypes.Code</c> added (smallint, default 0).</b> The
    /// badge carries the profile type so an offline gate can check it against
    /// that gate's allowed-type list with no lookup. <c>Id</c> is a Guid and
    /// will not fit in a QR that must stay readable on a creased badge, so a
    /// small number stands in for it. Additive with a default, so existing rows
    /// are valid immediately.</para>
    ///
    /// <para><b>Backfill.</b> Existing rows would otherwise all sit at
    /// <c>Code = 0</c>, the reserved "unassigned" value that no badge can carry
    /// — leaving every existing profile type unusable offline. The data step
    /// below numbers them deterministically (CreatedAt, then Id, so the result
    /// is stable across environments) starting at 1. It runs BEFORE the unique
    /// index is created, and only touches rows still at 0, so re-running against
    /// a partially-migrated database cannot renumber a type whose badges are
    /// already printed.</para>
    ///
    /// <para>The unique index is filtered to <c>IsActive = 1 AND Code &lt;&gt; 0</c>:
    /// unassigned rows do not collide with each other, and a soft-deleted type
    /// releases its code from the constraint. Releasing it from the CONSTRAINT
    /// is not permission to REUSE it — badges printed under a code stay in
    /// circulation, so reuse is barred operationally.</para>
    /// </summary>
    public partial class D809_OfflineBadgeQrAndProfileTypeCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "Code",
                table: "ProfileTypes",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AlterColumn<string>(
                name: "QrIdAtScan",
                table: "GateScans",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            // Assign a stable code to every pre-existing profile type. Ordered
            // by (CreatedAt, Id) so the numbering is deterministic across
            // environments, and guarded on Code = 0 so an already-assigned type
            // is never renumbered.
            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT [Id],
                           ROW_NUMBER() OVER (ORDER BY [CreatedAt], [Id]) AS [rn]
                    FROM [ProfileTypes]
                )
                UPDATE pt
                   SET pt.[Code] = CAST(n.[rn] AS smallint)
                  FROM [ProfileTypes] pt
                 INNER JOIN numbered n ON n.[Id] = pt.[Id]
                 WHERE pt.[Code] = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ProfileTypes_Code",
                table: "ProfileTypes",
                column: "Code",
                unique: true,
                filter: "[IsActive] = 1 AND [Code] <> 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProfileTypes_Code",
                table: "ProfileTypes");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "ProfileTypes");

            // Narrowing back truncates any offline badge already recorded at a
            // gate. Only safe on a database that never ran the offline mode.
            migrationBuilder.AlterColumn<string>(
                name: "QrIdAtScan",
                table: "GateScans",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);
        }
    }
}
