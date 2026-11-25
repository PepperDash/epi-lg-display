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

        [JsonProperty("wolBroadcastAddress")]
        public string WolBroadcastAddress { get; set; }

        [JsonProperty("wolPort")]
        public ushort? WolPort { get; set; }

        [JsonProperty("smallDisplay")]
        public bool SmallDisplay { get; set; }

        [JsonProperty("overrideWol")]
        public bool OverrideWol { get; set; }

        /// <summary>
        /// Set true for the LG UM5K series, verified on model 110UM5K-BP.CUSFLR (110" UM5K). That
        /// model uses input code a6 for HDMI 3 instead of 92: on site, "xb 01 92" was rejected with
        /// "b 01 NG92x", and with the panel switched to HDMI 3 from the remote the input poll
        /// answered "b 01 OKa6x". When true, HDMI 3 is selected with a6 and an a6 poll reply is
        /// reported as HDMI 3 (routing port hdmiIn3, input list key "a6"). The panel is never sent
        /// 92: a friendlyNames entry or SelectInput call that names HDMI 3 by the standard key "92"
        /// is translated to "a6" first.
        /// Only HDMI 3 is remapped: HDMI 1 (90), HDMI 2 (91), HDMI 4 (93) and DisplayPort (c0) have
        /// not been verified on this model and keep their standard codes. This model has no HDMI 4
        /// port (hide input 93 with a friendlyNames entry, "hideInput": true); it does have a
        /// DisplayPort input.
        /// Defaults to false, which leaves every input code unchanged.
        /// </summary>
        // NullValueHandling.Ignore so an explicit null keeps the default rather than throwing in
        // ToObject and stopping the device from being created.
        [JsonProperty("um5kSeriesInputCodes", NullValueHandling = NullValueHandling.Ignore)]
        public bool Um5kSeriesInputCodes { get; set; }

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
        //
        // NullValueHandling.Ignore so an explicit null keeps the default rather than throwing in
        // ToObject and stopping the device from being created.
        [JsonProperty("remoteOnly", NullValueHandling = NullValueHandling.Ignore)]
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