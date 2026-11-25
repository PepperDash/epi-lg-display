using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace PepperDash.Essentials.Plugins.Lg.Display.Tests;

/// <summary>
/// The LG UM5K series (110UM5K-BP.CUSFLR) uses a6 for HDMI 3 instead of 92. On site, "xb 01 92"
/// was answered "b 01 NG92x", and the input poll with the panel on HDMI 3 answered "b 01 OKa6x".
/// "um5kSeriesInputCodes": true remaps HDMI 3 only; with it false every code is unchanged.
/// The controller needs a Crestron runtime, so the behaviour is tested through
/// <see cref="LgInputCodes"/> and <see cref="MockLgDisplayResponder"/>, both compiled into the tests.
/// </summary>
public class Um5kSeriesInputCodesTests
{
    private static readonly Lazy<Type?> ConfigType = new(() =>
        AssemblyFixture.PluginAssembly.GetType("PepperDash.Essentials.Plugins.Lg.Display.LgDisplayPropertiesConfig"));

    private const string Um5kDeviceJson = """
        {
            "control": { "method": "com", "controlPortDevKey": "processor", "controlPortNumber": 1 },
            "id": "01",
            "pollIntervalMs": 10000,
            "um5kSeriesInputCodes": true
        }
        """;

    // ---- config ----

    [Fact]
    public void Config_Um5kSeriesInputCodes_Is_A_Bool_Bound_To_Its_Json_Name()
    {
        var prop = ConfigType.Value!.GetProperty("Um5kSeriesInputCodes");
        prop.Should().NotBeNull("LgDisplayPropertiesConfig should expose Um5kSeriesInputCodes");
        prop!.PropertyType.Name.Should().Be("Boolean", "a missing key must default to false");

        var attribute = prop.CustomAttributes.SingleOrDefault(a => a.AttributeType.Name == "JsonPropertyAttribute");
        attribute.Should().NotBeNull();
        attribute!.ConstructorArguments.Single().Value.Should().Be("um5kSeriesInputCodes");
    }

    [Fact]
    public void Config_Um5kSeriesInputCodes_Ignores_Explicit_Null()
    {
        // A bool cannot take null; without Ignore an explicit null throws in ToObject and the
        // device is never created
        var attribute = ConfigType.Value!.GetProperty("Um5kSeriesInputCodes")!.CustomAttributes
            .Single(a => a.AttributeType.Name == "JsonPropertyAttribute");

        var ignoresNull = attribute.NamedArguments.Any(a =>
            a.MemberName == "NullValueHandling" && Convert.ToInt32(a.TypedValue.Value) == 1); // 1 = Ignore
        ignoresNull.Should().BeTrue("um5kSeriesInputCodes should use NullValueHandling.Ignore like remoteOnly");
    }

    [Fact]
    public void Um5k_Device_Config_Deserializes_Flag_And_Binds_Every_Property()
    {
        var jo = JObject.Parse(Um5kDeviceJson);
        jo["um5kSeriesInputCodes"]!.Value<bool>().Should().BeTrue();

        foreach (var prop in jo.Properties().Where(p => p.Name != "control"))
        {
            var binds = ConfigType.Value!.GetProperties().Any(p =>
                p.CustomAttributes.Any(a => a.AttributeType.Name == "JsonPropertyAttribute"
                    && a.ConstructorArguments.Any(arg => string.Equals(arg.Value?.ToString(), prop.Name, StringComparison.Ordinal))));

            binds.Should().BeTrue($"\"{prop.Name}\" should bind to LgDisplayPropertiesConfig");
        }
    }

    // ---- input codes ----

    [Fact]
    public void FlagOn_Hdmi3_Sends_A6()
    {
        LgInputCodes.Hdmi3For(true).Should().Be("a6");
        // Hex upper-cased like the driver's other commands ("xb 01 C0", "xb 01 FF")
        LgInputCodes.Hdmi3Command("01", true).Should().Be("xb 01 A6");
    }

    [Fact]
    public void FlagOff_Hdmi3_Is_Still_92()
    {
        LgInputCodes.Hdmi3For(false).Should().Be("92");
        LgInputCodes.Hdmi3Command("01", false).Should().Be("xb 01 92");
        LgInputCodes.Hdmi3Command("1", false).Should().Be("xb 1 92");
    }

