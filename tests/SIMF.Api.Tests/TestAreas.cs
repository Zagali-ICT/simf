// The closed vocabulary behind the [Trait("Area", ...)] tag every test class in
// this assembly carries, and the [Trait("Speed", ...)] tag that separates the
// classes paying for a migrate-and-seed cycle from the ones that do not.
//
// Why constants rather than string literals: this suite is ~2,470 tests over
// ~300 classes and takes ~35 minutes, so a mistyped area ("programe") would not
// surface until someone ran the whole gate and found their filter silently
// empty. A const makes the compiler reject it instead.
//
// Adding a value here is a deliberate act. A dozen-ish buckets is what makes
// the filter useful; a bucket per class selects nothing.
//
// Usage and the exact filter commands: docs/manuals/Test-Guide.md section 12.1.
namespace SIMF.Api.Tests;

public static class TestAreas
{
    /// <summary>Trait name carried by every test class in this assembly.</summary>
    public const string TraitName = "Area";

    /// <summary>Trait name separating seeded integration classes from fast ones.</summary>
    public const string SpeedTraitName = "Speed";

    /// <summary>The class boots the API through SimfApiFactory: migrate + seed, seconds per class.</summary>
    public const string Seeded = "Seeded";

    /// <summary>The class asserts in process without booting the API: milliseconds.</summary>
    public const string Fast = "Fast";

    /// <summary>AI providers, routing, prompts, the assistant, transcripts and AI filtering.</summary>
    public const string Ai = "ai";

    /// <summary>Badge issue, self-claim, badge sign-in, offline upload and the walk-in desk.</summary>
    public const string Badges = "badges";

    /// <summary>CMS, news, media, sponsors, booths, exhibitors, FAQ, venue map, site settings.</summary>
    public const string Content = "content";

    /// <summary>Central file store, assets, storage providers, upload scanning, encryption at rest.</summary>
    public const string Files = "files";

    /// <summary>Gate and hall scanning: arrival, attendance, movement, offline roster.</summary>
    public const string Gates = "gates";

    /// <summary>Sign-in, tokens, second factor, passwords, roles, permissions, device keys.</summary>
    public const string Identity = "identity";

    /// <summary>Business, speaker and delegation meetings, networking, leads and requests.</summary>
    public const string Meetings = "meetings";

    /// <summary>Workers, health, rate limits, notifications, email, audit, configuration, deployment.</summary>
    public const string Ops = "ops";

    /// <summary>Sessions, speakers, questions, summaries, ratings, programme days and editions.</summary>
    public const string Programme = "programme";

    /// <summary>User profiles, registration, admission approval, attendee grids and lookups.</summary>
    public const string Profiles = "profiles";

    /// <summary>Excel exports, admin exports, statistics and the reporting endpoints.</summary>
    public const string Reporting = "reporting";

    /// <summary>Seat reservations, seat changes, bookings, hall capacity and schedule.</summary>
    public const string Seats = "seats";

    /// <summary>The anonymous surface, permission matrix, PII, secrets, TLS and hardening ratchets.</summary>
    public const string Security = "security";

    /// <summary>Every legal Area value. The guard test rejects anything outside this set.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Ai,
        Badges,
        Content,
        Files,
        Gates,
        Identity,
        Meetings,
        Ops,
        Programme,
        Profiles,
        Reporting,
        Seats,
        Security,
    };

    /// <summary>Every legal Speed value. Guarded on the same terms as
    /// <see cref="All"/>: a class carrying an Area but no Speed is invisible to
    /// BOTH `Speed=Fast` and `Speed=Seeded`, so it disappears from every filtered
    /// run while still looking correctly labelled.</summary>
    public static readonly IReadOnlySet<string> AllSpeeds = new HashSet<string>(StringComparer.Ordinal)
    {
        Fast,
        Seeded,
    };
}
