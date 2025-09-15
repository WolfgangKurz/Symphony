using HarmonyLib;

using LO_ClientNetwork;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

using UnityEngine;

namespace Symphony.Features {
	internal class SimpleUI : MonoBehaviour {
		private static ulong SquadClear_LastUnsetPC = 0;

		public void Start() {
			#region Patch
			var harmony = new Harmony("Symphony.SimpleUI");

			harmony.Patch(
				AccessTools.Method(typeof(Panel_GameModeMenu), nameof(Panel_GameModeMenu.OnBtnOfflineBattleCheck)),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.OfflineBattleBypass_Patch))
			);

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
				AccessTools.Method(typeof(Panel_ItemSelectInventory), "Start"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_ItemSelectInventory_Start_pre)),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_ItemSelectInventory_Start_post))
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

			harmony.Patch(
				AccessTools.Method(typeof(Panel_PcWarehouse), "Awake"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Inject_SortByName))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_AndroidInventory), "Awake"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Inject_SortByName))
			);

			harmony.Patch(
				AccessTools.Method(typeof(UIReuseScrollView), nameof(UIReuseScrollView.Scroll)),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.AccelerateScrollDelta))
			);
			harmony.Patch(
				AccessTools.Method(typeof(UIScrollView), nameof(UIScrollView.Scroll)),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.AccelerateScrollDelta))
			);
			harmony.Patch(
				AccessTools.Method(typeof(UIScrollView2), nameof(UIScrollView2.Scroll)),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.AccelerateScrollDelta))
			);

			harmony.Patch(
				AccessTools.Method(typeof(Panel_PcWarehouse), "Start"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_CharacterCostOff))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_AndroidInventory), "Start"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_CharacterCostOff))
			);

			harmony.Patch(
				AccessTools.Method(typeof(Panel_SquadInfo), "Start"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_Squad_Clear))
			);
			#endregion
		}

		private static bool OfflineBattleBypass_Patch(Panel_GameModeMenu __instance) {
			if (!Conf.SimpleUI.Use_OfflineBattle_Bypass.Value) return true;

			__instance.OnBtnMainStroyMode(); // OnBtnOfflineBattleCheck
			return false;
		}

		private const float SMALL_ORIGINAL6_RATIO = (1f / 8f * 6f); // 6 -> 8
		private const float SMALL_ORIGINAL5_RATIO = (1f / 7f * 5f); // 5 -> 7
		private const float SMALL_CONSUMABLE_RATIO = (1f / 7f * 6f); // 6 -> 7
		private static void GridItemsPatch_PCWarehouse_Start_pre(Panel_PcWarehouse __instance) {
			if (!Conf.SimpleUI.Small_CharWarehouse.Value) return;

			var _reUseGrid = (UIReuseGrid)__instance.GetType()
				.GetField("_reUseGrid", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			_reUseGrid.m_Column = 8;
			_reUseGrid.m_cellWidth = (int)(_reUseGrid.m_cellWidth * SMALL_ORIGINAL6_RATIO);
			_reUseGrid.m_cellHeight = (int)(_reUseGrid.m_cellHeight * SMALL_ORIGINAL6_RATIO);

			__instance.HeightInvenSquad = (int)(__instance.HeightInvenSquad * SMALL_ORIGINAL6_RATIO);
		}
		private static void GridItemsPatch_PCWarehouse_Start_post(Panel_PcWarehouse __instance) {
			if (Conf.SimpleUI.Small_CharWarehouse.Value) {
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

			if (Conf.SimpleUI.EnterToSearch_CharWarehouse.Value) {
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
			if (!Conf.SimpleUI.Small_CharSelection.Value) return;

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
			if (Conf.SimpleUI.Small_CharSelection.Value) {
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

			if (Conf.SimpleUI.EnterToSearch_CharSelection.Value) {
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
			if (!Conf.SimpleUI.Small_ItemWarehouse.Value) return;

			var _grid = (UIReuseGrid)__instance.GetType()
				.GetField("_grid", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			_grid.m_Column = 8;
			_grid.m_cellWidth = (int)(_grid.m_cellWidth * SMALL_ORIGINAL6_RATIO);
			_grid.m_cellHeight = (int)(_grid.m_cellHeight * SMALL_ORIGINAL6_RATIO);
		}
		private static void GridItemsPatch_CharacterBook_Start_post(Panel_CharacterBook __instance) {
			if (!Conf.SimpleUI.Small_ItemWarehouse.Value) return;

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
			if (!Conf.SimpleUI.Small_ItemWarehouse.Value) return;

			var _gridItemList = (UIReuseGrid)__instance.GetType()
				.GetField("_gridItemList", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			_gridItemList.m_Column = 7;
			_gridItemList.m_cellWidth = (int)(_gridItemList.m_cellWidth * SMALL_ORIGINAL5_RATIO);
			_gridItemList.m_cellHeight = (int)(_gridItemList.m_cellHeight * SMALL_ORIGINAL5_RATIO);
		}
		private static void GridItemsPatch_ItemInventory_Start_post(Panel_ItemInventory __instance) {
			if (Conf.SimpleUI.Small_ItemWarehouse.Value) {
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

			if (Conf.SimpleUI.EnterToSearch_ItemWarehouse.Value) {
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
			if (!Conf.SimpleUI.Small_ItemSelection.Value) return;

			var _gridItemList = (UIReuseGrid)__instance.GetType()
				.GetField("_gridItemList", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			_gridItemList.m_Column = 7;
			_gridItemList.m_cellWidth = (int)(_gridItemList.m_cellWidth * SMALL_ORIGINAL5_RATIO);
			_gridItemList.m_cellHeight = (int)(_gridItemList.m_cellHeight * SMALL_ORIGINAL5_RATIO);
		}
		private static void GridItemsPatch_ItemEquipInventory_Start_post(Panel_ItemEquipInventory __instance) {
			if (Conf.SimpleUI.Small_ItemSelection.Value) {
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

			if (Conf.SimpleUI.EnterToSearch_ItemSelection.Value) {
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
		private static void GridItemsPatch_ItemSelectInventory_Start_pre(Panel_ItemSelectInventory __instance) {
			if (!Conf.SimpleUI.Small_ItemSelection.Value) return;

			var _gridItemList = (UIReuseGrid)__instance.GetType()
				.GetField("_gridItemList", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			_gridItemList.m_Column = 8;
			_gridItemList.m_cellWidth = (int)(_gridItemList.m_cellWidth * SMALL_ORIGINAL6_RATIO);
			_gridItemList.m_cellHeight = (int)(_gridItemList.m_cellHeight * SMALL_ORIGINAL6_RATIO);
		}
		private static void GridItemsPatch_ItemSelectInventory_Start_post(Panel_ItemSelectInventory __instance) {
			if (Conf.SimpleUI.Small_ItemSelection.Value) {
				var _gridItemList = (UIReuseGrid)__instance.GetType()
					.GetField("_gridItemList", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(__instance);

				var m_cellList = (UIReuseScrollViewCell[])_gridItemList.GetType()
					.GetField("m_cellList", BindingFlags.NonPublic | BindingFlags.Instance)
					.GetValue(_gridItemList);

				foreach (var cell in m_cellList) {
					cell.transform.localScale = new Vector3(SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO);
				}
			}

			if (Conf.SimpleUI.EnterToSearch_ItemSelection.Value) {
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
			if (!Conf.SimpleUI.Small_TempInventory.Value) return;

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
			if (!Conf.SimpleUI.Small_TempInventory.Value) return;

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
			if (!Conf.SimpleUI.Small_Consumables.Value) return;

			var _reGrid = (UIReuseGrid)__instance.GetType()
				.GetField("_reGrid", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			_reGrid.m_Column = 7;
			_reGrid.m_cellWidth = (int)(_reGrid.m_cellWidth * SMALL_CONSUMABLE_RATIO);
			_reGrid.m_cellHeight = (int)(_reGrid.m_cellHeight * SMALL_CONSUMABLE_RATIO);
		}
		private static void GridItemsPatch_Consumable_Start_post(Panel_MaterialWarehouse __instance) {
			if (!Conf.SimpleUI.Small_Consumables.Value) return;

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
			if (!Conf.SimpleUI.Sort_Consumables.Value) return;
			if (__result.Count == 0) return;

			var tableManager = (LoTableManagerClient)__instance.GetType()
				.GetField("_TableManager", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(__instance);

			if (ConsumableKeyList == null) {
				var table = tableManager._Table_ItemConsumable;
				ConsumableKeyList = table.Keys.ToArray();
			}

			__result.Sort((x, y) => Array.IndexOf(ConsumableKeyList, x.ItemKeyString) - Array.IndexOf(ConsumableKeyList, y.ItemKeyString));
		}

		private static void AccelerateScrollDelta(ref float delta) {
			if (Conf.SimpleUI.Use_AccelerateScrollDelta.Value)
				delta *= 3f;
		}

		private static void Inject_SortByName(Panel_Base __instance) {
			if (!Conf.SimpleUI.Use_SortByName.Value) return;

			var goSortPanel = (GameObject)__instance.GetType()
				.GetField("_goSortPanel", BindingFlags.Instance | BindingFlags.NonPublic)?
				.GetValue(__instance);
			if (goSortPanel == null) {
				Plugin.Logger.LogWarning("[Symphony::SimpleUI] Failed to find SortPanel");
				return;
			}

			var menu = goSortPanel.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "Menu");
			if (menu == null) {
				Plugin.Logger.LogWarning("[Symphony::SimpleUI] Failed to find Menu on SortPanel");
				return;
			}

			var elCount = menu.childCount;
			for (var i = 0; i < elCount; i++) {
				var e = menu.GetChild(i);
				e.localPosition = e.localPosition - new Vector3(0, -74, 0);
			}

			var els = menu.GetComponentsInChildren<Transform>(true);

			var _sep = els.FirstOrDefault(x => x.name == "DecoSp12");
			if (_sep == null) {
				Plugin.Logger.LogWarning("[Symphony::SimpleUI] Failed to find Separator on SortPanel Menu");
				return;
			}

			var _btn = els.FirstOrDefault(x => x.name == "Marriage");
			if (_btn == null) {
				Plugin.Logger.LogWarning("[Symphony::SimpleUI] Failed to find Marriage button on SortPanel Menu");
				return;
			}

			var sep = GameObject.Instantiate(_sep.gameObject);
			sep.name = "DecoSp_Name";
			sep.transform.SetParent(_sep.parent);
			sep.transform.localPosition = _sep.localPosition - new Vector3(0, 74, 0);
			sep.transform.localScale = Vector3.one;

			var btn = GameObject.Instantiate(_btn.gameObject);
			btn.name = "Name";
			btn.transform.SetParent(_btn.parent);
			btn.transform.localPosition = _btn.localPosition - new Vector3(0, 74, 0);
			btn.transform.localScale = Vector3.one;

			btn.GetComponentsInChildren<UILocalize>(true).ToList().ForEach(DestroyImmediate);

			var lbl = btn.GetComponentsInChildren<UILabel>(true);
			foreach (var lb in lbl) lb.text = "이름";

			var btnOff = btn.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "btn_OFF");
			var uiButton = btnOff.GetComponent<UIButton>();
			uiButton.onClick.Clear();
			uiButton.onClick.Add(new(() => {
				try {
					var lbl = btnOff.GetComponentInChildren<UILabel>(true);
					OnSortName(__instance, lbl);
				} catch (Exception e) {
					Plugin.Logger.LogError(e);
				}
			}));
		}
		private static void OnSortName(Panel_Base instance, UILabel lbl) {
			if (instance == null) {
				Plugin.Logger.LogWarning("[Symphony::SimpleUI] instance is null");
				return;
			}

			var Sorting = instance.GetType()
				.GetMethod("Sorting", BindingFlags.Instance | BindingFlags.NonPublic);
			if (Sorting == null) {
				Plugin.Logger.LogWarning("[Symphony::SimpleUI] Failed to find Sorting method");
				return;
			}

			Sorting.Invoke(instance, [new Comparison<IReuseCellData>(SortName_Comparer)]);

			var label = (UILabel)instance.GetType()
				.GetField("_lblSort", BindingFlags.Instance | BindingFlags.NonPublic)
				.GetValue(instance);
			if (label != null)
				label.text = lbl?.text ?? "이름";
		}
		private static int SortName_Comparer(IReuseCellData a, IReuseCellData b) {
			if (a.IsFirst() && !b.IsFirst()) return -1;
			if (!a.IsFirst() && b.IsFirst()) return 1;
			if (a.IsLast() && !b.IsLast()) return 1;
			if (!a.IsLast() && b.IsLast()) return -1;

			if (string.Compare(a.GetName(), b.GetName(), Common.GetCultureInfo(), CompareOptions.StringSort) > 0)
				return -SingleTon<GameManager>.Instance.InvertSort;

			if (string.Compare(a.GetName(), b.GetName(), Common.GetCultureInfo(), CompareOptions.StringSort) < 0)
				return SingleTon<GameManager>.Instance.InvertSort;

			return a.GetPCID().CompareTo(b.GetPCID());
		}

		private static void Patch_CharacterCostOff(Panel_Base __instance) {
			if (!Conf.SimpleUI.Default_CharacterCost_Off.Value) return;

			var OnBtnCost = __instance.GetType()
				.GetMethod("OnBtnCost", BindingFlags.Instance | BindingFlags.Public);
			if (OnBtnCost == null) return;

			var _costToggle = (UIToggle)__instance.GetType()
				.GetField("_costToggle", BindingFlags.Instance | BindingFlags.NonPublic)
				.GetValue(__instance);
			_costToggle.value = false;
			OnBtnCost.Invoke(__instance, []);
		}

		private static void Patch_Squad_Clear(Panel_SquadInfo __instance) {
			var btn_src = __instance.GetComponentsInChildren<UIButton>()
				.FirstOrDefault(x => x.name == "BtnPresetOn")?
				.gameObject;
			if (btn_src == null) return;

			var btn = GameObject.Instantiate<GameObject>(btn_src, btn_src.transform.parent);
			btn.name = "BtnClear";
			btn.transform.localPosition = btn_src.transform.localPosition - new Vector3(0f, 106f, 0f);
			btn.GetComponentInChildren<UILabel>().text = "CLEAR";
			btn.GetComponent<UISprite>().spriteName = "UI_Icon_SquadPreset_Trashcan";

			var _btn = btn.GetComponent<UIButton>();
			_btn.onClick.Clear();
			_btn.onClick.Add(new(() => {
				IEnumerator Fn() {
					var squad = SingleTon<DataManager>.Instance.GetCurrentSquad(SingleTon<GameManager>.Instance.SquadType);
					var chars = squad.SquadSlotList // move leader to last of list
						.Where(r => r.PCId != 0 && r.PCId != squad.LeaderPCID)
						.Concat(SingleTon<DataManager>.Instance.GetUserInfo().MasterSquadIndex == squad.SquadIndex
							? [] // exclude leader for master squad
							: squad.SquadSlotList.Where(r => r.PCId == squad.LeaderPCID)
						)
						.ToArray();

					foreach (var chr in chars) {
						SquadClear_LastUnsetPC = 0;

						// FormationCharacterPick.OnPick
						MonoSingleton<SceneBase>.Instance.ShowWaitMessage(true);
						C2WPacket.Send_C2W_UNSET_PC_TO_SQUAD(
							SingleTon<DataManager>.Instance.AccessToken,
							SingleTon<DataManager>.Instance.WID,
							chr.PCId,
							squad.SquadIndex,
							SingleTon<DataManager>.Instance.GetSquadSlotNumber(chr.PCId)
						);

						yield return new WaitUntil(() => SquadClear_LastUnsetPC == chr.PCId);
					}

					var selector = FindObjectOfType<UISquadInfoCreatureSelect>()?.gameObject;
					if (selector != null) Destroy(selector);
				}
				_btn.StartCoroutine(Fn());
			}));
		}
		private void HandlePacketUnsetPcToSquad(WebResponseState obj) {
			W2C_UNSET_PC_TO_SQUAD data = obj as W2C_UNSET_PC_TO_SQUAD;
			MonoSingleton<SceneBase>.Instance.ShowWaitMessage(false);
			if (data.result.ErrorCode != 0) return;

			SquadClear_LastUnsetPC = data.result.PCID;
		}
	}
}
