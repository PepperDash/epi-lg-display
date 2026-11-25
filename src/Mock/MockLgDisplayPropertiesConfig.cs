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
}
