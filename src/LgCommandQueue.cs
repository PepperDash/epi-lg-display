#nullable enable

namespace PepperDash.Essentials.Plugins.Lg.Display
{
    /// <summary>
    /// Power state of the panel as the controller sees it when a request arrives.
    /// </summary>
    public enum LgPanelState
    {
        /// <summary>Panel is off and not cooling down.</summary>
        Off,
        /// <summary>Panel is on and not warming up.</summary>
        On,
        /// <summary>Power on was sent and the warm-up window is running.</summary>
        WarmingUp,
        /// <summary>Power off was sent and the cool-down window is running.</summary>
        CoolingDown,
    }

    /// <summary>
    /// A power command the controller should send.
    /// </summary>
    public enum LgPowerCommand
    {
        /// <summary>Send nothing.</summary>
        None,
        /// <summary>Send power on.</summary>
        On,
        /// <summary>Send power off.</summary>
        Off,
    }

    /// <summary>
    /// What the controller should do after an input request.
    /// </summary>
    public readonly struct LgInputDecision
    {
        /// <summary>Creates a decision.</summary>
        public LgInputDecision(bool sendInputNow, LgPowerCommand powerToSend)
        {
            SendInputNow = sendInputNow;
            PowerToSend = powerToSend;
        }

        /// <summary>True when the input should be sent right away.</summary>
        public bool SendInputNow { get; }

        /// <summary>Power command to send now (On when an input wakes an off panel).</summary>
        public LgPowerCommand PowerToSend { get; }
    }

    /// <summary>
    /// Decides which power and input commands to send to an LG panel. It keeps one pending power
    /// slot and one pending input slot, and the latest request wins:
    /// <list type="bullet">
    /// <item>While warming up, PowerOff is held and sent when warm-up ends; a later PowerOn cancels
    /// it (the panel is already turning on, so nothing is sent). PowerOn alone sends nothing.</item>
    /// <item>While cooling down, PowerOn is held and sent when cool-down ends; a later PowerOff
    /// cancels it. PowerOff alone sends nothing.</item>
    /// <item>An input is sent at once when the panel is on. Otherwise it replaces the pending
    /// input and is sent once the panel is ready (warm-up done plus a settle delay). Selecting an
    /// input also requests power on through the power slot, so a later PowerOff still wins.</item>
    /// </list>
    /// Not thread safe: the caller serializes access.
    /// </summary>
    /// <typeparam name="TInput">The input selector type.</typeparam>
    public sealed class LgCommandQueue<TInput> where TInput : class
    {
        /// <summary>Power command held until the warm-up or cool-down window ends.</summary>
        public LgPowerCommand PendingPower { get; private set; } = LgPowerCommand.None;

        /// <summary>Input held until the panel is ready, or null.</summary>
        public TInput? PendingInput { get; private set; }

        /// <summary>True while an input is waiting to be sent.</summary>
        public bool HasPendingInput => PendingInput != null;

        /// <summary>
        /// Records a power request. Returns the command to send now (None if it is held or not needed).
        /// </summary>
        public LgPowerCommand RequestPower(bool on, LgPanelState state)
        {
            switch (state)
            {
                case LgPanelState.WarmingUp:
                    // On: the panel is already turning on; drop any pending Off. Off: hold it.
                    PendingPower = on ? LgPowerCommand.None : LgPowerCommand.Off;
                    return LgPowerCommand.None;

                case LgPanelState.CoolingDown:
                    // Off: the panel is already turning off; drop any pending On. On: hold it.
                    PendingPower = on ? LgPowerCommand.On : LgPowerCommand.None;
                    return LgPowerCommand.None;

                default:
                    PendingPower = LgPowerCommand.None;
                    if (!on)
                    {
                        // Off is the latest request, so an input still waiting must not run.
                        PendingInput = null;
                    }
                    return on ? LgPowerCommand.On : LgPowerCommand.Off;
            }
        }

        /// <summary>
        /// Records an input request. Returns whether to send the input now and any power command
        /// to send now.
        /// </summary>
        public LgInputDecision RequestInput(TInput input, LgPanelState state)
        {
            if (state == LgPanelState.On)
            {
                // The newest input goes out now; an older pending one is obsolete.
                PendingInput = null;
                return new LgInputDecision(true, LgPowerCommand.None);
            }

            PendingInput = input;

            // Selecting an input is also a power-on request.
            var power = RequestPower(true, state);
            return new LgInputDecision(false, power);
        }

        /// <summary>
        /// Called when warm-up ends. Returns the power command to send (Off if one is pending).
        /// A pending input stays queued unless the panel is being turned off.
        /// </summary>
        public LgPowerCommand WarmupComplete()
        {
            var pending = PendingPower;
            PendingPower = LgPowerCommand.None;

            if (pending == LgPowerCommand.Off)
            {
                PendingInput = null;
                return LgPowerCommand.Off;
            }

            return LgPowerCommand.None;
        }

        /// <summary>
        /// Called when cool-down ends. Returns the power command to send (On if one is pending).
        /// A pending input stays queued only if the panel is being turned back on.
        /// </summary>
        public LgPowerCommand CooldownComplete()
        {
            var pending = PendingPower;
            PendingPower = LgPowerCommand.None;

            if (pending == LgPowerCommand.On)
            {
                return LgPowerCommand.On;
            }

            PendingInput = null;
            return LgPowerCommand.None;
        }

        /// <summary>
        /// Called when the panel should be ready for an input (warm-up plus settle delay).
        /// Returns and clears the pending input, or null if there is none or the panel is
        /// warming up or cooling down again (that window's completion handles it).
        /// </summary>
        public TInput? TakeReadyInput(LgPanelState state)
        {
            if (state == LgPanelState.WarmingUp || state == LgPanelState.CoolingDown)
            {
                return null;
            }

            var input = PendingInput;
            PendingInput = null;
            return input;
        }
    }
}
