using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class EnhanceSpeakers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Speakers");

            migrationBuilder.AddColumn<bool>(
                name: "AllowsDataSharing",
                table: "Speakers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowsMeetingRequests",
                table: "Speakers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AwardsArabic",
                table: "Speakers",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CountryId",
                table: "Speakers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "Speakers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "Speakers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualificationsArabic",
                table: "Speakers",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainingExperienceArabic",
                table: "Speakers",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserProfileId",
                table: "Speakers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "XUrl",
                table: "Speakers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Speakers_CountryId",
                table: "Speakers",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Speakers_UserProfileId",
                table: "Speakers",
                column: "UserProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Speakers_CountryId",
                table: "Speakers");

            migrationBuilder.DropIndex(
                name: "IX_Speakers_UserProfileId",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "AllowsDataSharing",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "AllowsMeetingRequests",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "AwardsArabic",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "CountryId",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "LinkedInUrl",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "QualificationsArabic",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "TrainingExperienceArabic",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "UserProfileId",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "XUrl",
                table: "Speakers");

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Speakers",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);
        }
    }
}
