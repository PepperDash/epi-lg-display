using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Epi.Display.Lg;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using DisplayBase = PepperDash.Essentials.Devices.Common.Displays.DisplayBase;


namespace PepperDash.Essentials.Plugins.Lg.Display
{
    public class LgDisplayIrController : DisplayBase, IBasicVolumeControls, IBridgeAdvanced, IHasInputs<string>
    {
        private readonly LgDisplayPropertiesConfig propertiesConfig;

        private IrOutputPortController irController;

        public const int InputPowerOn = 101;
        public const int InputPowerOff = 102;

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


        public LgDisplayIrController(string key, string name, LgDisplayPropertiesConfig config, IrOutputPortController irController)
            : base(key, name)
        {
            this.propertiesConfig = config;
            if (propertiesConfig == null)
            {
                this.LogError("Display configuration must be included");
                return;
            }

            this.irController = irController;
            if (this.irController == null)
            {
                this.LogError("IrOutputPortController instance must be included");
                return;
            }

            SetupInputs();


            DeviceManager.AddDevice(irController);

            CooldownTime = propertiesConfig.coolingTimeMs > 0 ? propertiesConfig.coolingTimeMs : 10000;
            WarmupTime = propertiesConfig.warmingTimeMs > 0 ? propertiesConfig.warmingTimeMs : 8000;
        }


        // protected override void CreateMobileControlMessengers()
        // {
        //     var mc = DeviceManager.AllDevices.OfType<IMobileControl>().FirstOrDefault();
        //     if (mc == null)
        //     {
        //         Debug.LogInformation("Mobile Control not found");
        //         return;
        //     }

        //     var messenger = new LgDisplayIrMobileControlMessenger($"{Key}", $"/device/{Key}", this);
        //     mc.AddDeviceMessenger(messenger);
        // }


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

            this.LogInformation("Linking to Trilist '{TrilistId}'", trilist.ID.ToString("X"));
            this.LogInformation("Linking to Bridge Type {BridgeType}", GetType().Name);

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


        public void SendIrCommand(string cmd)
        {
            if (string.IsNullOrEmpty(cmd))
            {
                this.LogError("SendIrCommand: ir command is null or empty");
                return;
            }

            this.LogInformation("SendIrCommand: ir command '{Command}'", cmd);

            irController?.PressRelease(cmd, true);
            irController?.PressRelease(cmd, false);
        }



        #region  Power Members

        /// <summary>
        /// Set Power On For Device
        /// </summary>
        public override void PowerOn()
        {
            this.LogInformation("PowerOn: ir command '{Command}'", IrStandardCommands.PowerOn);
            SendIrCommand(IrStandardCommands.PowerOn);
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
            this.LogInformation("PowerOff: ir command '{Command}'", IrStandardCommands.PowerOff);
            SendIrCommand(IrStandardCommands.PowerOff);
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
            this.LogInformation("PowerToggle: ir command '{Command}'", IrStandardCommands.PowerToggle);
            SendIrCommand(IrStandardCommands.PowerToggle);
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
                this.LogVerbose("ListRoutingInputPorts: key-'{Key}', connectionType-'{ConnectionType}', feedbackMatchObject-'{FeedbackMatchObject}'",
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
                    { "hdmi1", new LgDisplayIrInput("hdmi1", "HDMI 1", this) },
                    { "hdmi2", new LgDisplayIrInput("hdmi2", "HDMI 2", this) },
                    { "hdmi3", new LgDisplayIrInput("hdmi3", "HDMI 3", this) },
                    { "hdmi4", new LgDisplayIrInput("hdmi4", "HDMI 4", this) },
                    { "tv", new LgDisplayIrInput("tv", "TV", this) },
                    { "antenna", new LgDisplayIrInput("antenna", "Antenna", this) },
                    { "netflix", new LgDisplayIrInput("netflix", "Netflix", this) },
                    { "primeVideo", new LgDisplayIrInput("primeVideo", "Prime Video", this) }
                }
            };

