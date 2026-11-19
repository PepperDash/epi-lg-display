using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Lg.Display.Tests;

/// <summary>
/// Tests for the last-request-wins rule: one pending power slot and one pending input slot.
/// </summary>
public class LgCommandQueueTests
{
    /// <summary>
    /// Plays the controller's role: tracks the panel state, applies the queue's decisions and
    /// records every command that would go to the panel.
    /// </summary>
    private sealed class PanelHarness
    {
        public LgCommandQueue<string> Queue { get; } = new();
        public LgPanelState State { get; set; }
        public List<string> Sent { get; } = new();

        public PanelHarness(LgPanelState initial) => State = initial;

        public void PowerOn() => Apply(Queue.RequestPower(true, State));
        public void PowerOff() => Apply(Queue.RequestPower(false, State));

        public void Input(string input)
        {
            var decision = Queue.RequestInput(input, State);
            if (decision.SendInputNow) Sent.Add(input);
            Apply(decision.PowerToSend);
        }

        public void WarmupDone()
        {
            State.Should().Be(LgPanelState.WarmingUp);
            State = LgPanelState.On;
            Apply(Queue.WarmupComplete());
        }

        public void CooldownDone()
        {
            State.Should().Be(LgPanelState.CoolingDown);
            State = LgPanelState.Off;
            Apply(Queue.CooldownComplete());
        }

        /// <summary>The settle delay after warm-up has passed.</summary>
        public void Ready()
        {
            var input = Queue.TakeReadyInput(State);
            if (input != null) Sent.Add(input);
        }

        private void Apply(LgPowerCommand command)
        {
            switch (command)
            {
                case LgPowerCommand.On:
                    Sent.Add("On");
                    State = LgPanelState.WarmingUp;
                    break;
                case LgPowerCommand.Off:
                    Sent.Add("Off");
                    State = LgPanelState.CoolingDown;
                    break;
            }
        }
    }

    // ---- Power slot: warm-up ----

    [Fact]
    public void PowerOn_WhenOff_SendsOnImmediately()
    {
        var panel = new PanelHarness(LgPanelState.Off);

        panel.PowerOn();

        panel.Sent.Should().Equal("On");
        panel.State.Should().Be(LgPanelState.WarmingUp);
    }

    [Fact]
    public void OnThenOff_DuringWarmup_SendsOffWhenWarmupEnds()
    {
        var panel = new PanelHarness(LgPanelState.Off);

        panel.PowerOn();
        panel.PowerOff();
        panel.Sent.Should().Equal("On");

        panel.WarmupDone();

        panel.Sent.Should().Equal("On", "Off");
    }

    [Fact]
    public void OnOffOn_DuringWarmup_SendsNothingWhenWarmupEnds()
    {
        var panel = new PanelHarness(LgPanelState.Off);

        panel.PowerOn();
        panel.PowerOff();
        panel.PowerOn();
        panel.WarmupDone();
        panel.Ready();

        panel.Sent.Should().Equal("On");
        panel.Queue.PendingPower.Should().Be(LgPowerCommand.None);
    }

    [Fact]
    public void PowerOn_WhileWarmingUp_IsANoOp()
    {
        var panel = new PanelHarness(LgPanelState.WarmingUp);

        panel.PowerOn();
        panel.PowerOn();
        panel.WarmupDone();

        panel.Sent.Should().BeEmpty();
    }

    [Fact]
    public void RepeatedOff_DuringWarmup_SendsOneOff()
    {
        var panel = new PanelHarness(LgPanelState.WarmingUp);

        panel.PowerOff();
        panel.PowerOff();
        panel.WarmupDone();

        panel.Sent.Should().Equal("Off");
    }

    // ---- Power slot: cool-down ----

    [Fact]
    public void OffThenOn_DuringCooldown_SendsOnWhenCooldownEnds()
    {
        var panel = new PanelHarness(LgPanelState.On);

        panel.PowerOff();
        panel.PowerOn();
        panel.Sent.Should().Equal("Off");

        panel.CooldownDone();

        panel.Sent.Should().Equal("Off", "On");
    }

    [Fact]
    public void OffOnOff_DuringCooldown_SendsNothingWhenCooldownEnds()
    {
        var panel = new PanelHarness(LgPanelState.On);

        panel.PowerOff();
        panel.PowerOn();
        panel.PowerOff();
        panel.CooldownDone();

        panel.Sent.Should().Equal("Off");
    }

    [Fact]
    public void PowerOff_WhileCoolingDown_IsANoOp()
    {
        var panel = new PanelHarness(LgPanelState.CoolingDown);

        panel.PowerOff();
        panel.CooldownDone();

        panel.Sent.Should().BeEmpty();
    }

    [Fact]
    public void PowerCommands_WhenIdle_AreSentImmediately()
    {
        var queue = new LgCommandQueue<string>();

        queue.RequestPower(true, LgPanelState.On).Should().Be(LgPowerCommand.On);
        queue.RequestPower(false, LgPanelState.On).Should().Be(LgPowerCommand.Off);
        queue.RequestPower(false, LgPanelState.Off).Should().Be(LgPowerCommand.Off);
        queue.PendingPower.Should().Be(LgPowerCommand.None);
    }

