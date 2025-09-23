using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Epi.Display.Lg;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Devices;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Devices.Displays;
using DisplayBase = PepperDash.Essentials.Devices.Common.Displays.DisplayBase;


namespace PepperDash.Essentials.Plugins.Lg.Display
{
    public class LgDisplayIrController : DisplayBase, IBasicVolumeControls, IBridgeAdvanced, IHasInputs<string>
    {
        private readonly LgDisplayPropertiesConfig propertiesConfig;

        private GenericIrController irController;
        private string irCommand;
        public StringFeedback IrCommandFeedback { get; private set; }

        public const int InputPowerOn = 101;
        public const int InputPowerOff = 102;

        public static List<string> InputKeys = new List<string>();
        public ISelectableItems<string> Inputs { get; private set; }


        private bool isWarmingUp;
        public bool IsWarmingUp
        {
            get { return isWarmingUp; }
            set
            {
                isWarmingUp = value;
                IsWarmingUpFeedback.FireUpdate();
            }
        }

        private bool isCoolingDown;
        public bool IsCoolingDown
        {
            get { return isCoolingDown; }
            set
            {
                isCoolingDown = value;
                IsCoolingDownFeedback.FireUpdate();
            }
        }


        public LgDisplayIrController(string key, string name, LgDisplayPropertiesConfig propertiesConfig)
            : base(key, name)
        {
            this.propertiesConfig = propertiesConfig;
            if (propertiesConfig == null)
            {
                Debug.LogError(this, "Display configuration must be included");

                return;
            }

            AddPreActivationAction(() =>
            {
                irController = DeviceManager.GetDeviceForKey(Key) as GenericIrController;
                if (irController == null)
                {
                    Debug.LogError(this, "IR controller '{0}' not found", Key);
                    return;
                }
                else
                {
                    Debug.LogInformation(this, "Using IR controller '{0}'", Key);
                }

                IrCommandFeedback = new StringFeedback(() => irCommand ?? string.Empty);

            });

            CooldownTime = propertiesConfig.coolingTimeMs > 0 ? propertiesConfig.coolingTimeMs : 10000;
            WarmupTime = propertiesConfig.warmingTimeMs > 0 ? propertiesConfig.warmingTimeMs : 8000;
        }

        /// <summary>
        /// Initialization of the device
        /// </summary>
        public override void Initialize()
        {
            SetupInputs();

            base.Initialize();
        }

        /// <summary>
        /// Custom Activate
        /// </summary>
        /// <returns></returns>
        public override bool CustomActivate()
        {
            return base.CustomActivate();
        }

        protected override void CreateMobileControlMessengers()
        {
            var mc = DeviceManager.AllDevices.OfType<IMobileControl>().FirstOrDefault();
            if (mc == null)
            {
                Debug.LogInformation("Mobile Control not found");
                return;
            }

            var messenger = new LgDisplayIrMobileControlMessenger($"{Key}", $"/device/{Key}", this);
            mc.AddDeviceMessenger(messenger);
        }


        #region IBridgeAdvanced Members

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new LgDisplayBridgeJoinMap(joinStart);

            // This adds the join map to the collection on the bridge
            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);
            if (customJoins != null)
            {
                joinMap.SetCustomJoinData(customJoins);
            }

            Debug.LogInformation(this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
            Debug.LogInformation(this, "Linking to Bridge Type {0}", GetType().Name);

            // links to bridge
            // device name
            trilist.SetString(joinMap.Name.JoinNumber, Name);

            // power off/on
            trilist.SetSigTrueAction(joinMap.PowerOff.JoinNumber, PowerOff);
            trilist.SetSigTrueAction(joinMap.PowerOn.JoinNumber, PowerOn);

            // input (digital select, digital feedback, names)
            for (var i = 0; i < InputPorts.Count; i++)
            {
                var inputIndex = i;
                var input = InputPorts.ElementAt(inputIndex);

                if (input == null) continue;

                trilist.SetSigTrueAction((ushort)(joinMap.InputSelectOffset.JoinNumber + inputIndex), () =>
                {
                    ExecuteSwitch(GetInputPort(inputIndex + 1).Selector);
                    //SetInput = inputIndex + 1;
                });

                trilist.StringInput[(ushort)(joinMap.InputNamesOffset.JoinNumber + inputIndex)].StringValue = string.IsNullOrEmpty(input.Key) ? string.Empty : input.Key;

            }

            // input (analog select)
            trilist.SetUShortSigAction(joinMap.InputSelect.JoinNumber, analogValue =>
            {
                ExecuteSwitch(GetInputPort(analogValue).Selector);
                //SetInput = analogValue;
            });


            // bridge online change
            trilist.OnlineStatusChange += (sender, args) =>
            {
                if (!args.DeviceOnLine) return;

                // device name
                trilist.SetString(joinMap.Name.JoinNumber, Name);
            };
        }

