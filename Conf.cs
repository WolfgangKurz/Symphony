	using BepInEx;
using BepInEx.Configuration;

using System;
using System.Collections.Generic;
using System.IO;

namespace Symphony {
	internal class Conf {
		public static ConfigFile config = new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.cfg"), true);

		internal class GracefulFPS {
			public static readonly ConfigEntry<bool> DisplayFPS = config.Bind("GracefulFPS", "DisplayFPS", true, "Display FPS to screen");

			/// <summary>
			/// `Off`, `Fixed`, `VSync`
			/// </summary>
			public static readonly ConfigEntry<string> LimitFPS = config.Bind("GracefulFPS", "LimitFPS", "Off", "Limits game framerate. Uses MaxFPS value when Fixed");
			/// <summary>
			/// `Off`, `Fixed`, `VSync`
			/// </summary>
			public static readonly ConfigEntry<string> LimitBattleFPS = config.Bind("GracefulFPS", "LimitBattleFPS", "Off", "Limits battle framerate. Uses MaxBattleFPS value when Fixed");

			public static readonly ConfigEntry<int> MaxFPS = config.Bind("GracefulFPS", "MaxFPS", 60, "Framerate");
			public static readonly ConfigEntry<int> MaxBattleFPS = config.Bind("GracefulFPS", "MaxBattleFPS", 60, "Framerate");
		}
		internal class SimpleTweaks {
			public static readonly ConfigEntry<bool> UseLobbyHide = config.Bind("SimpleTweaks", "UseLobbyHide", true, $"Use hotkey to toggle lobby UI");
			public static readonly ConfigEntry<string> LobbyUIHideKey = config.Bind("SimpleTweaks", "LobbyHideKey", "Tab", $"Key to toggle lobby UI");

			public static readonly ConfigEntry<bool> Use_IgnoreWindowReset = config.Bind("SimpleTweaks", "Ignore_WindowReset", true, "Ignore window size aspect-ratio and position reset after resize");

			public static readonly ConfigEntry<bool> Use_FullScreenKey = config.Bind("SimpleTweaks", "Use_FullScreenKey", true, "Use FullScreen mode key change");
			public static readonly ConfigEntry<string> FullScreenKey = config.Bind("SimpleTweaks", "FullScreenKey", "F11", "Window mode change button replacement");

			public static readonly ConfigEntry<bool> MuteOnBackgroundFix = config.Bind("SimpleTweaks", "MuteOnBackgroundFix", false, $"Fix MuteOnBackground feature to prevent stop music playing even in background");

			public static readonly ConfigEntry<bool> Use_OfflineBattle_Memorize = config.Bind("SimpleTweaks", "Use_OfflineBattle_Memorize", false, "Remember last OfflineBattle options");
			public static readonly ConfigEntry<byte> OfflineBattle_Last_CharDiscomp = config.Bind("SimpleTweaks", "OfflineBattle_Last_CharDiscomp", (byte)3);
			public static readonly ConfigEntry<byte> OfflineBattle_Last_EquipDiscomp = config.Bind("SimpleTweaks", "OfflineBattle_Last_EquipDiscomp", (byte)3);
			public static readonly ConfigEntry<int> OfflineBattle_Last_Time = config.Bind("SimpleTweaks", "OfflineBattle_Last_Time", 1);

			public static readonly ConfigEntry<bool> UsePatchStorySkip = config.Bind("SimpleTweaks", "UsePatchStorySkip", true, $"Prevent StoryViewer from proceeding automatically when the Space key is held down, and remap the key to PatchStorySkipKey");
			public static readonly ConfigEntry<string> PatchStorySkipKey = config.Bind("SimpleTweaks", "PatchStorySkipKey", "LeftControl", $"Key to remap for StoryViewer");

			public static readonly ConfigEntry<bool> Use_QuickLogo = config.Bind("SimpleTweaks", "Use_SkipLogo", false, $"Make Logo screen passes quickly");
			public static readonly ConfigEntry<bool> Use_QuickTitle = config.Bind("SimpleTweaks", "Use_QuickTitle", false, $"Make Title screen touchable quickly");
			public static readonly ConfigEntry<bool> Use_AutoLogin = config.Bind("SimpleTweaks", "Use_AutoLogin", false, $"Do login automatically");
		}
		internal class SimpleUI {
			public static readonly ConfigEntry<bool> Use_OfflineBattle_Bypass = config.Bind("SimpleUI", "Use_OfflineBattle_Bypass", false, "Enter Maps screen instead open Offline battle screen");

