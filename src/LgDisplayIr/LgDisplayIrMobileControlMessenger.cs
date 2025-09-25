using System.Collections.Generic;
using Crestron.SimplSharp;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.AppServer.Messengers;

namespace PepperDash.Essentials.Plugins.Lg.Display
{
    class LgDisplayIrMobileControlMessenger : MessengerBase
    {
        private readonly LgDisplayIrController device;
        public LgDisplayIrMobileControlMessenger(string key, string messagePath, LgDisplayIrController device)
                : base(key, messagePath, device)
        {
            this.device = device;
            Debug.LogInformation("Constructing messenger for {0}", device.Key);
        }

        protected override void RegisterActions()
        {
            Debug.LogInformation("Registering actions for {0}", device.Key);

            // Register action to send IR command
            // - this is composited with the /device/{device-key} path to handle the correct message. ie, the full path from the frontend is /device/{device-key}/irCommand. The path in the constructor MUST follow the basic pattern defined there
            // AddAction("/powerToggle", (id, content) => PressAndHoldHandler.HandlePressAndHold(DeviceKey, content, device.PowerTogglePress));
            // AddAction("/irCommand/powerOn", (id, content) => PressAndHoldHandler.HandlePressAndHold(DeviceKey, content, device.PowerOnPress));
            // AddAction("/irCommand/powerOff", (id, content) => PressAndHoldHandler.HandlePressAndHold(DeviceKey, content, device.PowerOffPress));
            // AddAction("/irCommand/hdmi1", (id, content) => PressAndHoldHandler.HandlePressAndHold(DeviceKey, content, device.InputHdmi1));
            // AddAction("/irCommand/hdmi2", (id, content) => PressAndHoldHandler.HandlePressAndHold(DeviceKey, content, device.InputHdmi2));
            // AddAction("/irCommand/hdmi3", (id, content) => PressAndHoldHandler.HandlePressAndHold(DeviceKey, content, device.InputHdmi3));
            // AddAction("/irCommand/hdmi4", (id, content) => PressAndHoldHandler.HandlePressAndHold(DeviceKey, content, device.InputHdmi4));
            // AddAction("/irCommand/tv", (id, content) => PressAndHoldHandler.HandlePressAndHold(DeviceKey, content, device.InputTv));
            // AddAction("/irCommand/antenna", (id, content) => PressAndHoldHandler.HandlePressAndHold(DeviceKey, content, device.InputAntenna));
            // AddAction("/irCommand/netflix", (id, content) => PressAndHoldHandler.HandlePressAndHold(DeviceKey, content, device.InputNetflix));
            // AddAction("/irCommand/primeVideo", (id, content) => PressAndHoldHandler.HandlePressAndHold(DeviceKey, content, device.InputPrimeVideo));
            AddAction("/volumeUp", (id, content) => PressAndHoldHandler.HandlePressAndHold(DeviceKey, content, device.VolumeUp));
            AddAction("/volumeDown", (id, content) => PressAndHoldHandler.HandlePressAndHold(DeviceKey, content, device.VolumeDown));
            AddAction("/muteToggle", (id, content) => PressAndHoldHandler.HandlePressAndHold(DeviceKey, content, device.MuteToggle));
        }
    }
}