        #endregion


        private void SendIrCommand(string cmd)
        {
            if (string.IsNullOrEmpty(cmd))
            {
                Debug.LogError(this, "SendIrCommand: command is null or empty");
                return;
            }

            var irCmd = IrStandardCommands.GetCommandValue(cmd);
            if (string.IsNullOrEmpty(irCmd))
            {
                Debug.LogError(this, "SendIrCommand: ir command '{0}' not found", cmd);
                return;
            }

            irController?.Press(irCmd, true);
            irController?.Press(irCmd, false);
            Debug.LogInformation(this, "SendIrCommand: selector '{0}'", cmd);
        }



        #region  Power Members

        /// <summary>
        /// Set Power On For Device
        /// </summary>
        public override void PowerOn()
        {
            irController?.Press(IrStandardCommands.PowerOn, true);
            irController?.Press(IrStandardCommands.PowerOn, false);
        }

        /// <summary>
        /// Set Power On for Device on press
        /// </summary>
        /// <param name="pressRelease"></param>
        public void PowerOnPress(bool pressRelease)
        {
            if (pressRelease) return;
            PowerOn();
        }


        /// <summary>
        /// Set Power Off for Device
        /// </summary>
        public override void PowerOff()
        {
            irController?.Press(IrStandardCommands.PowerOff, true);
            irController?.Press(IrStandardCommands.PowerOff, false);
        }

        /// <summary>
        /// Set Power Off for Device on press
        /// </summary>
        /// <param name="pressRelease"></param>
        public void PowerOffPress(bool pressRelease)
        {
            if (pressRelease) return;
            PowerOff();
        }



        /// <summary>
        /// Toggle current power state for device
        /// </summary>
        public override void PowerToggle()
        {
            irController?.Press(IrStandardCommands.PowerToggle, true);
            irController?.Press(IrStandardCommands.PowerToggle, false);
        }


        /// <summary>
        /// Toggle current power state for device on press
        /// </summary>
        /// <param name="pressRelease"></param>
        public void PowerTogglePress(bool pressRelease)
        {
            if (pressRelease) return;
            PowerToggle();
        }

        protected override Func<bool> IsCoolingDownFeedbackFunc
        {
            get { return () => IsCoolingDown; }
        }

        protected override Func<bool> IsWarmingUpFeedbackFunc
        {
            get { return () => IsWarmingUp; }
        }

        #endregion



        #region Input Members

        private void AddRoutingInputPort(RoutingInputPort port, string fbMatch)
        {
            port.FeedbackMatchObject = fbMatch;
            InputPorts.Add(port);
        }

        private RoutingInputPort GetInputPort(int input)
        {
            return InputPorts.ElementAt(input);
        }

        /// <summary>
        /// Lists available input routing ports
        /// </summary>
        public void ListRoutingInputPorts()
        {
            foreach (var inputPort in InputPorts)
            {
                Debug.LogVerbose(this, "ListRoutingInputPorts: key-'{0}', connectionType-'{1}', feedbackMatchObject-'{2}'",
                    inputPort.Key, inputPort.ConnectionType, inputPort.FeedbackMatchObject);
            }
        }


