# PepperDash Essentials LG Display Plugin (c) 2025

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
    "coolingTime": 10000,
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
    }
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

- 1.8.0
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
        "smallDisplay": true,
        "overrideWol": true,
        "friendlyNames": [
            {
                "inputKey": "SampleString",
                "name": "SampleString",
                "hideInput": true
            }
        ]
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

- IBasicVolumeWithFeedback
- ICommunicationMonitor
- IInputHdmi1
- IInputHdmi2
- IInputHdmi3
- IInputDisplayPort1
- IBridgeAdvanced
- IHasInputs<string>
- IBasicVideoMuteWithFeedback
- IWarmingCooling
- ISelectableItems<string>
<!-- END Interfaces Implemented -->
<!-- START Base Classes -->
### Base Classes

- TwoWayDisplayBase
- DisplayControllerJoinMap
<!-- END Base Classes -->
<!-- START Public Methods -->
### Public Methods

- public void SetVolume(ushort level)
- public void MuteOn()
- public void MuteOff()
- public void MuteToggle()
- public void VolumeDown(bool pressRelease)
- public void VolumeUp(bool pressRelease)
- public void VideoMuteOn()
- public void VideoMuteOff()
- public void VideoMuteToggle()
- public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
- public void SendData(string s)
- public void ListRoutingInputPorts()
- public void MuteGet()
- public void VolumeGet()
- public void InputHdmi1()
- public void InputHdmi2()
- public void InputHdmi3()
- public void InputDisplayPort1()
- public void InputGet()
- public void PowerGet()
- public void UpdateInputFb(string s)
- public void UpdatePowerFb(string s)
- public void UpdateVideoMuteFb(string s)
- public void UpdateVolumeFb(string s)
- public void UpdateMuteFb(string s)
- public void StatusGet()
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