    [Theory]
    [InlineData("b 01 OKa6x")]
    [InlineData("b 01 OKA6x")]
    public void FlagOn_A6_Poll_Reply_Matches_Hdmi3(string line)
    {
        var r = LgResponseParser.Parse(line);

        r.Should().NotBeNull();
        r!.IsOk.Should().BeTrue();
        // UpdateInputFb lowercases the reply before matching the routing port's feedback code
        r.Data.ToLower().Should().Be(LgInputCodes.Hdmi3For(true));
        r.Data.ToLower().Should().NotBe(LgInputCodes.Hdmi3For(false));
    }

    [Fact]
    public void FlagOff_92_Poll_Reply_Matches_Hdmi3()
    {
        var r = LgResponseParser.Parse("b 01 OK92x");

        r.Should().NotBeNull();
        r!.Data.ToLower().Should().Be(LgInputCodes.Hdmi3For(false));
    }

    [Fact]
    public void Um5k_Panel_Rejects_92()
    {
        // What the 110UM5K-BP.CUSFLR answered to "xb 01 92"
        var r = LgResponseParser.Parse("b 01 NG92x");

        r.Should().NotBeNull();
        r!.IsOk.Should().BeFalse();
        r.Data.Should().Be(LgInputCodes.Hdmi3);
    }

    // ---- input keys: "92" still means HDMI 3 when the flag is on ----

    [Theory]
    [InlineData("92")]
    [InlineData("A6")]
    [InlineData("a6")]
    public void FlagOn_Hdmi3_Keys_Normalize_To_A6(string key)
    {
        // friendlyNames and SelectInput both go through NormalizeInputKey
        LgInputCodes.NormalizeInputKey(key, true).ToLower().Should().Be("a6");
    }

    [Theory]
    [InlineData("90")]
    [InlineData("91")]
    [InlineData("93")]
    [InlineData("c0")]
    public void FlagOn_Other_Keys_Are_Unchanged(string key)
    {
        LgInputCodes.NormalizeInputKey(key, true).Should().Be(key);
    }

    [Theory]
    [InlineData("90")]
    [InlineData("92")]
    [InlineData("a6")]
    [InlineData("c0")]
    public void FlagOff_Keys_Are_Unchanged(string key)
    {
        LgInputCodes.NormalizeInputKey(key, false).Should().Be(key);
    }

    // ---- mock panel answers like a UM5K when the flag is on ----

    private static MockLgDisplayResponder PoweredOnPanel(bool um5k) =>
        new(1, new MockLgDisplayOptions { PowerOn = true }, um5k);

    [Fact]
    public void Um5k_Mock_Accepts_A6_And_Rejects_92()
    {
        var panel = PoweredOnPanel(true);

        panel.SetInput("92").Should().BeFalse();
        panel.SetInput("a6").Should().BeTrue();
        panel.Respond("xb 01 92").Should().Be("b 01 NG92x");
        panel.Respond("xb 01 A6").Should().Be("b 01 OKa6x");
        panel.Respond("xb 01 ff").Should().Be("b 01 OKa6x");
    }

    [Fact]
    public void Standard_Mock_Accepts_92_And_Rejects_A6()
    {
        var panel = PoweredOnPanel(false);

        panel.SetInput("a6").Should().BeFalse();
        panel.SetInput("92").Should().BeTrue();
        panel.Respond("xb 01 a6").Should().Be("b 01 NGa6x");
        panel.Respond("xb 01 92").Should().Be("b 01 OK92x");
    }

    [Fact]
    public void Mock_Command_For_Hdmi3_Matches_What_The_Panel_Accepts()
    {
        // The command the controller sends for HDMI 3 is accepted by a panel of the same kind
        PoweredOnPanel(true).Respond(LgInputCodes.Hdmi3Command("01", true)).Should().Be("b 01 OKa6x");
        PoweredOnPanel(false).Respond(LgInputCodes.Hdmi3Command("01", false)).Should().Be("b 01 OK92x");
    }
}
