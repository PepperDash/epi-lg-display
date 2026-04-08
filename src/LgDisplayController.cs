using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Epi.Display.Lg;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Core.Queues;
using PepperDash.Essentials.Devices.Displays;
using TwoWayDisplayBase = PepperDash.Essentials.Devices.Common.Displays.TwoWayDisplayBase;


namespace PepperDash.Essentials.Plugins.Lg.Display
{
    public class LgDisplayController : TwoWayDisplayBase, IBasicVolumeWithFeedback, ICommunicationMonitor,
        IInputHdmi1, IInputHdmi2, IInputHdmi3, IInputHdmi4, IInputDisplayPort1, IBridgeAdvanced, IHasInputs<string>, IBasicVideoMuteWithFeedback, IWarmingCooling
    {
        GenericQueue ReceiveQueue;
        public const int InputPowerOn = 101;
        public const int InputPowerOff = 102;
        public static List<string> InputKeys = new List<string>();
        public List<BoolFeedback> InputFeedback;
        public IntFeedback InputNumberFeedback;
        private RoutingInputPort _currentInputPort;
        private List<bool> _inputFeedback;
        private int _inputNumber;
        private bool _isCoolingDown;
        private bool _isMuted;
        private bool _isSerialComm;
        private bool _isWarmingUp;
        private bool _inputSwitchPending;
        private string _lastCommandPrefix;
        private int _lastVolumeSent;
        private bool _powerIsOn;
        private bool _videoIsMuted;
        private ActionIncrementer _volumeIncrementer;
        private bool _volumeIsRamping;
        private ushort _volumeLevelForSig;
        private readonly bool _smallDisplay;
        private readonly bool _overrideWol;
        //private GenericUdpServer _woLServer;
        private readonly LgDisplayPropertiesConfig _config;


        public LgDisplayController(string key, string name, LgDisplayPropertiesConfig config, IBasicCommunication comms)
            : base(key, name)
        {
            Communication = comms;

            ReceiveQueue = new GenericQueue(key + "-queue");

            _config = config;
            var props = config;
            if (props == null)
            {
                Debug.LogError(this, "Display configuration must be included");
                return;
            }
            _smallDisplay = props.SmallDisplay;
            Id = !string.IsNullOrEmpty(props.Id) ? props.Id : "01";
            _upperLimit = props.volumeUpperLimit;
            _lowerLimit = props.volumeLowerLimit;
            _overrideWol = props.OverrideWol;
            _pollIntervalMs = props.pollIntervalMs > 1999 ? props.pollIntervalMs : 10000;
            _coolingTimeMs = props.coolingTimeMs > 0 ? props.coolingTimeMs : 10000;
            _warmingTimeMs = props.warmingTimeMs > 0 ? props.warmingTimeMs : 8000;
            //UdpSocketKey = props.udpSocketKey;

            InputNumberFeedback = new IntFeedback(() =>
            {
                return InputNumber;
            });

            Init();
        }

        public IBasicCommunication Communication { get; private set; }
        public CommunicationGather PortGather { get; private set; }

        public string Id { get; private set; }

        public bool PowerIsOn
        {
            get { return _powerIsOn; }
            set
            {
                if (_powerIsOn == value)
                {
                    return;
                }

                _powerIsOn = value;
                PowerIsOnFeedback.FireUpdate();
            }
        }

        public bool IsWarmingUp
        {
            get { return _isWarmingUp; }
            set
            {
                if (_isWarmingUp == value) return;

                _isWarmingUp = value;

                if (_isWarmingUp)
                {
                    WarmupTimer = new CTimer(o =>
                    {
                        IsWarmingUp = false;

                        if (!_inputSwitchPending)
                        {
                            InputGet();
                        }
                    }, WarmupTime);
                }
                else if (WarmupTimer != null)
                {
                    WarmupTimer.Stop();
                    WarmupTimer.Dispose();
                    WarmupTimer = null;
                }

                IsWarmingUpFeedback.FireUpdate();
            }
        }

        public bool IsCoolingDown
        {
            get { return _isCoolingDown; }
            set
            {
                if (_isCoolingDown == value) return;

                _isCoolingDown = value;

                if (_isCoolingDown)
                {
                    CooldownTimer = new CTimer(o => { IsCoolingDown = false; }, CooldownTime);
                }
                else if (CooldownTimer != null)
                {
                    CooldownTimer.Stop();
                    CooldownTimer.Dispose();
                    CooldownTimer = null;
                }

                IsCoolingDownFeedback.FireUpdate();
            }
        }

        public bool IsMuted
        {
            get { return _isMuted; }
            set
            {
                _isMuted = value;
                MuteFeedback.FireUpdate();
            }
        }
        public bool VideoIsMuted
        {
            get { return _videoIsMuted; }
            set
            {
                _videoIsMuted = value;
                VideoMuteIsOn.FireUpdate();
            }
        }

        private readonly int _lowerLimit;
        private readonly int _upperLimit;
        private readonly uint _coolingTimeMs;
        private readonly uint _warmingTimeMs;
        private readonly long _pollIntervalMs;

        public int InputNumber
        {
            get { return _inputNumber; }
            private set
            {
                if (_inputNumber == value) return;

                _inputNumber = value;
                InputNumberFeedback.FireUpdate();
                UpdateBooleanFeedback(value);
            }
        }

        private bool ScaleVolume { get; set; }

        protected override Func<bool> PowerIsOnFeedbackFunc
        {
            get { return () => PowerIsOn; }
        }

        protected override Func<bool> IsCoolingDownFeedbackFunc
        {
            get { return () => IsCoolingDown; }
        }

        protected override Func<bool> IsWarmingUpFeedbackFunc
        {
            get { return () => IsWarmingUp; }
        }

        protected override Func<string> CurrentInputFeedbackFunc
        {
            get { return () => _currentInputPort != null ? _currentInputPort.Key : string.Empty; }
        }

        #region IBasicVolumeWithFeedback Members

        /// <summary>
        /// Volume Level Feedback Property
        /// </summary>
        public IntFeedback VolumeLevelFeedback { get; private set; }

        /// <summary>
        /// Volume Mute Feedback Property
        /// </summary>
        public BoolFeedback MuteFeedback { get; private set; }

        /// <summary>
        /// Scales the level to the range of the display and sends the command
        /// Set: "kf [SetID] [Range 0x00 - 0x64]"
        /// </summary>
        /// <param name="level"></param>
        public void SetVolume(ushort level)
        {
            int scaled;
            _lastVolumeSent = level;
            if (!ScaleVolume)
            {
                scaled = (int)NumericalHelpers.Scale(level, 0, 65535, 0, 100);
            }
            else
            {
                scaled = (int)NumericalHelpers.Scale(level, 0, 65535, _lowerLimit, _upperLimit);
            }

            SendData(string.Format("kf {0} {1}", Id, scaled));
        }

        /// <summary>
        /// Set Mute On
        /// </summary>
        public void MuteOn()
        {
            SendData(string.Format("ke {0} {1}", Id, _smallDisplay ? "0" : "00"));
        }

        /// <summary>
        /// Set Mute Off
        /// </summary>
        public void MuteOff()
        {
            SendData(string.Format("ke {0} {1}", Id, _smallDisplay ? "1" : "01"));
        }

        /// <summary>
        /// Toggle Current Mute State
        /// </summary>
        public void MuteToggle()
        {
            if (IsMuted)
            {
                MuteOff();
            }
            else
            {
                MuteOn();
            }
        }

        /// <summary>
        /// Decrement Volume on Press
        /// </summary>
        /// <param name="pressRelease"></param>
        public void VolumeDown(bool pressRelease)
        {
            if (pressRelease)
            {
                _volumeIncrementer.StartDown();
                _volumeIsRamping = true;
            }
            else
            {
                _volumeIsRamping = false;
                _volumeIncrementer.Stop();
            }
        }

        /// <summary>
        /// Increment Volume on press
        /// </summary>
        /// <param name="pressRelease"></param>
        public void VolumeUp(bool pressRelease)
        {
            if (pressRelease)
            {
                _volumeIncrementer.StartUp();
                _volumeIsRamping = true;
            }
            else
            {
                _volumeIsRamping = false;
                _volumeIncrementer.Stop();
            }
        }

        #endregion

        #region IBasicVideoMuteWithFeedback Members

        public BoolFeedback VideoMuteIsOn { get; private set; }

        /// <summary>
        /// Set Video Mute On
        /// </summary>
        public void VideoMuteOn()
        {
            SendData(string.Format("kd {0} {1}", Id, _smallDisplay ? "1" : "01"));
        }

        /// <summary>
        /// Set Video Mute Off
        /// </summary>
        public void VideoMuteOff()
        {
            SendData(string.Format("kd {0} {1}", Id, _smallDisplay ? "0" : "00"));
        }

        /// <summary>
        /// Toggle Current Video Mute State
        /// </summary>
        public void VideoMuteToggle()
        {
            if (VideoIsMuted)
            {
                VideoMuteOff();
            }
            else
            {
                VideoMuteOn();
            }
        }

        public void VideoMuteGet()
        {
            SendData(string.Format("kd {0} FF", Id));
        }

        #endregion

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

            //var twoWayDisplay = this as TwoWayDisplayBase;
            //trilist.SetBool(joinMap.IsTwoWayDisplay.JoinNumber, twoWayDisplay != null);

            if (CommunicationMonitor != null)
            {
                CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            }

            // power off
            trilist.SetSigTrueAction(joinMap.PowerOff.JoinNumber, PowerOff);
            PowerIsOnFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.PowerOff.JoinNumber]);

            // power on 
            trilist.SetSigTrueAction(joinMap.PowerOn.JoinNumber, PowerOn);
            PowerIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PowerOn.JoinNumber]);

            IsCoolingDownFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsCoolingDown.JoinNumber]);
            IsWarmingUpFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsWarmingUp.JoinNumber]);

            // input (digital select, digital feedback, names)
            for (var i = 0; i < InputPorts.Count; i++)
            {
                var inputIndex = i;
                var input = InputPorts.ElementAt(inputIndex);

                if (input == null) continue;

                trilist.SetSigTrueAction((ushort)(joinMap.InputSelectOffset.JoinNumber + inputIndex), () =>
                {
                    SetInput = inputIndex + 1;
                });

                var inputName = input.Key;
                if (Inputs?.Items != null && input.FeedbackMatchObject is string fbMatch && Inputs.Items.TryGetValue(fbMatch, out var selectableItem))
                {
                    inputName = selectableItem.Name;
                }
                trilist.StringInput[(ushort)(joinMap.InputNamesOffset.JoinNumber + inputIndex)].StringValue = inputName ?? string.Empty;

                if (InputFeedback != null && inputIndex < InputFeedback.Count)
                {
                    InputFeedback[inputIndex].LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + (uint)inputIndex]);
                }
            }

            // input (analog select)
            trilist.SetUShortSigAction(joinMap.InputSelect.JoinNumber, analogValue =>
            {
                SetInput = analogValue;
            });

            // input (analog feedback)
            if (InputNumberFeedback != null)
                InputNumberFeedback.LinkInputSig(trilist.UShortInput[joinMap.InputSelect.JoinNumber]);

            if (CurrentInputFeedback != null)
                CurrentInputFeedback.OutputChange += (sender, args) => Debug.LogDebug(this, "CurrentInputFeedback: {0}", args.StringValue);

            // bridge online change
            trilist.OnlineStatusChange += (sender, args) =>
            {
                if (!args.DeviceOnLine) return;

                // device name
                trilist.SetString(joinMap.Name.JoinNumber, Name);

                PowerIsOnFeedback.FireUpdate();

                if (CurrentInputFeedback != null)
                    CurrentInputFeedback.FireUpdate();

                if (InputNumberFeedback != null)
                    InputNumberFeedback.FireUpdate();

                for (var i = 0; i < InputPorts.Count; i++)
                {
                    var inputIndex = i;
                    if (InputFeedback != null)
                        InputFeedback[inputIndex].FireUpdate();
                }
            };
        }

        #endregion

        #region ICommunicationMonitor Members

        public StatusMonitorBase CommunicationMonitor { get; private set; }

        #endregion

        public ISelectableItems<string> Inputs { get; private set; }

        private void Init()
        {
            WarmupTime = _warmingTimeMs > 0 ? _warmingTimeMs : 10000;
            CooldownTime = _coolingTimeMs > 0 ? _coolingTimeMs : 8000;

            _inputFeedback = new List<bool>();
            InputFeedback = new List<BoolFeedback>();

            if (_upperLimit != _lowerLimit && _upperLimit > _lowerLimit)
            {
                ScaleVolume = true;
            }

            PortGather = new CommunicationGather(Communication, "x");
            PortGather.LineReceived += PortGather_LineReceived;

            var socket = Communication as ISocketStatus;
            if (socket != null)
            {
                //This Instance Uses IP Control
                Debug.LogVerbose(this, "The LG Display Plugin does NOT support IP Control currently");
            }
            else
            {
                // This instance uses RS-232 Control
                _isSerialComm = true;
            }

            var pollInterval = _pollIntervalMs > 0 ? _pollIntervalMs : 10000;
            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, pollInterval, 180000, 300000,
                StatusGet);
            CommunicationMonitor.StatusChange += CommunicationMonitor_StatusChange;

            DeviceManager.AddDevice(CommunicationMonitor);

            if (!ScaleVolume)
            {
                _volumeIncrementer = new ActionIncrementer(655, 0, 65535, 800, 80,
                    v => SetVolume((ushort)v),
                    () => _lastVolumeSent);
            }
            else
            {
                var scaleUpper = NumericalHelpers.Scale(_upperLimit, 0, 100, 0, 65535);
                var scaleLower = NumericalHelpers.Scale(_lowerLimit, 0, 100, 0, 65535);

                _volumeIncrementer = new ActionIncrementer(655, (int)scaleLower, (int)scaleUpper, 800, 80,
                    v => SetVolume((ushort)v),
                    () => _lastVolumeSent);
            }

            MuteFeedback = new BoolFeedback(() => IsMuted);
            VolumeLevelFeedback = new IntFeedback(() => _volumeLevelForSig);
            VideoMuteIsOn = new BoolFeedback(() => VideoIsMuted);

            AddRoutingInputPort(
                new RoutingInputPort(RoutingPortNames.HdmiIn1, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.Hdmi, new Action(InputHdmi1), this), "90");
            AddRoutingInputPort(
                new RoutingInputPort(RoutingPortNames.HdmiIn2, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.Hdmi, new Action(InputHdmi2), this), "91");
            AddRoutingInputPort(
                new RoutingInputPort(RoutingPortNames.HdmiIn3, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.Hdmi, new Action(InputHdmi3), this), "92");
            AddRoutingInputPort(
                new RoutingInputPort(RoutingPortNames.HdmiIn4, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.Hdmi, new Action(InputHdmi4), this), "93");
            AddRoutingInputPort(
                new RoutingInputPort(RoutingPortNames.DisplayPortIn, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.DisplayPort, new Action(InputDisplayPort1), this), "c0");

            _inputFeedback = new List<bool>(new bool[InputPorts.Count + 1]);

            // Initialize InputFeedback with a BoolFeedback for each input
            InputFeedback = new List<BoolFeedback>();
            for (int i = 0; i < InputPorts.Count; i++)
            {
                int index = i + 1; // 1-based index to match InputNumber logic
                InputFeedback.Add(new BoolFeedback(() => _inputFeedback[index]));
            }

            SetupInputs();
        }

        public override bool CustomActivate()
        {
            Communication.Connect();

            if (_isSerialComm || _overrideWol)
            {
                CommunicationMonitor.Start();
            }

            return base.CustomActivate();
        }

        private void SetupInputs()
        {
            Inputs = new LgInputs
            {
                Items = new Dictionary<string, ISelectableItem>
                {
                    { "90", new LgInput("90", "HDMI 1", this) },
                    { "91", new LgInput("91", "HDMI 2", this) },
                    { "92", new LgInput("92", "HDMI 3", this) },
                    { "93", new LgInput("93", "HDMI 4", this) },
                    { "c0", new LgInput("c0", "DisplayPort", this) },
                }
            };
            ApplyFriendlyNames(_config);
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
                        var updatedInput = new LgInput(input.Key, friendly.Name, this);
                        Inputs.Items[friendly.InputKey] = updatedInput;
                    }
                }
            }
        }

        private void CommunicationMonitor_StatusChange(object sender, MonitorStatusChangeEventArgs e)
        {
            CommunicationMonitor.IsOnlineFeedback.FireUpdate();
        }

        private void PortGather_LineReceived(object sender, GenericCommMethodReceiveTextArgs args)
        {
            ReceiveQueue.Enqueue(new ProcessStringMessage(args.Text, ProcessResponse));
        }

        private void PollAfterNgResponse(string ngCommand)
        {
            switch (ngCommand)
            {
                case "a":
                    {
                        PowerGet();
                        break;
                    }
                case "b":
                    {
                        if (!PowerIsOn) return;

                        InputGet();
                        break;
                    }
                case "f":
                    {
                        if (!PowerIsOn) return;

                        VolumeGet();
                        break;
                    }
                case "e":
                    {
                        if (!PowerIsOn) return;

                        MuteGet();
                        break;
                    }
                case "d":
                    {
                        if (!PowerIsOn) return;

                        VideoMuteGet();
                        break;
                    }
            }
        }

        private void ProcessResponse(string s)
        {
            // Expected format: "{command} {id} {OK|NG}{value}x"
            // Example OK: "a 1 OK01x"  Example NG: "a 1 NG01x"
            var data = s.Trim().Split(' ');

            if (data.Length < 3)
            {
                this.LogVerbose("Unable to parse response, not enough data: {0}", s);
                return;
            }

            var command = data[0];
            var id = data[1];
            var statusAndValue = data[2];

            // Check for NG response
            if (statusAndValue.IndexOf("NG", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                this.LogVerbose("NG response received for command '{0}': {1}", command, s);
                PollAfterNgResponse(command);
                return;
            }

            // Strip OK prefix and trailing x delimiter
            var responseValue = statusAndValue
                .Replace("OK", "")
                .TrimEnd('x');

            // Normalize both IDs to integers for comparison (handles "1" vs "01")
            var normalizedReceived = NormalizeId(id);
            var normalizedExpected = NormalizeId(Id);

            if (!normalizedReceived.Equals(normalizedExpected))
            {
                this.LogVerbose("Device ID Mismatch - Expected: {0} ({1}), Received: {2} ({3}) - Discarding",
                    Id, normalizedExpected, id, normalizedReceived);
                return;
            }

            switch (command)
            {
                case "a":
                    UpdatePowerFb(responseValue);
                    break;
                case "b":
                    UpdateInputFb(responseValue);
                    break;
                case "f":
                    UpdateVolumeFb(responseValue);
                    break;
                case "e":
                    UpdateMuteFb(responseValue);
                    break;
                case "d":
                    UpdateVideoMuteFb(responseValue);
                    break;
            }
        }

        /// <summary>
        /// Normalizes device ID for comparison (converts to integer string to handle "01" vs "1")
        /// </summary>
        /// <param name="deviceId">Device ID to normalize</param>
        /// <returns>Normalized device ID as integer string</returns>
        private string NormalizeId(string deviceId)
        {
            try
            {
                // Convert to int and back to string to remove leading zeros
                return int.Parse(deviceId).ToString();
            }
            catch (Exception e)
            {
                Debug.LogError(this, "Failed to normalize device ID '{0}': {1}", deviceId, e.Message);
                return deviceId; // Return original if parsing fails
            }
        }

        private void AddRoutingInputPort(RoutingInputPort port, string fbMatch)
        {
            port.FeedbackMatchObject = fbMatch;
            InputPorts.Add(port);
        }

        /// <summary>
        /// Formats an outgoing message
        /// 
        /// </summary>
        /// <param name="s"></param>
        public void SendData(string s)
        {
            var commandPrefix = s.Length >= 2 ? s.Substring(0, 2) : string.Empty;

            if (_lastCommandPrefix == "kf" && commandPrefix != "kf")
            {
                CrestronEnvironment.Sleep(100);
            }

            _lastCommandPrefix = commandPrefix;

            Communication.SendText(s + "\x0D");
        }

        /// <summary>
        /// Sets the requested input
        /// </summary>
        private int SetInput
        {
            set
            {
                if (value <= 0 || value > InputPorts.Count)
                {
                    this.LogError("SetInput: Value {0} is out of range (1-{1})", value, InputPorts.Count);
                    return;
                }

                var portIndex = value - 1;

                var port = GetInputPort(portIndex);
                if (port == null)
                {
                    this.LogError("SetInput: Port at index {0} is null", portIndex);
                    return;
                }

                if (port.Selector is Action action)
                {
                    this.LogVerbose("SetInput: {0}", action?.Method.DeclaringType?.Name + "." + action?.Method.Name ?? "NULL");
                    ExecuteSwitch(action);
                }
                else
                {
                    this.LogError("SetInput: Port selector is not an Action! Type: {0}",
                        port.Selector?.GetType().Name ?? "NULL");
                }
            }
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
                this.LogVerbose("ListRoutingInputPorts: key-'{0}', connectionType-'{1}', feedbackMatchObject-'{2}'",
                    inputPort.Key, inputPort.ConnectionType, inputPort.FeedbackMatchObject);
            }
        }

        /// <summary>
        /// Poll Mute State
        /// </summary>
        public void MuteGet()
        {
            SendData(string.Format("ke {0} FF", Id));
        }

        /// <summary>
        /// Poll Volume
        /// </summary>
        public void VolumeGet()
        {
            SendData(string.Format("kf {0} FF", Id));
        }

        /// <summary>
        /// Select Hdmi 1 Input
        /// </summary>
        public void InputHdmi1()
        {
            SendData(string.Format("xb {0} 90", Id));
        }

        /// <summary>
        /// Select Hdmi 2 Input
        /// </summary>
        public void InputHdmi2()
        {
            SendData(string.Format("xb {0} 91", Id));
        }

        /// <summary>
        /// Select Hdmi 3 Input
        /// </summary>
        public void InputHdmi3()
        {
            SendData(string.Format("xb {0} 92", Id));
        }

        /// <summary>
        /// Select Hdmi 4 Input
        /// </summary>
        public void InputHdmi4()
        {
            SendData(string.Format("xb {0} 93", Id));
        }

        /// <summary>
        /// Select DisplayPort Input
        /// </summary>
        public void InputDisplayPort1()
        {
            SendData(string.Format("xb {0} C0", Id));
        }

        /// <summary>
        /// Poll input
        /// </summary>
        public void InputGet()
        {
            SendData(string.Format("xb {0} FF", Id));
        }

        /// <summary>
        /// Executes a switch, turning on display if necessary.
        /// </summary>
        /// <param name="selector"></param>
        public override void ExecuteSwitch(object selector)
        {
            if (!(selector is Action action))
            {
                this.LogVerbose("ExecuteSwitch: selector is not an Action. Type: {0}", selector?.GetType().Name ?? "NULL");
                return;
            }

            this.LogVerbose("ExecuteSwitch: preparing to execute {0}", action?.Method.DeclaringType?.Name + "." + action?.Method.Name ?? "NULL");

            _inputSwitchPending = true;

            if (PowerIsOn)
            {
                action();
                _inputSwitchPending = false;
            }
            else if (IsCoolingDown)
            {
                this.LogVerbose("ExecuteSwitch: Device is cooling down. Powering on and executing {0} after cooldown.", action?.Method.DeclaringType?.Name + "." + action?.Method.Name ?? "NULL");
                CrestronInvoke.BeginInvoke((o) =>
                {
                    CrestronEnvironment.Sleep((int)CooldownTime);

                    this.LogVerbose("ExecuteSwitch: Cooldown complete. Powering on.");
                    PowerOn();

                    CrestronEnvironment.Sleep((int)WarmupTime + 1000); // warmup time + 1000 for input switching delay                    

                    this.LogVerbose("ExecuteSwitch: Warmup complete. Executing {0}.", action?.Method.DeclaringType?.Name + "." + action?.Method.Name ?? "NULL");
                    action();
                    _inputSwitchPending = false;
                });
            }
            else
            {
                this.LogVerbose("ExecuteSwitch: Power is off. Powering on and executing {0} after warmup.", action?.Method.DeclaringType?.Name + "." + action?.Method.Name ?? "NULL");
                PowerOn();
                CrestronInvoke.BeginInvoke((o) =>
                {
                    CrestronEnvironment.Sleep((int)WarmupTime + 1000); // warmup time + 1000 for input switching delay                    

                    this.LogVerbose("ExecuteSwitch: Warmup complete. Executing {0}.", action?.Method.DeclaringType?.Name + "." + action?.Method.Name ?? "NULL");
                    action();
                    _inputSwitchPending = false;
                });
            }
        }


        /// <summary>
        /// Set Power On For Device
        /// </summary>
        public override void PowerOn()
        {
            if (IsCoolingDown)
            {
                CrestronInvoke.BeginInvoke((o) =>
                {
                    this.LogVerbose("PowerOn: Device is cooling down. Powering on after cooldown completes.");

                    CrestronEnvironment.Sleep((int)CooldownTime);

                    this.LogVerbose("PowerOn: Cooldown complete. Powering on.");
                    SendPowerOn();
                });
                return;
            }

            SendPowerOn();
        }

        /// <summary>
        /// Set Power Off for Device
        /// </summary>
        public override void PowerOff()
        {
            if (IsWarmingUp)
            {
                CrestronInvoke.BeginInvoke((o) =>
                {
                    this.LogVerbose("PowerOff: Device is warming up. Powering off after warmup completes.");

                    CrestronEnvironment.Sleep((int)WarmupTime);

                    this.LogVerbose("PowerOff: Warmup complete. Powering off.");
                    SendPowerOff();
                });
                return;
            }

            SendPowerOff();
        }

        private void SendPowerOn()
        {
            if (_isSerialComm || _overrideWol)
            {
                SendData(string.Format("ka {0} {1}", Id, _smallDisplay ? "1" : "01"));
            }

            IsCoolingDown = false;
            IsWarmingUp = true;
        }

        private void SendPowerOff()
        {
            SendData(string.Format("ka {0} {1}", Id, _smallDisplay ? "0" : "00"));

            IsWarmingUp = false;
            IsCoolingDown = true;
        }

        /// <summary>
        /// Poll Power
        /// </summary>
        public void PowerGet()
        {
            SendData(string.Format("ka {0} FF", Id));
        }


        /// <summary>
        /// Toggle current power state for device
        /// </summary>
        public override void PowerToggle()
        {
            if (PowerIsOn)
            {
                PowerOff();
            }
            else
            {
                PowerOn();
            }
        }

        /// <summary>
        /// Process Input Feedback from Response
        /// </summary>
        /// <param name="s">response from device</param>
        public void UpdateInputFb(string s)
        {
            var normalizedInput = s.ToLower();

            var newInput = InputPorts.FirstOrDefault(i => i.FeedbackMatchObject.Equals(normalizedInput));
            if (newInput != null && newInput != _currentInputPort)
            {
                _currentInputPort = newInput;
                CurrentInputFeedback.FireUpdate();
                InputNumber = InputPorts.ToList().IndexOf(newInput) + 1;
            }

            if (Inputs.Items.ContainsKey(normalizedInput))
            {
                foreach (var item in Inputs.Items)
                {
                    item.Value.IsSelected = item.Key.Equals(normalizedInput);
                }

                Inputs.CurrentItem = normalizedInput;
            }
        }

        /// <summary>
        /// Process Power Feedback from Response
        /// </summary>
        /// <param name="s">response from device</param>
        public void UpdatePowerFb(string s)
        {
            var wasOn = PowerIsOn;
            PowerIsOn = s.Contains("1");
        }

        /// <summary>
        /// Process Video Mute Feedback from Response
        /// </summary>
        /// <param name="s">response from device</param>
        public void UpdateVideoMuteFb(string s)
        {
            try
            {
                var state = Convert.ToInt32(s);

                if (state == 0)
                {
                    VideoIsMuted = false;
                }
                else if (state == 1)
                {
                    VideoIsMuted = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogVerbose(this, "Unable to parse {0} to Int32 {1}", s, e);
            }
        }

        /// <summary>
        /// Process Volume Feedback from Response
        /// </summary>
        /// <param name="s">response from device</param>
        public void UpdateVolumeFb(string s)
        {
            try
            {
                var vol = int.Parse(s, NumberStyles.HexNumber);
                ushort newVol;
                if (!ScaleVolume)
                {
                    newVol = (ushort)NumericalHelpers.Scale(Convert.ToDouble(vol), 0, 100, 0, 65535);
                }
                else
                {
                    newVol = (ushort)NumericalHelpers.Scale(Convert.ToDouble(vol), _lowerLimit, _upperLimit, 0, 65535);
                }
                if (!_volumeIsRamping)
                {
                    _lastVolumeSent = newVol;
                }

                if (newVol == _volumeLevelForSig)
                {
                    return;
                }
                _volumeLevelForSig = newVol;
                VolumeLevelFeedback.FireUpdate();
            }
            catch (Exception e)
            {
                Debug.LogVerbose(this, "Error updating volumefb for value: {1}: {0}", e, s);
            }
        }

        /// <summary>
        /// Process Mute Feedback from Response
        /// </summary>
        /// <param name="s">response from device</param>
        public void UpdateMuteFb(string s)
        {
            try
            {
                var state = Convert.ToInt32(s);

                if (state == 0)
                {
                    IsMuted = true;
                }
                else if (state == 1)
                {
                    IsMuted = false;
                }
            }
            catch (Exception e)
            {
                Debug.LogVerbose(this, "Unable to parse {0} to Int32 {1}", s, e);
            }
        }

        /// <summary>
        /// Updates Digital Route Feedback for Simpl EISC
        /// </summary>
        /// <param name="data">currently routed source</param>
        private void UpdateBooleanFeedback(int data)
        {
            try
            {
                if (data < 0 || data >= _inputFeedback.Count)
                {
                    Debug.LogVerbose(this, "Input index {0} out of range for _inputFeedback (size {1})", data, _inputFeedback.Count);
                    return;
                }

                if (_inputFeedback[data])
                {
                    return;
                }

                for (var i = 1; i < InputPorts.Count + 1; i++)
                {
                    _inputFeedback[i] = false;
                }

                _inputFeedback[data] = true;
                foreach (var item in InputFeedback)
                {
                    var update = item;
                    update.FireUpdate();
                }
            }
            catch (Exception e)
            {
                Debug.LogError(this, "{0}", e.Message);
            }
        }

        /// <summary>
        /// Starts the Poll Ring
        /// </summary>
        public void StatusGet()
        {
            //SendBytes(new byte[] { Header, StatusControlCmd, 0x00, 0x00, StatusControlGet, 0x00 });
            CrestronInvoke.BeginInvoke((o) =>
                {
                    PowerGet();

                    if (IsWarmingUp || IsCoolingDown)
                        return;

                    CrestronEnvironment.Sleep(1500);
                    InputGet();
                    CrestronEnvironment.Sleep(1500);
                    VolumeGet();
                    CrestronEnvironment.Sleep(1500);
                    MuteGet();
                });
        }


        private void WolFunction(string macAddress)
        {
            if (Regex.IsMatch(macAddress, @"^([0-9A-Fa-f]{2}[\.:-]){5}([0-9A-Fa-f]{2})$") ||
                Regex.IsMatch(macAddress, @"^([0-9A-Fa-f]{12})"))
            {
                var address = Regex.Replace(macAddress, @"(-|:|\.)", "").ToLower();

                var counter = 0;

                var bytes = new byte[1024];

                //Packet starts with 6 iterations of 0xFF
                for (var i = 0; i < 6; i++)
                {
                    bytes[counter++] = 0xFF;
                }

                //Packet has 16 iterations of the mac address
                for (var y = 0; y < 16; y++)
                {
                    var i = 0;
                    for (var z = 0; z < 6; z++)
                    {
                        bytes[counter++] =
                            byte.Parse(address.Substring(i, 2),
                                NumberStyles.HexNumber);
                        i += 2;
                    }
                }
                return;
            }

            Debug.LogVerbose(this, "Invalid MAC Address sent to WolFunction - {0}", macAddress);
            throw new ArgumentException("Invalid MAC Address");
        }
    }
}
