using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Lg.Display.Tests;

/// <summary>
/// Every input selection must go through the controller's power/input queue (ExecuteSwitch).
/// The controller needs a Crestron runtime, so these check the source rather than run it; the
/// queue's own behavior is covered by <see cref="LgCommandQueueTests"/>.
/// </summary>
public class InputSelectRoutingTests
{
    private static string SelectBody()
    {
        var source = AssemblyFixture.FindSourceForClass("LgInput");
        source.Should().NotBeNull("LgInput should be declared in src");

        var start = source!.IndexOf("public void Select()", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "LgInput should declare Select()");

        var open = source.IndexOf('{', start);
        var close = source.IndexOf('}', open);
        return source.Substring(open, close - open + 1);
    }

    [Fact]
    public void LgInput_Select_DoesNotSendDirectly()
    {
        // Sending xb directly skipped the queue: an input picked on the Mobile Control UI while the
        // panel was off was sent to a panel that ignores it, and the selection was lost.
        SelectBody().Should().NotContain("SendData");
    }

    [Fact]
    public void LgInput_Select_UsesControllerSelectInput()
    {
        SelectBody().Should().Contain("SelectInput(");
    }

    [Fact]
    public void Controller_SelectInput_GoesThroughExecuteSwitch()
    {
        var source = AssemblyFixture.FindSourceForClass("LgDisplayController");
        source.Should().NotBeNull();

        var start = source!.IndexOf("public void SelectInput(string inputKey)", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "LgDisplayController should declare SelectInput(string)");

        var end = source.IndexOf("private RoutingInputPort GetInputPort", start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start);

        var body = source.Substring(start, end - start);
        body.Should().Contain("ExecuteSwitch(action)");
        body.Should().NotContain("SendData");
    }
}
