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
        public const string DpadDown = "DOWN_ARROW";
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

        public static string GetCommandValue(string commandName)
        {
            Debug.LogInformation("IrStandardCommands: GetCommandValue() called for commandName-'{0}'", commandName);
            if (CommandDictionary.TryGetValue(commandName, out var value))
                return value;
            return null;
        }
    }
}
