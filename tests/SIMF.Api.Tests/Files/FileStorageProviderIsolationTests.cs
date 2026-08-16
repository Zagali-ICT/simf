// Tests: this file IS the test — an architecture ratchet over the file
//        subsystem's storage seam, not a unit test of a type.
using System.Reflection;
using System.Text.RegularExpressions;
using SIMF.Application.Files.Abstractions;
using SIMF.Infrastructure.Files;
using Xunit;

namespace SIMF.Api.Tests.Files;

/// <summary>
/// <see cref="IFileService"/> is the only way to reach a stored file's bytes.
///
/// <para>This was a doc comment for months, and around a dozen call sites went
/// straight to <see cref="IFileStorageProvider"/> anyway. Each one silently lost
/// four things: the service-policy authorization check, the fail-closed SHA-256
/// re-check that refuses a tampered Confidential+ blob, the
/// <c>FileDownloaded</c> / <c>FileAccessDenied</c> audit rows, and the nosniff +
/// forced-attachment response hardening. The worst of them served identity
/// documents on admin routes that the proper download path would have refused.
/// </para>
///
/// <para><b>What the compiler already enforces, and what it cannot.</b> The
/// provider is <c>internal</c> to SIMF.Application, so a bypass from SIMF.Api,
/// the Control Panel, the Website or any other assembly does not compile. That
/// is the real fix and it is structural. But C# visibility stops there: the
/// interface is declared in SIMF.Application and
/// <c>InternalsVisibleTo("SIMF.Infrastructure")</c> extends the same visibility
/// to the composition root — so every type in those two assemblies, which is
/// where the services live, can still take the provider as a dependency and
/// compile cleanly. <c>file</c> scope is per-file and cannot span the interface,
/// its implementation and the DI registration; a nested private interface cannot
/// be registered in DI at all. There is no visibility keyword that expresses
/// "only the file subsystem", so this fixture expresses it instead. Saying that
/// plainly is the point — the alternative was a comment asserting a property
/// nothing checks, which is exactly how the twelve bypasses accumulated.</para>
///
/// <para>Need something the storage layer can do that the service does not
/// expose? Add the method to <see cref="IFileService"/>, keeping it path-free and
/// backend-neutral so an S3 or Blob provider stays a DI change with no caller
/// change. Do not widen the allow-list below.</para>
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Files)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class FileStorageProviderIsolationTests
{
    /// <summary>The one CONSUMER allowed to hold the storage seam: the file
    /// service itself, which is what every other caller is supposed to go through.
    ///
    /// <para>An IMPLEMENTATION of the provider is not listed and never needs to be
    /// — those are filtered out wholesale, because a provider is the seam rather
    /// than a way around it. That is not a loophole (implementing the interface
    /// means writing a storage backend, not calling one) and it keeps the promise
    /// the interface makes: swapping the filesystem backend for S3 or Blob is a DI
    /// change, with no allow-list to edit here. It also has to be filtered rather
    /// than ignored, because the compiler gives a provider's async methods a
    /// nested state machine holding a <c>this</c> field of the provider's own
    /// type, which otherwise reads as a dependency on itself.</para></summary>
    private static readonly string[] AllowedProviderHolders =
    [
        typeof(StoredFileService).FullName!,
    ];

    private static Assembly[] SubsystemAssemblies =>
    [
        typeof(IFileStorageProvider).Assembly,
        typeof(StoredFileService).Assembly,
    ];

    /// <summary>Constructor-injection guard. Every type in SIMF.Application and
    /// SIMF.Infrastructure is checked, because those are precisely the two
    /// assemblies where the internal provider is still visible.</summary>
    [Fact]
    public void Only_the_file_service_may_depend_on_the_storage_provider()
    {
        var offenders = SubsystemAssemblies
            .SelectMany(TypesOf)
            .Where(DependsOnProvider)
            .Select(OutermostDeclaringType)
            .Where(type => !IsProvider(type))
            .Select(type => type.FullName ?? type.Name)
            .Distinct(StringComparer.Ordinal)
            .Where(name => !AllowedProviderHolders.Contains(name, StringComparer.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "A type outside the file subsystem takes IFileStorageProvider as a "
            + "dependency. Going straight to the provider skips the service-policy "
            + "check, the fail-closed SHA-256 re-check on Confidential+ files, the "
            + "download / access-denied audit rows and the nosniff + attachment "
            + "hardening — a tampered identity document would be served. Call "
            + "IFileService instead; if it cannot do what you need, add a "
            + "path-free, backend-neutral method to it.\n"
            + string.Join('\n', offenders.Select(name => "  " + name)));
    }

    /// <summary>Service-locator guard. Reflection cannot see a
    /// <c>GetRequiredService&lt;IFileStorageProvider&gt;()</c> call — it leaves no
    /// constructor parameter and no field — so that shape is caught in the source
    /// instead. The whole of <c>src</c> is scanned rather than just the two
    /// assemblies that can see the type today, so widening
    /// <c>InternalsVisibleTo</c> later does not quietly open the hole.</summary>
    [Fact]
    public void No_source_file_resolves_the_storage_provider_from_the_container()
    {
        var offenders = SourceFilesUnder("src")
            .Where(path => ContainerResolve.IsMatch(StripLineComments(File.ReadAllText(path))))
            .Select(path => Path.GetRelativePath(RepoRoot(), path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "A source file resolves IFileStorageProvider from the DI container. "
            + "That is the same bypass as injecting it — the policy check, the "
            + "integrity re-check, the audit rows and the response hardening all "
            + "live in IFileService. Resolve IFileService instead. The composition "
            + "root has no exemption here: it registers the provider with "
            + "AddSingleton<TService, TImplementation>() and never resolves it.\n"
            + string.Join('\n', offenders.Select(path => "  " + path)));
    }

    // Matches the resolve shape with or without a namespace qualifier, and
    // whichever of Get/GetRequired/GetServices was used.
    private static readonly Regex ContainerResolve = new(
        @"Get(Required)?Services?\s*<\s*(?:[A-Za-z0-9_.]*\.)?IFileStorageProvider\s*>",
        RegexOptions.Compiled);

    private static bool DependsOnProvider(Type type)
    {
        const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        const BindingFlags Constructors = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance;

        return type.GetConstructors(Constructors)
                   .Any(constructor => constructor.GetParameters().Any(p => IsProvider(p.ParameterType)))
               || type.GetFields(Members).Any(field => IsProvider(field.FieldType))
               || type.GetProperties(Members).Any(property => IsProvider(property.PropertyType));
    }

    /// <summary>True for the interface and for any implementation of it, so
    /// naming the concrete <c>FilesystemFileStorageProvider</c> is not a way
    /// around the guard.</summary>
    private static bool IsProvider(Type type) => typeof(IFileStorageProvider).IsAssignableFrom(type);

    /// <summary>A primary constructor's backing field, an async state machine and
    /// a closure display class are all nested inside the type that really holds
    /// the dependency, so offenders are reported (and allow-listed) by the type a
    /// reader would recognise.</summary>
    private static Type OutermostDeclaringType(Type type)
    {
        var current = type;
        while (current.DeclaringType is not null)
        {
            current = current.DeclaringType;
        }
        return current;
    }

    private static IEnumerable<Type> TypesOf(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            // A type that will not load cannot be a bypass; check the rest
            // rather than failing the guard for an unrelated reason.
            return exception.Types.Where(type => type is not null).Select(type => type!);
        }
    }

    private static IEnumerable<string> SourceFilesUnder(string relativeDirectory)
    {
        var root = Path.Combine(RepoRoot(), relativeDirectory);
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            : [];
    }

    /// <summary>Drops <c>//</c> and <c>///</c> lines so prose naming the pattern —
    /// including the doc comments that explain why it is banned — does not trip
    /// the scan, while code that actually resolves the provider does.</summary>
    private static string StripLineComments(string source) =>
        string.Join('\n', source
            .Split('\n')
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "SIMF.slnx")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
