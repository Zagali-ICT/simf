using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Common;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-151 — Country lookup configuration. Id is manually-assigned
/// ISO 3166-1 numeric (not IDENTITY). Seed covers GCC + key naval / partner
/// countries; admins can add the remaining ISO list via the CP CRUD.</summary>
internal sealed class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("Countries");
        builder.HasKey(country => country.Id);
        // ValueGeneratedNever — Id is supplied by the seed / CP form, never
        // by SQL Server. Without this, EF would default to IDENTITY for
        // an int PK.
        builder.Property(country => country.Id).ValueGeneratedNever();

        builder.Property(country => country.Code).HasMaxLength(2).IsRequired();
        builder.Property(country => country.NameEn).HasMaxLength(128).IsRequired();
        builder.Property(country => country.NameAr).HasMaxLength(128).IsRequired();
        builder.Property(country => country.PhonePrefix).HasMaxLength(8);

        builder.HasIndex(country => country.Code).IsUnique();
        builder.HasIndex(country => new { country.IsActive, country.DisplayOrder });

        // Seed snapshot — 2026-05-29 baseline. Frozen creation timestamp
        // so the migration is deterministic. The admin CP CRUD can add
        // any other ISO 3166-1 row via the standard create flow.
        var seedAt = new DateTimeOffset(2026, 5, 29, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(SeedRows(seedAt));
    }

    private static Country[] SeedRows(DateTimeOffset seedAt) =>
    [
        // GCC — first, alphabetical for AR display
        Seed( 48, "BH", "Bahrain",              "البحرين",                "+973",   10, seedAt),
        Seed(414, "KW", "Kuwait",               "الكويت",                 "+965",   20, seedAt),
        Seed(512, "OM", "Oman",                 "عُمان",                  "+968",   30, seedAt),
        Seed(634, "QA", "Qatar",                "قطر",                    "+974",   40, seedAt),
        Seed(682, "SA", "Saudi Arabia",         "المملكة العربية السعودية", "+966",  50, seedAt),
        Seed(784, "AE", "United Arab Emirates", "الإمارات العربية المتحدة",  "+971",  60, seedAt),
        // MENA partners
        Seed(818, "EG", "Egypt",                "مصر",                    "+20",    70, seedAt),
        Seed(400, "JO", "Jordan",               "الأردن",                 "+962",   80, seedAt),
        Seed(422, "LB", "Lebanon",              "لبنان",                  "+961",   90, seedAt),
        Seed(504, "MA", "Morocco",              "المغرب",                 "+212",  100, seedAt),
        Seed(792, "TR", "Türkiye",              "تركيا",                  "+90",   110, seedAt),
        Seed(729, "SD", "Sudan",                "السودان",                "+249",  120, seedAt),
        Seed(887, "YE", "Yemen",                "اليمن",                  "+967",  130, seedAt),
        Seed(364, "IR", "Iran",                 "إيران",                  "+98",   140, seedAt),
        Seed(368, "IQ", "Iraq",                 "العراق",                 "+964",  150, seedAt),
        Seed(275, "PS", "Palestine",            "فلسطين",                 "+970",  160, seedAt),
        Seed(262, "DJ", "Djibouti",             "جيبوتي",                 "+253",  170, seedAt),
        // Major naval powers + Western partners
        Seed(840, "US", "United States",        "الولايات المتحدة الأمريكية", "+1",   200, seedAt),
        Seed(826, "GB", "United Kingdom",       "المملكة المتحدة",          "+44",   210, seedAt),
        Seed(250, "FR", "France",               "فرنسا",                  "+33",   220, seedAt),
        Seed(276, "DE", "Germany",              "ألمانيا",                "+49",   230, seedAt),
        Seed(380, "IT", "Italy",                "إيطاليا",                "+39",   240, seedAt),
        Seed(724, "ES", "Spain",                "إسبانيا",                "+34",   250, seedAt),
        Seed(620, "PT", "Portugal",             "البرتغال",               "+351",  260, seedAt),
        Seed(528, "NL", "Netherlands",          "هولندا",                 "+31",   270, seedAt),
        Seed( 56, "BE", "Belgium",              "بلجيكا",                 "+32",   280, seedAt),
        Seed(756, "CH", "Switzerland",          "سويسرا",                 "+41",   290, seedAt),
        Seed( 40, "AT", "Austria",              "النمسا",                 "+43",   300, seedAt),
        Seed(752, "SE", "Sweden",               "السويد",                 "+46",   310, seedAt),
        Seed(578, "NO", "Norway",               "النرويج",                "+47",   320, seedAt),
        Seed(208, "DK", "Denmark",              "الدنمارك",               "+45",   330, seedAt),
        Seed(246, "FI", "Finland",              "فنلندا",                 "+358",  340, seedAt),
        Seed(372, "IE", "Ireland",              "أيرلندا",                "+353",  350, seedAt),
        Seed(300, "GR", "Greece",               "اليونان",                "+30",   360, seedAt),
        Seed(643, "RU", "Russia",               "روسيا",                  "+7",    370, seedAt),
        // Asia-Pacific
        Seed(392, "JP", "Japan",                "اليابان",                "+81",   400, seedAt),
        Seed(410, "KR", "South Korea",          "كوريا الجنوبية",         "+82",   410, seedAt),
        Seed(156, "CN", "China",                "الصين",                  "+86",   420, seedAt),
        Seed(356, "IN", "India",                "الهند",                  "+91",   430, seedAt),
        Seed(586, "PK", "Pakistan",             "باكستان",                "+92",   440, seedAt),
        Seed( 50, "BD", "Bangladesh",           "بنغلاديش",               "+880",  450, seedAt),
        Seed(360, "ID", "Indonesia",            "إندونيسيا",              "+62",   460, seedAt),
        Seed(458, "MY", "Malaysia",             "ماليزيا",                "+60",   470, seedAt),
        Seed(702, "SG", "Singapore",            "سنغافورة",               "+65",   480, seedAt),
        Seed(764, "TH", "Thailand",             "تايلاند",                "+66",   490, seedAt),
        Seed(704, "VN", "Viet Nam",             "فيتنام",                 "+84",   500, seedAt),
        Seed(608, "PH", "Philippines",          "الفلبين",                "+63",   510, seedAt),
        Seed( 36, "AU", "Australia",            "أستراليا",               "+61",   520, seedAt),
        Seed(554, "NZ", "New Zealand",          "نيوزيلندا",              "+64",   530, seedAt),
        // Americas
        Seed(124, "CA", "Canada",               "كندا",                   "+1",    600, seedAt),
        Seed( 76, "BR", "Brazil",               "البرازيل",               "+55",   610, seedAt),
        Seed(484, "MX", "Mexico",               "المكسيك",                "+52",   620, seedAt),
        Seed( 32, "AR", "Argentina",            "الأرجنتين",              "+54",   630, seedAt),
        // Africa beyond MENA
        Seed(710, "ZA", "South Africa",         "جنوب أفريقيا",           "+27",   700, seedAt),
        Seed(566, "NG", "Nigeria",              "نيجيريا",                "+234",  710, seedAt),
        Seed(404, "KE", "Kenya",                "كينيا",                  "+254",  720, seedAt),
        Seed(231, "ET", "Ethiopia",             "إثيوبيا",                "+251",  730, seedAt),
    ];

    private static Country Seed(int id, string code, string nameEn,
        string nameAr, string phonePrefix, int displayOrder, DateTimeOffset at) =>
        new()
        {
            Id = id,
            Code = code,
            NameEn = nameEn,
            NameAr = nameAr,
            PhonePrefix = phonePrefix,
            DisplayOrder = displayOrder,
            IsActive = true,
            CreatedAt = at,
        };
}
