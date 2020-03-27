using System;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Routing;
using PepperDash.Essentials.Bridges;
using Newtonsoft.Json;

namespace Epi.Display.Lg 
{
	public class LgDisplay : TwoWayDisplayBase, IBasicVolumeWithFeedback, ICommunicationMonitor, IBridge

	{
		public static void LoadPlugin()
		{
			PepperDash.Essentials.Core.DeviceFactory.AddFactoryForType("lgdisplayplugin", LgDisplay.BuildDevice);	
		}

		public static LgDisplay BuildDevice(DeviceConfig dc)
		{
			var config = JsonConvert.DeserializeObject<DeviceConfig>(dc.Properties.ToString());
			var newMe = new LgDisplay(dc.Key, dc.Name, config);
			return newMe;
		}

        public static string MinimumEssentialsFrameworkVersion = "1.4.32";

        /// <summary>
        /// Volume Level Feedback Property
        /// </summary>
        public IntFeedback VolumeLevelFeedback { get; private set; }

        /// <summary>
        /// Volume Mute Feedback Property
        /// </summary>
        public BoolFeedback MuteFeedback {get; private set;}

        public IBasicCommunication Communication { get; private set; }
        public CommunicationGather PortGather { get; private set; }
        public StatusMonitorBase CommunicationMonitor { get; private set; }

        public string ID { get; private set; }

        bool LastCommandSentWasVolume;
        private bool IsSerialComm = false;

        private bool _PowerIsOn { get; set; }
        public bool PowerIsOn
        {
            get
            {
                return _PowerIsOn;
            }
            set
            {
                _PowerIsOn = value;
                PowerIsOnFeedback.FireUpdate();
            }
        }

        private bool _IsWarmingUp { get; set; }
        public bool IsWarmingUp
        {
            get
            {
                return _IsWarmingUp;
            }
            set
            {
                _IsWarmingUp = value;
                IsWarmingUpFeedback.FireUpdate();
            }
        }

        private bool _IsCoolingDown { get; set; }
        public bool IsCoolingDown
        {
            get
            {
                return _IsCoolingDown;
            }
            set
            {
                _IsCoolingDown = value;
                IsCoolingDownFeedback.FireUpdate();
            }
        }
        ushort _VolumeLevelForSig;
        int _LastVolumeSent;

        private bool _IsMuted { get; set; }
        public bool IsMuted
        {
            get
            {
                return _IsMuted;
            }
            set
            {
                _IsMuted = value;
                MuteFeedback.FireUpdate();
            }
        }

        RoutingInputPort _CurrentInputPort;
        byte[] IncomingBuffer = new byte[] { };
        ActionIncrementer VolumeIncrementer;
        bool VolumeIsRamping;
        public bool IsInStandby { get; private set; }
        bool IsPoweringOnIgnorePowerFb;
        private int LowerLimit { get; set; }
        private int UpperLimit { get; set; }
        private uint CoolingTimeMs { get; set; }
        private uint WarmingTimeMs { get; set; }
        private long PollIntervalMs { get; set; }
        private string UdpSocketKey { get;  set; }
        private string MacAddress {get;  set; }
        private byte[] MagicPacket { get;  set; }
        private GenericUdpServer WoLServer;


        CTimer PollRing;

        public List<BoolFeedback> InputFeedback;
        public List<bool> _InputFeedback;
        public IntFeedback InputNumberFeedback;
        public static List<string> InputKeys = new List<string>();
        public const int InputPowerOn = 101;

        public const int InputPowerOff = 102;
        private int _InputNumber;
        public int InputNumber
        {
            get
            {
                return this._InputNumber;
            }
            set
            {
                this._InputNumber = value;
                InputNumberFeedback.FireUpdate();
                UpdateBooleanFeedback(value);
            }
        }
        private bool ScaleVolume { get; set; }

        protected override Func<bool> PowerIsOnFeedbackFunc { get { return () => PowerIsOn; } }
        protected override Func<bool> IsCoolingDownFeedbackFunc { get { return () => IsCoolingDown; } }
        protected override Func<bool> IsWarmingUpFeedbackFunc { get { return () => IsWarmingUp; } }
        protected override Func<string> CurrentInputFeedbackFunc { get { return () => _CurrentInputPort.Key; } }

