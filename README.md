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
This feature allows you to resize the game window after switching to windowed mode with `Alt+Enter`. It also remembers the adjusted window size and position.\
There are no settings for the user to configure.

If you need to reset the settings due to issues such as <ins>the window moving off-screen</ins>, you can resolve it by deleting the `Symphony.WindowedResize.cfg` file located in the `BepInEx/config` folder and restarting the game.

## Updates
If the plugin requires an update, the following message will be displayed when the game starts.

![Update Screen](doc/update.png)

Clicking the `[이동하기]` button will automatically take you to the Releases page of this repository.

## Disclaimer
This is an unofficial mod. By using this mod, you agree that you are solely responsible for any issues or damages that may arise. Always back up your important data before use. The developer assumes no liability for any loss or damage caused by the use of this mod.

## License
The `Symphony` project is under the `LGPL-2.1 license`.
