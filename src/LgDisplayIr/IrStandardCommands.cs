using System;
using System.Collections.Generic;
using PepperDash.Core;

namespace PepperDash.Essentials.Plugins.Lg.Display
{
    public static class IrStandardCommands
    {
        public const string PowerToggle = "POWER";
        public const string PowerOn = "POWER_ON";
        public const string PowerOff = "POWER_OFF";
        public const string KP1 = "1";
        public const string KP2 = "2";
        public const string KP3 = "3";
        public const string KP4 = "4";
        public const string KP5 = "5";
        public const string KP6 = "6";
        public const string KP7 = "7";
        public const string KP8 = "8";
        public const string KP9 = "9";
        public const string KP0 = "0";
        public const string VolumeUp = "VOL+";
        public const string VolumeDown = "VOL-";
        public const string MuteToggle = "MUTE";
        public const string ChannelUp = "CH+";
        public const string ChannelDown = "CH-";
        public const string Last = "LAST";
        public const string PageUp = "PAGE_UP";
        public const string PageDown = "PAGE_DOWN";
        public const string Home = "HOME";
        public const string Menu = "MENU";
        public const string DpadUp = "UP_ARROW";
        // DN_ARROW, not DOWN_ARROW. Every other entry in this table is a Crestron *standard command*
        // name (HDMI_1, VOL+, CH+, INPUT_CYCLE, FSCAN...) rather than the .ir file's own button
        // label, and the standard name for this one is DN_ARROW - "DOWN_ARROW" matched neither the
        // standard list nor the file's label ("Down"), so it could not have resolved.
        public const string DpadDown = "DN_ARROW";
        public const string DpadLeft = "LEFT_ARROW";
        public const string DpadRight = "RIGHT_ARROW";
        public const string DpadSelect = "SELECT";
        public const string Enter = "ENTER";
        public const string Back = "BACK";
        public const string Exit = "EXIT";
        public const string InputToggle = "INPUT_CYCLE";
        public const string InputHdmi1 = "HDMI_1";
        public const string InputHdmi2 = "HDMI_2";
        public const string InputHdmi3 = "HDMI_3";
        public const string InputHdmi4 = "HDMI_4";
        public const string InputAntenna = "ANTENNA";
        public const string InputTv = "TV";
        public const string Netflix = "NETFLIX";
        public const string PrimeVideo = "AMAZON_VIDEO";
        // TODO: Disney+ and Samsung TV Plus have no entry in FlatPanelDisplay_LG_65SK9500-SmartTV.ir.
        // They were here as "DISNEY_PLUS"/"SAMSUNG_TV_PLUS", guessed by convention from Netflix and
        // AMAZON_VIDEO rather than read off a driver, and there is no guarantee those codes exist to
        // be had. Removed rather than left in place: the front end builds its app buttons from the
        // controller's Inputs dictionary, so keeping them meant shipping two buttons that could
        // never do anything. If the codes turn up, add them to the .ir file first, then restore
        // these constants, their dictionary entries below, the routing ports and the Inputs entries
        // in LgDisplayIrController together.
        public const string Guide = "GUIDE";
        public const string FuncRed = "RED";
        public const string FuncGreen = "GREEN";
        public const string FuncYellow = "YELLOW";
        public const string FuncBlue = "BLUE";
        public const string Play = "PLAY";
        public const string Pause = "PAUSE";
        public const string FastForward = "FSCAN";
        public const string Rewind = "RSCAN";
        public const string Sleep = "SLEEP";

