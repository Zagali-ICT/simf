using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Abstractions;
using SIMF.Application.Files.Abstractions;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using FileServiceKind = SIMF.Common.Enums.FileService;

namespace SIMF.Api.Tests;

/// <summary>
/// The demo <c>@simf.local</c> account matrix, as a TEST FIXTURE.
///
/// <para>This used to live in <c>IdentitySeeder</c> and therefore ran inside
/// production startup behind an environment gate. The owner rule is that the
/// production seeder bootstraps identity and nothing else, so the matrix was
/// deleted from it; the integration suite needs the accounts (a gate operator, a
/// session moderator, one account per profile type), so the suite creates them
/// itself, here, against a database it owns and throws away.</para>
///
/// <para>Ordering matters and is the fixture's job: this runs AFTER
/// <c>SqlContentSeeder</c> (the profile types it looks up by name are seeded by
/// <c>docs/migrations/2026/SIMF_App_Lookups.sql</c>) and BEFORE
/// <c>DemoOperationalConfigSeeder</c> (which assigns <c>staff@simf.local</c> to
/// the demo gates and <c>moderator@simf.local</c> to the programme Q&amp;A
/// sessions).</para>
///
/// <para>Idempotent, and self-healing on the two images: an account is re-seeded
/// only when its pointer is empty <b>or</b> no longer resolves to content, so a
/// re-run never uploads twice yet a dangling pointer is repaired.</para>
/// </summary>
internal static class DemoAccountSeeder
{
    /// <summary>Saudi Arabia (Country.Id is the ISO-3166 numeric code, seeded
    /// through CountryConfiguration.HasData).</summary>
    private const int SaudiArabiaCountryId = 682;

    /// <summary>One account per user type / profile type so every role is
    /// exercisable from a fresh database. <c>ProfileType == null</c> means a CP
    /// admin (Administrator role, no profile). This is the SINGLE source of
    /// truth for the demo set - the interest and asset passes below read it too,
    /// so an account added here cannot be forgotten there.</summary>
    internal static readonly (string Email, string DisplayName, string EnName, string ArName,
        UserType UserType, string? ProfileType, string NationalId)[] Accounts =
    [
        ("admin@simf.local",     "Demo Admin",     "Demo Admin",     "مدير تجريبي",         UserType.Admin,   null,        "1000000001"),
        ("vvip@simf.local",      "Demo VVIP",      "Demo VVIP",      "شخصية بالغة الأهمية", UserType.Visitor, "VVIP",      "1000000002"),
        ("vip@simf.local",       "Demo VIP",       "Demo VIP",       "شخصية مهمة",          UserType.Visitor, "VIP",       "1000000003"),
        ("visitor@simf.local",   "Demo Visitor",   "Demo Visitor",   "زائر تجريبي",         UserType.Visitor, "Normal",    "1000000004"),
        ("staff@simf.local",     "Demo Staff",     "Demo Staff",     "موظف تجريبي",         UserType.Visitor, "Staff",     "1000000005"),
        ("moderator@simf.local", "Demo Moderator", "Demo Moderator", "منسّق تجريبي",        UserType.Visitor, "Moderator", "1000000006"),
        ("exhibitor@simf.local", "Demo Exhibitor", "Demo Exhibitor", "عارض تجريبي",         UserType.Visitor, "Exhibitor", "1000000007"),
        ("media@simf.local",     "Demo Media",     "Demo Media",     "إعلامي تجريبي",       UserType.Visitor, "Media",     "1000000008"),
        ("sponsor@simf.local",   "Demo Sponsor",   "Demo Sponsor",   "راعٍ تجريبي",         UserType.Visitor, "Sponsor",   "1000000009"),
    ];

    /// <summary>The demo accounts that carry a <see cref="UserProfile"/>
    /// (everything except the CP-only admin), i.e. the ones the profile
    /// completeness rule applies to.</summary>
    internal static IEnumerable<string> ProfileEmails =>
        Accounts.Where(demo => demo.ProfileType is not null).Select(demo => demo.Email);

    /// <summary>A placeholder portrait (64x64 PNG) stored as the demo accounts'
    /// face photo, and a placeholder ID card (96x64 PNG) stored as their identity
    /// document. Real bytes through the real upload pipeline, so a demo account
    /// satisfies the male-face + ID-document halves of
    /// <c>UserProfileService.IsProfileCompleteAsync</c> out of the box.</summary>
    private static readonly byte[] AvatarPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAIAAAAlC+aJAAAAwklEQVR42u3Yyw2EMAxF0dcGNVDF9F/BbFlTBNNARiEx+JMruYB7JCRia9s/qUcAAAAAAAAAAAAAADTne5z5AL/o1kQH/El/iKH3620Ncqk3NMir3sogx3oTw/KAyfp5AwAAngCT+kkDnxAAADwl0gPSv0Yr7AMVNrIKO3GFq0SRuxCnRQAAuE7zI1vmMZd4oTE8ScwwFKR+2KA49WMGhaofMCha/V2DAtbfMihmfb9hDYBLfacBQHyAY32PAQCA1QEXDhmFwqhDWYMAAAAASUVORK5CYII=");

