using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D495_AddOrganizationProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationProfile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TitleArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Slogan = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SloganArabic = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Bio = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BioArabic = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    VersionDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SysVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReleaseDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EventStartDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EventEndDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CurrentYear = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LocationText = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LocationTextArabic = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ContactWebsite = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    LiveStreamUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    FacebookUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    XUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    InstagramUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    YouTubeUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    TikTokUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    SnapchatUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    RegistrationSuccessMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    RegistrationSuccessMessageArabic = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationProfile", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationAboutItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TitleArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TextArabic = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationAboutItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationAboutItems_OrganizationProfile_OrganizationProfileId",
                        column: x => x.OrganizationProfileId,
                        principalTable: "OrganizationProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationDetails_OrganizationProfile_OrganizationProfileId",
                        column: x => x.OrganizationProfileId,
                        principalTable: "OrganizationProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "OrganizationProfile",
                columns: new[] { "Id", "Bio", "BioArabic", "ContactEmail", "ContactPhone", "ContactWebsite", "CreatedAt", "CreatedBy", "CurrentYear", "DeletedAt", "EventEndDate", "EventStartDate", "FacebookUrl", "InstagramUrl", "IsActive", "Latitude", "LinkedInUrl", "LiveStreamUrl", "LocationText", "LocationTextArabic", "Longitude", "Name", "NameArabic", "RegistrationSuccessMessage", "RegistrationSuccessMessageArabic", "ReleaseDate", "Slogan", "SloganArabic", "SnapchatUrl", "Status", "SysVersion", "TikTokUrl", "Title", "TitleArabic", "UpdatedAt", "UpdatedBy", "Version", "VersionDate", "XUrl", "YouTubeUrl" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), null, null, null, null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000000"), 2026, null, new DateTimeOffset(new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, true, null, null, null, "Saudi Arabia", "السعودية", null, "The International Maritime Forum", "الملتقى الدولي البحري", "Congratulations, welcome to the Fourth Saudi Forum.", "تهانينا، مرحباً بكم في الملتقى السعودي الرابع.", null, null, null, null, 1, null, null, "The Saudi International Maritime Forum", "الملتقى البحري السعودي الدولي", null, null, "1.0.0", null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAboutItems_OrganizationProfileId_IsActive_DisplayOrder",
                table: "OrganizationAboutItems",
                columns: new[] { "OrganizationProfileId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationDetails_OrganizationProfileId_IsActive_DisplayOrder",
                table: "OrganizationDetails",
                columns: new[] { "OrganizationProfileId", "IsActive", "DisplayOrder" });

            // D-495 — seed the "about" items (mission / vision) from the content the
            // app rendered as static l10n until now, so the table ships populated and
            // the app re-skins from config.
            migrationBuilder.InsertData(
                table: "OrganizationAboutItems",
                columns: new[] { "Id", "OrganizationProfileId", "Title", "TitleArabic", "Text", "TextArabic", "DisplayOrder", "CreatedAt", "CreatedBy", "IsActive" },
                values: new object[,]
                {
                    {
                        new Guid("00000000-0000-0000-0000-000000000031"),
                        new Guid("00000000-0000-0000-0000-000000000003"),
                        "Mission", "الرسالة",
                        "A Saudi global platform advancing dialogue on maritime-security issues",
                        "منصة سعودية عالمية لدعم الحوار في قضايا الأمن البحري",
                        0,
                        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                        new Guid("00000000-0000-0000-0000-000000000000"),
                        true
                    },
                    {
                        new Guid("00000000-0000-0000-0000-000000000032"),
                        new Guid("00000000-0000-0000-0000-000000000003"),
                        "Vision", "الرؤية",
                        "The Saudi International Maritime Forum is a high-level international event that brings together leaders, officials and experts to share experience and build a shared global understanding of the future of maritime security.",
                        "الملتقى البحري السعودي الدولي حدث دولي رفيع المستوى، يجمع القادة والمسؤولين والخبراء لتبادل التجارب وتعزيز فهم عالمي مشترك لمستقبل الأمن البحري.",
                        1,
                        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                        new Guid("00000000-0000-0000-0000-000000000000"),
                        true
                    },
                });

            // D-495 — seed the "details" rows (year / date / location).
            migrationBuilder.InsertData(
                table: "OrganizationDetails",
                columns: new[] { "Id", "OrganizationProfileId", "Name", "NameArabic", "Value", "DisplayOrder", "CreatedAt", "CreatedBy", "IsActive" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000041"), new Guid("00000000-0000-0000-0000-000000000003"), "Year", "السنة", "2026", 0, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new Guid("00000000-0000-0000-0000-000000000000"), true },
                    { new Guid("00000000-0000-0000-0000-000000000042"), new Guid("00000000-0000-0000-0000-000000000003"), "Date", "الزمن", "01-2026 — 04-2026", 1, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new Guid("00000000-0000-0000-0000-000000000000"), true },
                    { new Guid("00000000-0000-0000-0000-000000000043"), new Guid("00000000-0000-0000-0000-000000000003"), "Location", "المكان", "Saudi Arabia", 2, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new Guid("00000000-0000-0000-0000-000000000000"), true },
                });

            // D-495 — carry over any admin-set social / welcome values from the old
            // SiteSettings keys into the singleton (override the seed defaults only
            // when a real value exists), then retire those keys from the active set.
            migrationBuilder.Sql(@"
UPDATE [OrganizationProfile] SET
    [FacebookUrl]  = COALESCE((SELECT TOP 1 [Value] FROM [SystemSettings] WHERE [Key] = 'social.facebook'  AND [IsActive] = 1), [FacebookUrl]),
    [XUrl]         = COALESCE((SELECT TOP 1 [Value] FROM [SystemSettings] WHERE [Key] = 'social.x'          AND [IsActive] = 1), [XUrl]),
    [InstagramUrl] = COALESCE((SELECT TOP 1 [Value] FROM [SystemSettings] WHERE [Key] = 'social.instagram'  AND [IsActive] = 1), [InstagramUrl]),
    [LinkedInUrl]  = COALESCE((SELECT TOP 1 [Value] FROM [SystemSettings] WHERE [Key] = 'social.linkedin'   AND [IsActive] = 1), [LinkedInUrl]),
    [YouTubeUrl]   = COALESCE((SELECT TOP 1 [Value] FROM [SystemSettings] WHERE [Key] = 'social.youtube'    AND [IsActive] = 1), [YouTubeUrl]),
    [TikTokUrl]    = COALESCE((SELECT TOP 1 [Value] FROM [SystemSettings] WHERE [Key] = 'social.tiktok'     AND [IsActive] = 1), [TikTokUrl]),
    [SnapchatUrl]  = COALESCE((SELECT TOP 1 [Value] FROM [SystemSettings] WHERE [Key] = 'social.snapchat'   AND [IsActive] = 1), [SnapchatUrl]),
    [RegistrationSuccessMessage]       = COALESCE((SELECT TOP 1 [Value] FROM [SystemSettings] WHERE [Key] = 'registration.successMessage.en' AND [IsActive] = 1), [RegistrationSuccessMessage]),
    [RegistrationSuccessMessageArabic] = COALESCE((SELECT TOP 1 [Value] FROM [SystemSettings] WHERE [Key] = 'registration.successMessage.ar' AND [IsActive] = 1), [RegistrationSuccessMessageArabic])
WHERE [Id] = '00000000-0000-0000-0000-000000000003';

UPDATE [SystemSettings] SET [IsActive] = 0
WHERE [Key] IN ('social.facebook','social.x','social.instagram','social.linkedin','social.youtube','social.tiktok','social.snapchat','registration.successMessage.en','registration.successMessage.ar');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // D-495 — restore the migrated SiteSettings keys on rollback.
            migrationBuilder.Sql(@"
UPDATE [SystemSettings] SET [IsActive] = 1
WHERE [Key] IN ('social.facebook','social.x','social.instagram','social.linkedin','social.youtube','social.tiktok','social.snapchat','registration.successMessage.en','registration.successMessage.ar');
");

            migrationBuilder.DropTable(
                name: "OrganizationAboutItems");

            migrationBuilder.DropTable(
                name: "OrganizationDetails");

            migrationBuilder.DropTable(
                name: "OrganizationProfile");
        }
    }
}
