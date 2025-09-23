using System.Collections.Generic;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.Plugins.Lg.Display
{
    public class LgDisplayIRControllerFactory : EssentialsPluginDeviceFactory<LgDisplayController>
    {
        public LgDisplayIRControllerFactory()
        {
            TypeNames = new List<string> { "lgDisplayIr" };

            MinimumEssentialsFrameworkVersion = "2.16.0";
        }

        #region Overrides of EssentialsDeviceFactory<LgDisplayController>

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {

            var comms = CommFactory.CreateCommForDevice(dc);

            if (comms == null) return null;

            var config = dc.Properties.ToObject<LgDisplayPropertiesConfig>();

            return config == null ? null : new LgDisplayIrController(dc.Key, dc.Name, config, comms);
        }

        #endregion
    }
}