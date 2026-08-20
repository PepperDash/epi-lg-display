using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Epi.Display.Lg;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using DisplayBase = PepperDash.Essentials.Devices.Common.Displays.DisplayBase;


namespace PepperDash.Essentials.Plugins.Lg.Display
{
    public class LgDisplayIrController : DisplayBase, IBasicVolumeControls, IBridgeAdvanced, IHasInputs<string>,
        IDPad, INumericKeypad, ITransport, IChannel, IColor
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
                Debug.LogError(this, "Display configuration must be included");
                return;
            }

            this.irController = irController;
            if (this.irController == null)
            {
                Debug.LogError(this, "IrOutputPortController instance must be included");
                return;
            }

            SetupInputs();


            DeviceManager.AddDevice(irController);

            CooldownTime = propertiesConfig.coolingTimeMs > 0 ? propertiesConfig.coolingTimeMs : 10000;
            WarmupTime = propertiesConfig.warmingTimeMs > 0 ? propertiesConfig.warmingTimeMs : 8000;
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


        public void SendIrCommand(string cmd)
        {
            if (string.IsNullOrEmpty(cmd))
            {
                Debug.LogError(this, "SendIrCommand: ir command is null or empty");
                return;
            }

            // Resolved here rather than at each call site: ExecuteSwitch's string branch passes an
            // Inputs key straight through, and doing it once means no future caller can bypass it.
            // Resolution is idempotent, so the PowerOn/Menu/dpad paths that already pass standard
            // commands are unaffected.
            var resolved = IrStandardCommands.Resolve(cmd);

            if (!string.Equals(resolved, cmd, StringComparison.Ordinal))
                Debug.LogVerbose(this, "SendIrCommand: resolved selector '{0}' to ir command '{1}'", cmd, resolved);

            Debug.LogInformation(this, "SendIrCommand: ir command '{0}'", resolved);

            irController?.PressRelease(resolved, true);
            irController?.PressRelease(resolved, false);
        }



        #region  Power Members

        /// <summary>
        /// Set Power On For Device
        /// </summary>
        public override void PowerOn()
        {
            if (SuppressPower("PowerOn")) return;

            Debug.LogInformation(this, "PowerOn: ir command '{0}'", IrStandardCommands.PowerOn);
            SendIrCommand(IrStandardCommands.PowerOn);
        }

        /// <summary>
        /// True when this device is configured as a control-only remote, in which case it must never
        /// send power - the paired driver that owns the panel owns power. Logged rather than silent,
        /// because a suppressed power command is otherwise indistinguishable from a dead IR emitter.
        /// </summary>
        private bool SuppressPower(string caller)
        {
            if (!propertiesConfig.RemoteOnly) return false;

            Debug.LogVerbose(this, "{0} suppressed: remoteOnly is set, so the paired display driver owns power", caller);
            return true;
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
            if (SuppressPower("PowerOff")) return;

            Debug.LogInformation(this, "PowerOff: ir command '{0}'", IrStandardCommands.PowerOff);
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
            if (SuppressPower("PowerToggle")) return;

            Debug.LogInformation(this, "PowerToggle: ir command '{0}'", IrStandardCommands.PowerToggle);
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
            // TODO: Disney+ and Samsung TV Plus are not in FlatPanelDisplay_LG_65SK9500-SmartTV.ir,
            // and their command names were guesses by convention rather than read off a driver.
            // Registering them anyway would advertise inputs the display cannot switch to - the
            // front end lists whatever this dictionary holds, so they rendered as dead buttons.
            // If codes turn up, add them to the .ir file first, then restore the constants, the
            // routing ports and these entries together.

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
                Debug.LogInformation(this, $"UpdateInputFriendlyNames: key '{item.InputKey}', name '{item.Name}', hideInput '{item.HideInput}'");

                if (string.IsNullOrEmpty(item.InputKey))
                {
                    Debug.LogError(this, "UpdateInputFriendlyNames: InputKey is null or empty");
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
            Debug.LogInformation(this, "InputHdmi1: ir command '{0}'", IrStandardCommands.InputHdmi1);
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
            Debug.LogInformation(this, "InputHdmi2: ir command '{0}'", IrStandardCommands.InputHdmi2);
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
            Debug.LogInformation(this, "InputHdmi3: ir command '{0}'", IrStandardCommands.InputHdmi3);
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
            Debug.LogInformation(this, "InputHdmi4: ir command '{0}'", IrStandardCommands.InputHdmi4);
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
            Debug.LogInformation(this, "InputTv: ir command '{0}'", IrStandardCommands.InputTv);
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
            Debug.LogInformation(this, "InputAntenna: ir command '{0}'", IrStandardCommands.InputAntenna);
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
            Debug.LogInformation(this, "InputNetflix: ir command '{0}'", IrStandardCommands.Netflix);
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
            Debug.LogInformation(this, "InputPrimeVideo: ir command '{0}'", IrStandardCommands.PrimeVideo);
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
            Debug.LogInformation(this, "InputToggle: ir command '{0}'", IrStandardCommands.InputToggle);
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

            // if already on, just send command
            SendIrCommand(cmd);

            // A remote never powers the panel, so there is no power-on to wait on and nothing to
            // re-send afterwards. Returning here also avoids the second SendIrCommand below, which
            // for an app button would fire the same IR twice.
            if (propertiesConfig.RemoteOnly)
            {
                Debug.LogVerbose(this, "ExecuteSwitch: remoteOnly is set, skipping the implicit PowerOn and warmup re-send");
                return;
            }

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

            Debug.LogInformation(this, "VolumeUp: ir command '{0}'", IrStandardCommands.VolumeUp);
            SendIrCommand(IrStandardCommands.VolumeUp);
        }

        public void VolumeDown(bool pressRelease)
        {
            if (pressRelease) return;

            Debug.LogInformation(this, "VolumeDown: ir command '{0}'", IrStandardCommands.VolumeDown);
            SendIrCommand(IrStandardCommands.VolumeDown);
        }

        public void MuteToggle()
        {
            Debug.LogInformation(this, "MuteToggle: ir command '{0}'", IrStandardCommands.MuteToggle);
            SendIrCommand(IrStandardCommands.MuteToggle);
        }

        #endregion


        #region IDPad Members

        public void Up(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.DpadUp);
        }

        public void Down(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.DpadDown);
        }

        public void Left(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.DpadLeft);
        }

        public void Right(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.DpadRight);
        }

        public void Select(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.DpadSelect);
        }

        public void Menu(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.Menu);
        }

        /// <summary>
        /// Shared with IChannel.Exit - a single method satisfies both interfaces.
        /// </summary>
        public void Exit(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.Exit);
        }

        #endregion


        #region INumericKeypad Members

        public void Digit0(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.KP0);
        }

        public void Digit1(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.KP1);
        }

        public void Digit2(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.KP2);
        }

        public void Digit3(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.KP3);
        }

        public void Digit4(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.KP4);
        }

        public void Digit5(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.KP5);
        }

        public void Digit6(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.KP6);
        }

        public void Digit7(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.KP7);
        }

        public void Digit8(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.KP8);
        }

        public void Digit9(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.KP9);
        }

        // No LG remote-key equivalent for the keypad's accessory buttons (e.g. set-top-box
        // Dash/Enter) - hidden on the front end via HasKeypadAccessoryButtonN = false.
        public bool HasKeypadAccessoryButton1 => false;
        public string KeypadAccessoryButton1Label => string.Empty;
        public void KeypadAccessoryButton1(bool pressRelease) { }

        public bool HasKeypadAccessoryButton2 => false;
        public string KeypadAccessoryButton2Label => string.Empty;
        public void KeypadAccessoryButton2(bool pressRelease) { }

        #endregion


        #region ITransport Members

        public void Play(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.Play);
        }

        public void Pause(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.Pause);
        }

        public void Rewind(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.Rewind);
        }

        public void FFwd(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.FastForward);
        }

        // No corresponding IR command exists in IrStandardCommands for these on a typical LG
        // TV remote - no-ops, matching AppleTV's precedent for unmapped ITransport members.
        public void ChapMinus(bool pressRelease) { }
        public void ChapPlus(bool pressRelease) { }
        public void Stop(bool pressRelease) { }
        public void Record(bool pressRelease) { }

        #endregion


        #region IChannel Members

        public void ChannelUp(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.ChannelUp);
        }

        public void ChannelDown(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.ChannelDown);
        }

        public void LastChannel(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.Last);
        }

        public void Guide(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.Guide);
        }

        // No dedicated "info" IR command exists in IrStandardCommands today - no-op.
        public void Info(bool pressRelease) { }

        // IChannel.Exit is satisfied by the IDPad.Exit implementation above.

        #endregion


        #region IColor Members

        public void Red(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.FuncRed);
        }

        public void Green(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.FuncGreen);
        }

        public void Yellow(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.FuncYellow);
        }

        public void Blue(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.FuncBlue);
        }

        #endregion


        #region Remote buttons with no matching core interface yet

        // Home, Back, Enter, page up/down and sleep have no dedicated core DeviceTypeInterfaces
        // member (IDPad only has Menu/Exit). Exposed as plain methods - same precedent as
        // InputNetflix/InputPrimeVideo above - callable via devjson today; wiring these into the
        // React app's UI needs either a core interface addition (out of scope this pass - same
        // "touches shared core" category flagged for the Apple TV / IrDisplayBase questions) or a
        // room-plugin-specific messenger action.

        public void Home(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.Home);
        }

        public void Back(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.Back);
        }

        public void Enter(bool pressRelease)
        {
            if (pressRelease) return;
            SendIrCommand(IrStandardCommands.Enter);
        }

        #endregion
    }
}
