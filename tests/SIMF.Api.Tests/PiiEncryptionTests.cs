// NCA A2-10 — proves the UserProfile PII identifier columns are encrypted at rest
// (AES-GCM via IPiiEncryptor + an EF value converter) and round-trip transparently.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Abstractions;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Security)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
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
    public async Task NationalId_is_stored_encrypted_and_reads_back_decrypted()
    {
        var userId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Name = "Test User",
                NameArabic = "مستخدم اختبار",
                NationalityId = 682, // KSA — NationalityId is validated at the service layer, not a DB FK
                IsSaudi = true,
                NationalId = "1234567890",
            });
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

            // Raw read bypasses the value converter → the stored bytes are encrypted.
            var raw = await db.Database
                .SqlQuery<string>($"SELECT NationalId AS Value FROM UserProfiles WHERE UserId = {userId}")
                .SingleAsync();
            Assert.StartsWith("enc:1:", raw);
            Assert.DoesNotContain("1234567890", raw);

            // EF read goes through the converter → the original plaintext.
            var profile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == userId);
            Assert.Equal("1234567890", profile.NationalId);
        }
    }
}