    private static readonly byte[] IdDocumentPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAGAAAABACAIAAABqVuVZAAAAmElEQVR42u3cMQ2AQBBE0bNBkIAKSoTgDwUYwA0JCSUooNzA8JKv4DWz11zrhlEPNQSAAJUB7cepO0CAAAEClAHUT/M7AwQIECBAgAABAgQI0JeBvMUAAQIECBCgWKBl3fICBAgQoBQgKwYIECBALunynQIECBCgCCArBggQIECAAAECBAgQIECAAAECBOjHQPLzAiBAxUAX6SqBUHBIRtAAAAAASUVORK5CYII=");

    /// <summary>Creates (or repairs) the whole demo matrix inside
    /// <paramref name="services"/>, which must be a scoped provider.</summary>
    internal static async Task SeedAsync(
        IServiceProvider services, string password, CancellationToken cancellationToken = default)
    {
        await CreateAccountsAsync(services, password, cancellationToken);
        await LinkInterestsAsync(services, cancellationToken);
        await UploadProfileImagesAsync(services, cancellationToken);
    }

    /// <summary>One account per row of <see cref="Accounts"/>. Idempotent by
    /// email. An admin row carries the Administrator role and no profile; a
    /// visitor / partner row gets an <b>Approved</b> profile (Saudi nationality)
    /// with a minted QR badge, so it can pass a gate.</summary>
    private static async Task CreateAccountsAsync(
        IServiceProvider services, string password, CancellationToken cancellationToken)
    {
        var accounts = services.GetRequiredService<IUserAccountRepository>();
        var database = services.GetRequiredService<SimfAppDbContext>();
        var qrIdMinter = services.GetRequiredService<IQrIdMinter>();
        var pii = services.GetRequiredService<IPiiEncryptor>();
        var now = services.GetRequiredService<TimeProvider>().SimfNow();

        foreach (var demo in Accounts)
        {
            if (await accounts.FindByEmailAsync(demo.Email, cancellationToken) is not null)
            {
                continue; // idempotent - already seeded.
            }

            var user = new SimfUser
            {
                UserName = demo.Email,
                Email = demo.Email,
                EmailConfirmed = true,
                DisplayName = demo.DisplayName,
                AccountState = AccountState.Approved,
                UserType = demo.UserType,
                PasswordChangeRequired = false,
                CreatedAt = now,
                StateChangedAt = now,
            };

            var created = await accounts.CreateAsync(user, password, cancellationToken);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"The demo account {demo.Email} could not be created: "
                    + string.Join("; ", created.Errors.Select(error => error.Description))
                    + ". The fixture's generated password is validated against "
                    + "PasswordPolicy, so this is a real failure, not an unlucky draw.");
            }

            if (demo.UserType == UserType.Admin)
            {
                await accounts.AddToRoleAsync(user, AppRoles.Administrator, cancellationToken);
                continue;
            }

            var profileType = await database.ProfileTypes.SingleOrDefaultAsync(
                type => type.Name == demo.ProfileType, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"The demo account {demo.Email} needs the '{demo.ProfileType}' profile type, "
                    + "which docs/migrations/2026/SIMF_App_Lookups.sql seeds. Run the SQL "
                    + "content seed before this one.");

