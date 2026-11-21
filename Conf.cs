using BepInEx;
using BepInEx.Configuration;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Symphony {
	internal class Conf {
		public static ConfigFile config = new(Path.Combine(Paths.ConfigPath, "Symphony.cfg"), true);

		public static readonly ConfigEntry<string> LastVersionTag = config.Bind("Common", "LastVersionTag", "0.0.0");

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

			public static readonly ConfigEntry<bool> Use_ContinueBGM = config.Bind("SimpleTweaks", "Use_ContinueBGM", false, $"Continue BGM when any device changed and BGM has reset");
		}
		internal class SimpleUI {
			#region Battle
			public static readonly ConfigEntry<bool> Use_LastBattleMap = config.Bind("SimpleUI", "Use_LastBattleMap", false, "Whether to use the function that adds a button to the World screen that moves you directly to the last visited battle map.");
			public static readonly ConfigEntry<string> LastBattleMapKey = config.Bind("SimpleUI", "LastBattleMapKey", "");

			public static readonly ConfigEntry<bool> Use_LastOfflineBattle = config.Bind("SimpleUI", "Use_LastOfflineBattle", false, "Whether to use the function that adds a button to the World screen that moves you directly to the last visited offline battle map.");
			public static readonly ConfigEntry<string> LastOfflineBattleKey = config.Bind("SimpleUI", "LastOfflineBattleKey", "");

			public static readonly ConfigEntry<bool> Use_OfflineBattle_Bypass = config.Bind("SimpleUI", "Use_OfflineBattle_Bypass", false, "Enter Maps screen instead open Offline battle screen");

			public static readonly ConfigEntry<bool> Use_MapEnemyPreview = config.Bind("SimpleUI", "Use_MapEnemyPreview", false, "Displays enemies to be encountered in battle on the Battle Map Information");
			#endregion

			#region ListItemDisplay
			public static readonly ConfigEntry<bool> Default_CharacterCost_Off = config.Bind("SimpleUI", "Default_CharacterCost_Off", false, "Set Character's resource cost display default to Off");

			public static readonly ConfigEntry<bool> DblClick_CharWarehouse = config.Bind("SimpleUI", "DblClick_CharWarehouse", true, "Double-Click Character to go in Detail");

			public static readonly ConfigEntry<bool> Small_CharWarehouse = config.Bind("SimpleUI", "Small_CharWarehouse", false, "Display more items for Character Warehouse");
			public static readonly ConfigEntry<bool> Small_CharSelection = config.Bind("SimpleUI", "Small_CharSelection", false, "Display more items for Character Selection");
			public static readonly ConfigEntry<bool> Small_CharScrapbook = config.Bind("SimpleUI", "Small_CharScrapbook", false, "Display more items for Character Scrapbook");
			public static readonly ConfigEntry<bool> Small_ItemWarehouse = config.Bind("SimpleUI", "Small_ItemWarehouse", false, "Display more items for Item Warehouse");
			public static readonly ConfigEntry<bool> Small_ItemSelection = config.Bind("SimpleUI", "Small_ItemSelection", false, "Display more items for Item Selection");
			public static readonly ConfigEntry<bool> Small_TempInventory = config.Bind("SimpleUI", "Small_TempInventory", false, "Display more items for Temporary Inventory");
			public static readonly ConfigEntry<bool> Small_Consumables = config.Bind("SimpleUI", "Small_Consumables", false, "Display more items for Consumables");
			#endregion

			#region ListSearch
			public static readonly ConfigEntry<bool> EnterToSearch_CharWarehouse = config.Bind("SimpleUI", "EnterToSearch_CharWarehouse", false, "Press enter to search for Character Warehouse");
			public static readonly ConfigEntry<bool> EnterToSearch_CharSelection = config.Bind("SimpleUI", "EnterToSearch_CharSelection", false, "Press enter to search for Character Selection");
			public static readonly ConfigEntry<bool> EnterToSearch_ItemWarehouse = config.Bind("SimpleUI", "EnterToSearch_ItemWarehouse", false, "Press enter to search for Item Warehouse");
			public static readonly ConfigEntry<bool> EnterToSearch_ItemSelection = config.Bind("SimpleUI", "EnterToSearch_ItemSelection", false, "Press enter to search for Item Selection");
			#endregion

			#region ListSorting
			public static readonly ConfigEntry<bool> Sort_Consumables = config.Bind("SimpleUI", "Sort_Consumables", false, "Sort consumable items");

			public static readonly ConfigEntry<bool> Sort_Equips_ExclusiveFirst = config.Bind("SimpleUI", "Sort_Equips_ExclusiveFirst", false, "Sort equip items to show Exclusive equipment first");

			public static readonly ConfigEntry<bool> Use_SortBy_Extra = config.Bind("SimpleUI", "Use_SortBy_Extra", false, "Add extra sorting filter to Character list");
			#endregion

			#region CharacterDetail
			public static ConfigEntry<bool> Use_CharacterDetail_NextPrev = config.Bind("SimpleUI", "Use_CharacterDetail_NextPrev", false, "Add next/prev character button to Character Detail screen");

			public static ConfigEntry<bool> Use_Character_Favorite = config.Bind("SimpleUI", "Use_Character_Favorite", false, "Add favorite feature for Character");
			public static ConfigEntry<string> ChracterFavorites_Raw = config.Bind("SimpleUI", "ChracterFavorites", "", "List of favorite for Character");
			public static ObservableHashSet<ulong> ChracterFavorites = new(
				ChracterFavorites_Raw.Value
					.Split(",")
					.Select(x => {
						var b = ulong.TryParse(x, out var v);
						if (b) return (pass: true, value: v);
						return (pass: false, value: 0UL);
					})
					.Where(x => x.pass)
					.Select(x => x.value),
				() => {
					ChracterFavorites_Raw.Value = string.Join(",", Conf.SimpleUI.ChracterFavorites);
				}
			);

			public static ConfigEntry<bool> Use_BetterPCEnchant = config.Bind("SimpleUI", "Use_BetterPCEnchant", false, "Allows unit stats to be decreased on the enchant screen without resetting stat points");
			#endregion

			#region Workbench
			public static readonly ConfigEntry<bool> Use_CharacterMakingPreview = config.Bind("SimpleUI", "Use_CharacterMakingPreview", false, "Displays available result for Character making");
			public static readonly ConfigEntry<bool> Use_EquipMakingPreview = config.Bind("SimpleUI", "Use_EquipMakingPreview", false, "Displays available result for Equip making");

			public static readonly ConfigEntry<bool> Use_Disassemble_SelectAll_Character = config.Bind("SimpleUI", "Use_Disassemble_SelectAll_Character", false, "Add Select All button to Disassemble Character screen");
			public static readonly ConfigEntry<bool> Use_Disassemble_SelectAll_Equip = config.Bind("SimpleUI", "Use_Disassemble_SelectAll_Equip", false, "Add Select All button to Disassemble Equip screen");
			#endregion

			#region Composite
			public static readonly ConfigEntry<bool> Use_ScrapbookMustBeFancy = config.Bind("SimpleUI", "Use_ScrapbookMustBeFancy", true, "Beautify Scrapbook to usefully");

			public static readonly ConfigEntry<bool> Use_Exchange_NoMessyHand = config.Bind("SimpleUI", "Use_Exchange_NoMessyHand", true, "Enable the 'Hide Sold Out' in Exchange by default, Display consumables only related to selling");

			public static readonly ConfigEntry<bool> Use_BetterFacilityInventory = config.Bind("SimpleUI", "Use_BetterFacilityInventory", false, "Sort base facilities on installation");

			public static readonly ConfigEntry<bool> Use_GiveMeBackLivingStationNavigation = config.Bind("SimpleUI", "Use_GiveMeBackLivingStationNavigation", false, "Rollback old design navigation button");
			#endregion

			public static readonly ConfigEntry<bool> Use_Squad_Clear = config.Bind("SimpleUI", "Use_Squad_Clear", false, "Add clear button to squad screen");

			public static readonly ConfigEntry<bool> Use_AccelerateScrollDelta = config.Bind("SimpleUI", "Use_MultiplyScrollDelta", false, "Multiply scroll amount for scrollable list");

			public static readonly ConfigEntry<bool> Use_NovelDialog_LabelFix = config.Bind("SimpleUI", "Use_NovelDialog_LabelFix", false, "Fix Story Viewer's label invalid displaying");

			public static readonly ConfigEntry<bool> Use_ScrollableConsumableDescription= config.Bind("SimpleUI", "Use_ConsumableDescriptionScrollable", false, "Wrap consumable description with ScrollView");

			public static readonly ConfigEntry<string> List_BenefitUnits_Normal = config.Bind("SimpleUI", "List_BenefitUnits_Normal", "", "Unit key list to display normal benefit effect");
			public static readonly ConfigEntry<string> List_BenefitUnits_Final= config.Bind("SimpleUI", "List_BenefitUnits_Final", "", "Unit key list to display final benefit effect");
		}
		internal class Notification {
			public static readonly ConfigEntry<bool> Handle_Notification = config.Bind("Notification", "Handle_Notification", true, "Handle in-game push notification as windows notification.");
		}
		internal class Presets {
			public static readonly ConfigEntry<bool> Use_CharMaking_Preset = config.Bind("Presets", "Use_CharMakingPreset", false, "Use Preset for Character making screen");

			public static readonly ConfigEntry<bool> Use_Last_CharMakingData = config.Bind("Presets", "Use_Last_CharMakingData", false, "Load last character making data automatically");
			public static readonly ConfigEntry<string> Last_CharMaking_Data = config.Bind("Presets", "Last_CharMaking_Data", "0,0,0,0,0,0");

			public static readonly ConfigEntry<bool> Use_Last_EquipMakingData = config.Bind("Presets", "Use_Last_EquipMakingData", false, "Load last equipment making data automatically");
			public static readonly ConfigEntry<string> Last_EquipMaking_Data = config.Bind("Presets", "Last_EquipMaking_Data", "0,0,0");

			public static readonly ConfigEntry<bool> Use_Last_FacPartsMakingData = config.Bind("Presets", "Use_Last_FacPartsMakingData", false, "Load last facility parts making data automatically");
			public static readonly ConfigEntry<string> Last_FacPartsMaking_Data = config.Bind("Presets", "Last_FacPartsMaking_Data", "0,0,0");

			public static readonly ConfigEntry<string> CharMaking_Preset_Data = config.Bind("Presets", "CharMaking_Preset_Data", "");
		}
		internal class Automation {
			public static readonly ConfigEntry<bool> Use_Base_CollectAll_Restart = config.Bind("Automation", "Use_Base_CollectAll_Restart", false, "Restarts facility when 'Collect All' used");

			public static readonly ConfigEntry<bool> Use_CharacterShare = config.Bind("Automation", "Use_CharacterShare", false, "Add 'Character Share' button and 'Load Shared Character' in Character Detail Screen");
		}
		internal class Experimental {
			public static readonly ConfigEntry<bool> Fix_BattleFreezing = config.Bind("Experimental", "Fix_BattleFreezing", true, "Fix a Freezing issue in certain situations during Battle");

			public static readonly ConfigEntry<bool> Use_FastLoading = config.Bind("Experimental", "Use_FastLoading", false, "Skips loading unnecessary files on startup");
		}
		internal class KeyMapping {
			public static readonly ConfigEntry<bool> Use_KeyMapping = config.Bind("KeyMapping", "Use_KeyMapping", false, "Use KeyMapping feature");
			public static readonly ConfigEntry<float> Opacity = config.Bind("KeyMapping", "Opacity", 0.25f);
			public static readonly ConfigEntry<string> Activated = config.Bind("KeyMapping", "Activated", "Default", "Currently activated Key Mapping Group");
		}
		internal class Statistics {
			public static readonly ConfigEntry<bool> Use_ResourceLogging = config.Bind("Statistics", "Use_ResourceLogging", false, "Writes log for resource changes");

			public static readonly ConfigEntry<bool> Use_ItemsLogging = config.Bind("Statistics", "Use_ItemsLogging", false, "When use Resource Logging, item changes also writes");
		}

		/// <summary>
		/// App-life cache, not be written to file
		/// </summary>
		internal class Cache {
			public static bool FavoriteOnly = false;
			public static bool Workbench_Awaiting = false;

			public static int ExchangeCount = 0;
			public static bool ExchangeAwaiting = false;
		}
	}
}
