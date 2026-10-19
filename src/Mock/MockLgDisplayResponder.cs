using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PepperDash.Essentials.Plugins.Lg.Display.Mock
{
    /// <summary>
    /// How the mock panel misbehaves, for exercising the controller's error paths
    /// </summary>
    internal enum MockLgDisplayFault
    {
        /// <summary>Answer every command normally</summary>
        None,
        /// <summary>Answer every command with NG</summary>
        Ng,
        /// <summary>Never answer, so the communication monitor goes offline</summary>
        Silent
    }

    /// <summary>
    /// Answers LG commercial display RS232/IP commands the way a real panel does.
    /// Holds the panel state only and uses no Crestron types, so it can be linked into tests.
    /// </summary>
    /// <remarks>
    /// rx: "{c1}{c2} {setId} {data}"  e.g. "ka 01 ff"
    /// tx: "{c2} {setId} OK{data}x"   e.g. "a 01 OK01x"
    ///     "{c2} {setId} NG{data}x"   bad data, or the panel is off
    /// All values are hex on the wire.
    /// </remarks>
    internal class MockLgDisplayResponder
    {
        private const int QueryValue = 0xFF;
        private const int MaxVolume = 0x64;

        private static readonly Regex CommandRegex =
            new Regex(@"^([a-z])([a-z]) ([0-9a-f]{1,2}) ([0-9a-f]{1,2})$", RegexOptions.IgnoreCase);

        // A real panel answers NG to everything but power while it is off
        private static readonly HashSet<string> CommandsNeedingPower = new HashSet<string> { "xb", "kf", "ke", "kd" };

        /// <summary>
        /// Input codes the panel accepts on "xb", matching the controller's routing ports
        /// </summary>
        public static readonly HashSet<string> ValidInputs = new HashSet<string> { "90", "91", "92", "93", "c0" };

        private readonly object stateLock = new object();
        private bool powerIsOn;
        private string input;
        private int volume;
        private bool isMuted;
        private bool videoIsMuted;

        /// <summary>
        /// Creates a responder for the panel with the given set ID
        /// </summary>
        public MockLgDisplayResponder(int setId, MockLgDisplayOptions options)
        {
            options = options ?? new MockLgDisplayOptions();

            SetId = setId;
            powerIsOn = options.PowerOn;
            input = ValidInputs.Contains((options.Input ?? string.Empty).ToLower()) ? options.Input.ToLower() : "90";
            volume = Math.Max(0, Math.Min(MaxVolume, options.Volume));
            isMuted = options.Muted;
            videoIsMuted = options.VideoMuted;
        }

        /// <summary>
        /// The set ID the panel answers to (00 is the broadcast ID and is always accepted)
        /// </summary>
        public int SetId { get; private set; }

        /// <summary>
        /// Current fault mode
        /// </summary>
        public MockLgDisplayFault Fault { get; set; }

        /// <summary>
        /// Answers one command line (without the \r delimiter)
        /// </summary>
        /// <returns>The reply including the trailing "x", or null when the panel stays silent</returns>
        public string Respond(string line)
        {
            var match = CommandRegex.Match((line ?? string.Empty).Trim());
            if (!match.Success)
            {
                return null;
            }

            var c1 = match.Groups[1].Value.ToLower();
            var c2 = match.Groups[2].Value.ToLower();
            var command = c1 + c2;
            var rxId = int.Parse(match.Groups[3].Value, NumberStyles.HexNumber);
            var data = match.Groups[4].Value.ToLower().PadLeft(2, '0');
            var value = int.Parse(data, NumberStyles.HexNumber);

            // Unknown commands get no reply, so gaps in the mock show up as unanswered commands
            if (!IsKnown(command) || (rxId != SetId && rxId != 0))
            {
                return null;
            }

            lock (stateLock)
            {
                if (Fault == MockLgDisplayFault.Silent)
                {
                    return null;
                }

                var rejected = Fault == MockLgDisplayFault.Ng || (CommandsNeedingPower.Contains(command) && !powerIsOn);
                var result = rejected ? null : Handle(command, value);

                return result == null
                    ? string.Format("{0} {1:x2} NG{2}x", c2, SetId, data)
                    : string.Format("{0} {1:x2} OK{2}x", c2, SetId, result);
            }
        }

        /// <summary>
        /// Changes state as if someone used the panel's own buttons or remote
        /// </summary>
        public void SetPower(bool on)
        {
            lock (stateLock) powerIsOn = on;
        }

        /// <summary>
        /// Changes the input as if someone used the panel's own buttons or remote
        /// </summary>
        /// <returns>False if the input code is not one the panel supports</returns>
        public bool SetInput(string inputCode)
        {
            var code = (inputCode ?? string.Empty).ToLower();
            if (!ValidInputs.Contains(code))
            {
                return false;
            }

            lock (stateLock) input = code;
            return true;
        }

        /// <summary>
        /// Changes the volume (0-100) as if someone used the panel's own buttons or remote
        /// </summary>
        public void SetVolume(int level)
        {
            lock (stateLock) volume = Math.Max(0, Math.Min(MaxVolume, level));
        }

        /// <summary>
        /// Changes the mute state as if someone used the panel's own buttons or remote
        /// </summary>
        public void SetMute(bool muted)
        {
            lock (stateLock) isMuted = muted;
        }

        /// <summary>
        /// One-line description of the panel state, for the console
        /// </summary>
        public string Describe()
        {
            lock (stateLock)
            {
                return string.Format("setId={0:x2} power={1} input={2} volume={3} mute={4} videoMute={5} fault={6}",
                    SetId, powerIsOn ? "on" : "off", input, volume, isMuted, videoIsMuted, Fault);
            }
        }

        private static bool IsKnown(string command)
        {
            return command == "ka" || command == "xb" || command == "kf" || command == "ke" || command == "kd";
        }

        // Returns the OK data, or null for NG
        private string Handle(string command, int value)
        {
            switch (command)
            {
                case "ka":
                    if (value == 0 || value == 1) powerIsOn = value == 1;
                    else if (value != QueryValue) return null;
                    return powerIsOn ? "01" : "00";

                case "xb":
                    if (value != QueryValue)
                    {
                        var code = value.ToString("x2");
                        if (!ValidInputs.Contains(code)) return null;
                        input = code;
                    }
                    return input;

                case "kf":
                    if (value != QueryValue)
                    {
                        if (value > MaxVolume) return null;
                        volume = value;
                    }
                    return volume.ToString("x2");

                case "ke":
                    // 00 = muted, 01 = unmuted
                    if (value == 0 || value == 1) isMuted = value == 0;
                    else if (value != QueryValue) return null;
                    return isMuted ? "00" : "01";

                case "kd":
                    // 01 = screen off (video muted), 00 = screen on
                    if (value == 0 || value == 1) videoIsMuted = value == 1;
                    else if (value != QueryValue) return null;
                    return videoIsMuted ? "01" : "00";

                default:
                    return null;
            }
        }
    }
}