            var profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ProfileTypeId = profileType.Id,
                Name = demo.EnName,
                NameArabic = demo.ArName,
                Gender = Gender.Male,
                NationalityId = SaudiArabiaCountryId,
                IsSaudi = true,
                // Admitted outright: the QR minted below only works for an
                // approved attendee, and a demo that cannot pass a gate would be
                // useless for exactly the walkthroughs it exists to support.
                AdmissionState = AccountState.Approved,
                CreatedAt = now,
            };
            // Written through the same helper both real write paths use, so a
            // seeded profile carries a document row with a blind-index digest
            // rather than a bare number the duplicate-identity guard cannot see.
            ProfileIdentityStorage.SyncDocuments(profile, pii, demo.NationalId, null, null);
            await qrIdMinter.MintIfMissingAsync(profile, cancellationToken);
            database.UserProfiles.Add(profile);
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Gives the demo profiles a few OVERLAPPING interests so the
    /// "قابل أشخاص مثلك" recommender returns matches on a fresh database (it
    /// needs the caller AND at least one candidate to each carry an overlapping
    /// interest), and so the server completeness rule - which demands at least
    /// one interest - is satisfied. A profile that already has any interest is
    /// left untouched.</summary>
    private static async Task LinkInterestsAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var accounts = services.GetRequiredService<IUserAccountRepository>();
        var database = services.GetRequiredService<SimfAppDbContext>();

        string[] sharedInterestNames =
        [
            "Maritime Security",
            "Naval Defence Technologies",
            "Maritime Cybersecurity",
        ];
        var sharedInterests = await database.Interests
            .Where(interest => interest.IsActive && sharedInterestNames.Contains(interest.Name))
            .ToListAsync(cancellationToken);
        if (sharedInterests.Count == 0)
        {
            return; // the interest lookup is empty - nothing to link.
        }

        var linked = 0;
        foreach (var email in ProfileEmails)
        {
            var user = await accounts.FindByEmailAsync(email, cancellationToken);
            if (user is null) { continue; }

            var profile = await database.UserProfiles
                .Include(candidate => candidate.Interests)
                .SingleOrDefaultAsync(candidate => candidate.UserId == user.Id, cancellationToken);
            if (profile is null || profile.Interests.Count > 0)
            {
                continue; // no profile, or already has interests - idempotent skip.
            }

            foreach (var interest in sharedInterests)
            {
                profile.Interests.Add(interest);
            }
            linked++;
        }

        if (linked > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Gives every demo profile the two images the server completeness
    /// rule demands: the identity document (all registrants) and the face photo
    /// (required for a male registrant, and every demo profile is seeded
    /// <see cref="Gender.Male"/>). The bytes go through the ordinary
    /// <see cref="IFileService"/> pipeline - both are encrypted-at-rest services,
    /// so they cannot be pre-placed on disk like the public speaker photos.
    ///
    /// <para>An account is re-seeded when its pointer is empty <b>or</b> no
    /// longer resolves to content. Testing for emptiness alone is not enough: a
    /// non-empty pointer proves only that something was uploaded once, so a
    /// database restored past its file store left every demo account permanently
    /// broken - the pointer looked healthy, the seeder skipped it, and the image
    /// 404ed forever.</para>
    ///
    /// <para>Cross-DB safe: the App-side profile and the Identity-side user are
    /// saved through their own contexts, never in one transaction.</para></summary>
    private static async Task UploadProfileImagesAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var accounts = services.GetRequiredService<IUserAccountRepository>();
        var database = services.GetRequiredService<SimfAppDbContext>();
        var files = services.GetRequiredService<IFileService>();
        var timeProvider = services.GetRequiredService<TimeProvider>();

        foreach (var email in ProfileEmails)
        {
            var user = await accounts.FindByEmailAsync(email, cancellationToken);
            if (user is null) { continue; }

            var profile = await database.UserProfiles
                .SingleOrDefaultAsync(candidate => candidate.UserId == user.Id, cancellationToken);
            if (profile is null) { continue; }

            if (await NeedsReseedAsync(files, profile.IdImageFileId, cancellationToken))
            {
                var idDocument = await files.UploadAsync(
                    new UploadFileCommand(
                        FileServiceKind.IdDocument, user.Id, IdDocumentPng,
                        "demo-id-document.png", "image/png", user.Id, FailClosed: false),
                    cancellationToken);
                profile.IdImageFileId = idDocument.Id;
                profile.UpdatedAt = timeProvider.SimfNow();
                await database.SaveChangesAsync(cancellationToken);
            }

            if (await NeedsReseedAsync(files, user.AvatarFileId, cancellationToken))
            {
                var avatar = await files.UploadAsync(
                    new UploadFileCommand(
                        FileServiceKind.Avatar, user.Id, AvatarPng,
                        "demo-avatar.png", "image/png", user.Id, FailClosed: false),
                    cancellationToken);
                user.AvatarFileId = avatar.Id;
                await accounts.UpdateAsync(user).EnsureSuccessAsync();
            }
        }
    }

    /// <summary>True when an image pointer needs re-seeding: it is unset, or it
    /// is a well-formed id whose content has gone. The second case is the one a
    /// plain emptiness test misses.</summary>
    private static async Task<bool> NeedsReseedAsync(
        IFileService files, Guid? fileId, CancellationToken cancellationToken)
    {
        if (fileId is not { } id) { return true; }
        return !await files.ContentExistsAsync(id, cancellationToken);
    }
}
