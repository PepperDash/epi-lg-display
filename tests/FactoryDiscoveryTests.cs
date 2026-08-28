using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Lg.Display.Tests;

public class FactoryDiscoveryTests
{
    [Fact]
    public void Assembly_Loads_Successfully()
    {
        AssemblyFixture.PluginAssembly.Should().NotBeNull();
    }

    [Fact]
    public void Assembly_Name_Is_EpiDisplayLg()
    {
        AssemblyFixture.PluginAssembly.GetName().Name.Should().Be("epi-display-lg.4Series");
    }

    [Fact]
    public void Factory_Count_Is_Two()
    {
        // LgDisplayControllerFactory (network) + LgDisplayIRFactory (IR).
        AssemblyFixture.FindFactoryTypes().Should().HaveCount(2);
    }

    [Theory]
    [InlineData("LgDisplayControllerFactory")]
    [InlineData("LgDisplayIRFactory")]
    public void Factory_Exists_ByName(string factoryClassName)
    {
        AssemblyFixture.FindFactoryTypes()
            .Should().Contain(t => t.Name == factoryClassName,
                $"factory '{factoryClassName}' should be discoverable");
    }

    [Fact]
    public void All_Factories_Have_Parameterless_Constructor()
    {
        foreach (var factory in AssemblyFixture.FindFactoryTypes())
        {
            factory.GetConstructor(Type.EmptyTypes).Should()
                .NotBeNull($"Factory '{factory.Name}' must have a parameterless constructor");
        }
    }
}
