using System.Collections.Generic;
using System.Diagnostics;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Devices;

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
            // var irPort = IRPortHelper.GetIrOutputPortController(dc);
            // if (irPort == null)
            // {
            //     Debug.WriteLine($"No IR Output Port Controller found for device '{dc.Key}'. Cannot create LgDisplayIrController");
            //     return null;
            // }

            var propertiesConfig = dc.Properties.ToObject<LgDisplayPropertiesConfig>();

            return propertiesConfig == null ? null : new LgDisplayIrController(dc.Key, dc.Name, propertiesConfig);
        }
    }
}