        public LgDisplay(string key, string name, DeviceConfig config)
			: base(config.Key, config.Name)
		{
            Communication = CommFactory.CreateCommForDevice(config);
            var props = config.Properties.ToObject<LgDisplayPropertiesConfig>();
            if (props != null) {
                ID = string.IsNullOrEmpty(props.Id) ? props.Id : "01";
                UpperLimit = props.volumeUpperLimit;
                LowerLimit = props.volumeLowerLimit;
                PollIntervalMs = props.pollIntervalMs > 1999 ? props.pollIntervalMs : 10000;
                CoolingTimeMs = props.coolingTimeMs > 0 ? props.coolingTimeMs : 10000;
                WarmingTimeMs = props.warmingTimeMs > 0 ? props.warmingTimeMs : 8000;
                UdpSocketKey = props.udpSocketKey;
                MacAddress = props.macAddress;

                Init();
            }
		}

        

        private void Init()
        {
            WarmupTime = WarmingTimeMs > 0 ? WarmingTimeMs : 10000;
            CooldownTime = CoolingTimeMs > 0 ? CoolingTimeMs : 8000;

            _InputFeedback = new List<bool>();
            InputFeedback = new List<BoolFeedback>();

            if (UpperLimit != LowerLimit)
            {
                if (UpperLimit > LowerLimit)
                {
                    ScaleVolume = true;
                }
            }
            PortGather = new CommunicationGather(Communication, "x");
            PortGather.LineReceived += new EventHandler<GenericCommMethodReceiveTextArgs>(PortGather_LineReceived);

            var socket = Communication as ISocketStatus;
            if (socket != null)
            {
                //This Instance Uses IP Control
                Debug.Console(2, this, "The LG Display Plugin does NOT support IP Control currently");
                socket.ConnectionChange += new EventHandler<GenericSocketStatusChageEventArgs>(socket_ConnectionChange);
                IsSerialComm = false;
                var udpServer = DeviceManager.GetDeviceForKey(UdpSocketKey);
                WoLServer = udpServer as GenericUdpServer;
            }
            else
            {
                // This instance uses RS-232 Control
                IsSerialComm = true;
            }

            //TODO: determine your poll rate the first value in teh GenericCommunicationMonitor, currently 45s (45,000)
            var PollInterval = PollIntervalMs > 0 ? PollIntervalMs : 10000;
            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, PollInterval, 180000, 300000, StatusGet);

            DeviceManager.AddDevice(CommunicationMonitor);

            if (!ScaleVolume)
            {
                VolumeIncrementer = new ActionIncrementer(655, 0, 65535, 800, 80,
                    v => SetVolume((ushort)v),
                    () => _LastVolumeSent);
            }
            else
            {
                var ScaleUpper = NumericalHelpers.Scale((double)UpperLimit, 0, 100, 0, 65535);
                var ScaleLower = NumericalHelpers.Scale((double)LowerLimit, 0, 100, 0, 65535);

                VolumeIncrementer = new ActionIncrementer(655, (int)ScaleLower, (int)ScaleUpper, 800, 80,
                    v => SetVolume((ushort)v),
                    () => _LastVolumeSent);
            }

            if(!String.IsNullOrEmpty(MacAddress)) {
                MagicPacket = WolFunction(MacAddress);
            }

            AddRoutingInputPort(new RoutingInputPort(RoutingPortNames.HdmiIn1, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, new Action(InputHdmi1), this), "90");
            AddRoutingInputPort(new RoutingInputPort(RoutingPortNames.HdmiIn2, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, new Action(InputHdmi2), this), "91");
            AddRoutingInputPort(new RoutingInputPort(RoutingPortNames.DisplayPortIn, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.DisplayPort, new Action(InputDisplayPort), this), "C0");       
        }

        void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
        {
            throw new NotImplementedException();
        }

        public override bool CustomActivate()
        {
            Communication.Connect();
            CommunicationMonitor.StatusChange += new EventHandler<MonitorStatusChangeEventArgs>(CommunicationMonitor_StatusChange);
            if (IsSerialComm)
            {
                CommunicationMonitor.Start();
            }
            return true;
        }

        void CommunicationMonitor_StatusChange(object sender, MonitorStatusChangeEventArgs e)
        {
            throw new NotImplementedException();
        }

