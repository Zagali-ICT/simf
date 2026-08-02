using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D815_AddAccountAccessibilityPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AccessibilityCaptions",
                table: "UserProfiles",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AccessibilityConfiguredAt",
                table: "UserProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AccessibilityHighContrast",
                table: "UserProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AccessibilityReduceMotion",
                table: "UserProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AccessibilityScreenReaderAssist",
                table: "UserProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AccessibilityTextSize",
                table: "UserProfiles",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "normal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessibilityCaptions",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "AccessibilityConfiguredAt",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "AccessibilityHighContrast",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "AccessibilityReduceMotion",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "AccessibilityScreenReaderAssist",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "AccessibilityTextSize",
                table: "UserProfiles");
        }
    }
}
