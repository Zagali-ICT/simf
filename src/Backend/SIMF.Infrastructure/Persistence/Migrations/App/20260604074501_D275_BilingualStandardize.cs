using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D275_BilingualStandardize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaItems_IsActive_AlbumEn_DisplayOrder",
                table: "MediaItems");

            migrationBuilder.RenameColumn(
                name: "NameEn",
                table: "SessionCategories",
                newName: "NameArabic");

            migrationBuilder.RenameColumn(
                name: "NameAr",
                table: "SessionCategories",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "TitleEn",
                table: "News",
                newName: "TitleArabic");

            migrationBuilder.RenameColumn(
                name: "TitleAr",
                table: "News",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "ExcerptEn",
                table: "News",
                newName: "ExcerptArabic");

            migrationBuilder.RenameColumn(
                name: "ExcerptAr",
                table: "News",
                newName: "Excerpt");

            migrationBuilder.RenameColumn(
                name: "CategoryEn",
                table: "News",
                newName: "CategoryArabic");

            migrationBuilder.RenameColumn(
                name: "CategoryAr",
                table: "News",
                newName: "Category");

            migrationBuilder.RenameColumn(
                name: "BodyEn",
                table: "News",
                newName: "BodyArabic");

            migrationBuilder.RenameColumn(
                name: "BodyAr",
                table: "News",
                newName: "Body");

            migrationBuilder.RenameColumn(
                name: "NameEn",
                table: "MediaPartners",
                newName: "NameArabic");

            migrationBuilder.RenameColumn(
                name: "NameAr",
                table: "MediaPartners",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "TitleEn",
                table: "MediaItems",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "AlbumEn",
                table: "MediaItems",
                newName: "AlbumArabic");

            migrationBuilder.RenameColumn(
                name: "AlbumAr",
                table: "MediaItems",
                newName: "Album");

            migrationBuilder.RenameColumn(
                name: "QuestionEn",
                table: "FaqEntries",
                newName: "QuestionArabic");

            migrationBuilder.RenameColumn(
                name: "QuestionAr",
                table: "FaqEntries",
                newName: "Question");

            migrationBuilder.RenameColumn(
                name: "AnswerEn",
                table: "FaqEntries",
                newName: "AnswerArabic");

            migrationBuilder.RenameColumn(
                name: "AnswerAr",
                table: "FaqEntries",
                newName: "Answer");

            migrationBuilder.RenameColumn(
                name: "NameEn",
                table: "Countries",
                newName: "NameArabic");

            migrationBuilder.RenameColumn(
                name: "NameAr",
                table: "Countries",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "ContentEn",
                table: "ContentBlocks",
                newName: "ContentArabic");

            migrationBuilder.RenameColumn(
                name: "ContentAr",
                table: "ContentBlocks",
                newName: "Content");

            migrationBuilder.RenameColumn(
                name: "SectorEn",
                table: "Booths",
                newName: "SectorArabic");

            migrationBuilder.RenameColumn(
                name: "SectorAr",
                table: "Booths",
                newName: "Sector");

            migrationBuilder.RenameColumn(
                name: "NameEn",
                table: "Booths",
                newName: "NameArabic");

            migrationBuilder.RenameColumn(
                name: "NameAr",
                table: "Booths",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "ExhibitorNameEn",
                table: "Booths",
                newName: "ExhibitorNameArabic");

            migrationBuilder.RenameColumn(
                name: "ExhibitorNameAr",
                table: "Booths",
                newName: "ExhibitorName");

            migrationBuilder.RenameColumn(
                name: "DescriptionEn",
                table: "Booths",
                newName: "DescriptionArabic");

            migrationBuilder.RenameColumn(
                name: "DescriptionAr",
                table: "Booths",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "TitleEn",
                table: "Banners",
                newName: "TitleArabic");

            migrationBuilder.RenameColumn(
                name: "TitleAr",
                table: "Banners",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "BodyEn",
                table: "Banners",
                newName: "BodyArabic");

            migrationBuilder.RenameColumn(
                name: "BodyAr",
                table: "Banners",
                newName: "Body");

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 32,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Argentina", "الأرجنتين" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 36,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Australia", "أستراليا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 40,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Austria", "النمسا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 48,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Bahrain", "البحرين" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 50,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Bangladesh", "بنغلاديش" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 56,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Belgium", "بلجيكا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 76,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Brazil", "البرازيل" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 124,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Canada", "كندا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 156,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "China", "الصين" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 208,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Denmark", "الدنمارك" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 231,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Ethiopia", "إثيوبيا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 246,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Finland", "فنلندا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 250,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "France", "فرنسا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 262,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Djibouti", "جيبوتي" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 275,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Palestine", "فلسطين" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 276,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Germany", "ألمانيا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 300,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Greece", "اليونان" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 356,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "India", "الهند" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 360,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Indonesia", "إندونيسيا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 364,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Iran", "إيران" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 368,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Iraq", "العراق" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 372,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Ireland", "أيرلندا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 380,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Italy", "إيطاليا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 392,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Japan", "اليابان" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 400,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Jordan", "الأردن" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 404,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Kenya", "كينيا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 410,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "South Korea", "كوريا الجنوبية" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 414,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Kuwait", "الكويت" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 422,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Lebanon", "لبنان" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 458,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Malaysia", "ماليزيا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 484,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Mexico", "المكسيك" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 504,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Morocco", "المغرب" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 512,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Oman", "عُمان" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 528,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Netherlands", "هولندا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 554,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "New Zealand", "نيوزيلندا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 566,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Nigeria", "نيجيريا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 578,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Norway", "النرويج" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 586,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Pakistan", "باكستان" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 608,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Philippines", "الفلبين" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 620,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Portugal", "البرتغال" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 634,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Qatar", "قطر" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 643,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Russia", "روسيا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 682,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Saudi Arabia", "المملكة العربية السعودية" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 702,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Singapore", "سنغافورة" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 704,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Viet Nam", "فيتنام" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 710,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "South Africa", "جنوب أفريقيا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 724,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Spain", "إسبانيا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 729,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Sudan", "السودان" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 752,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Sweden", "السويد" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 756,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Switzerland", "سويسرا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 764,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Thailand", "تايلاند" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 784,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "United Arab Emirates", "الإمارات العربية المتحدة" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 792,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Türkiye", "تركيا" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 818,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Egypt", "مصر" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 826,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "United Kingdom", "المملكة المتحدة" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 840,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "United States", "الولايات المتحدة الأمريكية" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 887,
                columns: new[] { "Name", "NameArabic" },
                values: new object[] { "Yemen", "اليمن" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_IsActive_Album_DisplayOrder",
                table: "MediaItems",
                columns: new[] { "IsActive", "Album", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaItems_IsActive_Album_DisplayOrder",
                table: "MediaItems");

            migrationBuilder.RenameColumn(
                name: "NameArabic",
                table: "SessionCategories",
                newName: "NameEn");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "SessionCategories",
                newName: "NameAr");

            migrationBuilder.RenameColumn(
                name: "TitleArabic",
                table: "News",
                newName: "TitleEn");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "News",
                newName: "TitleAr");

            migrationBuilder.RenameColumn(
                name: "ExcerptArabic",
                table: "News",
                newName: "ExcerptEn");

            migrationBuilder.RenameColumn(
                name: "Excerpt",
                table: "News",
                newName: "ExcerptAr");

            migrationBuilder.RenameColumn(
                name: "CategoryArabic",
                table: "News",
                newName: "CategoryEn");

            migrationBuilder.RenameColumn(
                name: "Category",
                table: "News",
                newName: "CategoryAr");

            migrationBuilder.RenameColumn(
                name: "BodyArabic",
                table: "News",
                newName: "BodyEn");

            migrationBuilder.RenameColumn(
                name: "Body",
                table: "News",
                newName: "BodyAr");

            migrationBuilder.RenameColumn(
                name: "NameArabic",
                table: "MediaPartners",
                newName: "NameEn");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "MediaPartners",
                newName: "NameAr");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "MediaItems",
                newName: "TitleEn");

            migrationBuilder.RenameColumn(
                name: "AlbumArabic",
                table: "MediaItems",
                newName: "AlbumEn");

            migrationBuilder.RenameColumn(
                name: "Album",
                table: "MediaItems",
                newName: "AlbumAr");

            migrationBuilder.RenameColumn(
                name: "QuestionArabic",
                table: "FaqEntries",
                newName: "QuestionEn");

            migrationBuilder.RenameColumn(
                name: "Question",
                table: "FaqEntries",
                newName: "QuestionAr");

            migrationBuilder.RenameColumn(
                name: "AnswerArabic",
                table: "FaqEntries",
                newName: "AnswerEn");

            migrationBuilder.RenameColumn(
                name: "Answer",
                table: "FaqEntries",
                newName: "AnswerAr");

            migrationBuilder.RenameColumn(
                name: "NameArabic",
                table: "Countries",
                newName: "NameEn");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Countries",
                newName: "NameAr");

            migrationBuilder.RenameColumn(
                name: "ContentArabic",
                table: "ContentBlocks",
                newName: "ContentEn");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "ContentBlocks",
                newName: "ContentAr");

            migrationBuilder.RenameColumn(
                name: "SectorArabic",
                table: "Booths",
                newName: "SectorEn");

            migrationBuilder.RenameColumn(
                name: "Sector",
                table: "Booths",
                newName: "SectorAr");

            migrationBuilder.RenameColumn(
                name: "NameArabic",
                table: "Booths",
                newName: "NameEn");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Booths",
                newName: "NameAr");

            migrationBuilder.RenameColumn(
                name: "ExhibitorNameArabic",
                table: "Booths",
                newName: "ExhibitorNameEn");

            migrationBuilder.RenameColumn(
                name: "ExhibitorName",
                table: "Booths",
                newName: "ExhibitorNameAr");

            migrationBuilder.RenameColumn(
                name: "DescriptionArabic",
                table: "Booths",
                newName: "DescriptionEn");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Booths",
                newName: "DescriptionAr");

            migrationBuilder.RenameColumn(
                name: "TitleArabic",
                table: "Banners",
                newName: "TitleEn");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Banners",
                newName: "TitleAr");

            migrationBuilder.RenameColumn(
                name: "BodyArabic",
                table: "Banners",
                newName: "BodyEn");

            migrationBuilder.RenameColumn(
                name: "Body",
                table: "Banners",
                newName: "BodyAr");

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 32,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "الأرجنتين", "Argentina" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 36,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "أستراليا", "Australia" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 40,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "النمسا", "Austria" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 48,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "البحرين", "Bahrain" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 50,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "بنغلاديش", "Bangladesh" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 56,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "بلجيكا", "Belgium" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 76,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "البرازيل", "Brazil" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 124,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "كندا", "Canada" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 156,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "الصين", "China" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 208,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "الدنمارك", "Denmark" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 231,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "إثيوبيا", "Ethiopia" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 246,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "فنلندا", "Finland" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 250,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "فرنسا", "France" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 262,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "جيبوتي", "Djibouti" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 275,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "فلسطين", "Palestine" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 276,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "ألمانيا", "Germany" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 300,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "اليونان", "Greece" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 356,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "الهند", "India" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 360,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "إندونيسيا", "Indonesia" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 364,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "إيران", "Iran" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 368,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "العراق", "Iraq" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 372,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "أيرلندا", "Ireland" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 380,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "إيطاليا", "Italy" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 392,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "اليابان", "Japan" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 400,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "الأردن", "Jordan" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 404,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "كينيا", "Kenya" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 410,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "كوريا الجنوبية", "South Korea" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 414,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "الكويت", "Kuwait" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 422,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "لبنان", "Lebanon" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 458,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "ماليزيا", "Malaysia" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 484,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "المكسيك", "Mexico" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 504,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "المغرب", "Morocco" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 512,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "عُمان", "Oman" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 528,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "هولندا", "Netherlands" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 554,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "نيوزيلندا", "New Zealand" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 566,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "نيجيريا", "Nigeria" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 578,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "النرويج", "Norway" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 586,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "باكستان", "Pakistan" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 608,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "الفلبين", "Philippines" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 620,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "البرتغال", "Portugal" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 634,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "قطر", "Qatar" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 643,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "روسيا", "Russia" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 682,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "المملكة العربية السعودية", "Saudi Arabia" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 702,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "سنغافورة", "Singapore" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 704,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "فيتنام", "Viet Nam" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 710,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "جنوب أفريقيا", "South Africa" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 724,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "إسبانيا", "Spain" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 729,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "السودان", "Sudan" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 752,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "السويد", "Sweden" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 756,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "سويسرا", "Switzerland" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 764,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "تايلاند", "Thailand" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 784,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "الإمارات العربية المتحدة", "United Arab Emirates" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 792,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "تركيا", "Türkiye" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 818,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "مصر", "Egypt" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 826,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "المملكة المتحدة", "United Kingdom" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 840,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "الولايات المتحدة الأمريكية", "United States" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 887,
                columns: new[] { "NameAr", "NameEn" },
                values: new object[] { "اليمن", "Yemen" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_IsActive_AlbumEn_DisplayOrder",
                table: "MediaItems",
                columns: new[] { "IsActive", "AlbumEn", "DisplayOrder" });
        }
    }
}