    // ---- Input slot ----

    [Fact]
    public void Input_WhenOn_IsSentImmediately()
    {
        var panel = new PanelHarness(LgPanelState.On);

        panel.Input("HDMI1");

        panel.Sent.Should().Equal("HDMI1");
        panel.Queue.HasPendingInput.Should().BeFalse();
    }

    [Fact]
    public void Inputs_DuringWarmup_OnlyTheLastIsSentOnceReady()
    {
        var panel = new PanelHarness(LgPanelState.Off);

        panel.PowerOn();
        panel.Input("HDMI1");
        panel.Input("HDMI2");
        panel.Input("HDMI3");
        panel.WarmupDone();

        panel.Sent.Should().Equal("On");
        panel.Queue.PendingInput.Should().Be("HDMI3");

        panel.Ready();

        panel.Sent.Should().Equal("On", "HDMI3");
        panel.Queue.HasPendingInput.Should().BeFalse();
    }

    [Fact]
    public void Input_WhenOff_PowersOnThenSendsInputAfterWarmup()
    {
        var panel = new PanelHarness(LgPanelState.Off);

        panel.Input("HDMI2");

        panel.Sent.Should().Equal("On");
        panel.State.Should().Be(LgPanelState.WarmingUp);

        panel.WarmupDone();
        panel.Ready();

        panel.Sent.Should().Equal("On", "HDMI2");
    }

    [Fact]
    public void Inputs_WhenOff_SendOnePowerOnAndTheLastInput()
    {
        var panel = new PanelHarness(LgPanelState.Off);

        panel.Input("HDMI1");
        panel.Input("HDMI2");
        panel.Input("HDMI3");
        panel.WarmupDone();
        panel.Ready();

        panel.Sent.Should().Equal("On", "HDMI3");
    }

    [Fact]
    public void OffDuringCooldown_ThenInput_SendsOnThenThatInput()
    {
        var panel = new PanelHarness(LgPanelState.On);

        panel.PowerOff();
        panel.Input("HDMI4");
        panel.Sent.Should().Equal("Off");

        panel.CooldownDone();
        panel.Sent.Should().Equal("Off", "On");

        panel.WarmupDone();
        panel.Ready();

        panel.Sent.Should().Equal("Off", "On", "HDMI4");
    }

    [Fact]
    public void InputDuringCooldown_ThenPowerOff_OffWinsAndInputIsDropped()
    {
        var panel = new PanelHarness(LgPanelState.CoolingDown);

        panel.Input("HDMI1");
        panel.PowerOff();
        panel.CooldownDone();
        panel.Ready();

        panel.Sent.Should().BeEmpty();
        panel.Queue.HasPendingInput.Should().BeFalse();
    }

    [Fact]
    public void InputWhenOff_ThenPowerOffDuringWarmup_OffWinsAndInputIsDropped()
    {
        var panel = new PanelHarness(LgPanelState.Off);

        panel.Input("HDMI1");
        panel.PowerOff();
        panel.WarmupDone();
        panel.Ready();

        panel.Sent.Should().Equal("On", "Off");
        panel.Queue.HasPendingInput.Should().BeFalse();
    }

    [Fact]
    public void PowerOffThenInput_DuringWarmup_InputWinsAsTheLatestRequest()
    {
        var panel = new PanelHarness(LgPanelState.WarmingUp);

        panel.PowerOff();
        panel.Input("HDMI2");
        panel.WarmupDone();
        panel.Ready();

        panel.Sent.Should().Equal("HDMI2");
    }

    [Fact]
    public void InputDuringSettle_IsSentNowAndReplacesThePendingInput()
    {
        var panel = new PanelHarness(LgPanelState.Off);

        panel.Input("HDMI1");
        panel.WarmupDone();
        panel.Input("HDMI2");
        panel.Ready();

        panel.Sent.Should().Equal("On", "HDMI2");
    }

    [Fact]
    public void PowerOffDuringSettle_DropsThePendingInput()
    {
        var panel = new PanelHarness(LgPanelState.Off);

        panel.Input("HDMI1");
        panel.WarmupDone();
        panel.PowerOff();
        panel.Ready();

        panel.Sent.Should().Equal("On", "Off");
    }

    [Fact]
    public void TakeReadyInput_WhileWarmingOrCooling_LeavesTheInputPending()
    {
        var queue = new LgCommandQueue<string>();
        queue.RequestInput("HDMI1", LgPanelState.WarmingUp);

        queue.TakeReadyInput(LgPanelState.WarmingUp).Should().BeNull();
        queue.TakeReadyInput(LgPanelState.CoolingDown).Should().BeNull();
        queue.PendingInput.Should().Be("HDMI1");

        queue.TakeReadyInput(LgPanelState.Off).Should().Be("HDMI1");
        queue.HasPendingInput.Should().BeFalse();
    }

    [Fact]
    public void Input_WhenOff_RequestsPowerOnDecision()
    {
        var queue = new LgCommandQueue<string>();

        var decision = queue.RequestInput("HDMI1", LgPanelState.Off);

        decision.SendInputNow.Should().BeFalse();
        decision.PowerToSend.Should().Be(LgPowerCommand.On);
        queue.PendingInput.Should().Be("HDMI1");
    }
}
