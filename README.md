# Symphony
`Symphony` is a [BepInEx](https://github.com/BepInEx/BepInEx) plugin for the PC client of the game `LastOrigin` by `VALOFE`.
Its goal is to provide various quality-of-life (QoL) features for users.

## Installation
1. Download the **BepInEx-Unity.Mono-win-x64** version of [BepInEx](https://github.com/BepInEx/BepInEx) 6 from the [download page](https://github.com/BepInEx/BepInEx/releases/tag/v6.0.0-pre.2).
2. Unzip the downloaded file and paste its contents into the installation folder where `LastOrigin.exe` is located.\
At this point, the `winhttp.dll` file and the `LastOrigin.exe` file must be in the same folder.
3. Place `Symphony.dll` inside the `plugins` folder, which is within the `BepInEx` folder.

## Usage
This section explains each feature within the plugin.

### WindowedResize
This feature allows you to switch to windowed mode with a set key, after which you can resize and maximize the window. It also remembers the adjusted position and size.\
To specify the key for toggling between fullscreen and windowed mode, open the `Symphony.WindowedResize.cfg` file in the `BepInEx/config` folder with an editor and modify the `Key_Mode` value.\
If you leave the key field blank, no hotkey will be assigned. (The game's default `Enter` and `Keypad Enter` will be used.)

The default key for toggling fullscreen/windowed mode is `F11`.

If you encounter a problem, <ins>such as the window moving off-screen</ins>, and need to reset the settings, you can resolve it by deleting all values in the config file except for `Key_Mode` and then restarting.

### MaximumFrame
Limits the game's maximum framerate.\
To configure, open the `Symphony.MaximumFrame.cfg` file in the `BepInEx/config` folder with an editor and modify the `maximumFrame = -1` value.

A value of `-1` means the game will use its original framerate. If you set it to `30`, the game's screen refresh rate will be limited to `30` frames.\
A game restart is not required for the changes to take effect (the configuration is re-read every 5 seconds).

### BattleHotkey
Assigns keyboard shortcuts for the `Skill 1`, `Skill 2`, `Move`, `Wait`, `Select Enemy`, `Select Enemy Tile`, `Confirm (after selecting an enemy or tile)`, and `Start Action` buttons in combat.\
To configure, open the `Symphony.BattleHotkey.cfg` file in the `BepInEx/config` folder with an editor and change each entry to your desired key.\
Leaving a key assignment blank will disable the shortcut for that action. (Deleting a configuration line will cause it to revert to the default value).

The default values are as follows:
| Function | Key |
|--------------------|------------|
| Skill 1 | Number 1 |
| Skill 2 | Number 2 |
| Move | Number 3 |
| Wait | Number 4 |
| Select Enemy/Tile | Numpad 1-9 |
| Start Action | Numpad + |

`Confirm (after selecting an enemy or tile)` is triggered by pressing the key corresponding to the selected enemy or tile a second time.
The numbering for enemies and tiles corresponds to the layout of a numpad (`Enemy 1` is located at the bottom left).

A game restart is not required for the changes to take effect (the configuration is re-read every time you enter the battle screen).

### LobbyHide
Assigns a keyboard shortcut to hide the UI on the lobby screen, a feature that was originally triggered by clicking an empty area.
To configure, open the `Symphony.LobbyHide.cfg` file in the `BepInEx/config` folder with an editor and change it to your desired key.
Leaving the key assignment blank will disable the shortcut. (Deleting the configuration line will cause it to revert to the default value).

The default shortcut is `Tab`.

A game restart is not required for the changes to take effect (the configuration is re-read every time you enter the lobby screen).


## Updates
If the plugin requires an update, the following message will be displayed when the game starts.

![Update Screen](doc/update.png)

Clicking the `[이동하기]` button will automatically take you to the Releases page of this repository.

## Disclaimer
This is an unofficial mod. By using this mod, you agree that you are solely responsible for any issues or damages that may arise. Always back up your important data before use. The developer assumes no liability for any loss or damage caused by the use of this mod.

## License
The `Symphony` project is under the `LGPL-2.1 license`.
