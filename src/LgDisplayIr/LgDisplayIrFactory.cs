using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using Serilog.Events;

namespace PepperDash.Essentials.Plugins.Lg.Display
{
    public class LgDisplayIRFactory : EssentialsPluginDeviceFactory<LgDisplayIrController>
    {
        public LgDisplayIRFactory()
        {
            TypeNames = new List<string> { "lgDisplayIr" };

            // Keep equal to the PepperDashEssentials PackageReference version in the csproj: the version
            // this plugin is compiled against is its minimum
            MinimumEssentialsFrameworkVersion = "3.0.0-rc.20";
        }


        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            var irPort = IRPortHelper.GetIrOutputPortController(dc);
            if (irPort == null)
            {
                Debug.LogMessage(LogEventLevel.Error, "No IR Output Port Controller found for device '{Key}'. Cannot create LgDisplayIrController", null, dc.Key);
                return null;
            }
            var propertiesConfig = dc.Properties.ToObject<LgDisplayPropertiesConfig>();

            return propertiesConfig == null ? null : new LgDisplayIrController(dc.Key, dc.Name, propertiesConfig, irPort);
        }
    }
}