        void PortGather_LineReceived(object sender, GenericCommMethodReceiveTextArgs args)
        {
            Debug.Console(1, this, "RX : '{0}'", args.Text);

            var data = Regex.Replace(args.Text, "OK", "").Split(' ');


            var command = data[0];
            var id = data[1];
            var responseValue = data[2];

            if (id.Equals(this.ID))
            {
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
                    default:
                        break;
                }
            }
            else
                Debug.Console(2, this, "Device ID Mismatch - Discarding Response");

        }

        void AddRoutingInputPort(RoutingInputPort port, string fbMatch)
        {
            port.FeedbackMatchObject = fbMatch;
            InputPorts.Add(port);
        }

        /// <summary>
        /// Scales the level to the range of the display and sends the command
        /// Set: "kf [SetID] [Range 0x00 - 0x64]"
        /// </summary>
        /// <param name="level"></param>
        public void SetVolume(ushort level)
        {
            int scaled;
            _LastVolumeSent = level;
            if (!ScaleVolume)
            {
                scaled = (int)NumericalHelpers.Scale(level, 0, 65535, 0, 100);
            }
            else
            {
                scaled = (int)NumericalHelpers.Scale(level, 0, 65535, LowerLimit, UpperLimit);
            }

            SendData(string.Format("kf {0} {1}", ID, scaled));       
        }

        /// <summary>
        /// Formats an outgoing message
        /// 
        /// </summary>
        /// <param name="s"></param>
        void SendData(string s)
        {
            if (LastCommandSentWasVolume)
            {
                if (s[1] != 'f')
                {
                    CrestronEnvironment.Sleep(100);
                }
            }
            if (s[1] == 'f')
            {
                LastCommandSentWasVolume = true;
            }
            else
            {
                LastCommandSentWasVolume = false;
            }
            Communication.SendText(s + "\x0D");
        }

        /// <summary>
        /// Set Mute On
        /// </summary>
        public void MuteOn()
        {
            SendData(string.Format("ke {0} 0", ID));
        }

        /// <summary>
        /// Set Mute Off
        /// </summary>
        public void MuteOff()
        {
            SendData(string.Format("ke {0} 1", ID));
        }

        /// <summary>
        /// Poll Mute State
        /// </summary>
        public void MuteGet()
        {
            SendData(string.Format("ke {0} FF", ID));
        }

        /// <summary>
        /// Toggle Current Mute State
        /// </summary>
        public void MuteToggle()
        {
            if (IsMuted)
                MuteOff();
            else
                MuteOn();
        }

        /// <summary>
        /// Decrement Volume on Press
        /// </summary>
        /// <param name="pressRelease"></param>
        public void VolumeDown(bool pressRelease)
        {
            if (pressRelease)
            {
                VolumeIncrementer.StartDown();
                VolumeIsRamping = true;
            }
            else
            {
                VolumeIsRamping = false;
                VolumeIncrementer.Stop();
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
                VolumeIncrementer.StartUp();
                VolumeIsRamping = true;
            }
            else
            {
                VolumeIsRamping = false;
                VolumeIncrementer.Stop();
            }
        }

        /// <summary>
        /// Poll Volume
        /// </summary>
        public void VolumeGet()
        {
            SendData(string.Format("kf {0} FF", ID));
            PollRing = new CTimer(o => MuteGet(), null, 100);
        }

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey)
        {
            this.LinkToApiExt(trilist, joinStart, joinMapKey);
        }

        /// <summary>
        /// Select Hdmi 1 Input
        /// </summary>
        public void InputHdmi1()
        {
            SendData(string.Format("xb {0} 90", ID));
        }

        /// <summary>
        /// Select Hdmi 2 Input
        /// </summary>
        public void InputHdmi2()
        {
            SendData(string.Format("xb {0} 91", ID));
        }

        /// <summary>
        /// Select DisplayPort Input
        /// </summary>
        public void InputDisplayPort()
        {
            SendData(string.Format("xb {0} C0", ID));
        }

        /// <summary>
        /// Poll input
        /// </summary>
        public void InputGet()
        {
            SendData(string.Format("xb {0} FF", ID));
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
                (selector as Action)();
            else // if power is off, wait until we get on FB to send it. 
            {
                // One-time event handler to wait for power on before executing switch
                EventHandler<FeedbackEventArgs> handler = null; // necessary to allow reference inside lambda to handler
                handler = (o, a) =>
                {
                    if (!_IsWarmingUp) // Done warming
                    {
                        IsWarmingUpFeedback.OutputChange -= handler;
                        (selector as Action)();
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
            if (IsSerialComm)
            {
                SendData(string.Format("ka {0} 1", ID));
            }
            if (!IsSerialComm)
            {
                if (WoLServer != null || MagicPacket != null)
                {
                    WoLServer.Connect();
                    WoLServer.SendBytes(MagicPacket);
                }
                else
                    Debug.Console(0, this, "Problem Generatig the Magic Packet!!! - WoL Failed!");
            }
        }

        /// <summary>
        /// Set Power Off for Device
        /// </summary>
        public override void PowerOff()
        {
            SendData(string.Format("ka {0} 0", ID));
        }

        /// <summary>
        /// Poll Power
        /// </summary>
        public void PowerGet()
        {
            SendData(string.Format("ka {0} FF", ID));
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
                PowerOn();
        }

        /// <summary>
        /// Process Input Feedback from Response
        /// </summary>
        /// <param name="s">response from device</param>
        public void UpdateInputFb(string s)
        {
            var newInput = InputPorts.FirstOrDefault(i => i.FeedbackMatchObject.Equals(s));
            if (newInput != null && newInput != _CurrentInputPort)
            {
                _CurrentInputPort = newInput;
                CurrentInputFeedback.FireUpdate();
                var key = newInput.Key;
                switch (key)
                {
                    case "hdmiIn1" :
                        InputNumber = 1;
                        break;
                    case "hdmiIn2" :
                        InputNumber = 2;
                        break;
                    case "displayPortIn" :
                        InputNumber = 3;
                        break;
                    default :
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
            PowerIsOn = s.Contains("1") ? true : false;
            PowerIsOnFeedback.FireUpdate();
        }

        /// <summary>
        /// Process Volume Feedback from Response
        /// </summary>
        /// <param name="s">response from device</param>
        public void UpdateVolumeFb(string s)
        {
            ushort newVol = 0;
            if (!ScaleVolume)
            {
                newVol = (ushort)NumericalHelpers.Scale(Convert.ToDouble(s), 0, 100, 0, 65535);
            }
            else
            {
                newVol = (ushort)NumericalHelpers.Scale(Convert.ToDouble(s), LowerLimit, UpperLimit, 0, 65535);
            }
            if (!VolumeIsRamping)
                _LastVolumeSent = newVol;
            if (newVol != _VolumeLevelForSig)
            {
                _VolumeLevelForSig = newVol;
                VolumeLevelFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Process Mute Feedback from Response
        /// </summary>
        /// <param name="s">response from device</param>
        public void UpdateMuteFb(string s)
        {
            IsMuted = s.Contains("1") ? true : false;
        }

        /// <summary>
        /// Updates Digital Route Feedback for Simpl EISC
        /// </summary>
        /// <param name="data">currently routed source</param>
        private void UpdateBooleanFeedback(int data)
        {
            try
            {
                if (_InputFeedback[data] == true)
                    return;
                else
                {
                    for (int i = 1; i < InputPorts.Count + 1; i++)
                    {
                        _InputFeedback[i] = false;
                    }
                    _InputFeedback[data] = true;
                    foreach (var item in InputFeedback)
                    {
                        var update = item;
                        update.FireUpdate();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Console(0, this, "Exception Here - {0}", e.Message);
            }
        }

        /// <summary>
        /// Starts the Poll Ring
        /// </summary>
        public void StatusGet()
        {
            //SendBytes(new byte[] { Header, StatusControlCmd, 0x00, 0x00, StatusControlGet, 0x00 });

            PowerGet();
            if (PollRing != null) PollRing = null;
            PollRing = new CTimer(o => InputGet(), null, 1000);

        }




        private byte[] WolFunction(string macAddress)
        {
            if (Regex.IsMatch(macAddress, @"^([0-9A-Fa-f]{2}[\.:-]){5}([0-9A-Fa-f]{2})$") ||
                Regex.IsMatch(macAddress, @"^([0-9A-Fa-f]{12})"))
            {

                var _macAddress = (Regex.Replace(macAddress, @"(-|:|\.)", "")).ToLower();

                var counter = 0;

                byte[] bytes = new byte[1024];

                //Packet starts with 6 iterations of 0xFF
                for (int i = 0; i < 6; i++)
                    bytes[counter++] = 0xFF;

                //Packet has 16 iterations of the mac address
                for (int y = 0; y < 16; y++)
                {
                    int i = 0;
                    for (int z = 0; z < 6; z++)
                    {
                        bytes[counter++] =
                            byte.Parse(_macAddress.Substring(i, 2),
                            NumberStyles.HexNumber);
                        i += 2;
                    }
                }
                return bytes;
            }
            else
            {
                Debug.Console(2, this, "Invalid Mad Address sent to WolFunction - {0}", macAddress);
                throw new ArgumentException("Invalid MAC Address");
            }
        }


    }
}