        public static readonly Dictionary<string, string> CommandDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { nameof(PowerToggle), PowerToggle },
            { nameof(PowerOn), PowerOn },
            { nameof(PowerOff), PowerOff },
            { nameof(KP1), KP1 },
            { nameof(KP2), KP2 },
            { nameof(KP3), KP3 },
            { nameof(KP4), KP4 },
            { nameof(KP5), KP5 },
            { nameof(KP6), KP6 },
            { nameof(KP7), KP7 },
            { nameof(KP8), KP8 },
            { nameof(KP9), KP9 },
            { nameof(KP0), KP0 },
            { nameof(VolumeUp), VolumeUp },
            { nameof(VolumeDown), VolumeDown },
            { nameof(MuteToggle), MuteToggle },
            { nameof(ChannelUp), ChannelUp },
            { nameof(ChannelDown), ChannelDown },
            { nameof(Last), Last },
            { nameof(PageUp), PageUp },
            { nameof(PageDown), PageDown },
            { nameof(Home), Home },
            { nameof(Menu), Menu },
            { nameof(DpadUp), DpadUp },
            { nameof(DpadDown), DpadDown },
            { nameof(DpadLeft), DpadLeft },
            { nameof(DpadRight), DpadRight },
            { nameof(DpadSelect), DpadSelect },
            { nameof(Enter), Enter },
            { nameof(Back), Back },
            { nameof(Exit), Exit },
            { nameof(InputToggle), InputToggle },
            { nameof(InputHdmi1), InputHdmi1 },
            { nameof(InputHdmi2), InputHdmi2 },
            { nameof(InputHdmi3), InputHdmi3 },
            { nameof(InputHdmi4), InputHdmi4 },
            { nameof(InputAntenna), InputAntenna },
            { nameof(InputTv), InputTv },
            { nameof(Netflix), Netflix },
            { nameof(PrimeVideo), PrimeVideo },
            { nameof(Guide), Guide },
            { nameof(FuncRed), FuncRed },
            { nameof(FuncGreen), FuncGreen },
            { nameof(FuncYellow), FuncYellow },
            { nameof(FuncBlue), FuncBlue },
            { nameof(Play), Play },
            { nameof(Pause), Pause },
            { nameof(FastForward), FastForward },
            { nameof(Rewind), Rewind },
            { nameof(Sleep), Sleep }
        };

        // The front end selects an input or app by its key in LgDisplayIrController.Inputs
        // ("primeVideo", "hdmi1", ...), and those keys are not command names. Most of them happen to
        // equal the .ir file's button label case-insensitively - "netflix" is "Netflix", "hdmi1" is
        // "HDMI1", "tv" is "TV" - so they resolved by coincidence rather than by design. "primeVideo"
        // is where the coincidence runs out: the file labels it "Prime_Video" and its standard
        // command is "AMAZON_VIDEO", so the key matched neither and every Prime Video tap logged
        // "IR Driver ... does not contain command primeVideo" (verified on the bench 2026-08-19).
        // Map the keys explicitly so no button depends on its name happening to line up.
        public static readonly Dictionary<string, string> InputKeyCommands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "hdmi1", InputHdmi1 },
            { "hdmi2", InputHdmi2 },
            { "hdmi3", InputHdmi3 },
            { "hdmi4", InputHdmi4 },
            { "tv", InputTv },
            { "antenna", InputAntenna },
            { "netflix", Netflix },
            { "primeVideo", PrimeVideo }
        };

        public static string GetCommandValue(string commandName)
        {
            Debug.LogInformation("IrStandardCommands: GetCommandValue() called for commandName-'{0}'", commandName);
            if (CommandDictionary.TryGetValue(commandName, out var value))
                return value;
            return null;
        }

        /// <summary>
        /// Resolves whatever a caller supplied - an <see cref="InputKeyCommands"/> input key, a
        /// constant name from <see cref="CommandDictionary"/>, or an already-resolved standard
        /// command - to the command name to hand the IR port. Resolution is idempotent: passing a
        /// standard command back in returns it unchanged. Anything unrecognized also passes through
        /// unchanged, so a hand-written selector still reaches the driver and its own error log.
        /// </summary>
        public static string Resolve(string selector)
        {
            if (string.IsNullOrEmpty(selector))
                return selector;

            if (InputKeyCommands.TryGetValue(selector, out var byInputKey))
                return byInputKey;

            if (CommandDictionary.TryGetValue(selector, out var byConstantName))
                return byConstantName;

            return selector;
        }
    }
}
