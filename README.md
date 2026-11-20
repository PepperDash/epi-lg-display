![PepperDash Essentials Plugin Logo](/images/essentials-plugin-blue.png)

![PepperDash Essentials](https://img.shields.io/badge/PepperDash%20Essentials-≥%20v3.0.0--rc.20-blue)
![.NET](https://img.shields.io/badge/.NET-8-512BD4)
![Crestron](https://img.shields.io/badge/Crestron-4--Series-lightgrey)
![License](https://img.shields.io/badge/license-MIT-green)

# PepperDash Essentials LG Display Plugin (c) 2026

## License

Provided under MIT license

## Overview

This repo contains a plugin for use with [PepperDash Essentials](https://github.com/PepperDash/Essentials). This plugin enables Essentials to communicate with and control an LG display over RS-232.

## Example Config Object

```json
{
  "key": "display01",
  "name": "Display 1",
  "group": "displays",
  "type": "lg",
  "properties": {
    "id": "01",
    "volumeUpperLimit": 100,
    "volumeLowerLimit": 0,
    "pollIntervalMs": 45000,
    "coolingTimeMs": 10000,
    "warmingTimeMs": 10000,
    "smallDisplay": false,
    "control": {
      "method": "com",
      "controlPortNumber": 1,
      "controlPortDevKey": "processor",
      "comParams": {
        "protocol": "RS232",
        "parity": "None",
        "baudRate": 9600,
        "dataBits": 8,
        "softwareHandshake": "None",
        "hardwareHandshake": "None",
        "stopBits": 1
      }
    },
    "friendlyNames": [                      //if you want to use friendly names, add this section
	        {
	        	"inputKey": "90",           //The input key for the input you want to use a friendly name for, this has to a valid input key(90,91,c0)
	        	"name": "Friendly Name 1",  //The desired name to be displayed on the screen
            "hideInput": false              //if set to true, the input will not be displayed in the list of inputs
	        },
	        {
	        	"inputKey": "91",
	        	"name": "Friendly Name 2",
            "hideInput": false
	        },
	        {
	        	"inputKey": "c0",
	        	"name": "Friendly Name 3",
            "hideInput": true

	        }
        ],
  }

}
```

The `smallDisplay` configuration option is used to control padding of the power on command. If `smallDisplay` is `true`, the power on command sent will be `ka 01 1`. If `smallDisplay` is `false`, the power on command sent will be `ka 01 01`.

For more configuration information, see the [PepperDash Essentials wiki](https://github.com/PepperDash/Essentials/wiki).

## Mock Display (bench testing without hardware)

The `mockLgDisplay` type builds the real LG controller on an in-process mock panel instead of a serial port. Power, warm-up and cool-down, inputs, volume, mute, polling and NG handling all run through the same code as with real hardware, so the room and the touchpanel behave the same way. To switch an existing display to the mock, change only `type`. Any `control` block is ignored. The type was named `lgDisplayMock` before 2026-10-19, and that name still works as an alias.

```json
{
  "key": "display01",
  "name": "Display 1",
  "group": "displays",
  "type": "mockLgDisplay",
  "properties": {
    "id": "01",
    "mock": {                     // optional; these are the defaults
      "responseDelayMs": 50,      // delay before the panel answers each command
      "powerOn": false,           // starting power state
      "input": "90",              // starting input: 90-93 (HDMI 1-4) or c0 (DisplayPort)
      "volume": 20,               // starting volume, 0-100
      "muted": false,
      "videoMuted": false
    }
  }
}
```

Like a real panel, the mock answers NG to input, volume and mute commands while it is off, and it answers the RS232 protocol only, so it starts polling without `overrideWol`.

Use the `mocklg` console command to make changes at the panel, or to inject faults. The controller picks up panel-side changes on its next poll.

```
mocklg display01 status
mocklg display01 power on|off
mocklg display01 input 90|91|92|93|c0
mocklg display01 volume 0-100
mocklg display01 mute on|off
mocklg display01 fault none|ng|silent    // ng: answer everything with NG; silent: stop answering (goes offline)
```

## Github Actions

This repo contains two Github Action workflows that will build this project automatically. Modify the SOLUTION_PATH and SOLUTION_FILE environment variables as needed. Any branches named `feature/*`, `release/*`, `hotfix/*` or `development` will automatically be built with the action and create a release in the repository with a version number based on the latest release on the master branch. If there are no releases yet, the version number will be 0.0.1. The version number will be modified based on what branch triggered the build:

- `feature` branch builds will be tagged with an `alpha` descriptor, with the Action run appended: `0.0.1-alpha-1`
- `development` branch builds will be tagged with a `beta` descriptor, with the Action run appended: `0.0.1-beta-2`
- `release` branches will be tagged with an `rc` descriptor, with the Action run appended: `0.0.1-rc-3`
- `hotfix` branch builds will be tagged with a `hotfix` descriptor, with the Action run appended: `0.0.1-hotfix-4`

Builds on the `Main` branch will ONLY be triggered by manually creating a release using the web interface in the repository. They will be versioned with the tag that is created when the release is created. The tags MUST take the form `major.minor.revision` to be compatible with the build process. A tag like `v0.1.0-alpha` is NOT compatabile and may result in the build process failing.

If you have any questions about the action, contact Andrew Welker or Neil Dorin.
<!-- START Minimum Essentials Framework Versions -->
### Minimum Essentials Framework Versions

- 3.0.0
- 3.0.0
- 3.0.0
<!-- END Minimum Essentials Framework Versions -->
<!-- START Config Example -->
### Config Example

```json
{
    "key": "GeneratedKey",
    "uid": 1,
    "name": "GeneratedName",
    "type": "LgDisplayProperties",
    "group": "Group",
    "properties": {
        "id": "SampleString",
        "volumeUpperLimit": 0,
        "volumeLowerLimit": 0,
        "pollIntervalMs": 0,
        "coolingTimeMs": "SampleValue",
        "warmingTimeMs": "SampleValue",
        "udpSocketKey": "SampleString",
        "macAddress": "SampleString",
        "wolBroadcastAddress": "SampleString",
        "wolPort": "SampleValue",
        "smallDisplay": true,
        "overrideWol": true,
        "friendlyNames": [
            {
                "inputKey": "SampleString",
                "name": "SampleString",
                "hideInput": true
            }
        ],
        "RemoteOnly": true
    }
}
```
<!-- END Config Example -->
<!-- START Supported Types -->

<!-- END Supported Types -->
<!-- START Join Maps -->

<!-- END Join Maps -->
<!-- START Interfaces Implemented -->
### Interfaces Implemented

- ISelectableItems<string>
- IBasicVolumeWithFeedback
- ICommunicationMonitor
- IBridgeAdvanced
- IHasInputs<string>
- IBasicVideoMuteWithFeedback
- IWarmingCooling
- IBasicCommunication
- IBasicVolumeControls
- IDPad
- INumericKeypad
- ITransport
- IChannel
- IColor
<!-- END Interfaces Implemented -->
<!-- START Base Classes -->
### Base Classes

- DisplayControllerJoinMap
- TwoWayDisplayBase
- LgDisplayPropertiesConfig
- DisplayBase
<!-- END Base Classes -->
<!-- START Public Methods -->
### Public Methods

- public void Config_Class_Exists()
- public void Config_Has_Parameterless_Constructor()
- public void Config_Property_Has_JsonPropertyAttribute(string jsonName)
- public void FriendlyName_Property_Has_JsonPropertyAttribute(string jsonName)
- public void Config_Property_Type_Matches(string propertyName, string expectedTypeName)
- public void FriendlyNames_Is_List_Of_FriendlyName()
- public void Config_Sample_Json_Has_Expected_Keys()
- public void FriendlyNames_Deserialize_As_List_With_Expected_Shape()
- public void Deployed_Config_Shapes_Bind_Every_Property(string json)
- public void Tcp_Bench_Config_Uses_Port_9761_With_OverrideWol()
- public void Parse_CleanReply(string line, string cmd, string id, bool ok, string data)
- public void Parse_PowerOffReplyAfterUnsolicitedMcLines_IsNotLost()
- public void Parse_ReplyAfterStrayBytesAndMcLines_IsNotLost()
- public void Parse_InputReplyAfterMcLines_IsNotLost()
- public void Parse_NoReply_ReturnsNull(string? line)
- public void Assembly_Loads_Successfully()
- public void Assembly_Name_Is_EpiDisplayLg()
- public void Factory_Count_Is_Three()
- public void Factory_Exists_ByName(string factoryClassName)
- public void All_Factories_Have_Parameterless_Constructor()
- public void Factory_Sets_MinimumEssentialsFrameworkVersion_To_Pinned_Version(string factoryClassName)
- public void Factory_Sets_TypeNames(string factoryClassName)
- public void Factory_Source_Contains_TypeName(string factoryClassName, string typeName)
- public void No_Duplicate_TypeNames_Across_Factories()
- public void Select()
- public void LgInput_Select_DoesNotSendDirectly()
- public void LgInput_Select_UsesControllerSelectInput()
- public void Controller_SelectInput_GoesThroughExecuteSwitch()
- public void SelectInput(string inputKey)
- public void PowerOn()
- public void PowerOff()
- public void Input(string input)
- public void WarmupDone()
- public void CooldownDone()
- public void Ready()
- public void PowerOn_WhenOff_SendsOnImmediately()
- public void OnThenOff_DuringWarmup_SendsOffWhenWarmupEnds()
- public void OnOffOn_DuringWarmup_SendsNothingWhenWarmupEnds()
- public void PowerOn_WhileWarmingUp_IsANoOp()
- public void RepeatedOff_DuringWarmup_SendsOneOff()
- public void OffThenOn_DuringCooldown_SendsOnWhenCooldownEnds()
- public void OffOnOff_DuringCooldown_SendsNothingWhenCooldownEnds()
- public void PowerOff_WhileCoolingDown_IsANoOp()
- public void PowerCommands_WhenIdle_AreSentImmediately()
- public void Input_WhenOn_IsSentImmediately()
- public void Inputs_DuringWarmup_OnlyTheLastIsSentOnceReady()
- public void Input_WhenOff_PowersOnThenSendsInputAfterWarmup()
- public void Inputs_WhenOff_SendOnePowerOnAndTheLastInput()
- public void OffDuringCooldown_ThenInput_SendsOnThenThatInput()
- public void InputDuringCooldown_ThenPowerOff_OffWinsAndInputIsDropped()
- public void InputWhenOff_ThenPowerOffDuringWarmup_OffWinsAndInputIsDropped()
- public void PowerOffThenInput_DuringWarmup_InputWinsAsTheLatestRequest()
- public void InputDuringSettle_IsSentNowAndReplacesThePendingInput()
- public void PowerOffDuringSettle_DropsThePendingInput()
- public void TakeReadyInput_WhileWarmingOrCooling_LeavesTheInputPending()
- public void Input_WhenOff_RequestsPowerOnDecision()
- public void Select()
- public LgPowerCommand RequestPower(bool on, LgPanelState state)
- public LgInputDecision RequestInput(TInput input, LgPanelState state)
- public LgPowerCommand WarmupComplete()
- public LgPowerCommand CooldownComplete()
- public void SetVolume(ushort level)
- public void MuteOn()
- public void MuteOff()
- public void MuteToggle()
- public void VolumeDown(bool pressRelease)
- public void VolumeUp(bool pressRelease)
- public void VideoMuteOn()
- public void VideoMuteOff()
- public void VideoMuteToggle()
- public void VideoMuteGet()
- public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
- public void SendData(string s)
- public void SelectInput(string inputKey)
- public void ListRoutingInputPorts()
- public void MuteGet()
- public void VolumeGet()
- public void InputHdmi1()
- public void InputHdmi2()
- public void InputHdmi3()
- public void InputHdmi4()
- public void InputDisplayPort1()
- public void InputGet()
- public void PowerGet()
- public void UpdateInputFb(string s)
- public void UpdatePowerFb(string s)
- public void UpdateVideoMuteFb(string s)
- public void UpdateVolumeFb(string s)
- public void UpdateMuteFb(string s)
- public void StatusGet()
- public void Connect()
- public void Disconnect()
- public void SendText(string text)
- public void SendBytes(byte[] bytes)
- public string Respond(string line)
- public void SetPower(bool on)
- public bool SetInput(string inputCode)
- public void SetVolume(int level)
- public void SetMute(bool muted)
- public string Describe()
- public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
- public void SendIrCommand(string cmd)
- public void PowerOnPress(bool pressRelease)
- public void PowerOffPress(bool pressRelease)
- public void PowerTogglePress(bool pressRelease)
- public void ListRoutingInputPorts()
- public void InputHdmi1()
- public void InputHdmi1(bool pressRelease)
- public void InputHdmi2()
- public void InputHdmi2(bool pressRelease)
- public void InputHdmi3()
- public void InputHdmi3(bool pressRelease)
- public void InputHdmi4()
- public void InputHdmi4(bool pressRelease)
- public void InputTv()
- public void InputTv(bool pressRelease)
- public void InputAntenna()
- public void InputAntenna(bool pressRelease)
- public void InputNetflix()
- public void InputNetflix(bool pressRelease)
- public void InputPrimeVideo()
- public void InputPrimeVideo(bool pressRelease)
- public void InputToggle()
- public void InputToggle(bool pressRelease)
- public void VolumeUp(bool pressRelease)
- public void VolumeDown(bool pressRelease)
- public void MuteToggle()
- public void Up(bool pressRelease)
- public void Down(bool pressRelease)
- public void Left(bool pressRelease)
- public void Right(bool pressRelease)
- public void Select(bool pressRelease)
- public void Menu(bool pressRelease)
- public void Exit(bool pressRelease)
- public void Digit0(bool pressRelease)
- public void Digit1(bool pressRelease)
- public void Digit2(bool pressRelease)
- public void Digit3(bool pressRelease)
- public void Digit4(bool pressRelease)
- public void Digit5(bool pressRelease)
- public void Digit6(bool pressRelease)
- public void Digit7(bool pressRelease)
- public void Digit8(bool pressRelease)
- public void Digit9(bool pressRelease)
- public void KeypadAccessoryButton1(bool pressRelease)
- public void KeypadAccessoryButton2(bool pressRelease)
- public void Play(bool pressRelease)
- public void Pause(bool pressRelease)
- public void Rewind(bool pressRelease)
- public void FFwd(bool pressRelease)
- public void ChapMinus(bool pressRelease)
- public void ChapPlus(bool pressRelease)
- public void Stop(bool pressRelease)
- public void Record(bool pressRelease)
- public void ChannelUp(bool pressRelease)
- public void ChannelDown(bool pressRelease)
- public void LastChannel(bool pressRelease)
- public void Guide(bool pressRelease)
- public void Info(bool pressRelease)
- public void Red(bool pressRelease)
- public void Green(bool pressRelease)
- public void Yellow(bool pressRelease)
- public void Blue(bool pressRelease)
- public void Home(bool pressRelease)
- public void Back(bool pressRelease)
- public void Enter(bool pressRelease)
- public void Select()
<!-- END Public Methods -->
<!-- START Bool Feedbacks -->
### Bool Feedbacks

- MuteFeedback
- VideoMuteIsOn
<!-- END Bool Feedbacks -->
<!-- START Int Feedbacks -->
### Int Feedbacks

- InputNumberFeedback
- VolumeLevelFeedback
<!-- END Int Feedbacks -->
<!-- START String Feedbacks -->

<!-- END String Feedbacks -->
