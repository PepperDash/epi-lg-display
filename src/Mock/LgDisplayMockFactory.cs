using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.Plugins.Lg.Display.Mock
{
    /// <summary>
    /// Builds the real <see cref="LgDisplayController"/> on an in-process mock panel, so a room can be
    /// bench-tested without a display. A real config switches to the mock by changing only "type";
    /// any "control" block is ignored.
    /// </summary>
    public class LgDisplayMockFactory : EssentialsPluginDeviceFactory<LgDisplayController>
    {
        /// <summary>
        /// Registers the "lgDisplayMock" type
        /// </summary>
        public LgDisplayMockFactory()
        {
            TypeNames = new List<string> { "lgDisplayMock" };

            MinimumEssentialsFrameworkVersion = "1.8.0";
        }

        /// <inheritdoc />
        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            var config = (dc.Properties ?? new JObject()).ToObject<LgDisplayMockPropertiesConfig>()
                         ?? new LgDisplayMockPropertiesConfig();
            var options = config.Mock ?? new LgMockOptions();

            var responder = new LgMockResponder(ParseSetId(config.Id), options);
            var comms = new LgMockCommunication(dc.Key + "-mockComm", responder, options.ResponseDelayMs);

            LgMockConsole.Register(dc.Key, responder);
            Debug.LogInformation("Building mock LG display '{0}' (no hardware). Console: lgmock {0} status", dc.Key);

            return new LgDisplayController(dc.Key, dc.Name, config, comms);
        }

        // The controller defaults the set ID to "01"; the wire format is hex
        private static int ParseSetId(string id)
        {
            int setId;
            return !string.IsNullOrEmpty(id) && int.TryParse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out setId)
                ? setId
                : 1;
        }
    }
}
