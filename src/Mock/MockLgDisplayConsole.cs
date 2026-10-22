using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using PepperDash.Core;

namespace PepperDash.Essentials.Plugins.Lg.Display
{
    /// <summary>
    /// "mocklg" console command for driving mock panels by device key
    /// </summary>
    internal static class MockLgDisplayConsole
    {
        private const string Usage =
            "mocklg <deviceKey> status | power on|off | input 90|91|92|93|c0 | volume 0-100 | mute on|off | fault none|ng|silent";

        // The console rejects help text over 79 bytes; the full usage prints when the command is run without arguments
        private const string Help = "Mock LG display: mocklg <key> status|power|input|volume|mute|fault";

        private static readonly Dictionary<string, MockLgDisplayResponder> Responders =
            new Dictionary<string, MockLgDisplayResponder>(StringComparer.OrdinalIgnoreCase);

        private static readonly object RegisterLock = new object();
        private static bool commandAdded;

        /// <summary>
        /// Makes a mock panel reachable from the console, adding the command on first use
        /// </summary>
        public static void Register(string deviceKey, MockLgDisplayResponder responder)
        {
            lock (RegisterLock)
            {
                Responders[deviceKey] = responder;

                if (commandAdded)
                {
                    return;
                }

                // The console command is a convenience: never let it stop the display from loading
                // Only a successful add counts, so the next mock to register retries after a failure.
                try
                {
                    commandAdded = CrestronConsole.AddNewConsoleCommand(Handle, "mocklg", Help, ConsoleAccessLevelEnum.AccessOperator);
                    if (!commandAdded)
                    {
                        Debug.LogError("Unable to add the mocklg console command (it may already exist)");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Unable to add the mocklg console command: {0}", e.Message);
                }
            }
        }

        private static void Handle(string args)
        {
            var parts = (args ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Mocks can still be registering while the command runs, so read the dictionary under the lock
            MockLgDisplayResponder responder = null;
            string[] keys;
            lock (RegisterLock)
            {
                if (parts.Length >= 1)
                {
                    Responders.TryGetValue(parts[0], out responder);
                }

                keys = Responders.Keys.ToArray();
            }

            if (parts.Length < 2 || responder == null)
            {
                CrestronConsole.ConsoleCommandResponse("Usage: {0}\r\nMock displays: {1}\r\n", Usage,
                    string.Join(", ", keys));
                return;
            }

            var verb = parts[1].ToLower();
            var arg = parts.Length > 2 ? parts[2].ToLower() : string.Empty;
            var ok = true;

            switch (verb)
            {
                case "status":
                    break;
                case "power":
                    ok = arg == "on" || arg == "off";
                    if (ok) responder.SetPower(arg == "on");
                    break;
                case "input":
                    ok = responder.SetInput(arg);
                    break;
                case "volume":
                    int level;
                    ok = int.TryParse(arg, out level) && level >= 0 && level <= 100;
                    if (ok) responder.SetVolume(level);
                    break;
                case "mute":
                    ok = arg == "on" || arg == "off";
                    if (ok) responder.SetMute(arg == "on");
                    break;
                case "fault":
                    ok = arg == "none" || arg == "ng" || arg == "silent";
                    if (ok) responder.Fault = arg == "ng" ? MockLgDisplayFault.Ng : arg == "silent" ? MockLgDisplayFault.Silent : MockLgDisplayFault.None;
                    break;
                default:
                    ok = false;
                    break;
            }

            if (!ok)
            {
                CrestronConsole.ConsoleCommandResponse("Usage: {0}\r\n", Usage);
                return;
            }

            // Panel-side changes reach the controller on its next poll, like a real panel
            CrestronConsole.ConsoleCommandResponse("{0}: {1}\r\n", parts[0], responder.Describe());
        }
    }
}
