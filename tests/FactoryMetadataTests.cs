using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Lg.Display.Tests;

public class FactoryMetadataTests
{
    [Theory]
    [InlineData("LgDisplayControllerFactory")]
    [InlineData("LgDisplayIRFactory")]
    public void Factory_Sets_MinimumEssentialsFrameworkVersion_To_3_0_0(string factoryClassName)
    {
        var content = AssemblyFixture.FindSourceForClass(factoryClassName);
        content.Should().NotBeNull($"source for '{factoryClassName}' should exist");

        // Must match the exact PepperDashEssentials version pinned in the csproj, not a bare
        // "3.0.0" (no stable/GA 3.0.0 has shipped; semver ranks a prerelease/RC lower than the
        // plain version, so a mismatched literal can fail the runtime compatibility gate).
        Regex.IsMatch(content!, @"MinimumEssentialsFrameworkVersion\s*=\s*""3\.0\.0-rc\.1""")
            .Should().BeTrue($"{factoryClassName} should set MinimumEssentialsFrameworkVersion to \"3.0.0-rc.1\", matching the pinned PackageReference");
    }

    [Theory]
    [InlineData("LgDisplayControllerFactory")]
    [InlineData("LgDisplayIRFactory")]
    public void Factory_Sets_TypeNames(string factoryClassName)
    {
        var content = AssemblyFixture.FindSourceForClass(factoryClassName);
        content.Should().NotBeNull($"source for '{factoryClassName}' should exist");

        Regex.IsMatch(content!, @"TypeNames\s*=\s*new\s+List<string>")
            .Should().BeTrue($"{factoryClassName} should set TypeNames in the constructor");
    }

    [Theory]
    [InlineData("LgDisplayControllerFactory", "lgDisplay")]
    [InlineData("LgDisplayControllerFactory", "lgPlugin")]
    [InlineData("LgDisplayControllerFactory", "lg")]
    [InlineData("LgDisplayIRFactory", "lgDisplayIr")]
    public void Factory_Source_Contains_TypeName(string factoryClassName, string typeName)
    {
        var content = AssemblyFixture.FindSourceForClass(factoryClassName);
        content.Should().NotBeNull($"source for '{factoryClassName}' should exist");
        content!.Should().Contain($"\"{typeName}\"",
            $"{factoryClassName} should register type name \"{typeName}\"");
    }

    [Fact]
    public void No_Duplicate_TypeNames_Across_Factories()
    {
        var all = new List<string>();
        foreach (var factory in new[] { "LgDisplayControllerFactory", "LgDisplayIRFactory" })
        {
            var content = AssemblyFixture.FindSourceForClass(factory);
            content.Should().NotBeNull($"source for '{factory}' should exist");
            var match = Regex.Match(content!, @"TypeNames\s*=\s*new\s+List<string>\s*\{([^}]+)\}");
            if (!match.Success) continue;
            all.AddRange(Regex.Matches(match.Groups[1].Value, @"""([^""]+)""").Select(m => m.Groups[1].Value));
        }

        all.Should().OnlyHaveUniqueItems("TypeNames should be unique across all factories");
    }
}
