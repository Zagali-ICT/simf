// This file is the thing that keeps the Area scheme alive.
//
// SIMF.Api.Tests is ~2,470 tests and ~35 minutes, so nobody runs it whole while
// working. Selecting a subset used to mean guessing at class-name substrings
// (--filter "FullyQualifiedName~Meeting"), and a guess that missed two classes
// let a broken branch reach main. [Trait("Area", ...)] replaces the guess with a
// filter, but only while every class carries one - a scheme that 90% of classes
// follow is worse than none, because the filter looks like it worked.
//
// So the coverage is asserted, not documented. A new test class with no Area
// trait fails the build here, and the fix is one attribute line.
//
// Filter commands: docs/manuals/Test-Guide.md section 12.1.
using System.Reflection;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class TestAreaTraitTests
{
    [Fact]
    public void EveryTestClassDeclaresExactlyOneAreaTrait()
    {
        var offenders = TestClasses()
            .Select(type => new { Type = type, Areas = AreaValues(type) })
            .Where(entry => entry.Areas.Count != 1)
            .Select(entry => $"{entry.Type.FullName} ({entry.Areas.Count} Area traits)")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Every test class must declare exactly one [Trait(TestAreas.TraitName, ...)] so it "
            + "stays selectable - see docs/manuals/Test-Guide.md section 12.1. Offenders: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void EveryAreaTraitComesFromTheClosedVocabulary()
    {
        var offenders = TestClasses()
            .SelectMany(type => AreaValues(type).Select(area => new { Type = type, Area = area }))
            .Where(entry => !TestAreas.All.Contains(entry.Area))
            .Select(entry => $"{entry.Type.FullName} = \"{entry.Area}\"")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Area traits must use a TestAreas constant, not a literal, and a new value has to be "
            + "added to TestAreas.All deliberately. Offenders: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// Every non-abstract class in this assembly that xUnit will collect tests from.
    /// </summary>
    private static IReadOnlyList<Type> TestClasses() =>
        typeof(TestAreas).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(HasTestMethod)
            .ToList();

    private static bool HasTestMethod(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Any(method => method.GetCustomAttributes(typeof(FactAttribute), inherit: true).Length > 0);

    /// <summary>
    /// xUnit 2's TraitAttribute exposes no Name/Value properties, so the declared
    /// constructor arguments are the only way to read a trait back by reflection.
    /// </summary>
    private static IReadOnlyList<string> AreaValues(Type type) =>
        type.GetCustomAttributesData()
            .Where(data => data.AttributeType == typeof(TraitAttribute))
            .Where(data => data.ConstructorArguments.Count == 2
                && data.ConstructorArguments[0].Value as string == TestAreas.TraitName)
            .Select(data => data.ConstructorArguments[1].Value as string ?? string.Empty)
            .ToList();
}
