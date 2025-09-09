﻿using BepInEx;
using BepInEx.Configuration;

using HarmonyLib;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using UnityEngine;

namespace Symphony.Features {
	internal class SimpleUI : MonoBehaviour {
		internal static ConfigFile config = new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.SimpleUI.cfg"), true);

		internal static ConfigEntry<bool> Small_CharWarehouse = config.Bind("SimpleUI", "Small_CharWarehouse", false, "Display more items for Character Warehouse");
		internal static ConfigEntry<bool> Small_CharSelection = config.Bind("SimpleUI", "Small_CharSelection", false, "Display more items for Character Selection");
		internal static ConfigEntry<bool> Small_CharScrapbook = config.Bind("SimpleUI", "Small_CharScrapbook", false, "Display more items for Character Scrapbook");
		internal static ConfigEntry<bool> Small_ItemWarehouse = config.Bind("SimpleUI", "Small_ItemWarehouse", false, "Display more items for Item Warehouse");
		internal static ConfigEntry<bool> Small_ItemSelection = config.Bind("SimpleUI", "Small_ItemWarehouse", false, "Display more items for Item Selection");
		internal static ConfigEntry<bool> Small_TempInventory = config.Bind("SimpleUI", "Small_TempInventory", false, "Display more items for Temporary Inventory");

		internal static ConfigEntry<bool> Small_Consumables = config.Bind("SimpleUI", "Small_Consumables", false, "Display more items for Consumables");
		internal static ConfigEntry<bool> Sort_Consumables = config.Bind("SimpleUI", "Sort_Consumables", false, "Sort consumable items");

		internal static ConfigEntry<bool> EnterToSearch_CharWarehouse = config.Bind("SimpleUI", "EnterToSearch_CharWarehouse", false, "Press enter to search for Character Warehouse");
		internal static ConfigEntry<bool> EnterToSearch_CharSelection = config.Bind("SimpleUI", "EnterToSearch_CharSelection", false, "Press enter to search for Character Selection");
		internal static ConfigEntry<bool> EnterToSearch_ItemWarehouse = config.Bind("SimpleUI", "EnterToSearch_ItemWarehouse", false, "Press enter to search for Item Warehouse");
		internal static ConfigEntry<bool> EnterToSearch_ItemSelection = config.Bind("SimpleUI", "EnterToSearch_ItemSelection", false, "Press enter to search for Item Selection");