        private void SetupInputs()
        {
            AddRoutingInputPort(
                new RoutingInputPort(RoutingPortNames.HdmiIn1, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.Hdmi, new Action(InputHdmi1), this), IrStandardCommands.InputHdmi1);
            AddRoutingInputPort(
                new RoutingInputPort(RoutingPortNames.HdmiIn2, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.Hdmi, new Action(InputHdmi2), this), IrStandardCommands.InputHdmi2);
            AddRoutingInputPort(
                new RoutingInputPort(RoutingPortNames.HdmiIn3, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.Hdmi, new Action(InputHdmi3), this), IrStandardCommands.InputHdmi3);
            AddRoutingInputPort(
                new RoutingInputPort(RoutingPortNames.HdmiIn4, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.Hdmi, new Action(InputHdmi4), this), IrStandardCommands.InputHdmi4);
            AddRoutingInputPort(
                new RoutingInputPort(RoutingPortNames.AnyVideoIn, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.Composite, new Action(InputTv), this), IrStandardCommands.InputTv);
            AddRoutingInputPort(
                new RoutingInputPort(RoutingPortNames.AntennaIn, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.None, new Action(InputAntenna), this), IrStandardCommands.InputAntenna);
            AddRoutingInputPort(
                new RoutingInputPort(RoutingPortNames.AnyVideoIn, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.Streaming, new Action(InputNetflix), this), IrStandardCommands.Netflix);
            AddRoutingInputPort(
                new RoutingInputPort(RoutingPortNames.AnyVideoIn, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.Streaming, new Action(InputPrimeVideo), this), IrStandardCommands.PrimeVideo);

            Inputs = new LgDisplayIrInputs
            {
                Items = new Dictionary<string, ISelectableItem>
                {
                    {
                        "hdmi1", new LgDisplayIrInput("hdmi1", "HDMI 1", this)
                    },
                    {
                        "hdmi2", new LgDisplayIrInput("hdmi2", "HDMI 2", this)
                    },
                    {
                        "hdmi3", new LgDisplayIrInput("hdmi3", "HDMI 3", this)
                    },
                    {
                        "hdmi4", new LgDisplayIrInput("hdmi4", "HDMI 4", this)
                    },
                    {
                        "tv", new LgDisplayIrInput("tv", "TV", this)
                    },
                    {
                        "antenna", new LgDisplayIrInput("antenna", "Antenna", this)
                    },
                    {
                        "netflix", new LgDisplayIrInput("netflix", "Netflix", this)
                    },
                    {
                        "primeVideo", new LgDisplayIrInput("primeVideo", "Prime Video", this)
                    }
                }
            };
            ApplyFriendlyNames(propertiesConfig);

        }
        private void ApplyFriendlyNames(LgDisplayPropertiesConfig config)
        {
            if (config?.FriendlyNames == null || Inputs == null || Inputs.Items == null)
                return;

            foreach (var friendly in config.FriendlyNames)
            {
                if (!string.IsNullOrEmpty(friendly.InputKey) && !string.IsNullOrEmpty(friendly.Name))
                {
                    if (friendly.HideInput)
                    {
                        // Remove the input if hideInput is true
                        Inputs.Items.Remove(friendly.InputKey);
                    }
                    else if (Inputs.Items.TryGetValue(friendly.InputKey, out var input))
                    {
                        // Create a new instance of the input with the updated name  
                        var updatedInput = new LgDisplayIrInput(input.Key, friendly.Name, this);
                        Inputs.Items[friendly.InputKey] = updatedInput;
                    }
                }
            }
        }

        /// <summary>
        /// Select Hdmi 1 Input
        /// </summary>
        public void InputHdmi1()
        {
            irController?.Press(IrStandardCommands.InputHdmi1, true);
            irController?.Press(IrStandardCommands.InputHdmi1, false);
        }

        /// <summary>
        /// Select Hdmi 1 Input on press
        /// </summary>
        /// <param name="pressRelease"></param>
        public void InputHdmi1(bool pressRelease)
        {
            if (pressRelease) return;
            InputHdmi1();
        }

        /// <summary>
        /// Select Hdmi 2 Input
        /// </summary>
        public void InputHdmi2()
        {
            irController?.Press(IrStandardCommands.InputHdmi2, true);
            irController?.Press(IrStandardCommands.InputHdmi2, false);
        }

        /// <summary>
        /// Select Hdmi 2 Input on press
        /// </summary>
        /// <param name="pressRelease"></param>
        public void InputHdmi2(bool pressRelease)
        {
            if (pressRelease) return;
            InputHdmi2();
        }

        /// <summary>
        /// Select Hdmi 3 Input
        /// </summary>
        public void InputHdmi3()
        {
            irController?.Press(IrStandardCommands.InputHdmi3, true);
            irController?.Press(IrStandardCommands.InputHdmi3, false);
        }

        /// <summary>
        /// Select Hdmi 3 Input on press
        /// </summary>
        /// <param name="pressRelease"></param>
        public void InputHdmi3(bool pressRelease)
        {
            if (pressRelease) return;
            InputHdmi3();
        }

        /// <summary>
        /// Select Hdmi 4 Input
        /// </summary>
        public void InputHdmi4()
        {
            irController?.Press(IrStandardCommands.InputHdmi4, true);
            irController?.Press(IrStandardCommands.InputHdmi4, false);
        }

