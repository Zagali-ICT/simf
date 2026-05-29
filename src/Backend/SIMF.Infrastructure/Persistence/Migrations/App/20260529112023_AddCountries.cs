using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class AddCountries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PhonePrefix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Countries",
                columns: new[] { "Id", "Code", "CreatedAt", "DisplayOrder", "IsActive", "NameAr", "NameEn", "PhonePrefix", "UpdatedAt" },
                values: new object[,]
                {
                    { 32, "AR", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 630, true, "الأرجنتين", "Argentina", "+54", null },
                    { 36, "AU", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 520, true, "أستراليا", "Australia", "+61", null },
                    { 40, "AT", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 300, true, "النمسا", "Austria", "+43", null },
                    { 48, "BH", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 10, true, "البحرين", "Bahrain", "+973", null },
                    { 50, "BD", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 450, true, "بنغلاديش", "Bangladesh", "+880", null },
                    { 56, "BE", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 280, true, "بلجيكا", "Belgium", "+32", null },
                    { 76, "BR", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 610, true, "البرازيل", "Brazil", "+55", null },
                    { 124, "CA", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 600, true, "كندا", "Canada", "+1", null },
                    { 156, "CN", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 420, true, "الصين", "China", "+86", null },
                    { 208, "DK", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 330, true, "الدنمارك", "Denmark", "+45", null },
                    { 231, "ET", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 730, true, "إثيوبيا", "Ethiopia", "+251", null },
                    { 246, "FI", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 340, true, "فنلندا", "Finland", "+358", null },
                    { 250, "FR", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 220, true, "فرنسا", "France", "+33", null },
                    { 262, "DJ", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 170, true, "جيبوتي", "Djibouti", "+253", null },
                    { 275, "PS", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 160, true, "فلسطين", "Palestine", "+970", null },
                    { 276, "DE", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 230, true, "ألمانيا", "Germany", "+49", null },
                    { 300, "GR", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 360, true, "اليونان", "Greece", "+30", null },
                    { 356, "IN", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 430, true, "الهند", "India", "+91", null },
                    { 360, "ID", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 460, true, "إندونيسيا", "Indonesia", "+62", null },
                    { 364, "IR", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 140, true, "إيران", "Iran", "+98", null },
                    { 368, "IQ", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 150, true, "العراق", "Iraq", "+964", null },
                    { 372, "IE", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 350, true, "أيرلندا", "Ireland", "+353", null },
                    { 380, "IT", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 240, true, "إيطاليا", "Italy", "+39", null },
                    { 392, "JP", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 400, true, "اليابان", "Japan", "+81", null },
                    { 400, "JO", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 80, true, "الأردن", "Jordan", "+962", null },
                    { 404, "KE", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 720, true, "كينيا", "Kenya", "+254", null },
                    { 410, "KR", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 410, true, "كوريا الجنوبية", "South Korea", "+82", null },
                    { 414, "KW", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 20, true, "الكويت", "Kuwait", "+965", null },
                    { 422, "LB", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 90, true, "لبنان", "Lebanon", "+961", null },
                    { 458, "MY", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 470, true, "ماليزيا", "Malaysia", "+60", null },
                    { 484, "MX", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 620, true, "المكسيك", "Mexico", "+52", null },
                    { 504, "MA", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 100, true, "المغرب", "Morocco", "+212", null },
                    { 512, "OM", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 30, true, "عُمان", "Oman", "+968", null },
                    { 528, "NL", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 270, true, "هولندا", "Netherlands", "+31", null },
                    { 554, "NZ", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 530, true, "نيوزيلندا", "New Zealand", "+64", null },
                    { 566, "NG", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 710, true, "نيجيريا", "Nigeria", "+234", null },
                    { 578, "NO", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 320, true, "النرويج", "Norway", "+47", null },
                    { 586, "PK", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 440, true, "باكستان", "Pakistan", "+92", null },
                    { 608, "PH", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 510, true, "الفلبين", "Philippines", "+63", null },
                    { 620, "PT", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 260, true, "البرتغال", "Portugal", "+351", null },
                    { 634, "QA", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 40, true, "قطر", "Qatar", "+974", null },
                    { 643, "RU", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 370, true, "روسيا", "Russia", "+7", null },
                    { 682, "SA", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 50, true, "المملكة العربية السعودية", "Saudi Arabia", "+966", null },
                    { 702, "SG", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 480, true, "سنغافورة", "Singapore", "+65", null },
                    { 704, "VN", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 500, true, "فيتنام", "Viet Nam", "+84", null },
                    { 710, "ZA", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 700, true, "جنوب أفريقيا", "South Africa", "+27", null },
                    { 724, "ES", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 250, true, "إسبانيا", "Spain", "+34", null },
                    { 729, "SD", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 120, true, "السودان", "Sudan", "+249", null },
                    { 752, "SE", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 310, true, "السويد", "Sweden", "+46", null },
                    { 756, "CH", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 290, true, "سويسرا", "Switzerland", "+41", null },
                    { 764, "TH", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 490, true, "تايلاند", "Thailand", "+66", null },
                    { 784, "AE", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 60, true, "الإمارات العربية المتحدة", "United Arab Emirates", "+971", null },
                    { 792, "TR", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 110, true, "تركيا", "Türkiye", "+90", null },
                    { 818, "EG", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 70, true, "مصر", "Egypt", "+20", null },
                    { 826, "GB", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 210, true, "المملكة المتحدة", "United Kingdom", "+44", null },
                    { 840, "US", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 200, true, "الولايات المتحدة الأمريكية", "United States", "+1", null },
                    { 887, "YE", new DateTimeOffset(new DateTime(2026, 5, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 130, true, "اليمن", "Yemen", "+967", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Countries_Code",
                table: "Countries",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Countries_IsActive_DisplayOrder",
                table: "Countries",
                columns: new[] { "IsActive", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Countries");
        }
    }
}
