using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Epi.Display.Lg;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Devices.Displays;
using DisplayBase = PepperDash.Essentials.Devices.Common.Displays.DisplayBase;


namespace PepperDash.Essentials.Plugins.Lg.Display
{
    public class LgDisplayIrController : DisplayBase, IBasicVolumeControls, IInputHdmi1, IInputHdmi2, IInputHdmi3, IInputDisplayPort1, IBridgeAdvanced, IHasInputs<string>
    {
        private readonly LgDisplayPropertiesConfig config;

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


        public LgDisplayIrController(string key, string name, LgDisplayPropertiesConfig config, IBasicCommunication comms)
            : base(key, name)
        {
            this.config = config;
            var props = config;
            if (props == null)
            {
                Debug.LogError(this, "Display configuration must be included");

                return;
            }

            CooldownTime = props.coolingTimeMs > 0 ? props.coolingTimeMs : 10000;
            WarmupTime = props.warmingTimeMs > 0 ? props.warmingTimeMs : 8000;
        }

        /// <summary>
        /// Initialization of the device
        /// </summary>
        public override void Initialize()
        {
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
                new RoutingInputPort(RoutingPortNames.DisplayPortIn, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                    eRoutingPortConnectionType.DisplayPort, new Action(InputDisplayPort1), this), "c0");

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
                    SetInput = inputIndex + 1;
                });

                trilist.StringInput[(ushort)(joinMap.InputNamesOffset.JoinNumber + inputIndex)].StringValue = string.IsNullOrEmpty(input.Key) ? string.Empty : input.Key;

            }

            // input (analog select)
            trilist.SetUShortSigAction(joinMap.InputSelect.JoinNumber, analogValue =>
            {
                SetInput = analogValue;
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




        #region  Power Members

        /// <summary>
        /// Set Power On For Device
        /// </summary>
        public override void PowerOn()
        {
            // TODO - Implement IR control
        }

        /// <summary>
        /// Set Power Off for Device
        /// </summary>
        public override void PowerOff()
        {
            // TODO - Implement IR control
        }


        /// <summary>
        /// Toggle current power state for device
        /// </summary>
        public override void PowerToggle()
        {
            // TODO - Implement IR control
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

        /// <summary>
        /// Sets the requested input
        /// </summary>
        private int SetInput
        {
            set
            {
                if (value <= 0 || value > InputPorts.Count)
                {
                    Debug.LogError(this, "SetInput: Value {0} is out of range (1-{1})", value, InputPorts.Count);
                    return;
                }

                var portIndex = value - 1;


                var port = GetInputPort(portIndex);
                if (port == null)
                {
                    Debug.LogError(this, "SetInput: Port at index {0} is null", portIndex);
                    return;
                }


                if (port.Selector is Action action)
                {
                    ExecuteSwitch(action);
                }
                else
                {
                    Debug.LogError(this, "SetInput: Port selector is not an Action! Type: {0}",

                        port.Selector?.GetType().Name ?? "NULL");
                }
            }
        }

        private void SetupInputs()
        {
            Inputs = new LgDisplayIrInputs
            {
                Items = new Dictionary<string, ISelectableItem>
                {
                    {
                        "90", new LgDisplayIrInput("90", "HDMI 1", this)
                    },
                    {
                        "91", new LgDisplayIrInput("91", "HDMI 2", this)
                    },
                    {
                        "92", new LgDisplayIrInput("92", "HDMI 3", this)
                    },
                    {
                        "c0", new LgDisplayIrInput("c0", "DisplayPort", this)
                    },
                }
            };
            ApplyFriendlyNames(config);

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

        /// <summary>
        /// Select Hdmi 1 Input
        /// </summary>
        public void InputHdmi1()
        {
            // TODO - Implement IR control
        }

        /// <summary>
        /// Select Hdmi 2 Input
        /// </summary>
        public void InputHdmi2()
        {
            // TODO - Implement IR control
        }

        /// <summary>
        /// Select Hdmi 3 Input
        /// </summary>
        public void InputHdmi3()
        {
            // TODO - Implement IR control
        }

        /// <summary>
        /// Select DisplayPort Input
        /// </summary>
        public void InputDisplayPort1()
        {
            // TODO - Implement IR control
        }

        /// <summary>
        /// Executes a switch, turning on display if necessary.
        /// </summary>
        /// <param name="selector"></param>
        public override void ExecuteSwitch(object selector)
        {
            // if (PowerIsOn)
            // {
            //     var action = selector as Action;
            //     if (action != null)
            //     {
            //         action();
            //     }
            //     else
            //     {
            //         Debug.LogError(this, "ExecuteSwitch: selector is not an Action! Type: {0}", selector?.GetType().Name ?? "NULL");
            //     }
            // }
            // else // if power is off, wait until we get on FB to send it. 
            // {
            //     // One-time event handler to wait for power on before executing switch
            //     EventHandler<FeedbackEventArgs> handler = null; // necessary to allow reference inside lambda to handler
            //     handler = (o, a) =>
            //     {
            //         if (isWarmingUp)
            //         {
            //             return;
            //         }

            //         IsWarmingUpFeedback.OutputChange -= handler;

            //         var action = selector as Action;
            //         if (action != null)
            //         {
            //             action();
            //         }
            //     };
            //     IsWarmingUpFeedback.OutputChange += handler; // attach and wait for on FB
            //     PowerOn();
            // }
        }

        #endregion


        #region Volume Members

        public void VolumeUp(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        public void VolumeDown(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        public void MuteToggle()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
