using BepInEx;
using BepInEx.Configuration;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Symphony {
	internal class Migration {
		public static void Migrate() {
			Migration.MigrateFiles();
			Migration.MigrateConf();
		}

		private static void MigrateFiles() {
			{ // BetterTextureFilter caches (feature removed)
				var path = Path.Combine(Plugin.GameDir, "BetterTextureFilter");
				if (Directory.Exists(path)) {
					Plugin.Logger.LogMessage("[Symphony] BetterTextureFilter cache detected, remove it.");
					try {
						Directory.Delete(path, true);
					} catch (Exception e) {
						Plugin.Logger.LogWarning($"[Symphony] Failed to remove BetterTextureFilter cache: {e.Message}");
					}
				}
			}
		}

		private static void MigrateConf() {
			var config = Conf.config;

			#region Migration Old Configs
			{ // from MaximumFrame
				var path = Path.Combine(Paths.ConfigPath, "Symphony.MaximumFrame.cfg");
				if (File.Exists(path)) {
					Plugin.Logger.LogMessage("[Symphony] MaximumFrame configuration detected, migrate it.");
					var _old = new ConfigFile(path, false);
					var frame = _old.Bind("MaximumFrame", "maximumFrame", -1).Value;

					Conf.GracefulFPS.LimitFPS.Value = frame > 0 ? "Fixed" : "Off";
					Conf.GracefulFPS.MaxFPS.Value = Math.Max(frame, 1);

					File.Delete(path);
					config.Save();
				}
			}
			{ // from LobbyHide
				var path = Path.Combine(Paths.ConfigPath, "Symphony.LobbyHide.cfg");
				if (File.Exists(path)) {
					Plugin.Logger.LogMessage("[Symphony] LobbyHide configuration detected, migrate it.");
					var _old = new ConfigFile(path, false);
					var keyCodeName = _old.Bind("LobbyHide", "Toggle", "Tab").Value;

					if (keyCodeName != "" && Helper.KeyCodeParse(keyCodeName, out var _)) {
						Conf.SimpleTweaks.UseLobbyHide.Value = true;
						Conf.SimpleTweaks.LobbyUIHideKey.Value = keyCodeName;
					}
					else
						Conf.SimpleTweaks.UseLobbyHide.Value = false;

					File.Delete(path);
					config.Save();
				}
			}
			{ // from WindowedResize
				var path = Path.Combine(Paths.ConfigPath, "Symphony.WindowedResize.cfg");
				if (File.Exists(path)) {
					Plugin.Logger.LogMessage("[Symphony] WindowedResize configuration detected, migrate it.");
					var _old = new ConfigFile(path, false);

					var useFullScreenKey = _old.Bind("WindowedResize", "Use_FullScreenKey", true).Value;
					Conf.SimpleTweaks.Use_FullScreenKey.Value = useFullScreenKey;

					var keyCodeName = _old.Bind("WindowedResize", "Key_Mode", "F11").Value;
					if (keyCodeName != "" && Helper.KeyCodeParse(keyCodeName, out var _)) {
						Conf.SimpleTweaks.FullScreenKey.Value = keyCodeName;
					}

					File.Delete(path);
					config.Save();
				}
			}
			#endregion

			#region Migration Pre-ConfigManager Configs
			{ // SimpleTweaks -> ConfigManager.GracefulFPS
				bool value_bool;
				int value_int;

				if (config.TryGetOrphanedEntry("SimpleTweaks", "DisplayFPS", out value_bool)) {
					Conf.GracefulFPS.DisplayFPS.Value = value_bool;
					config.RemoveAll(new ConfigDefinition("SimpleTweaks", "DisplayFPS"));
				}

				if (config.TryGetOrphanedEntry("SimpleTweaks", "LimitFPS", out value_bool)) {
					Conf.GracefulFPS.LimitFPS.Value = value_bool ? "Fixed" : "Off";
					config.RemoveAll(new ConfigDefinition("SimpleTweaks", "LimitFPS"));
				}
				if (config.TryGetOrphanedEntry("SimpleTweaks", "LimitBattleFPS", out value_bool)) {
					Conf.GracefulFPS.LimitBattleFPS.Value = value_bool ? "Fixed" : "Off";
					config.RemoveAll(new ConfigDefinition("SimpleTweaks", "LimitBattleFPS"));
				}

				if (config.TryGetOrphanedEntry("SimpleTweaks", "MaxFPS", out value_int)) {
					Conf.GracefulFPS.MaxFPS.Value = value_int;
					config.RemoveAll(new ConfigDefinition("SimpleTweaks", "MaxFPS"));
				}
				if (config.TryGetOrphanedEntry("SimpleTweaks", "MaxBattleFPS", out value_int)) {
					Conf.GracefulFPS.MaxBattleFPS.Value = value_int;
					config.RemoveAll(new ConfigDefinition("SimpleTweaks", "MaxBattleFPS"));
				}
			}
			{ // SimpleTweaks -> ConfigManager.SimpleTweaks
				var path = Path.Combine(Paths.ConfigPath, "Symphony.SimpleTweaks.cfg");
				if (File.Exists(path)) {
					Plugin.Logger.LogMessage("[Symphony] SimpleTweaks old configuration detected, migrate it.");
					var prev = new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.SimpleTweaks.cfg"), true);

					Conf.SimpleTweaks.UseLobbyHide.Value = prev.Bind("SimpleTweaks", "UseLobbyHide", true).Value;
					Conf.SimpleTweaks.LobbyUIHideKey.Value = prev.Bind("SimpleTweaks", "LobbyUIHideKey", "Tab").Value;

					Conf.SimpleTweaks.Use_IgnoreWindowReset.Value = prev.Bind("SimpleTweaks", "Use_IgnoreWindowReset", true).Value;

					Conf.SimpleTweaks.Use_FullScreenKey.Value = prev.Bind("SimpleTweaks", "Use_FullScreenKey", true).Value;
					Conf.SimpleTweaks.FullScreenKey.Value = prev.Bind("SimpleTweaks", "FullScreenKey", "F11").Value;

					Conf.SimpleTweaks.MuteOnBackgroundFix.Value = prev.Bind("SimpleTweaks", "MuteOnBackground", false).Value;

					Conf.SimpleTweaks.UsePatchStorySkip.Value = prev.Bind("SimpleTweaks", "UsePatchStorySkip", true).Value;
					Conf.SimpleTweaks.PatchStorySkipKey.Value = prev.Bind("SimpleTweaks", "PatchStorySpacebar", "LeftControl").Value;

					File.Delete(path);
				}
			}
			{ // SimpleTweaks -> ConfigManager.SimpleUI
				var path = Path.Combine(Paths.ConfigPath, "Symphony.SimpleUI.cfg");
				if (File.Exists(path)) {
					Plugin.Logger.LogMessage("[Symphony] SimpleUI old configuration detected, migrate it.");
					var prev = new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.SimpleUI.cfg"), true);

					Conf.SimpleUI.Small_CharWarehouse.Value = prev.Bind("SimpleUI", "Small_CharWarehouse", false).Value;
					Conf.SimpleUI.Small_CharSelection.Value = prev.Bind("SimpleUI", "Small_CharSelection", false).Value;
					Conf.SimpleUI.Small_CharScrapbook.Value = prev.Bind("SimpleUI", "Small_CharScrapbook", false).Value;
					Conf.SimpleUI.Small_ItemWarehouse.Value = prev.Bind("SimpleUI", "Small_ItemWarehouse", false).Value;
					Conf.SimpleUI.Small_ItemSelection.Value = prev.Bind("SimpleUI", "Small_ItemSelection", false).Value;
					Conf.SimpleUI.Small_TempInventory.Value = prev.Bind("SimpleUI", "Small_TempInventory", false).Value;

					Conf.SimpleUI.Small_Consumables.Value = prev.Bind("SimpleUI", "Small_Consumables", false).Value;
					Conf.SimpleUI.Sort_Consumables.Value = prev.Bind("SimpleUI", "Sort_Consumables", false).Value;

					Conf.SimpleUI.EnterToSearch_CharWarehouse.Value = prev.Bind("SimpleUI", "EnterToSearch_CharWarehouse", false).Value;
					Conf.SimpleUI.EnterToSearch_CharSelection.Value = prev.Bind("SimpleUI", "EnterToSearch_CharSelection", false).Value;
					Conf.SimpleUI.EnterToSearch_ItemWarehouse.Value = prev.Bind("SimpleUI", "EnterToSearch_ItemWarehouse", false).Value;
					Conf.SimpleUI.EnterToSearch_ItemSelection.Value = prev.Bind("SimpleUI", "EnterToSearch_ItemSelection", false).Value;

					File.Delete(path);
				}
			}
			{ // SimpleTweaks -> ConfigManager.BattleHotkey
				var path = Path.Combine(Paths.ConfigPath, "Symphony.BattleHotKey.cfg");
				if (File.Exists(path)) {
					Plugin.Logger.LogMessage("[Symphony] BattleHotKey old configuration detected, migrate it.");
					File.Delete(path); // feature deleted, just delete file
				}
			}
			#endregion

			#region Migration renamed Configs
			{ // HelpfulBase -> Automation
				bool value_bool;

				if (config.TryGetOrphanedEntry("HelpfulBase", "Use_GetAll", out value_bool)) {
					Plugin.Logger.LogMessage("[Symphony] HelpfulBase configuration detected, migrate it.");
					Conf.Automation.Use_Base_CollectAll_Restart.Value = value_bool;
					config.RemoveAll(new ConfigDefinition("HelpfulBase", "Use_GetAll"));
				}
			}
			{ // Automation.Use_Base_GetAll -> Automation.Use_Base_CollectAll_Restart
				bool value_bool;

				if (config.TryGetOrphanedEntry("Automation", "Use_Base_GetAll", out value_bool)) {
					Plugin.Logger.LogMessage("[Symphony] Use_Base_GetAll configuration detected, migrate it.");
					Conf.Automation.Use_Base_CollectAll_Restart.Value = value_bool;
					config.RemoveAll(new ConfigDefinition("Automation", "Use_Base_GetAll"));
				}
			}

			{ // LastBattle -> Notification (Section name not changed after copy-paste)
				bool value_bool;

				if (config.TryGetOrphanedEntry("LastBattle", "Handle_Notification", out value_bool)) {
					Plugin.Logger.LogMessage("[Symphony] LastBattle configuration detected, migrate it.");
					Conf.Notification.Handle_Notification.Value = value_bool;
					config.RemoveAll(new ConfigDefinition("LastBattle", "Handle_Notification"));
				}
			}

			{ // LastBattle -> SimpleUI
				bool value_bool;
				string value_str;

				if (config.TryGetOrphanedEntry("LastBattle", "Use_LastBattleMap", out value_bool)) {
					Plugin.Logger.LogMessage("[Symphony] LastBattle configuration detected, migrate it.");
					Conf.SimpleUI.Use_LastBattleMap.Value = value_bool;
					config.RemoveAll(new ConfigDefinition("LastBattle", "Use_LastBattleMap"));
				}
				if (config.TryGetOrphanedEntry("LastBattle", "LastBattleMapKey", out value_str)) {
					Plugin.Logger.LogMessage("[Symphony] LastBattle configuration detected, migrate it.");
					Conf.SimpleUI.LastBattleMapKey.Value = value_str;
					config.RemoveAll(new ConfigDefinition("LastBattle", "LastBattleMapKey"));
				}
			}

			{ // SimpleUI.Use_SortByXXX -> SimpleUI.Use_SortBy_Extra
				bool value_bool;

				if (config.TryGetOrphanedEntry("SimpleUI", "Use_SortByName", out value_bool)) {
					Plugin.Logger.LogMessage("[Symphony] SimpleUI.Use_SortByXXX configuration detected, migrate it.");
					Conf.SimpleUI.Use_SortBy_Extra.Value = value_bool;
					config.RemoveAll(new ConfigDefinition("SimpleUI", "Use_SortByName"));
					config.RemoveAll(new ConfigDefinition("SimpleUI", "Use_SortByGroup"));
					config.RemoveAll(new ConfigDefinition("SimpleUI", "Use_SortByLinks"));
				}
				if (config.TryGetOrphanedEntry("SimpleUI", "Use_SortByGroup", out value_bool)) {
					Plugin.Logger.LogMessage("[Symphony] SimpleUI.Use_SortByXXX configuration detected, migrate it.");
					Conf.SimpleUI.Use_SortBy_Extra.Value = value_bool;
					config.RemoveAll(new ConfigDefinition("SimpleUI", "Use_SortByName"));
					config.RemoveAll(new ConfigDefinition("SimpleUI", "Use_SortByGroup"));
					config.RemoveAll(new ConfigDefinition("SimpleUI", "Use_SortByLinks"));
				}
				if (config.TryGetOrphanedEntry("SimpleUI", "Use_SortByLinks", out value_bool)) {
					Plugin.Logger.LogMessage("[Symphony] SimpleUI.Use_SortByXXX configuration detected, migrate it.");
					Conf.SimpleUI.Use_SortBy_Extra.Value = value_bool;
					config.RemoveAll(new ConfigDefinition("SimpleUI", "Use_SortByName"));
					config.RemoveAll(new ConfigDefinition("SimpleUI", "Use_SortByGroup"));
					config.RemoveAll(new ConfigDefinition("SimpleUI", "Use_SortByLinks"));
				}
			}

			{ // Experimental.*KeyMapping* -> KeyMapping.*
				bool value_bool;
				float value_float;
				string value_string;

				if (config.TryGetOrphanedEntry("Experimental", "Use_KeyMapping", out value_bool)) {
					Plugin.Logger.LogMessage("[Symphony] Experimental.Use_KeyMapping configuration detected, migrate it.");
					Conf.KeyMapping.Use_KeyMapping.Value = value_bool;
					config.RemoveAll(new ConfigDefinition("Experimental", "Use_KeyMapping"));
				}
				if (config.TryGetOrphanedEntry("Experimental", "KeyMapping_Opacity", out value_float)) {
					Plugin.Logger.LogMessage("[Symphony] Experimental.KeyMapping_Opacity configuration detected, migrate it.");
					Conf.KeyMapping.Opacity.Value = value_float;
					config.RemoveAll(new ConfigDefinition("Experimental", "KeyMapping_Opacity"));
				}
				if (config.TryGetOrphanedEntry("Experimental", "KeyMapping_Active", out value_string)) {
					Plugin.Logger.LogMessage("[Symphony] Experimental.KeyMapping_Active configuration detected, migrate it.");
					Conf.KeyMapping.Activated.Value = value_string;
					config.RemoveAll(new ConfigDefinition("Experimental", "KeyMapping_Active"));
				}
			}
			#endregion

			#region Migration removed Configs
			{ // BattleHotkey
				var entries = config.GetAllOrphanedEntries("BattleHotkey").ToArray();
				foreach (var e in entries) config.RemoveAll(e);
			}

			{ // Experimental.Use_BetterTextureFilter*
				var entries = config.GetAllOrphanedEntries("Experimental")
					.Where(x => x.Key.StartsWith("Use_BetterTextureFilter", StringComparison.Ordinal))
					.ToArray();
				if (entries.Length > 0) {
					Plugin.Logger.LogMessage("[Symphony] BetterTextureFilter configuration detected, remove it.");
					foreach (var e in entries) config.RemoveAll(e);
				}
			}
			#endregion

			config.Save();
		}
	}

	file static class ConfigFileHelper {
		public static bool TryGetOrphanedEntry<T>(this ConfigFile config, string section, string key, out T value) {
			var orphaned = (Dictionary<ConfigDefinition, string>)config.GetType()
				.GetProperty("OrphanedEntries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
				.GetValue(config);

			var def = new ConfigDefinition(section, key);
			if (!orphaned.TryGetValue(def, out var v)) {
				value = default;
				return false;
			}

			value = (T)TomlTypeConverter.ConvertToValue(v, typeof(T));
			return true;
		}
		public static IEnumerable<ConfigDefinition> GetAllOrphanedEntries(this ConfigFile config, string section = null, string key = null) {
			var orphaned = (Dictionary<ConfigDefinition, string>)config.GetType()
				.GetProperty("OrphanedEntries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
				.GetValue(config);

			return orphaned.Keys.Where(x =>
				(section != null ? x.Section == section : true) &&
				(key != null ? x.Key == key : true)
			);
		}
		public static void RemoveAll(this ConfigFile config, ConfigDefinition def) {
			config.Remove(def);

			var orphaned = (Dictionary<ConfigDefinition, string>)config.GetType()
				.GetProperty("OrphanedEntries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
				.GetValue(config);
			orphaned.Remove(def);
		}
	}
}
