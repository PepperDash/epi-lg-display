using Newtonsoft.Json;

namespace PepperDash.Essentials.Plugins.Lg.Display
{
    /// <summary>
    /// Config for the "mockLgDisplay" type: the real display properties plus an optional "mock" block
    /// </summary>
    public class MockLgDisplayPropertiesConfig : LgDisplayPropertiesConfig
    {
        /// <summary>
        /// Starting state and behavior of the mock panel
        /// </summary>
        [JsonProperty("mock", NullValueHandling = NullValueHandling.Ignore)]
        public MockLgDisplayOptions Mock { get; set; }
    }

    /// <summary>
    /// Starting state and behavior of the mock panel
    /// </summary>
    public class MockLgDisplayOptions
    {
        /// <summary>
        /// Creates the defaults: panel off, HDMI 1, volume 20, 50ms replies
        /// </summary>
        public MockLgDisplayOptions()
        {
            Input = "90";
            Volume = 20;
            ResponseDelayMs = 50;
        }

        /// <summary>
        /// Delay before the panel answers each command, in ms
        /// </summary>
        [JsonProperty("responseDelayMs")]
        public int ResponseDelayMs { get; set; }

        /// <summary>
        /// Whether the panel starts powered on
        /// </summary>
        [JsonProperty("powerOn")]
        public bool PowerOn { get; set; }

        /// <summary>
        /// Starting input code: 90-93 (HDMI 1-4) or c0 (DisplayPort)
        /// </summary>
        [JsonProperty("input")]
        public string Input { get; set; }

        /// <summary>
        /// Starting volume, 0-100
        /// </summary>
        [JsonProperty("volume")]
        public int Volume { get; set; }

        /// <summary>
        /// Whether the panel starts muted
        /// </summary>
        [JsonProperty("muted")]
        public bool Muted { get; set; }

        /// <summary>
        /// Whether the panel starts video muted (screen off)
        /// </summary>
        [JsonProperty("videoMuted")]
        public bool VideoMuted { get; set; }
    }
}
