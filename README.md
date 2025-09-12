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

### GracefulFPS
A collection of FPS related features for the game.

Displays in-game FPS and offers separate limit settings for global and battle scene.
You can set the FPS limit to Off (Default), Fixed, or VSync.

### SimpleTweaks
A collection of various simple quality-of-life improvements for the game.

This provides features that a hotkey to hide the lobby UI, remap the switching full-screen mode key, game volume control, remap the skip key for StoryViewer, prevents a forced aspect ratio on the game window and keeps the window position from resetting after resizing, and fix some bugs.

The default key for toggling fullscreen/windowed mode is `F11`.

### SimpleUI
This modifies parts of the UI to make them easier to use.

You can reduce the display size of the various combatant, equipment, and consumable lists. (This allows you to see more items on one screen.)
You can also sort the consumable list.
You can now press Enter to search in some search boxes.
You can accelerate scroll with mouse wheel.

### BattleHotkey
Assigns keyboard shortcuts for the `Skill 1`, `Skill 2`, `Move`, `Standby`, `Select Enemy`, `Select Enemy Tile`, `Confirm (after selecting an enemy or tile)`, and `Start Action` buttons in battle.

`Confirm (after selecting an enemy or tile)` works by pressing the key corresponding to the selected enemy or tile a second time.
The numbering for enemies and tiles corresponds to the numpad layout (Enemy `1` is located at the bottom left).


## Updates
If the plugin requires an update, the following message will be displayed when the game starts.

![Update Screen](doc/update.png)

Clicking the `[Github 페이지로 이동]` button will automatically take you to the Releases page of this repository.

## Disclaimer
This is an unofficial mod. By using this mod, you agree that you are solely responsible for any issues or damages that may arise. Always back up your important data before use. The developer assumes no liability for any loss or damage caused by the use of this mod.

## License
The `Symphony` project is under the `LGPL-2.1 license`.
