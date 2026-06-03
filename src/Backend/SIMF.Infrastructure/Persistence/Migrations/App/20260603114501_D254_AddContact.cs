using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D254_AddContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContactId",
                table: "Sponsors",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContactId",
                table: "Speakers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContactId",
                table: "MediaPartners",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContactId",
                table: "Companies",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContactId",
                table: "Booths",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LogoRelativePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PhonePrimary = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PhoneSecondary = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    FacebookUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    XUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    InstagramUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    CountryId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contacts_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sponsors_ContactId",
                table: "Sponsors",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Speakers_ContactId",
                table: "Speakers",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPartners_ContactId",
                table: "MediaPartners",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_ContactId",
                table: "Companies",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Booths_ContactId",
                table: "Booths",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_CountryId",
                table: "Contacts",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_IsActive_NameAr",
                table: "Contacts",
                columns: new[] { "IsActive", "NameAr" });

            migrationBuilder.AddForeignKey(
                name: "FK_Booths_Contacts_ContactId",
                table: "Booths",
                column: "ContactId",
                principalTable: "Contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Contacts_ContactId",
                table: "Companies",
                column: "ContactId",
                principalTable: "Contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaPartners_Contacts_ContactId",
                table: "MediaPartners",
                column: "ContactId",
                principalTable: "Contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Speakers_Contacts_ContactId",
                table: "Speakers",
                column: "ContactId",
                principalTable: "Contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sponsors_Contacts_ContactId",
                table: "Sponsors",
                column: "ContactId",
                principalTable: "Contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Booths_Contacts_ContactId",
                table: "Booths");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Contacts_ContactId",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaPartners_Contacts_ContactId",
                table: "MediaPartners");

            migrationBuilder.DropForeignKey(
                name: "FK_Speakers_Contacts_ContactId",
                table: "Speakers");

            migrationBuilder.DropForeignKey(
                name: "FK_Sponsors_Contacts_ContactId",
                table: "Sponsors");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropIndex(
                name: "IX_Sponsors_ContactId",
                table: "Sponsors");

            migrationBuilder.DropIndex(
                name: "IX_Speakers_ContactId",
                table: "Speakers");

            migrationBuilder.DropIndex(
                name: "IX_MediaPartners_ContactId",
                table: "MediaPartners");

            migrationBuilder.DropIndex(
                name: "IX_Companies_ContactId",
                table: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Booths_ContactId",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "ContactId",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "ContactId",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "ContactId",
                table: "MediaPartners");

            migrationBuilder.DropColumn(
                name: "ContactId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ContactId",
                table: "Booths");
        }
    }
}
