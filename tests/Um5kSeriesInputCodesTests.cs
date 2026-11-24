using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace PepperDash.Essentials.Plugins.Lg.Display.Tests;

/// <summary>
/// The LG UM5K series (110UM5K-BP.CUSFLR) uses a6 for HDMI 3 instead of 92. On site, "xb 01 92"
/// was answered "b 01 NG92x", and the input poll with the panel on HDMI 3 answered "b 01 OKa6x".
/// "um5kSeriesInputCodes": true remaps HDMI 3 only; with it false every code is unchanged.
/// The controller needs a Crestron runtime, so the code choice is tested through
/// <see cref="LgInputCodes"/> (compiled into the tests) and the controller's use of it is checked
/// in the source, like <see cref="InputSelectRoutingTests"/>.
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

    [Fact]
    public void Config_Summary_Names_The_Model_And_The_Evidence()
    {
        var source = AssemblyFixture.FindSourceForClass("LgDisplayPropertiesConfig");
        source.Should().NotBeNull();

        var end = source!.IndexOf("public bool Um5kSeriesInputCodes", StringComparison.Ordinal);
        end.Should().BeGreaterThan(0);
        var start = source.LastIndexOf("/// <summary>", end, StringComparison.Ordinal);
        var summary = source.Substring(start, end - start);

        summary.Should().Contain("110UM5K-BP.CUSFLR");
        summary.Should().Contain("NG92x");
        summary.Should().Contain("OKa6x");
        summary.Should().Contain("Only HDMI 3 is remapped");
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

    // ---- controller uses the switchable code everywhere HDMI 3 is keyed ----

    private static string ControllerSource()
    {
        var source = AssemblyFixture.FindSourceForClass("LgDisplayController");
        source.Should().NotBeNull();
        return source!;
    }

    [Fact]
    public void Controller_Has_No_Hardcoded_92()
    {
        // Code only: the doc comments still mention "92" when describing the two codes
        var source = ControllerSource();
        source.Should().NotContain("this), \"92\")", "the HDMI 3 routing port must use Hdmi3InputCode");
        source.Should().NotContain("{ \"92\",", "the HDMI 3 input list key must use Hdmi3InputCode");
        source.Should().NotContain("LgInput(\"92\"", "the HDMI 3 input list item must use Hdmi3InputCode");
        source.Should().NotContain("xb {0} 92", "InputHdmi3 must send LgInputCodes.Hdmi3Command");
    }

    [Fact]
    public void Controller_Hdmi3_Routing_Port_Matches_Hdmi3InputCode()
    {
        var source = ControllerSource();
        var start = source.IndexOf("RoutingPortNames.HdmiIn3", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0);
        var end = source.IndexOf(';', start);

        source.Substring(start, end - start).Should().Contain("Hdmi3InputCode");
    }

    [Fact]
    public void Controller_Input_List_Keys_Hdmi3_By_Hdmi3InputCode()
    {
        ControllerSource().Should().Contain("{ Hdmi3InputCode, new LgInput(Hdmi3InputCode, \"HDMI 3\", this) }");
    }

    [Fact]
    public void Controller_InputHdmi3_Sends_Switchable_Code()
    {
        var source = ControllerSource();
        var start = source.IndexOf("public void InputHdmi3()", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0);
        var close = source.IndexOf('}', start);

        source.Substring(start, close - start).Should().Contain("LgInputCodes.Hdmi3Command(Id, um5kSeriesInputCodes)");
    }

    [Fact]
    public void Controller_Hdmi3InputCode_Reads_The_Config_Flag()
    {
        var source = ControllerSource();
        source.Should().Contain("um5kSeriesInputCodes = props.Um5kSeriesInputCodes;");
        source.Should().Contain("LgInputCodes.Hdmi3For(um5kSeriesInputCodes)");
    }
}