			public static readonly ConfigEntry<bool> Small_CharWarehouse = config.Bind("SimpleUI", "Small_CharWarehouse", false, "Display more items for Character Warehouse");
			public static readonly ConfigEntry<bool> Small_CharSelection = config.Bind("SimpleUI", "Small_CharSelection", false, "Display more items for Character Selection");
			public static readonly ConfigEntry<bool> Small_CharScrapbook = config.Bind("SimpleUI", "Small_CharScrapbook", false, "Display more items for Character Scrapbook");
			public static readonly ConfigEntry<bool> Small_ItemWarehouse = config.Bind("SimpleUI", "Small_ItemWarehouse", false, "Display more items for Item Warehouse");
			public static readonly ConfigEntry<bool> Small_ItemSelection = config.Bind("SimpleUI", "Small_ItemSelection", false, "Display more items for Item Selection");
			public static readonly ConfigEntry<bool> Small_TempInventory = config.Bind("SimpleUI", "Small_TempInventory", false, "Display more items for Temporary Inventory");

			public static readonly ConfigEntry<bool> Small_Consumables = config.Bind("SimpleUI", "Small_Consumables", false, "Display more items for Consumables");
			public static readonly ConfigEntry<bool> Sort_Consumables = config.Bind("SimpleUI", "Sort_Consumables", false, "Sort consumable items");

			public static readonly ConfigEntry<bool> EnterToSearch_CharWarehouse = config.Bind("SimpleUI", "EnterToSearch_CharWarehouse", false, "Press enter to search for Character Warehouse");
			public static readonly ConfigEntry<bool> EnterToSearch_CharSelection = config.Bind("SimpleUI", "EnterToSearch_CharSelection", false, "Press enter to search for Character Selection");
			public static readonly ConfigEntry<bool> EnterToSearch_ItemWarehouse = config.Bind("SimpleUI", "EnterToSearch_ItemWarehouse", false, "Press enter to search for Item Warehouse");
			public static readonly ConfigEntry<bool> EnterToSearch_ItemSelection = config.Bind("SimpleUI", "EnterToSearch_ItemSelection", false, "Press enter to search for Item Selection");

			public static readonly ConfigEntry<bool> Use_AccelerateScrollDelta = config.Bind("SimpleUI", "Use_MultiplyScrollDelta", false, "Multiply scroll amount for scrollable list");

			public static readonly ConfigEntry<bool> Use_SortByName = config.Bind("SimpleUI", "Use_SortByName", false, "Add sorting filter to Character list");
			public static readonly ConfigEntry<bool> Default_CharacterCost_Off = config.Bind("SimpleUI", "Default_CharacterCost_Off", false, "Set Character's resource cost display default to Off");

			public static readonly ConfigEntry<bool> Use_Squad_Clear = config.Bind("SimpleUI", "Use_Squad_Clear", false, "Add clear button to squad screen");

			public static readonly ConfigEntry<bool> Use_Disassemble_SelectAll_Character = config.Bind("SimpleUI", "Use_Disassemble_SelectAll_Character", false, "Add Select All button to Disassemble Character screen");
			public static readonly ConfigEntry<bool> Use_Disassemble_SelectAll_Equip = config.Bind("SimpleUI", "Use_Disassemble_SelectAll_Equip", false, "Add Select All button to Disassemble Equip screen");

			public static readonly ConfigEntry<bool> Use_ScrapbookMustBeFancy = config.Bind("SimpleUI", "Use_ScrapbookMustBeFancy", true, "Beautify Scrapbook to usefully");

			public static readonly ConfigEntry<bool> Use_CharacterMakingPreview = config.Bind("SimpleUI", "Use_CharacterMakingPreview", false, "Displays available result for Character making");
			public static readonly ConfigEntry<bool> Use_EquipMakingPreview = config.Bind("SimpleUI", "Use_EquipMakingPreview", false, "Displays available result for Equip making");
		}
		internal class BattleHotkey {
			public static readonly ConfigEntry<bool> Use_SkillPanel = config.Bind("BattleHotkey", "Use_SkillPanel", true, "Use skill panel hotkeys");
			public static readonly ConfigEntry<string>[] Key_SkillPanel = [
				config.Bind("BattleHotkey", "Skill1", "Alpha1", "Skill1 button hotkey"),
				config.Bind("BattleHotkey", "Skill2", "Alpha2", "Skill2 button hotkey"),
				config.Bind("BattleHotkey", "Move", "Alpha3", "Move button hotkey"),
				config.Bind("BattleHotkey", "Wait", "Alpha4", "Wait button hotkey"),
			];

