using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D766_InlineContactDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Booths_Contacts_ContactId",
                table: "Booths");

            migrationBuilder.DropForeignKey(
                name: "FK_Exhibitors_Contacts_ContactId",
                table: "Exhibitors");

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
                name: "IX_Exhibitors_ContactId",
                table: "Exhibitors");

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
                table: "Exhibitors");

            migrationBuilder.DropColumn(
                name: "ContactId",
                table: "Booths");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Sponsors",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CityArabic",
                table: "Sponsors",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CountryId",
                table: "Sponsors",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Sponsors",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "Sponsors",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramUrl",
                table: "Sponsors",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Sponsors",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "Sponsors",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Sponsors",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhonePrimary",
                table: "Sponsors",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneSecondary",
                table: "Sponsors",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "XUrl",
                table: "Sponsors",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Speakers",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CityArabic",
                table: "Speakers",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Speakers",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramUrl",
                table: "Speakers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Speakers",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Speakers",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhonePrimary",
                table: "Speakers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneSecondary",
                table: "Speakers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "MediaPartners",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CityArabic",
                table: "MediaPartners",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CountryId",
                table: "MediaPartners",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "MediaPartners",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "MediaPartners",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramUrl",
                table: "MediaPartners",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "MediaPartners",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "MediaPartners",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "MediaPartners",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhonePrimary",
                table: "MediaPartners",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneSecondary",
                table: "MediaPartners",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "XUrl",
                table: "MediaPartners",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Exhibitors",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CityArabic",
                table: "Exhibitors",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CountryId",
                table: "Exhibitors",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "Exhibitors",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramUrl",
                table: "Exhibitors",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Exhibitors",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "Exhibitors",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Exhibitors",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneSecondary",
                table: "Exhibitors",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "XUrl",
                table: "Exhibitors",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficerCity",
                table: "Booths",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficerCityArabic",
                table: "Booths",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OfficerCountryId",
                table: "Booths",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficerFacebookUrl",
                table: "Booths",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficerInstagramUrl",
                table: "Booths",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OfficerLatitude",
                table: "Booths",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficerLinkedInUrl",
                table: "Booths",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OfficerLongitude",
                table: "Booths",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficerNameArabic",
                table: "Booths",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficerPhoneSecondary",
                table: "Booths",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficerWebsite",
                table: "Booths",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficerXUrl",
                table: "Booths",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sponsors_CountryId",
                table: "Sponsors",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPartners_CountryId",
                table: "MediaPartners",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Exhibitors_CountryId",
                table: "Exhibitors",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Booths_OfficerCountryId",
                table: "Booths",
                column: "OfficerCountryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Booths_Countries_OfficerCountryId",
                table: "Booths",
                column: "OfficerCountryId",
                principalTable: "Countries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Exhibitors_Countries_CountryId",
                table: "Exhibitors",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaPartners_Countries_CountryId",
                table: "MediaPartners",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sponsors_Countries_CountryId",
                table: "Sponsors",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Booths_Countries_OfficerCountryId",
                table: "Booths");

            migrationBuilder.DropForeignKey(
                name: "FK_Exhibitors_Countries_CountryId",
                table: "Exhibitors");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaPartners_Countries_CountryId",
                table: "MediaPartners");

            migrationBuilder.DropForeignKey(
                name: "FK_Sponsors_Countries_CountryId",
                table: "Sponsors");

            migrationBuilder.DropIndex(
                name: "IX_Sponsors_CountryId",
                table: "Sponsors");

            migrationBuilder.DropIndex(
                name: "IX_MediaPartners_CountryId",
                table: "MediaPartners");

            migrationBuilder.DropIndex(
                name: "IX_Exhibitors_CountryId",
                table: "Exhibitors");

            migrationBuilder.DropIndex(
                name: "IX_Booths_OfficerCountryId",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "CityArabic",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "CountryId",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "InstagramUrl",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "LinkedInUrl",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "PhonePrimary",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "PhoneSecondary",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "XUrl",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "CityArabic",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "InstagramUrl",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "PhonePrimary",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "PhoneSecondary",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "City",
                table: "MediaPartners");

            migrationBuilder.DropColumn(
                name: "CityArabic",
                table: "MediaPartners");

            migrationBuilder.DropColumn(
                name: "CountryId",
                table: "MediaPartners");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "MediaPartners");

            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "MediaPartners");

            migrationBuilder.DropColumn(
                name: "InstagramUrl",
                table: "MediaPartners");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "MediaPartners");

            migrationBuilder.DropColumn(
                name: "LinkedInUrl",
                table: "MediaPartners");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "MediaPartners");

            migrationBuilder.DropColumn(
                name: "PhonePrimary",
                table: "MediaPartners");

            migrationBuilder.DropColumn(
                name: "PhoneSecondary",
                table: "MediaPartners");

            migrationBuilder.DropColumn(
                name: "XUrl",
                table: "MediaPartners");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Exhibitors");

            migrationBuilder.DropColumn(
                name: "CityArabic",
                table: "Exhibitors");

            migrationBuilder.DropColumn(
                name: "CountryId",
                table: "Exhibitors");

            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "Exhibitors");

            migrationBuilder.DropColumn(
                name: "InstagramUrl",
                table: "Exhibitors");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Exhibitors");

            migrationBuilder.DropColumn(
                name: "LinkedInUrl",
                table: "Exhibitors");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Exhibitors");

            migrationBuilder.DropColumn(
                name: "PhoneSecondary",
                table: "Exhibitors");

            migrationBuilder.DropColumn(
                name: "XUrl",
                table: "Exhibitors");

            migrationBuilder.DropColumn(
                name: "OfficerCity",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "OfficerCityArabic",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "OfficerCountryId",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "OfficerFacebookUrl",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "OfficerInstagramUrl",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "OfficerLatitude",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "OfficerLinkedInUrl",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "OfficerLongitude",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "OfficerNameArabic",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "OfficerPhoneSecondary",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "OfficerWebsite",
                table: "Booths");

            migrationBuilder.DropColumn(
                name: "OfficerXUrl",
                table: "Booths");

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
                table: "Exhibitors",
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
                    CountryId = table.Column<int>(type: "int", nullable: true),
                    City = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CityArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    FacebookUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    InstagramUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LogoRelativePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NameArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PhonePrimary = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PhoneSecondary = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Website = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    XUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
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
                name: "IX_Exhibitors_ContactId",
                table: "Exhibitors",
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
                name: "IX_Contacts_IsActive_NameArabic",
                table: "Contacts",
                columns: new[] { "IsActive", "NameArabic" });

            migrationBuilder.AddForeignKey(
                name: "FK_Booths_Contacts_ContactId",
                table: "Booths",
                column: "ContactId",
                principalTable: "Contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Exhibitors_Contacts_ContactId",
                table: "Exhibitors",
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
    }
}
