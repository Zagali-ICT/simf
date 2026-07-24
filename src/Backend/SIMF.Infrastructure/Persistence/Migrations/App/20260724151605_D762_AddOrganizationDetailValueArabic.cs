using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <summary>D-762 — an additive, nullable <c>ValueArabic</c> column on
    /// <c>OrganizationDetails</c> so an About "detail" row carries a bilingual value
    /// (previously only its label was bilingual). Nullable: a language-neutral value
    /// (a year, a URL) leaves it null and the app falls back to <c>Value</c>. The real
    /// Arabic values for the seeded rows are set idempotently by
    /// <c>docs/migrations/2026/SIMF_App_Organization.sql</c>.</summary>
    public partial class D762_AddOrganizationDetailValueArabic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ValueArabic",
                table: "OrganizationDetails",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValueArabic",
                table: "OrganizationDetails");
        }
    }
}