			public static readonly ConfigEntry<bool> Use_TeamGrid = config.Bind("BattleHotkey", "Use_TeamGrid", true, "Use team grid hotkeys");
			public static readonly ConfigEntry<string>[] Key_TeamGrid = [
				config.Bind("BattleHotkey", "Team1", "Z", "Team grid 1 button hotkey"),
				config.Bind("BattleHotkey", "Team2", "X", "Team grid 2 button hotkey"),
				config.Bind("BattleHotkey", "Team3", "C", "Team grid 3 button hotkey"),
				config.Bind("BattleHotkey", "Team4", "A", "Team grid 4 button hotkey"),
				config.Bind("BattleHotkey", "Team5", "S", "Team grid 5 button hotkey"),
				config.Bind("BattleHotkey", "Team6", "D", "Team grid 6 button hotkey"),
				config.Bind("BattleHotkey", "Team7", "Q", "Team grid 7 button hotkey"),
				config.Bind("BattleHotkey", "Team8", "W", "Team grid 8 button hotkey"),
				config.Bind("BattleHotkey", "Team9", "E", "Team grid 9 button hotkey"),
			];
			public static readonly ConfigEntry<bool> Use_EnemyGrid = config.Bind("BattleHotkey", "Use_EnemyGrid", true, "Use enemy grid hotkeys");
			public static readonly ConfigEntry<string>[] Key_EnemyGrid = [
				config.Bind("BattleHotkey", "Enemy1", "Keypad1", "Enemy grid 1 button hotkey"),
				config.Bind("BattleHotkey", "Enemy2", "Keypad2", "Enemy grid 2 button hotkey"),
				config.Bind("BattleHotkey", "Enemy3", "Keypad3", "Enemy grid 3 button hotkey"),
				config.Bind("BattleHotkey", "Enemy4", "Keypad4", "Enemy grid 4 button hotkey"),
				config.Bind("BattleHotkey", "Enemy5", "Keypad5", "Enemy grid 5 button hotkey"),
				config.Bind("BattleHotkey", "Enemy6", "Keypad6", "Enemy grid 6 button hotkey"),
				config.Bind("BattleHotkey", "Enemy7", "Keypad7", "Enemy grid 7 button hotkey"),
				config.Bind("BattleHotkey", "Enemy8", "Keypad8", "Enemy grid 8 button hotkey"),
				config.Bind("BattleHotkey", "Enemy9", "Keypad9", "Enemy grid 9 button hotkey"),
			];
			public static readonly ConfigEntry<bool> Use_PlayButton = config.Bind("BattleHotkey", "Use_PlayButton", true, "Use play button hotkeys");
			public static readonly ConfigEntry<string> Key_Play = config.Bind("BattleHotkey", "Play", "KeypadPlus", "Play button hotkey");
		}
		internal class LastBattle {
			public static readonly ConfigEntry<bool> Use_LastBattleMap = config.Bind("LastBattle", "Use_LastBattleMap", false, "Whether to use the function that adds a button to the World screen that moves you directly to the last visited battle map.");
			public static readonly ConfigEntry<string> LastBattleMapKey = config.Bind("LastBattle", "LastBattleMapKey", "");
		}
		internal class Notification {
			public static readonly ConfigEntry<bool> Handle_Notification = config.Bind("LastBattle", "Handle_Notification", true, "Handle in-game push notification as windows notification.");
		}
		internal class Presets {
			public static readonly ConfigEntry<bool> Use_CharMaking_Preset= config.Bind("Presets", "Use_CharMakingPreset", false, "Use Preset for Character making screen");
			public static readonly ConfigEntry<bool> Use_Last_CharMakingData = config.Bind("Presets", "Use_Last_CharMakingData", false, "Load last character making data automatically");

			public static readonly ConfigEntry<string> Last_CharMaking_Data = config.Bind("Presets", "Last_CharMaking_Data", "0,0,0,0,0,0");
			public static readonly ConfigEntry<string> CharMaking_Preset_Data = config.Bind("Presets", "CharMaking_Preset_Data", "");
		}
		internal class Automation {
			public static readonly ConfigEntry<bool> Use_Base_GetAll = config.Bind("Automation", "Use_Base_GetAll", false, "Use Get All button for Base");

