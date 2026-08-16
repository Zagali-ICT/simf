// NCA A2-10 — proves the PII identifier columns are encrypted at rest (AES-GCM via
// IPiiEncryptor + an EF value converter) and round-trip transparently.
//
// The identity numbers moved off UserProfile.NationalId / IqamaNumber /
// PassportNumber and onto ProfileIdentityDocument.Number, which is a SEPARATE
// converter registration in SimfAppDbContext — the loop that encrypts the profile
// row is keyed on UserProfile property names and cannot see another entity. That
// registration is what this test guards, and its failure mode is silence: Decrypt
// returns unmarked plaintext unchanged, so a missed registration reads back
// perfectly while the number sits in the clear on disk.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Abstractions;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class PiiEncryptionTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public PiiEncryptionTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public void Encryptor_marks_ciphertext_round_trips_and_passes_legacy_plaintext()
    {
        using var scope = _factory.Services.CreateScope();
        var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptor>();

        Assert.True(pii.IsKeyConfigured);

        var encrypted = pii.Encrypt("1234567890");
        Assert.NotNull(encrypted);
        Assert.StartsWith("enc:1:", encrypted);
        Assert.NotEqual("1234567890", encrypted);
        Assert.Equal("1234567890", pii.Decrypt(encrypted));

        // Legacy plaintext (no marker) and null are returned unchanged.
        Assert.Equal("legacy-plain", pii.Decrypt("legacy-plain"));
        Assert.Null(pii.Encrypt(null));
        Assert.Null(pii.Decrypt(null));
    }

    [Fact]
    public async Task A_national_id_is_stored_encrypted_and_reads_back_decrypted()
    {
        const string number = "1234567890";
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            db.UserProfiles.Add(new UserProfile
            {
                Id = profileId,
                UserId = userId,
                Name = "Test User",
                NameArabic = "مستخدم اختبار",
                NationalityId = 682, // KSA — NationalityId is validated at the service layer, not a DB FK
                IsSaudi = true,
                IdentityDocuments =
                {
                    new ProfileIdentityDocument
                    {
                        Kind = IdentityDocumentKind.NationalId,
                        Number = number,
                        NumberHash = "pii-" + Guid.NewGuid().ToString("N"),
                    },
                },
            });
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

            // Raw read bypasses the value converter → the stored bytes are encrypted.
            var raw = await db.Database
                .SqlQuery<string>(
                    $"SELECT Number AS Value FROM ProfileIdentityDocuments WHERE ProfileId = {profileId}")
                .SingleAsync();
            Assert.StartsWith("enc:1:", raw);
            Assert.DoesNotContain(number, raw);

            // EF read goes through the converter → the original plaintext.
            var document = await db.UserProfiles
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .SelectMany(p => p.IdentityDocuments)
                .SingleAsync();
            Assert.Equal(number, document.Number);
        }
    }

    [Fact]
    public async Task A_mobile_number_is_stored_encrypted_and_reads_back_decrypted()
    {
        // The other half of the profile row's PII. It stayed on UserProfile when
        // the identity documents moved off it, so the converter loop there is
        // still load-bearing and still needs a raw-SQL witness of its own.
        const string mobile = "+966501234567";
        var userId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Name = "Mobile User",
                NameArabic = "مستخدم جوال",
                NationalityId = 682,
                IsSaudi = true,
                SaudiMobile = mobile,
            });
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

            var raw = await db.Database
                .SqlQuery<string>(
                    $"SELECT SaudiMobile AS Value FROM UserProfiles WHERE UserId = {userId}")
                .SingleAsync();
            Assert.StartsWith("enc:1:", raw);
            Assert.DoesNotContain(mobile, raw);

            var profile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == userId);
            Assert.Equal(mobile, profile.SaudiMobile);
        }
    }
}
