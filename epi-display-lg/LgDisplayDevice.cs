using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Routing;

using PepperDash.Essentials.Core.Queues;

namespace Epi.Display.Lg
{
    public class LgDisplayController : TwoWayDisplayBase, IBasicVolumeWithFeedback, ICommunicationMonitor,
        IBridgeAdvanced

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
        private bool _lastCommandSentWasVolume;
        private int _lastVolumeSent;
        private CTimer _pollRing;
        private bool _powerIsOn;
        private ActionIncrementer _volumeIncrementer;
        private bool _volumeIsRamping;
        private ushort _volumeLevelForSig;
        private bool _smallDisplay;
        //private GenericUdpServer _woLServer;

        public LgDisplayController(string key, string name, LgDisplayPropertiesConfig config, IBasicCommunication comms)
            : base(key, name)
        {
            Communication = comms;

            ReceiveQueue = new GenericQueue(key + "-queue");

            var props = config;
            if (props == null)
            {
                Debug.Console(0, this, Debug.ErrorLogLevel.Error, "Display configuration must be included");
                return;
            }
            _smallDisplay = props.SmallDisplay;
            Id = string.IsNullOrEmpty(props.Id) ? props.Id : "01";
            _upperLimit = props.volumeUpperLimit;
            _lowerLimit = props.volumeLowerLimit;
            _pollIntervalMs = props.pollIntervalMs > 1999 ? props.pollIntervalMs : 10000;
            _coolingTimeMs = props.coolingTimeMs > 0 ? props.coolingTimeMs : 10000;
            _warmingTimeMs = props.warmingTimeMs > 0 ? props.warmingTimeMs : 8000;
            //UdpSocketKey = props.udpSocketKey;

            InputNumberFeedback = new IntFeedback(() => _inputNumber);

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

                if (_powerIsOn)
                {
                    IsWarmingUp = true;

                    WarmupTimer = new CTimer(o =>
                    {
                        IsWarmingUp = false;
                    }, WarmupTime);
                }
                else
                {
                    IsCoolingDown = true;

                    CooldownTimer = new CTimer(o =>
                    {
                        IsCoolingDown = false;
                    }, CooldownTime);
                }
                
                PowerIsOnFeedback.FireUpdate();               
            }
        }

        public bool IsWarmingUp
        {
            get { return _isWarmingUp; }
            set
            {
                _isWarmingUp = value;
                IsWarmingUpFeedback.FireUpdate();
            }
        }

        public bool IsCoolingDown
        {
            get { return _isCoolingDown; }
            set
            {
                _isCoolingDown = value;
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
            get { return () => _currentInputPort.Key; }
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
                scaled = (int) NumericalHelpers.Scale(level, 0, 65535, 0, 100);
            }
            else
            {
                scaled = (int) NumericalHelpers.Scale(level, 0, 65535, _lowerLimit, _upperLimit);
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

        #region IBridgeAdvanced Members

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            LinkDisplayToApi(this, trilist, joinStart, joinMapKey, bridge);
        }

        #endregion

        #region ICommunicationMonitor Members

        public StatusMonitorBase CommunicationMonitor { get; private set; }

        #endregion

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
                Debug.Console(2, this, "The LG Display Plugin does NOT support IP Control currently");
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
                    v => SetVolume((ushort) v),
                    () => _lastVolumeSent);
            }
            else
            {
                var scaleUpper = NumericalHelpers.Scale(_upperLimit, 0, 100, 0, 65535);
                var scaleLower = NumericalHelpers.Scale(_lowerLimit, 0, 100, 0, 65535);

                _volumeIncrementer = new ActionIncrementer(655, (int) scaleLower, (int) scaleUpper, 800, 80,
                    v => SetVolume((ushort) v),
                    () => _lastVolumeSent);
            }

            MuteFeedback = new BoolFeedback(() => IsMuted);
            VolumeLevelFeedback = new IntFeedback(() => _volumeLevelForSig);

            AddRoutingInputPort(
                new RoutingInputPort(RoutingPortNames.HdmiIn1, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.Hdmi, new Action(InputHdmi1), this), "90");
            AddRoutingInputPort(
                new RoutingInputPort(RoutingPortNames.HdmiIn2, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.Hdmi, new Action(InputHdmi2), this), "91");

            AddRoutingInputPort(
                new RoutingInputPort(RoutingPortNames.DisplayPortIn, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.DisplayPort, new Action(InputDisplayPort), this), "c0");
        }

        public override bool CustomActivate()
        {
            Communication.Connect();

            if (_isSerialComm)
            {
                CommunicationMonitor.Start();
            }

            return base.CustomActivate();
        }

        private void CommunicationMonitor_StatusChange(object sender, MonitorStatusChangeEventArgs e)
        {
            CommunicationMonitor.IsOnlineFeedback.FireUpdate();
        }

        private void PortGather_LineReceived(object sender, GenericCommMethodReceiveTextArgs args)
        {
            ReceiveQueue.Enqueue(new ProcessStringMessage(args.Text, ProcessResponse));
        }

        private void ProcessResponse(string s)
        {
            var data = s.Trim().Replace("OK", "").Split(' ');

            string command;
            string id;
            string responseValue;

            if (data.Length < 3)
            {
                Debug.Console(2, this, "Unable to parse message, not enough data in message: {0}", s);
                return;      
            }
            else
            {
                command = data[0];
                id = data[1];
                responseValue = data[2];
            }

            if (!id.Equals(Id))
            {
                Debug.Console(2, this, "Device ID Mismatch - Discarding Response");
                return;
            }

            //command = 'ka' 
            switch (command)
            {
                case ("a"):
                    UpdatePowerFb(responseValue);
                    break;
                case ("b"):
                    UpdateInputFb(responseValue);
                    break;
                case ("f"):
                    UpdateVolumeFb(responseValue);
                    break;
                case ("e"):
                    UpdateMuteFb(responseValue);
                    break;
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
        private void SendData(string s)
        {
            if (_lastCommandSentWasVolume)
            {
                if (s[1] != 'f')
                {
                    CrestronEnvironment.Sleep(100);
                }
            }

            _lastCommandSentWasVolume = s[1] == 'f';

            Communication.SendText(s + "\x0D");
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
        /// Select DisplayPort Input
        /// </summary>
        public void InputDisplayPort()
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
            //if (!(selector is Action))
            //    Debug.Console(1, this, "WARNING: ExecuteSwitch cannot handle type {0}", selector.GetType());

            if (PowerIsOn)
            {
                var action = selector as Action;
                if (action != null)
                {
                    action();
                }
            }
            else // if power is off, wait until we get on FB to send it. 
            {
                // One-time event handler to wait for power on before executing switch
                EventHandler<FeedbackEventArgs> handler = null; // necessary to allow reference inside lambda to handler
                handler = (o, a) =>
                {
                    if (_isWarmingUp)
                    {
                        return;
                    }

                    IsWarmingUpFeedback.OutputChange -= handler;

                    var action = selector as Action;
                    if (action != null)
                    {
                        action();
                    }
                };
                IsWarmingUpFeedback.OutputChange += handler; // attach and wait for on FB
                PowerOn();
            }
        }


        /// <summary>
        /// Set Power On For Device
        /// </summary>
        public override void PowerOn()
        {
            if (_isSerialComm)
            {
                SendData(string.Format("ka {0} {1}", Id, _smallDisplay ? "1": "01"));
            }
        }

        /// <summary>
        /// Set Power Off for Device
        /// </summary>
        public override void PowerOff()
        {
            SendData(string.Format("ka {0} {1}", Id, _smallDisplay ? "0" : "00"));
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
            var newInput = InputPorts.FirstOrDefault(i => i.FeedbackMatchObject.Equals(s.ToLower()));
            if (newInput != null && newInput != _currentInputPort)
            {
                _currentInputPort = newInput;
                CurrentInputFeedback.FireUpdate();
                var key = newInput.Key;
                switch (key)
                {
                    case "hdmiIn1":
                        InputNumber = 1;
                        break;
                    case "hdmiIn2":
                        InputNumber = 2;
                        break;
                    case "displayPortIn":
                        InputNumber = 3;
                        break;
                }
            }
        }

        /// <summary>
        /// Process Power Feedback from Response
        /// </summary>
        /// <param name="s">response from device</param>
        public void UpdatePowerFb(string s)
        {
            PowerIsOn = s.Contains("1");
            
        }

        /// <summary>
        /// Process Volume Feedback from Response
        /// </summary>
        /// <param name="s">response from device</param>
        public void UpdateVolumeFb(string s)
        {
            try
            {
                ushort newVol;
                if (!ScaleVolume)
                {
                    newVol = (ushort)NumericalHelpers.Scale(Convert.ToDouble(s), 0, 100, 0, 65535);
                }
                else
                {
                    newVol = (ushort)NumericalHelpers.Scale(Convert.ToDouble(s), _lowerLimit, _upperLimit, 0, 65535);
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
                Debug.Console(2, this, "Error updating volumefb for value: {1}: {0}", e, s);
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
                Debug.Console(2, this, "Unable to parse {0} to Int32 {1}", s, e);
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
                Debug.Console(0, this, "{0}", e.Message);
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
                    CrestronEnvironment.Sleep(100);
                    InputGet();
                    CrestronEnvironment.Sleep(100);
                    VolumeGet();
                    CrestronEnvironment.Sleep(100);
                    MuteGet();
                });
        }


        private void WolFunction(string macAddress)
        {
            if (Regex.IsMatch(macAddress, @"^([0-9A-Fa-f]{2}[\.:-]){5}([0-9A-Fa-f]{2})$") ||
                Regex.IsMatch(macAddress, @"^([0-9A-Fa-f]{12})"))
            {
                var address = (Regex.Replace(macAddress, @"(-|:|\.)", "")).ToLower();

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

            Debug.Console(2, this, "Invalid Mad Address sent to WolFunction - {0}", macAddress);
            throw new ArgumentException("Invalid MAC Address");
        }
    }
}