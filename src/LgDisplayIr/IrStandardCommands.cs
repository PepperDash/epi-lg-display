using System;

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

        public static string GetCommandValue(string commandName)
        {
            var fields = typeof(IrStandardCommands).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
            foreach (var field in fields)
            {
                if (string.Equals(field.Name, commandName, StringComparison.OrdinalIgnoreCase))
                {
                    return field.GetValue(null) as string;
                }
            }
            return null;
        }
    }
}
