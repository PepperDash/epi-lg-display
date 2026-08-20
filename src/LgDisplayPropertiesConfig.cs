using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Core;

namespace PepperDash.Essentials.Plugins.Lg.Display
{
    public class LgDisplayPropertiesConfig
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("volumeUpperLimit")]
        public int volumeUpperLimit { get; set; }

        [JsonProperty("volumeLowerLimit")]
        public int volumeLowerLimit { get; set; }

        [JsonProperty("pollIntervalMs")]
        public long pollIntervalMs { get; set; }

        [JsonProperty("coolingTimeMs")]
        public uint coolingTimeMs { get; set; }

        [JsonProperty("warmingTimeMs")]
        public uint warmingTimeMs { get; set; }

        [JsonProperty("udpSocketKey")]
        public string udpSocketKey { get; set; }

        [JsonProperty("macAddress")]
        public string macAddress { get; set; }

        [JsonProperty("smallDisplay")]
        public bool SmallDisplay { get; set; }

        [JsonProperty("overrideWol")]
        public bool OverrideWol { get; set; }

        [JsonProperty("friendlyNames")]
        public List<FriendlyName> FriendlyNames { get; set; }

        // Set true when this device is a control-only REMOTE paired alongside another driver that
        // owns the same panel - e.g. an RS232 LG for power/inputs/feedback plus this IR driver for
        // dpad, keypad, transport and app buttons.
        //
        // It suppresses every power command this driver would otherwise send, including the implicit
        // PowerOn that ExecuteSwitch fires before switching an input. That implicit power-on is
        // correct when this driver IS the display (blind IR cannot check power state, so switching an
        // input has to assume the panel may be off) but wrong for a remote: the paired RS232 device
        // owns power because it is the only one of the pair with real feedback, and two blind power
        // paths to one panel is how state desyncs.
        [JsonProperty("remoteOnly")]
        public bool RemoteOnly { get; set; }

        public LgDisplayPropertiesConfig()
        {
            FriendlyNames = new List<FriendlyName>();
        }
    }

    public class FriendlyName
    {
        [JsonProperty("inputKey")]
        public string InputKey { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("hideInput")]
        public bool HideInput { get; set; }
    }

}