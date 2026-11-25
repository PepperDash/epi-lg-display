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
    "um5kSeriesInputCodes": false,          //true for the LG UM5K series (110UM5K-BP.CUSFLR): HDMI 3 is a6 instead of 92
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
	        	"inputKey": "90",           //The input key for the input you want to use a friendly name for, must be "inputKey" (an entry named "key" is ignored) with a valid LG input code: 90, 91, 92, 93 or c0
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

### UM5K series input codes (`um5kSeriesInputCodes`)

Set `"um5kSeriesInputCodes": true` for the LG UM5K series. It was verified on the 110" model **110UM5K-BP.CUSFLR**, which uses input code `a6` for HDMI 3 instead of `92`. On site, `xb 01 92` was rejected with `b 01 NG92x`, and with the panel switched to HDMI 3 from the remote the input poll answered `b 01 OKa6x`.

When it is `true`:

- HDMI 3 is selected with `xb {id} A6`, upper-case like the driver's other hex commands.
- An `a6` input poll reply is reported as HDMI 3, so routing to `hdmiIn3`, the bridge input joins and the Mobile Control input list all follow it.
- The panel is never sent `92`. HDMI 3 is keyed `a6` in the input list; where a config or caller names HDMI 3 by the standard key `"92"` (a `friendlyNames` entry, or `SelectInput("92")` from a room plugin or `devjson`), the plugin translates it to `a6` first, so the panel still receives `xb {id} A6`.
- The `mockLgDisplay` type answers like a UM5K panel: it accepts `a6` for HDMI 3 and answers `92` with NG.

Only HDMI 3 is remapped. HDMI 1 (`90`), HDMI 2 (`91`), HDMI 4 (`93`) and DisplayPort (`c0`) have not been verified on this model, so they keep their standard codes. The default is `false`, which leaves every input code unchanged.

Confirmed working on site on 2026-11-24 with two 110UM5K-BP.CUSFLR panels over RS-232: with the option set, routing to `hdmiIn3` sends `xb 01 A6` and the panel switches to HDMI 3.

#### Using it in a config

1. Load a build of this plugin that includes `um5kSeriesInputCodes`. Older builds send `xb {id} 92` for HDMI 3 whatever the config says, and the panel answers `NG92x`.
2. Add `"um5kSeriesInputCodes": true` to the `properties` of each UM5K display. Leave it out (or `false`) for every other LG model.
3. Keep the tie line on `hdmiIn3` if that is where the cable is. The port name does not change; only the code sent to the panel does.
4. In `friendlyNames`, use `"inputKey"` with the standard codes. `92` still names HDMI 3, so you don't need to write `a6`.
5. The 110UM5K-BP.CUSFLR has no HDMI 4 port, but the driver still lists HDMI 4 (`93`). Hide it so it doesn't show in the UI. The panel does have a DisplayPort input (`c0`), so leave it shown unless the site doesn't use it:

```json
"um5kSeriesInputCodes": true,
"friendlyNames": [
  { "inputKey": "90", "name": "HDMI 1",      "hideInput": false },
  { "inputKey": "91", "name": "HDMI 2",      "hideInput": false },
  { "inputKey": "92", "name": "HDMI 3",      "hideInput": false },
  { "inputKey": "93", "name": "HDMI 4",      "hideInput": true },
  { "inputKey": "c0", "name": "DisplayPort", "hideInput": false }
]
```

To check it on a processor, turn on stream debug for the display's COM device (the display key plus `-com`), then select HDMI 3:

```
setdevicestreamdebug:1 display01-com both
devjson:1 {"deviceKey":"display01","methodName":"InputHdmi3","params":[]}
```

The TX line should read `xb 01 A6`. If it reads `xb 01 92`, the loaded build doesn't include this option.

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
mocklg display01 input 90|91|92|93|c0    // a6 instead of 92 when um5kSeriesInputCodes is true
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

- 3.0.0-rc.20
<!-- END Minimum Essentials Framework Versions -->
<!-- START Config Example -->
### Config Example

```json
{
    "key": "display-1",
    "uid": 1,
    "name": "Display",
    "type": "lgDisplay",
    "group": "display",
    "properties": {
        "id": "01",
        "volumeUpperLimit": 100,
        "volumeLowerLimit": 0,
        "pollIntervalMs": 10000,
        "coolingTimeMs": 10000,
        "warmingTimeMs": 8000,
        "smallDisplay": false,
        "overrideWol": false,
        "um5kSeriesInputCodes": false,
        "friendlyNames": [
            {
                "inputKey": "90",
                "name": "HDMI 1",
                "hideInput": false
            },
            {
                "inputKey": "c0",
                "name": "DisplayPort",
                "hideInput": true
            }
        ],
        "control": {
            "method": "com",
            "controlPortDevKey": "processor",
            "controlPortNumber": 1,
            "comParams": {
                "protocol": "RS232",
                "baudRate": 9600,
                "dataBits": 8,
                "stopBits": 1,
                "parity": "None",
                "softwareHandshake": "None",
                "hardwareHandshake": "None",
                "pacing": 0
            }
        }
    }
}
```
<!-- END Config Example -->
<!-- START Supported Types -->
### Supported Types

- lgDisplay
- lgPlugin
- lg
- lgDisplayIr
- mockLgDisplay
- lgDisplayMock
<!-- END Supported Types -->
<!-- START Join Maps -->

<!-- END Join Maps -->
<!-- START Interfaces Implemented -->
### Interfaces Implemented

- ISelectableItems<string>
- IBasicVolumeWithFeedback
- ICommunicationMonitor
- IInputHdmi1
- IInputHdmi2
- IInputHdmi3
- IInputHdmi4
- IInputDisplayPort1
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

- public void Select()
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
- public string Respond(string line)
- public void SetPower(bool on)
- public bool SetInput(string inputCode)
- public void SetVolume(int level)
- public void SetMute(bool muted)
- public string Describe()
- public void Connect()
- public void Disconnect()
- public void SendText(string text)
- public void SendBytes(byte[] bytes)
- public void Select()
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
