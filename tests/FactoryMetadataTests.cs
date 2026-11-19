using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Lg.Display.Tests;

public class FactoryMetadataTests
{
    [Theory]
    [InlineData("LgDisplayControllerFactory")]
    [InlineData("LgDisplayIRFactory")]
    [InlineData("MockLgDisplayFactory")]
    public void Factory_Sets_MinimumEssentialsFrameworkVersion_To_Pinned_Version(string factoryClassName)
    {
        var content = AssemblyFixture.FindSourceForClass(factoryClassName);
        content.Should().NotBeNull($"source for '{factoryClassName}' should exist");

        // Must be the plain numeric core of the pinned PackageReference (3.0.0-rc.20 -> "3.0.0").
        // The Essentials plugin loader parses this with System.Version; a prerelease suffix such as
        // "-rc.20" fails to parse and the loader skips the plugin entirely
        // (Global.IsRunningMinimumVersionOrHigher, Essentials 3.1.4).
        Regex.IsMatch(content!, @"MinimumEssentialsFrameworkVersion\s*=\s*""3\.0\.0""")
            .Should().BeTrue($"{factoryClassName} should set MinimumEssentialsFrameworkVersion to \"3.0.0\" (no prerelease suffix, or the plugin won't load)");
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
    [InlineData("MockLgDisplayFactory", "mockLgDisplay")]
    [InlineData("MockLgDisplayFactory", "lgDisplayMock")]
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
        foreach (var factory in new[] { "LgDisplayControllerFactory", "LgDisplayIRFactory", "MockLgDisplayFactory" })
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
