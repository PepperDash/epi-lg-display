using System.Collections.Generic;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.Plugins.Lg.Display
{
    public class LgDisplayIRFactory : EssentialsPluginDeviceFactory<LgDisplayIrController>
    {
        public LgDisplayIRFactory()
        {
            TypeNames = new List<string> { "lgDisplayIr" };

            MinimumEssentialsFrameworkVersion = "2.16.0";
        }


        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            var propertiesConfig = dc.Properties.ToObject<LgDisplayPropertiesConfig>();

            return propertiesConfig == null ? null : new LgDisplayIrController(dc.Key, dc.Name, propertiesConfig);
        }
    }
}