		public void Start() {
			#region Patch
			var harmony = new Harmony("Symphony.SimpleUI");
			harmony.Patch(
				AccessTools.Method(typeof(Panel_PcWarehouse), "Start"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_PCWarehouse_Start_pre)),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_PCWarehouse_Start_post))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_AndroidInventory), "Start"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_AndroidInventory_Start_pre)),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_AndroidInventory_Start_post))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_CharacterBook), "Start"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_CharacterBook_Start_pre)),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_CharacterBook_Start_post))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_ItemInventory), "Start"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_ItemInventory_Start_pre)),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_ItemInventory_Start_post))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_ItemEquipInventory), "Start"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_ItemEquipInventory_Start_pre)),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_ItemEquipInventory_Start_post))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_TempInventory), "Start"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_TempInventory_Start_pre)),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_TempInventory_Start_post))
			);

			harmony.Patch(
				AccessTools.Method(typeof(Panel_MaterialWarehouse), "Start"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_Consumable_Start_pre)),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_Consumable_Start_post))
			);
			harmony.Patch(
				AccessTools.Method(typeof(DataManager), "GetItemConsumableEnchantCreate"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.DataSortPatch_DataManager_List))
			);
			harmony.Patch(
				AccessTools.Method(typeof(DataManager), "GetItemConsumableGift"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.DataSortPatch_DataManager_List))
			);
			harmony.Patch(
				AccessTools.Method(typeof(DataManager), "GetItemConsumableCollection"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.DataSortPatch_DataManager_List))
			);
			harmony.Patch(
				AccessTools.Method(typeof(DataManager), "GetItemConsumableSticker"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.DataSortPatch_DataManager_List))
			);
			#endregion
		}

		private const float SMALL_ORIGINAL6_RATIO = (1f / 8f * 6f); // 6 -> 8
		private const float SMALL_ORIGINAL5_RATIO = (1f / 7f * 5f); // 5 -> 7
		private const float SMALL_CONSUMABLE_RATIO = (1f / 7f * 6f); // 6 -> 7
		private static void GridItemsPatch_PCWarehouse_Start_pre(Panel_PcWarehouse __instance) {
			if (!Small_CharWarehouse.Value) return;

			var _reUseGrid = (UIReuseGrid)__instance.GetType()
				.GetField("_reUseGrid", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			_reUseGrid.m_Column = 8;
			_reUseGrid.m_cellWidth = (int)(_reUseGrid.m_cellWidth * SMALL_ORIGINAL6_RATIO);
			_reUseGrid.m_cellHeight = (int)(_reUseGrid.m_cellHeight * SMALL_ORIGINAL6_RATIO);
		}
		private static void GridItemsPatch_PCWarehouse_Start_post(Panel_PcWarehouse __instance) {
			if (Small_CharWarehouse.Value) {
				var _reUseGrid = (UIReuseGrid)__instance.GetType()
					.GetField("_reUseGrid", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(__instance);

				var m_cellList = (UIReuseScrollViewCell[])_reUseGrid.GetType()
					.GetField("m_cellList", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(_reUseGrid);

				foreach (var cell in m_cellList) {
					cell.transform.localScale = new Vector3(SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO);
				}
			}

			if(EnterToSearch_CharWarehouse.Value) {
				var _inputSearch = (UIInput)__instance.GetType()
					.GetField("_inputSearch", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(__instance);
				_inputSearch.onReturnKey = UIInput.OnReturnKey.Submit;
				_inputSearch.onSubmit.Add(new(() => {
					if (string.IsNullOrEmpty(_inputSearch.value)) {
						var ev = _inputSearch.transform.Find("BtnReset")?.gameObject.GetComponent<UIButton>()?.onClick;
						if (ev != null) EventDelegate.Execute(ev);
					}
					else {
						var ev = _inputSearch.transform.Find("BtnSearch")?.gameObject.GetComponent<UIButton>()?.onClick;
						if (ev != null) EventDelegate.Execute(ev);
					}
				}));
			}
		}
		private static void GridItemsPatch_AndroidInventory_Start_pre(Panel_AndroidInventory __instance) {
			if (!Small_CharSelection.Value) return;

			var _reUseGrid = (UIReuseGrid[])__instance.GetType()
				.GetField("_reUseGrid", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			foreach (var grid in _reUseGrid) {
				grid.m_Column = 8;
				grid.m_cellWidth = (int)(grid.m_cellWidth * SMALL_ORIGINAL6_RATIO);
				grid.m_cellHeight = (int)(grid.m_cellHeight * SMALL_ORIGINAL6_RATIO);
			}
		}
		private static void GridItemsPatch_AndroidInventory_Start_post(Panel_AndroidInventory __instance) {
			if (Small_CharSelection.Value) {
				var _reUseGrid = (UIReuseGrid[])__instance.GetType()
					.GetField("_reUseGrid", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(__instance);

				foreach (var grid in _reUseGrid) {
					var m_cellList = (UIReuseScrollViewCell[])grid.GetType()
					.GetField("m_cellList", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(grid);

					foreach (var cell in m_cellList) {
						cell.transform.localScale = new Vector3(SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO);
					}
				}
			}

			if (EnterToSearch_CharSelection.Value) {
				var _inputSearch = (UIInput)__instance.GetType()
					.GetField("_inputSearch", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(__instance);
				_inputSearch.onReturnKey = UIInput.OnReturnKey.Submit;
				_inputSearch.onSubmit.Add(new(() => {
					if (string.IsNullOrEmpty(_inputSearch.value)) {
						var ev = _inputSearch.transform.Find("BtnReset")?.gameObject.GetComponent<UIButton>()?.onClick;
						if (ev != null) EventDelegate.Execute(ev);
					}
					else {
						var ev = _inputSearch.transform.Find("BtnSearch")?.gameObject.GetComponent<UIButton>()?.onClick;
						if (ev != null) EventDelegate.Execute(ev);
					}
				}));
			}
		}
		private static void GridItemsPatch_CharacterBook_Start_pre(Panel_CharacterBook __instance) {
			if (!Small_ItemWarehouse.Value) return;

			var _grid = (UIReuseGrid)__instance.GetType()
				.GetField("_grid", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			_grid.m_Column = 8;
			_grid.m_cellWidth = (int)(_grid.m_cellWidth * SMALL_ORIGINAL6_RATIO);
			_grid.m_cellHeight = (int)(_grid.m_cellHeight * SMALL_ORIGINAL6_RATIO);
		}
		private static void GridItemsPatch_CharacterBook_Start_post(Panel_CharacterBook __instance) {
			if (!Small_ItemWarehouse.Value) return;

			var _grid = (UIReuseGrid)__instance.GetType()
				.GetField("_grid", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			var m_cellList = (UIReuseScrollViewCell[])_grid.GetType()
				.GetField("m_cellList", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(_grid);

			foreach (var cell in m_cellList) {
				cell.transform.localScale = new Vector3(SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO);
			}
		}
		private static void GridItemsPatch_ItemInventory_Start_pre(Panel_ItemInventory __instance) {
			if (!Small_ItemWarehouse.Value) return;

			var _gridItemList = (UIReuseGrid)__instance.GetType()
				.GetField("_gridItemList", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			_gridItemList.m_Column = 7;
			_gridItemList.m_cellWidth = (int)(_gridItemList.m_cellWidth * SMALL_ORIGINAL5_RATIO);
			_gridItemList.m_cellHeight = (int)(_gridItemList.m_cellHeight * SMALL_ORIGINAL5_RATIO);
		}
		private static void GridItemsPatch_ItemInventory_Start_post(Panel_ItemInventory __instance) {
			if (Small_ItemWarehouse.Value) {
				var _gridItemList = (UIReuseGrid)__instance.GetType()
					.GetField("_gridItemList", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(__instance);

				var m_cellList = (UIReuseScrollViewCell[])_gridItemList.GetType()
					.GetField("m_cellList", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(_gridItemList);

				foreach (var cell in m_cellList) {
					cell.transform.localScale = new Vector3(SMALL_ORIGINAL5_RATIO, SMALL_ORIGINAL5_RATIO, SMALL_ORIGINAL5_RATIO);
				}
			}

			if (EnterToSearch_ItemWarehouse.Value) {
				var _inputSearch = (UIInput)__instance.GetType()
					.GetField("_inputSearch", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(__instance);
				_inputSearch.onReturnKey = UIInput.OnReturnKey.Submit;
				_inputSearch.onSubmit.Add(new(() => {
					if (string.IsNullOrEmpty(_inputSearch.value)) {
						var ev = _inputSearch.transform.Find("BtnReset")?.gameObject.GetComponent<UIButton>()?.onClick;
						if (ev != null) EventDelegate.Execute(ev);
					}
					else {
						var ev = _inputSearch.transform.Find("BtnSearch")?.gameObject.GetComponent<UIButton>()?.onClick;
						if (ev != null) EventDelegate.Execute(ev);
					}
				}));
			}
		}
		private static void GridItemsPatch_ItemEquipInventory_Start_pre(Panel_ItemEquipInventory __instance) {
			if (!Small_ItemSelection.Value) return;

			var _gridItemList = (UIReuseGrid)__instance.GetType()
				.GetField("_gridItemList", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			_gridItemList.m_Column = 7;
			_gridItemList.m_cellWidth = (int)(_gridItemList.m_cellWidth * SMALL_ORIGINAL5_RATIO);
			_gridItemList.m_cellHeight = (int)(_gridItemList.m_cellHeight * SMALL_ORIGINAL5_RATIO);
		}
		private static void GridItemsPatch_ItemEquipInventory_Start_post(Panel_ItemEquipInventory __instance) {
			if (Small_ItemSelection.Value) {
				var _gridItemList = (UIReuseGrid)__instance.GetType()
					.GetField("_gridItemList", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(__instance);

				var m_cellList = (UIReuseScrollViewCell[])_gridItemList.GetType()
					.GetField("m_cellList", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(_gridItemList);

				foreach (var cell in m_cellList) {
					cell.transform.localScale = new Vector3(SMALL_ORIGINAL5_RATIO, SMALL_ORIGINAL5_RATIO, SMALL_ORIGINAL5_RATIO);
				}
			}

			if (EnterToSearch_ItemSelection.Value) {
				var _inputSearch = (UIInput)__instance.GetType()
					.GetField("_inputSearch", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(__instance);
				_inputSearch.onReturnKey = UIInput.OnReturnKey.Submit;
				_inputSearch.onSubmit.Add(new(() => {
					if (string.IsNullOrEmpty(_inputSearch.value)) {
						var ev = _inputSearch.transform.Find("BtnReset")?.gameObject.GetComponent<UIButton>()?.onClick;
						if (ev != null) EventDelegate.Execute(ev);
					}
					else {
						var ev = _inputSearch.transform.Find("BtnSearch")?.gameObject.GetComponent<UIButton>()?.onClick;
						if (ev != null) EventDelegate.Execute(ev);
					}
				}));
			}
		}
		private static void GridItemsPatch_TempInventory_Start_pre(Panel_TempInventory __instance) {
			if (!Small_TempInventory.Value) return;

			var _reUseGridPc = (UIReuseGrid)__instance.GetType()
				.GetField("_reUseGridPc", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			_reUseGridPc.m_Column = 8;
			_reUseGridPc.m_cellWidth = (int)(_reUseGridPc.m_cellWidth * SMALL_ORIGINAL6_RATIO);
			_reUseGridPc.m_cellHeight = (int)(_reUseGridPc.m_cellHeight * SMALL_ORIGINAL6_RATIO);

			var _reUseGridEquip = (UIReuseGrid)__instance.GetType()
				.GetField("_reUseGridEquip", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			_reUseGridEquip.m_Column = 8;
			_reUseGridEquip.m_cellWidth = (int)(_reUseGridEquip.m_cellWidth * SMALL_ORIGINAL6_RATIO);
			_reUseGridEquip.m_cellHeight = (int)(_reUseGridEquip.m_cellHeight * SMALL_ORIGINAL6_RATIO);
		}
		private static void GridItemsPatch_TempInventory_Start_post(Panel_TempInventory __instance) {
			if (!Small_TempInventory.Value) return;

			{
				var _reUseGridPc = (UIReuseGrid)__instance.GetType()
					.GetField("_reUseGridPc", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(__instance);

				var m_cellList = (UIReuseScrollViewCell[])_reUseGridPc.GetType()
					.GetField("m_cellList", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(_reUseGridPc);

				foreach (var cell in m_cellList) {
					cell.transform.localScale = new Vector3(SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO);
				}
			}
			{
				var _reUseGridEquip = (UIReuseGrid)__instance.GetType()
					.GetField("_reUseGridEquip", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(__instance);

				var m_cellList = (UIReuseScrollViewCell[])_reUseGridEquip.GetType()
					.GetField("m_cellList", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(_reUseGridEquip);

				foreach (var cell in m_cellList) {
					cell.transform.localScale = new Vector3(SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO);
				}
			}
		}

		private static void GridItemsPatch_Consumable_Start_pre(Panel_MaterialWarehouse __instance) {
			if (!Small_Consumables.Value) return;

			var _reGrid = (UIReuseGrid)__instance.GetType()
				.GetField("_reGrid", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			_reGrid.m_Column = 7;
			_reGrid.m_cellWidth = (int)(_reGrid.m_cellWidth * SMALL_CONSUMABLE_RATIO);
			_reGrid.m_cellHeight = (int)(_reGrid.m_cellHeight * SMALL_CONSUMABLE_RATIO);
		}
		private static void GridItemsPatch_Consumable_Start_post(Panel_MaterialWarehouse __instance) {
			if (!Small_Consumables.Value) return;

			var _reGrid = (UIReuseGrid)__instance.GetType()
				.GetField("_reGrid", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			var m_cellList = (UIReuseScrollViewCell[])_reGrid.GetType()
				.GetField("m_cellList", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(_reGrid);

			foreach (var cell in m_cellList) {
				cell.transform.localScale = new Vector3(SMALL_CONSUMABLE_RATIO, SMALL_CONSUMABLE_RATIO, SMALL_CONSUMABLE_RATIO);
			}
		}
		private static string[] ConsumableKeyList = null; // Cache
		private static void DataSortPatch_DataManager_List(DataManager __instance, ref List<ClientItemInfo> __result) {
			if (!Sort_Consumables.Value) return;
			if (__result.Count == 0) return;

			var tableManager = (LoTableManagerClient)__instance.GetType()
				.GetField("_TableManager", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			if(ConsumableKeyList == null) {
				var table = tableManager._Table_ItemConsumable;
				ConsumableKeyList = table.Keys.ToArray();
			}

			__result.Sort((x, y) => Array.IndexOf(ConsumableKeyList, x.ItemKeyString) - Array.IndexOf(ConsumableKeyList, y.ItemKeyString));
		}
	}
}
