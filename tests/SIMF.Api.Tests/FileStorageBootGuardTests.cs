using Microsoft.Extensions.Options;
using SIMF.Common.Options;
using SIMF.Infrastructure;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>Unit cover for the file-store encryption boot guard. The cipher
/// refuses both of these configurations too, but it is not constructed until the
/// first upload, so without the guard a broken key ring survives the deploy and
/// surfaces hours later as a failed upload. No host, DB or filesystem.</summary>
[Trait(TestAreas.TraitName, TestAreas.Files)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class FileStorageBootGuardTests
{
    // 32 distinct bytes per seed → a valid, distinct AES-256 key. The guard never
    // decodes the value, but a realistic one keeps the fixture honest against the
    // cipher it is mirroring.
    private static string Key(byte seed) =>
        Convert.ToBase64String(Enumerable.Range(seed, 32).Select(i => (byte)i).ToArray());

    private static IServiceProvider Services(FileStorageOptions options) =>
        new FileStorageOptionsProvider(Options.Create(options));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_rotation_whose_previous_version_equals_the_active_one_refuses_to_boot(
        bool isProduction)
    {
        var services = Services(new FileStorageOptions
        {
            EncryptionKey = Key(0),
            KekVersion = 2,
            PreviousEncryptionKey = Key(40),
            PreviousKekVersion = 2,
        });

        var ex = Assert.Throws<InvalidOperationException>(
            () => DependencyInjection.EnsureFileStorageEncryptionConfigured(isProduction, services));

        Assert.Contains("FileStorage:PreviousKekVersion", ex.Message, StringComparison.Ordinal);
        Assert.Contains("FileStorage:KekVersion", ex.Message, StringComparison.Ordinal);
        // The guard names the setting to change and the version it found. It must
        // never put key material into a boot log, which anyone reading a failed
        // start can see.
        Assert.DoesNotContain(Key(0), ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Key(40), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rotation_with_two_distinct_versions_boots()
    {
        var services = Services(new FileStorageOptions
        {
            EncryptionKey = Key(0),
            KekVersion = 2,
            PreviousEncryptionKey = Key(40),
            PreviousKekVersion = 1,
        });

        Assert.Null(Record.Exception(
            () => DependencyInjection.EnsureFileStorageEncryptionConfigured(true, services)));
    }

    [Fact]
    public void Matching_versions_with_no_previous_key_boot_because_no_rotation_is_in_flight()
    {
        // The default deployment shape: one key, and the previous-version stamp
        // left wherever it happens to sit. There is no second key to collide with,
        // so there is nothing to refuse.
        var services = Services(new FileStorageOptions
        {
            EncryptionKey = Key(0),
            KekVersion = 1,
            PreviousEncryptionKey = string.Empty,
            PreviousKekVersion = 1,
        });

        Assert.Null(Record.Exception(
            () => DependencyInjection.EnsureFileStorageEncryptionConfigured(true, services)));
    }

    [Fact]
    public void A_missing_key_refuses_to_boot_in_production()
    {
        var services = Services(new FileStorageOptions { EncryptionKey = string.Empty });

        var ex = Assert.Throws<InvalidOperationException>(
            () => DependencyInjection.EnsureFileStorageEncryptionConfigured(true, services));

        Assert.Contains("FileStorage:EncryptionKey", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_key_outside_production_boots()
    {
        // A developer machine legitimately has no KEK until it uploads something,
        // which is why this half of the guard is production-only and the
        // version-collision half is not.
        var services = Services(new FileStorageOptions { EncryptionKey = string.Empty });

        Assert.Null(Record.Exception(
            () => DependencyInjection.EnsureFileStorageEncryptionConfigured(false, services)));
    }

    /// <summary>The guard resolves exactly one service, so the fixture supplies
    /// exactly that one. A stub keeps the test in-process, with no container and no
    /// host behind it.</summary>
    private sealed class FileStorageOptionsProvider(IOptions<FileStorageOptions> options)
        : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IOptions<FileStorageOptions>) ? options : null;
    }
}
