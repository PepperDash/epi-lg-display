using Newtonsoft.Json;
using PepperDash.Core;
using System.Collections.Generic;

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