			public static readonly ConfigEntry<bool> Use_OfflineBattle_Restart = config.Bind("Automation", "Use_OfflineBattle_Restart", false, "Add Restart button to offline battle result screen");
			public static readonly ConfigEntry<byte> OfflineBattle_Last_CharDiscomp = config.Bind("Automation", "OfflineBattle_Last_CharDiscomp", (byte)1);
			public static readonly ConfigEntry<byte> OfflineBattle_Last_EquipDiscomp = config.Bind("Automation", "OfflineBattle_Last_EquipDiscomp", (byte)1);
		}

		public static void Migrate() {
			#region Migration Old Configs
			{ // from MaximumFrame
				var path = Path.Combine(Paths.ConfigPath, "Symphony.MaximumFrame.cfg");
				if (File.Exists(path)) {
					Plugin.Logger.LogMessage("[Symphony] MaximumFrame configuration detected, migration it.");
					var _old = new ConfigFile(path, false);
					var frame = _old.Bind("MaximumFrame", "maximumFrame", -1).Value;

					GracefulFPS.LimitFPS.Value = frame > 0 ? "Fixed" : "Off";
					GracefulFPS.MaxFPS.Value = Math.Max(frame, 1);

					File.Delete(path);
					config.Save();
				}
			}
			{ // from LobbyHide
				var path = Path.Combine(Paths.ConfigPath, "Symphony.LobbyHide.cfg");
				if (File.Exists(path)) {
					Plugin.Logger.LogMessage("[Symphony] LobbyHide configuration detected, migration it.");
					var _old = new ConfigFile(path, false);
					var keyCodeName = _old.Bind("LobbyHide", "Toggle", "Tab").Value;

					if (keyCodeName != "" && Helper.KeyCodeParse(keyCodeName, out var _)) {
						SimpleTweaks.UseLobbyHide.Value = true;
						SimpleTweaks.LobbyUIHideKey.Value = keyCodeName;
					}
					else
						SimpleTweaks.UseLobbyHide.Value = false;

					File.Delete(path);
					config.Save();
				}
			}
			{ // from WindowedResize
				var path = Path.Combine(Paths.ConfigPath, "Symphony.WindowedResize.cfg");
				if (File.Exists(path)) {
					Plugin.Logger.LogMessage("[Symphony] WindowedResize configuration detected, migration it.");
					var _old = new ConfigFile(path, false);

					var useFullScreenKey = _old.Bind("WindowedResize", "Use_FullScreenKey", true).Value;
					SimpleTweaks.Use_FullScreenKey.Value = useFullScreenKey;

					var keyCodeName = _old.Bind("WindowedResize", "Key_Mode", "F11").Value;
					if (keyCodeName != "" && Helper.KeyCodeParse(keyCodeName, out var _)) {
						SimpleTweaks.FullScreenKey.Value = keyCodeName;
					}

					File.Delete(path);
					config.Save();
				}
			}
			#endregion

			#region Migration Pre-ConfigManager Configs
			{ // SimpleTweaks -> ConfigManager.GracefulFPS
				ConfigEntry<bool> entry_bool;
				ConfigEntry<int> entry_int;

				if (config.TryGetEntry("SimpleTweaks", "DisplayFPS", out entry_bool)) {
					GracefulFPS.DisplayFPS.Value = entry_bool.Value;
					config.Remove(new ConfigDefinition("SimpleTweaks", "DisplayFPS"), out _);
				}

				if (config.TryGetEntry("SimpleTweaks", "LimitFPS", out entry_bool)) {
					GracefulFPS.LimitFPS.Value = entry_bool.Value ? "Fixed" : "Off";
					config.Remove(new ConfigDefinition("SimpleTweaks", "LimitFPS"), out _);
				}
				if (config.TryGetEntry("SimpleTweaks", "LimitBattleFPS", out entry_bool)) {
					GracefulFPS.LimitBattleFPS.Value = entry_bool.Value ? "Fixed" : "Off";
					config.Remove(new ConfigDefinition("SimpleTweaks", "LimitBattleFPS"), out _);
				}

				if (config.TryGetEntry("SimpleTweaks", "MaxFPS", out entry_int)) {
					GracefulFPS.MaxFPS.Value = entry_int.Value;
					config.Remove(new ConfigDefinition("SimpleTweaks", "MaxFPS"), out _);
				}
				if (config.TryGetEntry("SimpleTweaks", "MaxBattleFPS", out entry_int)) {
					GracefulFPS.MaxBattleFPS.Value = entry_int.Value;
					config.Remove(new ConfigDefinition("SimpleTweaks", "MaxBattleFPS"), out _);
				}
			}
			{ // SimpleTweaks -> ConfigManager.SimpleTweaks
				var path = Path.Combine(Paths.ConfigPath, "Symphony.SimpleTweaks.cfg");
				if (File.Exists(path)) {
					Plugin.Logger.LogMessage("[Symphony] SimpleTweaks old configuration detected, migration it.");
					var prev = new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.SimpleTweaks.cfg"), true);

					SimpleTweaks.UseLobbyHide.Value = prev.Bind("SimpleTweaks", "UseLobbyHide", true).Value;
					SimpleTweaks.LobbyUIHideKey.Value = prev.Bind("SimpleTweaks", "LobbyUIHideKey", "Tab").Value;

					SimpleTweaks.Use_IgnoreWindowReset.Value = prev.Bind("SimpleTweaks", "Use_IgnoreWindowReset", true).Value;

					SimpleTweaks.Use_FullScreenKey.Value = prev.Bind("SimpleTweaks", "Use_FullScreenKey", true).Value;
					SimpleTweaks.FullScreenKey.Value = prev.Bind("SimpleTweaks", "FullScreenKey", "F11").Value;

					SimpleTweaks.MuteOnBackgroundFix.Value = prev.Bind("SimpleTweaks", "MuteOnBackground", false).Value;

					SimpleTweaks.UsePatchStorySkip.Value = prev.Bind("SimpleTweaks", "UsePatchStorySkip", true).Value;
					SimpleTweaks.PatchStorySkipKey.Value = prev.Bind("SimpleTweaks", "PatchStorySpacebar", "LeftControl").Value;

					File.Delete(path);
				}
			}
			{ // SimpleTweaks -> ConfigManager.SimpleUI
				var path = Path.Combine(Paths.ConfigPath, "Symphony.SimpleUI.cfg");
				if (File.Exists(path)) {
					Plugin.Logger.LogMessage("[Symphony] SimpleUI old configuration detected, migration it.");
					var prev = new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.SimpleUI.cfg"), true);

					SimpleUI.Small_CharWarehouse.Value = prev.Bind("SimpleUI", "Small_CharWarehouse", false).Value;
					SimpleUI.Small_CharSelection.Value = prev.Bind("SimpleUI", "Small_CharSelection", false).Value;
					SimpleUI.Small_CharScrapbook.Value = prev.Bind("SimpleUI", "Small_CharScrapbook", false).Value;
					SimpleUI.Small_ItemWarehouse.Value = prev.Bind("SimpleUI", "Small_ItemWarehouse", false).Value;
					SimpleUI.Small_ItemSelection.Value = prev.Bind("SimpleUI", "Small_ItemSelection", false).Value;
					SimpleUI.Small_TempInventory.Value = prev.Bind("SimpleUI", "Small_TempInventory", false).Value;

					SimpleUI.Small_Consumables.Value = prev.Bind("SimpleUI", "Small_Consumables", false).Value;
					SimpleUI.Sort_Consumables.Value = prev.Bind("SimpleUI", "Sort_Consumables", false).Value;

					SimpleUI.EnterToSearch_CharWarehouse.Value = prev.Bind("SimpleUI", "EnterToSearch_CharWarehouse", false).Value;
					SimpleUI.EnterToSearch_CharSelection.Value = prev.Bind("SimpleUI", "EnterToSearch_CharSelection", false).Value;
					SimpleUI.EnterToSearch_ItemWarehouse.Value = prev.Bind("SimpleUI", "EnterToSearch_ItemWarehouse", false).Value;
					SimpleUI.EnterToSearch_ItemSelection.Value = prev.Bind("SimpleUI", "EnterToSearch_ItemSelection", false).Value;

					File.Delete(path);
				}
			}
			{ // SimpleTweaks -> ConfigManager.BattleHotkey
				var path = Path.Combine(Paths.ConfigPath, "Symphony.BattleHotKey.cfg");
				if (File.Exists(path)) {
					Plugin.Logger.LogMessage("[Symphony] BattleHotKey old configuration detected, migration it.");
					var prev = new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.BattleHotKey.cfg"), true);

					BattleHotkey.Use_SkillPanel.Value = prev.Bind("BattleHotKey", "Use_SkillPanel", true).Value;
					BattleHotkey.Key_SkillPanel[0].Value = prev.Bind("BattleHotKey", "Skill1", "Alpha1").Value;
					BattleHotkey.Key_SkillPanel[1].Value = prev.Bind("BattleHotKey", "Skill2", "Alpha2").Value;
					BattleHotkey.Key_SkillPanel[2].Value = prev.Bind("BattleHotKey", "Move", "Alpha3").Value;
					BattleHotkey.Key_SkillPanel[3].Value = prev.Bind("BattleHotKey", "Wait", "Alpha4").Value;

					BattleHotkey.Use_TeamGrid.Value = prev.Bind("BattleHotKey", "Use_TeamGrid", true).Value;
					BattleHotkey.Key_TeamGrid[0].Value = prev.Bind("BattleHotKey", "Team1", "Z").Value;
					BattleHotkey.Key_TeamGrid[1].Value = prev.Bind("BattleHotKey", "Team2", "X").Value;
					BattleHotkey.Key_TeamGrid[2].Value = prev.Bind("BattleHotKey", "Team3", "C").Value;
					BattleHotkey.Key_TeamGrid[3].Value = prev.Bind("BattleHotKey", "Team4", "A").Value;
					BattleHotkey.Key_TeamGrid[4].Value = prev.Bind("BattleHotKey", "Team5", "S").Value;
					BattleHotkey.Key_TeamGrid[5].Value = prev.Bind("BattleHotKey", "Team6", "D").Value;
					BattleHotkey.Key_TeamGrid[6].Value = prev.Bind("BattleHotKey", "Team7", "Q").Value;
					BattleHotkey.Key_TeamGrid[7].Value = prev.Bind("BattleHotKey", "Team8", "W").Value;
					BattleHotkey.Key_TeamGrid[8].Value = prev.Bind("BattleHotKey", "Team9", "E").Value;

					BattleHotkey.Use_EnemyGrid.Value = prev.Bind("BattleHotKey", "Use_EnemyGrid", true).Value;
					BattleHotkey.Key_EnemyGrid[0].Value = prev.Bind("BattleHotKey", "Enemy1", "Keypad1").Value;
					BattleHotkey.Key_EnemyGrid[1].Value = prev.Bind("BattleHotKey", "Enemy2", "Keypad2").Value;
					BattleHotkey.Key_EnemyGrid[2].Value = prev.Bind("BattleHotKey", "Enemy3", "Keypad3").Value;
					BattleHotkey.Key_EnemyGrid[3].Value = prev.Bind("BattleHotKey", "Enemy4", "Keypad4").Value;
					BattleHotkey.Key_EnemyGrid[4].Value = prev.Bind("BattleHotKey", "Enemy5", "Keypad5").Value;
					BattleHotkey.Key_EnemyGrid[5].Value = prev.Bind("BattleHotKey", "Enemy6", "Keypad6").Value;
					BattleHotkey.Key_EnemyGrid[6].Value = prev.Bind("BattleHotKey", "Enemy7", "Keypad7").Value;
					BattleHotkey.Key_EnemyGrid[7].Value = prev.Bind("BattleHotKey", "Enemy8", "Keypad8").Value;
					BattleHotkey.Key_EnemyGrid[8].Value = prev.Bind("BattleHotKey", "Enemy9", "Keypad9").Value;

					BattleHotkey.Use_PlayButton.Value = prev.Bind("BattleHotKey", "Use_PlayButton", true).Value;
					BattleHotkey.Key_Play.Value = prev.Bind("BattleHotKey", "Play", "KeypadPlus").Value;

					File.Delete(path);
				}
			}
			#endregion

			#region Migration renamed Configs
			{ // HelpfulBase -> Automation
				ConfigEntry<bool> entry_bool;

				if (config.TryGetEntry("HelpfulBase", "Use_GetAll", out entry_bool)){
					Automation.Use_Base_GetAll.Value = entry_bool.Value;
					config.Remove(new ConfigDefinition("Automation", "Use_Base_GetAll"), out _);
				}
			}
			#endregion
		}
	}
}
