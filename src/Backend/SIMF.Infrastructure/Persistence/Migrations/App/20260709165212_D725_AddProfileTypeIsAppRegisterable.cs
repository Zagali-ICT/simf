using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D725_AddProfileTypeIsAppRegisterable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAppRegisterable",
                table: "ProfileTypes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // D-725 (owner item 1) — the column defaults every existing row to
            // true (registerable); flip the CP-only operational types (Staff,
            // Moderator) to false so they stop appearing in the app sign-up
            // picker. MobileAppRole is persisted as its enum name (D-161).
            // Keys on the role, not the name, so a renamed Staff/Moderator row
            // is still caught. Idempotent — safe to re-run. Mirrors the
            // IdentitySeeder default for a fresh-seeded DB.
            migrationBuilder.Sql(
                "UPDATE [ProfileTypes] SET [IsAppRegisterable] = 0 "
                + "WHERE [MobileAppRole] IN (N'Staff', N'Moderator');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAppRegisterable",
                table: "ProfileTypes");
        }
    }
}
