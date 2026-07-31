// Architecture guard — Arch SEV-1.1 (Domain-on-Identity coupling).
//
// WHY THIS FILE IS WRITTEN BACKWARDS. Decisions D-090..D-093 record the R5
// pure-POCO split as landed, and docs/SIMF-Architecture-Refactor-Plan.md
// carried "DONE - D-090->093" and "Arch SEV-1.1 fully closed" for two months.
// None of that code is on this branch: SimfUser still derives from
// IdentityUser<Guid>, SimfRole from IdentityRole<Guid>, and SIMF.Domain.csproj
// still references Microsoft.Extensions.Identity.Stores. D-093 also claimed a
// DomainPurityTests fixture existed; it did not, which is precisely how the
// claim survived unchallenged.
//
// The honest test asserts SIMF.Domain is Identity-free - and it would fail
// today. A permanently red test is not a guard, it is noise that a suite learns
// to ignore, and it cannot be committed to a branch whose build gate is green.
// So the first three Facts assert the CURRENT, KNOWN-BAD state instead. They
// pass now, and they turn red the moment somebody actually does the split -
// which is the point: the person doing it is then forced to flip these
// assertions and update the plan's status table in the same commit, and the
// "it's closed" claim can never drift away from the code again.
//
// TO FLIP THEM (when R5 is genuinely done):
//   1. SimfUser.BaseType   -> typeof(object)          (drop IdentityUser<Guid>)
//   2. SimfRole.BaseType   -> typeof(object)          (or delete SimfRole from
//                                                      Domain per R5g)
//   3. Domain's referenced assemblies -> no Microsoft.*Identity* entry
//      (drop the PackageReference from SIMF.Domain.csproj)
//   4. Update the R5 rows in docs/SIMF-Architecture-Refactor-Plan.md.
// Fact 4 is NOT inverted: it is green today and must stay green, so the leak
// cannot widen past the two types below while SEV-1.1 is open.
using System.Reflection;
using Microsoft.AspNetCore.Identity;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class DomainPurityTests
{
    private const string PlanDoc = "docs/SIMF-Architecture-Refactor-Plan.md";

    /// <summary>The two Domain types that are allowed to derive from an
    /// ASP.NET Identity base while Arch SEV-1.1 is open. Nothing may be added
    /// to this list — the point of the guard is that the leak stops here.</summary>
    private static readonly string[] KnownIdentityDerivedDomainTypes =
    [
        "SIMF.Domain.IdentityAccess.SimfRole",
        "SIMF.Domain.IdentityAccess.SimfUser",
    ];

    private static Assembly DomainAssembly => typeof(SimfUser).Assembly;

    [Fact]
    public void Domain_SimfUser_still_derives_from_AspNet_Identity()
    {
        // BaseType is never null for a class, so the ! is a declaration that
        // this path cannot be hit, not a hidden assumption.
        Assert.Equal(typeof(IdentityUser<Guid>), typeof(SimfUser).BaseType!);
    }

    [Fact]
    public void Domain_SimfRole_still_derives_from_AspNet_Identity()
    {
        Assert.Equal(typeof(IdentityRole<Guid>), typeof(SimfRole).BaseType!);
    }

    [Fact]
    public void Domain_assembly_still_references_AspNet_Identity()
    {
        var identityReferences = IdentityReferencesOf(DomainAssembly);

        Assert.True(
            identityReferences.Count > 0,
            "SIMF.Domain no longer references ASP.NET Identity — Arch SEV-1.1 "
            + "looks CLOSED. That is the goal, so this is good news, but this "
            + "fixture is deliberately inverted and now needs flipping: assert "
            + "the reference is ABSENT, flip the two BaseType Facts to "
            + "typeof(object), and update the R5 rows in " + PlanDoc + ".");
    }

    /// <summary>Forward guard — green today, and it must stay green. The two
    /// known offenders are grandfathered; a third would mean the coupling is
    /// spreading while the refactor is still open.</summary>
    [Fact]
    public void No_other_Domain_type_derives_from_an_AspNet_Identity_type()
    {
        var offenders = DomainAssembly.GetExportedTypes()
            .Where(DerivesFromIdentity)
            .Select(type => type.FullName ?? type.Name)
            .Where(name => !KnownIdentityDerivedDomainTypes.Contains(name, StringComparer.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "A new SIMF.Domain type derives from an ASP.NET Identity base. Arch "
            + "SEV-1.1 is already open for SimfUser and SimfRole (see " + PlanDoc
            + "); widening it makes the eventual POCO split bigger for no gain. "
            + "Model the type as a plain Domain POCO and keep the Identity "
            + "shape in Infrastructure.\n"
            + string.Join('\n', offenders.Select(name => "  " + name)));
    }

    private static bool DerivesFromIdentity(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (IsIdentityAssembly(current.Assembly.GetName().Name))
            {
                return true;
            }
        }
        return false;
    }

    private static List<string> IdentityReferencesOf(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(IsIdentityAssembly)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)];

    /// <summary>The Identity types live in <c>Microsoft.Extensions.Identity.*</c>
    /// (Core / Stores) even though their namespace is
    /// <c>Microsoft.AspNetCore.Identity</c>, so both prefixes are checked.</summary>
    private static bool IsIdentityAssembly(string? assemblyName) =>
        assemblyName is not null
        && (assemblyName.StartsWith("Microsoft.Extensions.Identity", StringComparison.Ordinal)
            || assemblyName.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal));
}