            UpdateInputFriendlyNames(propertiesConfig);
        }
        private void UpdateInputFriendlyNames(LgDisplayPropertiesConfig config)
        {
            if (config?.FriendlyNames == null || Inputs?.Items == null)
                return;

            foreach (var item in config.FriendlyNames)
            {
                this.LogInformation("UpdateInputFriendlyNames: key '{InputKey}', name '{Name}', hideInput '{HideInput}'", item.InputKey, item.Name, item.HideInput);

                if (string.IsNullOrEmpty(item.InputKey))
                {
                    this.LogError("UpdateInputFriendlyNames: InputKey is null or empty");
                    continue;
                }

                if (item.HideInput)
                {
                    Inputs.Items.Remove(item.InputKey);
                }
                else if (Inputs.Items.TryGetValue(item.InputKey, out var inputItem))
                {
                    var updatedInputItem = new LgDisplayIrInput(item.InputKey, item.Name, this);
                    Inputs.Items[item.InputKey] = updatedInputItem;
                }
            }
        }

        /// <summary>
        /// Select Hdmi 1 Input
        /// </summary>
        public void InputHdmi1()
        {
            this.LogInformation("InputHdmi1: ir command '{Command}'", IrStandardCommands.InputHdmi1);
            SendIrCommand(IrStandardCommands.InputHdmi1);
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
            this.LogInformation("InputHdmi2: ir command '{Command}'", IrStandardCommands.InputHdmi2);
            SendIrCommand(IrStandardCommands.InputHdmi2);
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
            this.LogInformation("InputHdmi3: ir command '{Command}'", IrStandardCommands.InputHdmi3);
            SendIrCommand(IrStandardCommands.InputHdmi3);
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
            this.LogInformation("InputHdmi4: ir command '{Command}'", IrStandardCommands.InputHdmi4);
            SendIrCommand(IrStandardCommands.InputHdmi4);
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
            this.LogInformation("InputTv: ir command '{Command}'", IrStandardCommands.InputTv);
            SendIrCommand(IrStandardCommands.InputTv);
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
            this.LogInformation("InputAntenna: ir command '{Command}'", IrStandardCommands.InputAntenna);
            SendIrCommand(IrStandardCommands.InputAntenna);
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
            this.LogInformation("InputNetflix: ir command '{Command}'", IrStandardCommands.Netflix);
            SendIrCommand(IrStandardCommands.Netflix);
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
            this.LogInformation("InputPrimeVideo: ir command '{Command}'", IrStandardCommands.PrimeVideo);
            SendIrCommand(IrStandardCommands.PrimeVideo);
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
            this.LogInformation("InputToggle: ir command '{Command}'", IrStandardCommands.InputToggle);
            SendIrCommand(IrStandardCommands.InputToggle);
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
            this.LogInformation("ExecuteSwitch: selector '{Selector}' type '{SelectorType}'", selector, selector?.GetType().Name ?? "null");



            string cmd = null;

            if (selector is RoutingInputPort port)
            {
                cmd = port.FeedbackMatchObject as string;
                if (string.IsNullOrEmpty(cmd))
                {
                    this.LogError("ExecuteSwitch: command not found for input port '{Key}'", port.Key);
                    return;
                }
            }
            else if (selector is string strCmd)
            {
                cmd = strCmd;
                if (string.IsNullOrEmpty(cmd))
                {
                    this.LogError("ExecuteSwitch: selector is null or empty");
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
                    this.LogError("ExecuteSwitch: selector is null or empty");
                    return;
                }
            }

            // if already on, just send command
            SendIrCommand(cmd);


            // if warming up, wait for warmup to complete before sending command
            EventHandler<FeedbackEventArgs> handler = null; // necessary to allow reference inside lambda to handler
            handler = (o, a) =>
            {
                if (isWarmingUp)
                {
                    return;
                }

                IsWarmingUpFeedback.OutputChange -= handler;

                SendIrCommand(cmd);

            };
            IsWarmingUpFeedback.OutputChange += handler; // attach and wait for on FB
            PowerOn();
        }

        #endregion


        #region Volume Members

        public void VolumeUp(bool pressRelease)
        {
            if (pressRelease) return;

            this.LogInformation("VolumeUp: ir command '{Command}'", IrStandardCommands.VolumeUp);
            SendIrCommand(IrStandardCommands.VolumeUp);
        }

        public void VolumeDown(bool pressRelease)
        {
            if (pressRelease) return;

            this.LogInformation("VolumeDown: ir command '{Command}'", IrStandardCommands.VolumeDown);
            SendIrCommand(IrStandardCommands.VolumeDown);
        }

        public void MuteToggle()
        {
            this.LogInformation("MuteToggle: ir command '{Command}'", IrStandardCommands.MuteToggle);
            SendIrCommand(IrStandardCommands.MuteToggle);
        }

        #endregion
    }
}