        /// <summary>
        /// Select Hdmi 4 Input on press
        /// </summary>
        /// <param name="pressRelease"></param>
        public void InputHdmi4(bool pressRelease)
        {
            if (pressRelease) return;
            InputHdmi4();
        }

        /// <summary>
        /// Select Tv
        /// </summary>
        public void InputTv()
        {
            irController?.Press(IrStandardCommands.InputTv, true);
            irController?.Press(IrStandardCommands.InputTv, false);
        }

        /// <summary>
        /// Select Tv on press
        /// </summary>
        /// <param name="pressRelease"></param>
        public void InputTv(bool pressRelease)
        {
            if (pressRelease) return;
            InputTv();
        }

        /// <summary>
        /// Select Antenna
        /// </summary>
        public void InputAntenna()
        {
            irController?.Press(IrStandardCommands.InputAntenna, true);
            irController?.Press(IrStandardCommands.InputAntenna, false);
        }

        /// <summary>
        /// Select Antenna on press
        /// </summary>
        /// <param name="pressRelease"></param>
        public void InputAntenna(bool pressRelease)
        {
            if (pressRelease) return;
            InputAntenna();
        }

        /// <summary>
        /// Select Netflix
        /// </summary>
        public void InputNetflix()
        {
            irController?.Press(IrStandardCommands.Netflix, true);
            irController?.Press(IrStandardCommands.Netflix, false);
        }

        /// <summary>
        /// Select Netflix on press
        /// </summary>
        /// <param name="pressRelease"></param>
        public void InputNetflix(bool pressRelease)
        {
            if (pressRelease) return;
            InputNetflix();
        }

        /// <summary>
        /// Select Amazon Prime Video
        /// </summary>
        public void InputPrimeVideo()
        {
            irController?.Press(IrStandardCommands.PrimeVideo, true);
            irController?.Press(IrStandardCommands.PrimeVideo, false);
        }

        /// <summary>
        /// Select Amazon Prime Video on press
        /// </summary>
        /// <param name="pressRelease"></param>
        public void InputPrimeVideo(bool pressRelease)
        {
            if (pressRelease) return;
            InputPrimeVideo();
        }

        public void InputToggle()
        {
            irController?.Press(IrStandardCommands.InputToggle, true);
            irController?.Press(IrStandardCommands.InputToggle, false);
        }

        public void InputToggle(bool pressRelease)
        {
            if (pressRelease) return;
            InputToggle();
        }

        /// <summary>
        /// Executes a switch, turning on display if necessary.
        /// </summary>
        /// <param name="selector"></param>
        public override void ExecuteSwitch(object selector)
        {
            Debug.LogInformation(this, $"ExecuteSwitch: selector '{selector}' type '{selector?.GetType().Name ?? "null"}'");

            string cmd = null;

            if (selector is RoutingInputPort port)
            {
                cmd = port.FeedbackMatchObject as string;
                if (string.IsNullOrEmpty(cmd))
                {
                    Debug.LogError(this, "ExecuteSwitch: command not found for input port '{0}'", port.Key);
                    return;
                }
            }
            else if (selector is string strCmd)
            {
                cmd = strCmd;
                if (string.IsNullOrEmpty(cmd))
                {
                    Debug.LogError(this, "ExecuteSwitch: selector is null or empty");
                    return;
                }
            }
            else if (selector is int intCmd)
            {
                cmd = intCmd.ToString();
            }
            else if (selector is ushort ushortCmd)
            {
                cmd = ushortCmd.ToString();
            }
            else
            {
                cmd = selector?.ToString();
                if (string.IsNullOrEmpty(cmd))
                {
                    Debug.LogError(this, "ExecuteSwitch: selector is null or empty");
                    return;
                }
            }

            SendIrCommand(cmd);
        }

        #endregion


        #region Volume Members

        public void VolumeUp(bool pressRelease)
        {
            if (pressRelease) return;
            irController?.Press(IrStandardCommands.VolumeUp, true);
            irController?.Press(IrStandardCommands.VolumeUp, false);
        }

        public void VolumeDown(bool pressRelease)
        {
            if (pressRelease) return;
            irController?.Press(IrStandardCommands.VolumeDown, true);
            irController?.Press(IrStandardCommands.VolumeDown, false);
        }

        public void MuteToggle()
        {
            irController?.Press(IrStandardCommands.MuteToggle, true);
            irController?.Press(IrStandardCommands.MuteToggle, false);
        }

        public void MuteToggle(bool pressRelease)
        {
            if (pressRelease) return;
            MuteToggle();
        }

        #endregion
    }
}
