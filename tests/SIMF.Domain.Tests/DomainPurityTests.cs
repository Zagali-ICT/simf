using System.Linq;
using System.Reflection;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Domain.Tests;

/// <summary>
/// R5g — D-093: architectural unit tests that pin the "Domain has no
/// framework dependencies" contract. Pre-R5g the Domain project referenced
/// <c>Microsoft.Extensions.Identity.Stores</c> so <see cref="SimfUser"/>
/// could inherit <c>IdentityUser&lt;Guid&gt;</c>; R5a → R5f moved the
/// EF-tracked persistence shape into Infrastructure's
/// <c>IdentitySimfUser</c> shim and turned <see cref="SimfUser"/> into a
/// POCO. These tests fail loudly if a future commit accidentally
/// re-introduces the dependency (e.g. by adding the package reference
/// back to <c>SIMF.Domain.csproj</c>, or by inheriting from
/// <c>IdentityUser&lt;TKey&gt;</c>, or by typing a Domain method
/// parameter as <c>DbContext</c>).
/// </summary>
public sealed class DomainPurityTests
{
    private static readonly Assembly DomainAssembly = typeof(SimfUser).Assembly;

    [Fact]
    public void Domain_assembly_does_not_reference_AspNetCore_Identity()
    {
        var referenced = DomainAssembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            referenced,
            name => name.StartsWith(
                "Microsoft.AspNetCore.Identity",
                System.StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            referenced,
            name => name.StartsWith(
                "Microsoft.Extensions.Identity",
                System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Domain_assembly_does_not_reference_EntityFrameworkCore()
    {
        var referenced = DomainAssembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            referenced,
            name => name.StartsWith(
                "Microsoft.EntityFrameworkCore",
                System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SimfUser_is_a_POCO_not_inheriting_any_framework_type()
    {
        var simfUser = typeof(SimfUser);

        Assert.Equal(typeof(object), simfUser.BaseType);
    }
}
