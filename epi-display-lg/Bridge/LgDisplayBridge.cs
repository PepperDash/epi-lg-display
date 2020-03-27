using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharp.Reflection;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using Newtonsoft.Json;
using Epi.Display.Lg;
using Epi.Display.Lg.Bridge.JoinMap;

namespace Epi.Display.Lg.Bridge
{
    public static class LgDisplayBridge
    {
        /// <summary>
        /// Link to API using bridge map
        /// </summary>
        /// <param name="displayDevice"></param>
        /// <param name="trilist"></param>
        /// <param name="joinStart"></param>
        /// <param name="joinMapKey"></param>
        public static void LinkToApiExt(this LgDisplay displayDevice, BasicTriList trilist, uint joinStart, string joinMapKey)
        {
            //int inputNumber = 0;
            //IntFeedback inputNumberFeedback;
            //List<string> inputKeys = new List<string>();

            LgDisplayJoinMap joinMap = new LgDisplayJoinMap();

            var joinMapSerialized = JoinMapHelper.GetJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<LgDisplayJoinMap>(joinMapSerialized);

            joinMap.OffsetJoinNumbers(joinStart);
            Debug.Console(2, displayDevice, "JoinStart: {0}", joinStart);

            Debug.Console(1, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
            Debug.Console(0, "Linking to Display: {0}", displayDevice.Name);
            Debug.Console(2, "I'm starting at Join: {0}", joinMap.Name);

            trilist.StringInput[joinMap.Name].StringValue = displayDevice.Name;
            Debug.Console(2, displayDevice, "Setting Name Feedback on Seerial Join {0}", joinMap.Name);

            var commMonitor = displayDevice as ICommunicationMonitor;
            if (commMonitor != null)
            {
                commMonitor.CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline]);
            }

            displayDevice.InputNumberFeedback.LinkInputSig(trilist.UShortInput[joinMap.InputSelect]);
            Debug.Console(2, displayDevice, "Setting Input Number Feedback on Analog Join {0}", joinMap.InputSelect);

            // Two way feedbacks
            var twoWayDisplay = displayDevice as PepperDash.Essentials.Core.TwoWayDisplayBase;
            if (twoWayDisplay != null)
            {
                trilist.SetBool(joinMap.IsTwoWayDisplay, true);
                Debug.Console(2, displayDevice, "Setting IsTwoWayDisplay Feedback on Digital Join {0}", joinMap.IsTwoWayDisplay);
                twoWayDisplay.CurrentInputFeedback.OutputChange += new EventHandler<FeedbackEventArgs>(CurrentInputFeedback_OutputChange);
            }

            // Power Off
            trilist.SetSigTrueAction(joinMap.PowerOff, () =>
            {
                displayDevice.PowerOff();
            });
            displayDevice.PowerIsOnFeedback.OutputChange += new EventHandler<FeedbackEventArgs>(PowerIsOnFeedback_OutputChange);
            displayDevice.PowerIsOnFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.PowerOff]);
            Debug.Console(2, displayDevice, "Setting PowerOff Control & Feedback on Digital Join {0}", joinMap.PowerOff);

            // PowerOn
            trilist.SetSigTrueAction(joinMap.PowerOn, () =>
            {
                displayDevice.PowerOn();
            });
            displayDevice.PowerIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PowerOn]);
            Debug.Console(2, displayDevice, "Setting PowerOn Control & Feedback on Digital Join {0}", joinMap.PowerOn);

            // Input digitals
            int count = 1;
            var displayBase = displayDevice as PepperDash.Essentials.Core.DisplayBase;

            foreach (var input in displayDevice.InputPorts)
            {
                var i = input;
                trilist.SetSigTrueAction((ushort)(joinMap.InputSelectOffset + count), () => { displayDevice.ExecuteSwitch(displayDevice.InputPorts[i.Key.ToString()].Selector); });
                Debug.Console(2, displayDevice, "Setting Input Press Select Action on Digital Join {0} to Input: {1}", joinMap.InputSelectOffset + count, displayDevice.InputPorts[i.Key.ToString()].Key.ToString());
                trilist.StringInput[(ushort)(joinMap.InputNamesOffset + count)].StringValue = i.Key.ToString();
                displayDevice.InputFeedback[count].LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset + (uint)count]);
                count++;
            }


            // Input analog
            Debug.Console(2, displayDevice, "Setting Input Value Select Action on Analog Join {0}", joinMap.InputSelect);
            trilist.SetUShortSigAction(joinMap.InputSelect, (a) =>
            {
                if (a == 0)
                {
                    displayDevice.PowerOff();
                }
                else if (a > 0 && a < displayDevice.InputPorts.Count)
                {
                    displayDevice.ExecuteSwitch(displayDevice.InputPorts.ElementAt(a - 1).Selector);
                }
                else if (a == 102)
                {
                    displayDevice.PowerToggle();
                }
                Debug.Console(2, displayDevice, "InputChange {0}", a);
            });

            // Volume
            var volumeDisplay = displayDevice as IBasicVolumeControls;
            if (volumeDisplay != null)
            {
                Debug.Console(2, displayDevice, "Setting VolumeUp Control on Digital Join {0}", joinMap.VolumeUp);
                trilist.SetBoolSigAction(joinMap.VolumeUp, (b) => volumeDisplay.VolumeUp(b));

                Debug.Console(2, displayDevice, "Setting VolumeDown Control on Digital Join {0}", joinMap.VolumeDown);
                trilist.SetBoolSigAction(joinMap.VolumeDown, (b) => volumeDisplay.VolumeDown(b));

                Debug.Console(2, displayDevice, "Setting VolumeMuteToggle Control & Feedback on Digital Join {0}", joinMap.VolumeMute);
                trilist.SetSigTrueAction(joinMap.VolumeMute, () => volumeDisplay.MuteToggle());

                trilist.SetSigTrueAction(joinMap.VolumeMuteOn, () => displayDevice.MuteOn());
                trilist.SetSigTrueAction(joinMap.VolumeMuteOff, () => displayDevice.MuteOff());

                var volumeDisplayWithFeedback = volumeDisplay as IBasicVolumeWithFeedback;
                if (volumeDisplayWithFeedback != null)
                {
                    trilist.SetUShortSigAction(joinMap.VolumeLevel, new Action<ushort>((u) => volumeDisplayWithFeedback.SetVolume(u)));
                    Debug.Console(2, displayDevice, "Setting VolumeLevel Control & Feedback on Analog Join {0}", joinMap.VolumeLevel);

                    volumeDisplayWithFeedback.VolumeLevelFeedback.LinkInputSig(trilist.UShortInput[joinMap.VolumeLevel]);
                    volumeDisplayWithFeedback.MuteFeedback.LinkInputSig(trilist.BooleanInput[joinMap.VolumeMuteOn]);
                    volumeDisplayWithFeedback.MuteFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.VolumeMuteOff]);
                }
            }
        }

        /// <summary>
        /// Current inpout feedback change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void CurrentInputFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            Debug.Console(0, "CurrentInputFeedback_OutputChange {0}", e.StringValue);

        }

        /// <summary>
        /// PowerIsOn feedback change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void PowerIsOnFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            // Debug.Console(0, "PowerIsOnFeedback_OutputChange {0}",  e.BoolValue);
            if (!e.BoolValue)
            {


            }
            else
            {

            }
        }
    }
}