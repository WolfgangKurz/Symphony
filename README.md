[한국어](README.ko.md) | [English](README.md)

# Symphony
`Symphony` is a [BepInEx](https://github.com/BepInEx/BepInEx) plugin for the PC client of the game `LastOrigin` by `VALOFE`.
Its goal is to provide various quality-of-life (QoL) features for users.

## Installation
1. Download the **BepInEx-Unity.Mono-win-x64** version of [BepInEx](https://github.com/BepInEx/BepInEx) 6 from the [download page](https://github.com/BepInEx/BepInEx/releases/tag/v6.0.0-pre.2).
2. Unzip the downloaded file and paste its contents into the installation folder where `LastOrigin.exe` is located.\
At this point, the `winhttp.dll` file and the `LastOrigin.exe` file must be in the same folder.
3. Place `Symphony.dll` inside the `plugins` folder, which is within the `BepInEx` folder.

## How to Use
You can configure the plugin's options on the game screen by pressing the `F12` key.

The following is a description of each feature within the plugin.

### QuickSetting
Simple Control-Panel to control some in-game settings.

### GracefulFPS
A collection of FPS related features for the game.

- Displays in-game FPS and offers separate limit settings for global and battle scene.\
  You can set the FPS limit to `Off(Use vanilla)`, `Fixed`, or `VSync`.

### SimpleTweaks
A collection of various simple quality-of-life improvements for the game.

- Provides a feature to assign a hotkey for hiding the lobby UI.
- Provides features to prevent the game window's aspect ratio from being locked, prevent the window position from resetting after resizing, and allow reassigning the fullscreen toggle key.
- Provides a feature to remember Offline Battle's last option.
- Provides features to change the mute behavior in the background and to control the volume.
- Provides a feature to reassign the story viewer skip key.
- Provides a fast logo screen, immediate login-able, and an automatic login feature.
- Provides feature to select all character or equip in disassemble screen.
- Prevents BGM from resetting on audio device changes, ensuring continuous playback.

The default key for toggling fullscreen/windowed mode is `F11`.

### SimpleUI
This modifies parts of the UI to make them easier to use.

- Provides a feature that, when an Auto-Battle is in progress, tapping the Battle map will bring up the Map screen instead of the Auto-Battle screen.
- Provides the ability to reduce the display size of combatant, equipment, and consumable lists. (Allows more items to be seen on one screen.)
- Provides a sorting function for the consumable list.
- Provides instant search with the Enter key in some search input fields.
- Provides accelerated scrolling via the mouse wheel.
- Provides the Set Character's resource cost display default to Off feature.
- Provides clearing all member in squad feature.
- Provides Scrapbook improvement feature(`Scrapbook Must Be Fancy`).
- Provides Enemy Preview for Battle Information.
- Provides improvements for Exchange screen.

### BattleHotkey
Assigns keyboard shortcuts for the `Skill 1`, `Skill 2`, `Move`, `Standby`, `Select Enemy`, `Select Enemy Tile`, `Confirm (after selecting an enemy or tile)`, and `Start Action` buttons in battle.

`Confirm (after selecting an enemy or tile)` works by pressing the key corresponding to the selected enemy or tile a second time.
The numbering for enemies and tiles corresponds to the numpad layout (Enemy `1` is located at the bottom left).

### LastBattle
A button will be added to the World Menu to quickly move to the last visited Battle Map.

### Notification
This handles in-game push-notification as Windows Notification.

### Presets
A collection of various preset functions.

- Adds a resource preset for the amount of resources used in making a Character.
- Adds a feature that automatically loads the amount of resources last used for making.

### Automation
This is an <span style="color:crimson">__Automation(macro)__</span> feature.\
Please be aware that its use may lead to restrictions by the game operator.\
Please use with caution.

- Provides a Get-All and Restart-All function for completed Facilities in the Base.
- Provides a Restart function for completed Auto-Battles. (The Disassembling settings will use the settings from the last Auto-Battle run on PC.)

### Experimental
A collection of experimental features.

- Key mapping
- Fix a Freezing issue in certain situations during Battle

## Updates
If the plugin requires an update, the following message will be displayed when the game starts.

![Update Screen](doc/update.png)

Clicking the `[설치]`(Install) button will download and install the latest version plugin automatically, and restart the game after complete install.

Clicking the `[Github]` button will automatically take you to the Releases page of this repository.

## Disclaimer
This is an unofficial mod. By using this mod, you agree that you are solely responsible for any issues or damages that may arise. Always back up your important data before use. The developer assumes no liability for any loss or damage caused by the use of this mod.

## License
The `Symphony` project is under the `LGPL-2.1 license`.

### Exceptions
The following exceptions apply.

`VALOFE`, the developer/operator of the game `LastOrigin` (including its subsidiaries and affiliates), is not required to fulfill all or part of the obligations normally required under `LGPL-2.1`—such as `source code disclosure`, `permitting relinking or linking to alternative versions`, and `allowing reverse engineering`—when using, modifying, or distributing this library and source code **within the scope directly related to the development and operation of `LastOrigin`**.

This additional permission is **limited to VALOFE** and may not be transferred or assigned to any third party.

This additional permission applies **only to code owned by the copyright holder (project owner)** and does not affect components or external dependencies that include third-party copyrights.

These exceptions do not restrict the rights of other users; all third parties other than VALOFE remain subject to the base license (`LGPL-2.1`).
