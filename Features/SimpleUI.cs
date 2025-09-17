using Com.LuisPedroFonseca.ProCamera2D;

using HarmonyLib;

using LO_ClientNetwork;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using UnityEngine;

namespace Symphony.Features {
	internal class SimpleUI : MonoBehaviour {
		private static ulong SquadClear_LastUnsetPC = 0;

		private static ButtonChangeSupport Disassemble_Char_All_Buttons = null;
		private static ButtonChangeSupport Disassemble_Equip_All_Buttons = null;

		private static UIAtlas asset_masterAtlas = null;

		public void Start() {
			var harmony = new Harmony("Symphony.SimpleUI");

			#region Bypass World Button while Offline Battle
			harmony.Patch(
				AccessTools.Method(typeof(Panel_GameModeMenu), nameof(Panel_GameModeMenu.OnBtnOfflineBattleCheck)),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.OfflineBattleBypass_Patch))
			);
			#endregion

			#region Smaller List Items
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
			#endregion

			#region Smaller Consumable List Items & Sorting
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

			#region Sort by Name
			harmony.Patch(
				AccessTools.Method(typeof(Panel_PcWarehouse), "Awake"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Inject_SortByName))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_AndroidInventory), "Awake"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Inject_SortByName))
			);
			#endregion

			#region Scroll Acceleration
			harmony.Patch(
				AccessTools.Method(typeof(UIReuseScrollView), nameof(UIReuseScrollView.Scroll)),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Accelerate_ScrollDelta))
			);
			harmony.Patch(
				AccessTools.Method(typeof(UIScrollView), nameof(UIScrollView.Scroll)),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Accelerate_ScrollDelta))
			);
			harmony.Patch(
				AccessTools.Method(typeof(UIScrollView2), nameof(UIScrollView2.Scroll)),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Accelerate_ScrollDelta))
			);

			harmony.Patch(
				AccessTools.Method(typeof(ProCamera2DPanAndZoom), "Pan"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Accelerate_CameraPanDelta))
			);
			harmony.Patch(
				AccessTools.Method(typeof(ProCamera2DPanAndZoom), "Zoom"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Accelerate_CameraZoomDelta))
			);
			#endregion

			#region Character Cost Display Defaultly Off on Character List
			harmony.Patch(
				AccessTools.Method(typeof(Panel_PcWarehouse), "Start"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_CharacterCostOff))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_AndroidInventory), "Start"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_CharacterCostOff))
			);
			#endregion

			#region Squad Clear Button
			harmony.Patch(
				AccessTools.Method(typeof(Panel_SquadInfo), "Start"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_Squad_Clear))
			);
			EventManager.StartListening(this, 12U, new Action<WebResponseState>(this.HandlePacketUnsetPcToSquad));
			#endregion

			#region Disassemble Select All Characters & Equips
			harmony.Patch(
				AccessTools.Method(typeof(Panel_PcWarehouse), "Start"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_Disassemble_AllSelect_Char))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_ItemSelectInventory), "Start"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_Disassemble_AllSelect_Equip))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_PcWarehouse), "RefreshTotalSelectBtn"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_Disassemble_Char_RefreshTotalSelectBtn))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_ItemSelectInventory), "RefreshTotalSelectBtn"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_Disassemble_Equip_RefreshTotalSelectBtn))
			);
			#endregion

			#region Scrapbook Must Be Fancy
			harmony.Patch(
				AccessTools.Method(typeof(Panel_CharacterBookDetail), nameof(Panel_CharacterBookDetail.OnBtnView)),
				transpiler: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_ScrapbookMBF_NoRotate))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_CharacterBookDetail), "change"),
				transpiler: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_ScrapbookMBF_NoRotate_final))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_CharacterBookDetail), "OnBtnView"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.ScrapbookMBF_ChangeButton))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_CharacterBookDetail), "coModeLoad"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.ScrapbookMBF_Model_BGOnLoad))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_CharacterBookDetail), "RefreshSkinAndWound"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.ScrapbookMBF_Skin_BGOnLoad))
			);
			#endregion
		}

		private static void LazyInit() {
			if (asset_masterAtlas == null) {
				asset_masterAtlas = SingleTon<ResourceManager>.Instance.LoadAtlas("masterAtlas");
			}
		}

		#region Bypass World Button while Offline Battle
		private static bool OfflineBattleBypass_Patch(Panel_GameModeMenu __instance) {
			if (!Conf.SimpleUI.Use_OfflineBattle_Bypass.Value) return true;

			__instance.OnBtnMainStroyMode(); // OnBtnOfflineBattleCheck
			return false;
		}
		#endregion

		#region Smaller Consumable List Items
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
		#endregion

		#region Smaller Consumable List Items & Sorting
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
		#endregion

		#region Scroll Acceleration
		private static void Accelerate_ScrollDelta(ref float delta) {
			if (Conf.SimpleUI.Use_AccelerateScrollDelta.Value)
				delta *= 3f;
		}
		private static void Accelerate_CameraPanDelta(ref float deltaTime) {
			if (Conf.SimpleUI.Use_AccelerateScrollDelta.Value)
				deltaTime *= 10f;
		}
		private static void Accelerate_CameraZoomDelta(ref float deltaTime) {
			if (Conf.SimpleUI.Use_AccelerateScrollDelta.Value)
				deltaTime *= -1f;
		}
		#endregion

		#region Sort by Name
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
				void OnSortName(Panel_Base instance, UILabel lbl) {
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

					int SortName_Comparer(IReuseCellData a, IReuseCellData b) {
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
					Sorting.Invoke(instance, [new Comparison<IReuseCellData>(SortName_Comparer)]);

					var label = (UILabel)instance.GetType()
						.GetField("_lblSort", BindingFlags.Instance | BindingFlags.NonPublic)
						.GetValue(instance);
					if (label != null)
						label.text = lbl?.text ?? "이름";
				}

				try {
					var lbl = btnOff.GetComponentInChildren<UILabel>(true);
					OnSortName(__instance, lbl);
				} catch (Exception e) {
					Plugin.Logger.LogError(e);
				}
			}));
		}
		#endregion

		#region Character Cost Display Defaultly Off on Character List
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
		#endregion

		#region Squad Clear Button
		private static void Patch_Squad_Clear(Panel_SquadInfo __instance) {
			if(!Conf.SimpleUI.Use_Squad_Clear.Value) return;

			var btn_src = __instance.GetComponentsInChildren<UIButton>()
				.FirstOrDefault(x => x.name == "BtnPresetOn")?
				.gameObject;
			if (btn_src == null) return;

			var btn = GameObject.Instantiate<GameObject>(btn_src, btn_src.transform.parent);
			btn.name = "BtnClear";
			btn.transform.localPosition = btn_src.transform.localPosition - new Vector3(0f, 110f, 0f);
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

						var waitFor = chr.PCId;
						yield return new WaitUntil(() => SquadClear_LastUnsetPC == waitFor);
					}

					var selector = FindObjectOfType<UISquadInfoCreatureSelect>()?.gameObject;
					if (selector != null) Destroy(selector);
				}
				_btn.StartCoroutine(Fn());
			}));
		}
		private void HandlePacketUnsetPcToSquad(WebResponseState obj) {
			W2C_UNSET_PC_TO_SQUAD data = obj as W2C_UNSET_PC_TO_SQUAD;
			if (data.result.ErrorCode != 0) return;

			SquadClear_LastUnsetPC = data.result.PCID;
		}
		#endregion

		#region Disassemble Select All Characters & Equips
		private static bool Disassemble_All_IsTargetPc(ClientPcInfo p)
			=> p.PCTable.Enchant_Exclusive == 0 && p.Level == 1 && !p.IsMarriagePc();
		private static bool Disassemble_All_IsTargetItem(ClientItemInfo e)
			=> e.TableItemEquip.Enchant_Exclusive == 0 && e.EnchantLevel == 0;

		private static void Patch_Disassemble_AllSelect_Char(Panel_PcWarehouse __instance) {
			if (!Conf.SimpleUI.Use_Disassemble_SelectAll_Character.Value) return;
			if (__instance.XGetFieldValue<byte>("_invenType") != (byte)3) return; // Panel_PcWarehouse.WAREHOUSETYPE

			// Copy button
			var btn_src = __instance.XGetFieldValue<ButtonChangeSupport>("_btnBSelect");
			if (btn_src == null) {
				Plugin.Logger.LogWarning("[Symphony::SimpleUI] Cannot find 'B Select' button");
				return;
			}

			var btnGroup = new ButtonChangeSupport();

			GameObject BuildButton(GameObject source, string name, Func<string, string> sourceLabel, string label, Action onClick) {
				source.SetActive(true); // to move and access components inactive button

				IEnumerator Reposition(UISprite a, UISprite b) {
					yield return null; // ensure run at next frame

					var yBtn = a.transform.parent.Find("btnDecision").transform.localPosition.y + 100f;
					a.SetRect(646f, yBtn, 152f, 90f);
					b.SetRect(808f, yBtn, 152f, 90f);
				}

				DestroyImmediate(source.GetComponentInChildren<UILocalize>(true));

				var deco = source.transform.Find("DecoSp").GetComponent<UISprite>();
				deco.width = 152;
				
				var lblSrc = source.GetComponentInChildren<UILabel>();
				lblSrc.text = sourceLabel?.Invoke(lblSrc.text) ?? lblSrc.text;
				lblSrc.fontSize = 28;
				
				var btn = GameObject.Instantiate(source, source.transform.parent);
				btn.name = name;
				
				var lbl = btn.GetComponentInChildren<UILabel>();
				lbl.text = label;
				
				var _btn = btn.GetComponent<UIButton>();
				if (_btn != null) {
					_btn.onClick.Clear();
					_btn.onClick.Add(new(() => onClick?.Invoke()));
				}

				__instance.StartCoroutine(Reposition(source.GetComponent<UISprite>(), btn.GetComponent<UISprite>()));

				return btn;
			}
			void UpdateList() {
				__instance.XGetFieldValue<UIReuseGrid>("_reUseGrid").UpdateAllCellData();
				__instance.XGetMethodVoid("RefreshTotalSelectBtn").Invoke();
			}

			btnGroup._enableBt = BuildButton(
				btn_src._enableBt,
				"AllSelectAll",
				(x) => x.Replace("B급", "B급\n"),
				"전체\n일괄 선택",
				() => {
					var _listCurFilter = __instance.XGetFieldValue<List<ItemCellInvenCharacter>>("_listCurFilter");
					var _listDisSelectedPc = __instance.XGetFieldValue<List<ClientPcInfo>>("_listDisSelectedPc");
					foreach (var item in _listCurFilter.FindAll(p => Disassemble_All_IsTargetPc(p.pcInfo))) {
						// Const.MAX_PC_DISASSEMBLE_CLIENT = byte.MaxValue
						if (!_listDisSelectedPc.Contains(item.pcInfo) && _listDisSelectedPc.Count >= byte.MaxValue) {
							__instance.ShowMessage(Localization.Get("818"));
							break;
						}

						item.IsSelect = true;
						if (!_listDisSelectedPc.Contains(item.pcInfo))
							_listDisSelectedPc.Add(item.pcInfo);
					}

					UpdateList();
				}
			);
			btnGroup._disableBt = BuildButton(
				btn_src._disableBt,
				"AllDisableAll",
				(x) => x.Replace("B급", "B급\n"),
				"전체\n일괄 해제",
				() => {
					var _listCurFilter = __instance.XGetFieldValue<List<ItemCellInvenCharacter>>("_listCurFilter");
					foreach (var item in _listCurFilter)
						item.IsSelect = false;

					var _listDisSelectedPc = __instance.XGetFieldValue<List<ClientPcInfo>>("_listDisSelectedPc");
					_listDisSelectedPc.Clear();

					UpdateList();
				}
			);
			btnGroup._bgBt = BuildButton(
				btn_src._bgBt,
				"NotFindAll",
				(x) => x.Replace("B급", "B급\n"),
				"전체\n일괄 선택",
				null
			);
			Disassemble_Char_All_Buttons = btnGroup;

			IEnumerator UpdateButton() {
				yield return null; // BuildButton.Reposition
				yield return null; // ensure after Reposition

				UpdateList();
			}
			__instance.StartCoroutine(UpdateButton());
		}
		private static void Patch_Disassemble_Char_RefreshTotalSelectBtn(Panel_PcWarehouse __instance) {
			var btnGroup = Disassemble_Char_All_Buttons;
			if (btnGroup == null) return;

			var _listDisSelectedPc = __instance.XGetFieldValue<List<ClientPcInfo>>("_listDisSelectedPc");
			var listAvailable = __instance.XGetFieldValue<List<ItemCellInvenCharacter>>("_listCurFilter")
				.FindAll(p => Disassemble_All_IsTargetPc(p.pcInfo) &&
					!_listDisSelectedPc.Contains(p.pcInfo)
				);
			var listSelected = _listDisSelectedPc.FindAll(Disassemble_All_IsTargetPc);

			if (listAvailable.Count == 0 && listSelected.Count == 0)
				btnGroup.ButtonAllDisable();
			else if (listAvailable.Count > 0)
				btnGroup.ButtonEnable();
			else
				btnGroup.ButtonDisable();
		}

		private static void Patch_Disassemble_AllSelect_Equip(Panel_ItemSelectInventory __instance) {
			if (!Conf.SimpleUI.Use_Disassemble_SelectAll_Equip.Value) return;
			if (__instance.XGetFieldValue<byte>("_invenType") != (byte)1) return; // Panel_ItemSelectInventory.INVENTYPE_EQUIP_DISASSEMBLE

			// Copy button
			var btn_src = __instance.XGetFieldValue<ButtonChangeSupport>("_btnBSelect");
			if (btn_src == null) {
				Plugin.Logger.LogWarning("[Symphony::SimpleUI] Cannot find 'B Select' button");
				return;
			}

			var btnGroup = new ButtonChangeSupport();

			GameObject BuildButton(GameObject source, string name, Func<string, string> sourceLabel, string label, Action onClick) {
				source.SetActive(true); // to move and access components inactive button

				IEnumerator Reposition(UISprite a, UISprite b) {
					yield return null; // ensure run at next frame

					var yBtn = a.transform.parent.Find("btnDecision").transform.localPosition.y + 110f;
					a.SetRect(-315f, yBtn, 152f, 100f);
					b.SetRect(-153f, yBtn, 152f, 100f);
				}

				DestroyImmediate(source.GetComponentInChildren<UILocalize>(true));

				var deco = source.transform.Find("DecoSp").GetComponent<UISprite>();
				deco.width = 152;

				var lblSrc = source.GetComponentInChildren<UILabel>();
				lblSrc.text = sourceLabel?.Invoke(lblSrc.text) ?? lblSrc.text;
				lblSrc.fontSize = 28;

				var btn = GameObject.Instantiate(source, source.transform.parent);
				btn.SetActive(true);
				btn.name = name;

				var lbl = btn.GetComponentInChildren<UILabel>();
				lbl.text = label;

				var _btn = btn.GetComponent<UIButton>();
				if (_btn != null) {
					_btn.onClick.Clear();
					_btn.onClick.Add(new(() => onClick?.Invoke()));
				}

				__instance.StartCoroutine(Reposition(source.GetComponent<UISprite>(), btn.GetComponent<UISprite>()));

				return btn;
			}
			void UpdateList() {
				__instance.XGetFieldValue<UIReuseGrid>("_gridItemList").UpdateAllCellData();
				__instance.XGetMethodVoid("RefreshTotalSelectBtn").Invoke();
			}

			btnGroup._enableBt = BuildButton(
				btn_src._enableBt,
				"AllSelectAll",
				(x) => x.Replace("B급", "B급\n"),
				"전체\n일괄 선택",
				() => {
					var _listCurFilter = __instance.XGetFieldValue<List<ItemCellInvenItem>>("_listCurFilter");
					var _listDisSelectedItem = __instance.XGetFieldValue<List<ClientItemInfo>>("_listDisSelectedItem");
					foreach (var item in _listCurFilter.FindAll(e => Disassemble_All_IsTargetItem(e.itemInfo))) {
						// Const.MAX_PC_DISASSEMBLE_CLIENT = byte.MaxValue
						if (!_listDisSelectedItem.Contains(item.itemInfo) && _listDisSelectedItem.Count >= byte.MaxValue) {
							__instance.ShowMessage(Localization.Get("819"));
							break;
						}

						item.IsSelect = true;
						if (!_listDisSelectedItem.Contains(item.itemInfo))
							_listDisSelectedItem.Add(item.itemInfo);
					}

					UpdateList();
				}
			);
			btnGroup._disableBt = BuildButton(
				btn_src._disableBt,
				"AllDisableAll",
				(x) => x.Replace("B급", "B급\n"),
				"전체\n일괄 해제",
				() => {
					var _listCurFilter = __instance.XGetFieldValue<List<ItemCellInvenItem>>("_listCurFilter");
					foreach (var item in _listCurFilter)
						item.IsSelect = false;

					var _listDisSelectedItem = __instance.XGetFieldValue<List<ClientItemInfo>>("_listDisSelectedItem");
					_listDisSelectedItem.Clear();

					UpdateList();
				}
			);
			btnGroup._bgBt = BuildButton(
				btn_src._bgBt,
				"NotFindAll",
				(x) => x.Replace("B급", "B급\n"),
				"전체\n일괄 선택",
				null
			);
			Disassemble_Equip_All_Buttons = btnGroup;

			IEnumerator UpdateButton() {
				yield return null; // BuildButton.Reposition
				yield return null; // ensure after Reposition

				UpdateList();
			}
			__instance.StartCoroutine(UpdateButton());
		}
		private static void Patch_Disassemble_Equip_RefreshTotalSelectBtn(Panel_ItemSelectInventory __instance) {
			var btnGroup = Disassemble_Equip_All_Buttons;
			if (btnGroup == null) return;

			var _listDisSelectedItem = __instance.XGetFieldValue<List<ClientItemInfo>>("_listDisSelectedItem");
			var listAvailable = __instance.XGetFieldValue<List<ItemCellInvenItem>>("_listCurFilter")
				.FindAll(e => Disassemble_All_IsTargetItem(e.itemInfo) &&
					!_listDisSelectedItem.Contains(e.itemInfo)
				);
			var listSelected = _listDisSelectedItem.FindAll(Disassemble_All_IsTargetItem);

			if (listAvailable.Count == 0 && listSelected.Count == 0)
				btnGroup.ButtonAllDisable();
			else if (listAvailable.Count > 0)
				btnGroup.ButtonEnable();
			else
				btnGroup.ButtonDisable();

			IEnumerator UpdateButton() {
				yield return null; // BuildButton.Reposition
				yield return null; // ensure after Reposition

				// Move at initial not work properly, so move buttons forcely
				var yBtn = btnGroup._disableBt.transform.parent.Find("btnDecision").transform.localPosition.y + 110f;
				btnGroup._disableBt.GetComponent<UISprite>()?.SetRect(-153f, yBtn, 152f, 100f);
				btnGroup._bgBt.GetComponent<UISprite>()?.SetRect(-153f, yBtn, 152f, 100f);
			}
			__instance.StartCoroutine(UpdateButton());
		}
		#endregion

		#region Scrapbook Must Be Fancy
		private static IEnumerable<CodeInstruction> Patch_ScrapbookMBF_NoRotate(MethodBase original, IEnumerable<CodeInstruction> instructions) {
			Plugin.Logger.LogInfo("[Symphony::SimpleUI] Start to patch Panel_CharacterBookDetail.OnBtnView");

			var matcher = new CodeMatcher(instructions);
			matcher.MatchForward(false,
				/* TweenRotation.Begin(this._texPc.gameObject, 0.2f, Quaternion.Euler(0.0f, 0.0f, 90f)).SetOnFinished(new EventDelegate.Callback(this.change)); */
				new CodeMatch(OpCodes.Ldarg_0), // this
				new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Panel_CharacterBookDetail), "_texPc")), // ._texPc
				new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(UIWidget), nameof(UIWidget.gameObject))), // .gameObject

				new CodeMatch(OpCodes.Ldc_R4, 0.2f), // 0.2f

				new CodeMatch(OpCodes.Ldc_R4, 0.0f), // 0.0f
				new CodeMatch(OpCodes.Ldc_R4, 0.0f), // 0.0f
				new CodeMatch(OpCodes.Ldc_R4, 90.0f), // 90.0f
				new CodeMatch(OpCodes.Call, AccessTools.Method(
					typeof(Quaternion),
					nameof(Quaternion.Euler),
					[typeof(float), typeof(float), typeof(float)]
				)), // Quaternion.Euler

				new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(TweenRotation), nameof(TweenRotation.Begin))), // TweenRotation.Begin

				new CodeMatch(OpCodes.Ldarg_0), // this
				new CodeMatch(OpCodes.Ldftn, AccessTools.Method(typeof(Panel_CharacterBookDetail), "change")), // .change

				new CodeMatch(OpCodes.Newobj), // new EventDelegate/Callback::.ctor
				new CodeMatch(OpCodes.Callvirt) // UITweener::SetOnFinished
			);;

			if (matcher.IsInvalid) {
				Plugin.Logger.LogWarning("[Symphony::SimpleUI] Failed to patch Panel_CharacterBookDetail.OnBtnView, target instructions not found");
				return instructions;
			}

			matcher.Advance(6);
			
			// Change 90f to calling ScrapbookMBF_OnView_TweenRotate
			var new_inst = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SimpleUI), nameof(SimpleUI.ScrapbookMBF_OnView_TweenRotate)));
			new_inst.labels = matcher.Instruction.labels;
			matcher.RemoveInstruction();
			matcher.Insert(new_inst);

			return matcher.InstructionEnumeration();
		}
		private static IEnumerable<CodeInstruction> Patch_ScrapbookMBF_NoRotate_final(MethodBase original, IEnumerable<CodeInstruction> instructions) {
			Plugin.Logger.LogInfo("[Symphony::SimpleUI] Start to patch Panel_CharacterBookDetail.change");

			var matcher = new CodeMatcher(instructions);
			matcher.MatchForward(false,
				/* this._cameraModel.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -90f); */
				new CodeMatch(OpCodes.Ldarg_0), // this
				new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Panel_CharacterBookDetail), "_cameraModel")), // ._cameraModel
				new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Component), "transform")), // .transform

				new CodeMatch(OpCodes.Ldc_R4, 0.0f), // 0.0f
				new CodeMatch(OpCodes.Ldc_R4, 0.0f), // 0.0f
				new CodeMatch(OpCodes.Ldc_R4, -90.0f), // -90.0f
				new CodeMatch(OpCodes.Newobj), // new Vector3::.ctor

				new CodeMatch(OpCodes.Callvirt, AccessTools.PropertySetter(typeof(Transform), nameof(Transform.localEulerAngles))) // .localEulerAngles =
			); ;

			if (matcher.IsInvalid) {
				Plugin.Logger.LogWarning("[Symphony::SimpleUI] Failed to patch Panel_CharacterBookDetail.change, target instructions not found");
				return instructions;
			}

			matcher.Advance(5);

			// Change -90f to calling ScrapbookMBF_OnView_TweenRotate_final
			var new_inst = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SimpleUI), nameof(SimpleUI.ScrapbookMBF_OnView_TweenRotate_final)));
			new_inst.labels = matcher.Instruction.labels;
			matcher.RemoveInstruction();
			matcher.Insert(new_inst);

			return matcher.InstructionEnumeration();
		}
		private static float ScrapbookMBF_OnView_TweenRotate() {
			if (!Conf.SimpleUI.Use_ScrapbookMustBeFancy.Value) return 90f;
			return 0f;
		}
		private static float ScrapbookMBF_OnView_TweenRotate_final() {
			if (!Conf.SimpleUI.Use_ScrapbookMustBeFancy.Value) return -90f;
			return 0f;
		}
		private static void ScrapbookMBF_ChangeButton(Panel_CharacterBookDetail __instance) {
			LazyInit();

			if (!Conf.SimpleUI.Use_ScrapbookMustBeFancy.Value) return;

			var _model = __instance.XGetFieldValue<GameObject>("_model");
			if (_model == null) {
				Plugin.Logger.LogWarning("[Symphony::SimpleUI] _model not found");
				return;
			}

			bool hasBg = new Func<bool>(() => {
				if (_model.TryGetComponent<ActorSpinePartsView>(out var aspv))
					return aspv.IsUseBg();
				else if (_model.TryGetComponent<ActorPartsView>(out var apv))
					return apv.IsUseBg();
				return false;
			}).Invoke();
			bool hasParts = new Func<bool>(() => {
				if (_model.TryGetComponent<ActorSpinePartsView>(out var aspv))
					return aspv.IsUseParts();
				else if (_model.TryGetComponent<ActorPartsView>(out var apv))
					return apv.IsUseParts();
				return false;
			}).Invoke();

			bool ToggleBG() {
				var active = false;
				if (_model.TryGetComponent<ActorSpinePartsView>(out var aspv)) {
					active = !aspv.IsBgActive();
					aspv.SetBgView(active);
				}
				else if (_model.TryGetComponent<ActorPartsView>(out var apv)) {
					active = !apv.IsBgActive();
					apv.SetBgView(active);
				}
				return active;
			}
			bool ToggleParts() {
				var active = false;
				if (_model.TryGetComponent<ActorSpinePartsView>(out var aspv)) {
					active = !aspv.IsPartsActive();
					aspv.SetPartsView(active);
				}
				else if (_model.TryGetComponent<ActorPartsView>(out var apv)) {
					active = !apv.IsPartsActive();
					apv.SetPartsView(active);
				}
				return active;
			}

			var goModeChange = __instance.XGetFieldValue<GameObject>("_goModeChange");
			goModeChange.SetActive(false);

			var goScreenshot = __instance.XGetFieldValue<GameObject>("_goScreenShot");
			goScreenshot.SetActive(hasBg);
			if (hasBg) {
				goScreenshot.transform.localEulerAngles = new Vector3(0f, 0f, -90f);

				var sp = goScreenshot.GetComponent<UISprite>();
				sp.atlas = asset_masterAtlas;

				var bgsp = goScreenshot.transform.Find("GameObject")?.GetComponent<UISprite>();
				if (bgsp != null) bgsp.color = new Color(0f, 0f, 0f, 0.7373f);

				var btn = goScreenshot.GetComponent<UIButton>();
				btn.normalSprite = "newui_Lobby_Icon_bg_on";
				btn.onClick.Clear();
				btn.onClick.Add(new(() => {
					var active = ToggleBG();
					btn.normalSprite = active ? "newui_Lobby_Icon_bg_on" : "newui_Lobby_Icon_bg_off";
				}));
			}

			var goFacingCamera = __instance.XGetFieldValue<GameObject>("_goFacingCamera");
			goFacingCamera.SetActive(hasParts);
			if (hasParts) {
				goFacingCamera.transform.localEulerAngles = new Vector3(0f, 0f, -90f);

				var sp = goFacingCamera.GetComponent<UISprite>();
				sp.atlas = asset_masterAtlas;

				var bgsp = goFacingCamera.transform.Find("GameObject")?.GetComponent<UISprite>();
				if (bgsp != null) bgsp.color = new Color(0f, 0f, 0f, 0.7373f);

				var btn = goFacingCamera.GetComponent<UIButton>();
				btn.normalSprite = "newui_Lobby_Icon_Props_on";
				btn.onClick.Clear();
				btn.onClick.Add(new(() => {
					var active = ToggleParts();
					btn.normalSprite = active ? "newui_Lobby_Icon_Props_on" : "newui_Lobby_Icon_Props_off";
				}));
			}
		}

		private static void ScrapbookMBF_Skin_BGOnLoad(Panel_CharacterBookDetail __instance) {
			if (!Conf.SimpleUI.Use_ScrapbookMustBeFancy.Value) return;

			IEnumerator Fn() {
				yield return null;

				var _model = __instance.XGetFieldValue<GameObject>("_model");
				if (_model == null) {
					Plugin.Logger.LogWarning("[Symphony::SimpleUI] _model not found");
					yield break;
				}

				if (_model.TryGetComponent<ActorSpinePartsView>(out var aspv)) {
					if (aspv.IsUseBg())
						aspv.SetBgView(true);
				}
				else if (_model.TryGetComponent<ActorPartsView>(out var apv)) {
					if (apv.IsUseBg())
						apv.SetBgView(true);
				}
			}
			__instance.StartCoroutine(Fn());
		}
		private static void ScrapbookMBF_Model_BGOnLoad(Panel_CharacterBookDetail __instance, ref IEnumerator __result) {
			if (!Conf.SimpleUI.Use_ScrapbookMustBeFancy.Value) return;

			var orig = __result;
			IEnumerator Fn() {
				while (orig.MoveNext())
					yield return orig.Current;

				if (!Conf.SimpleUI.Use_ScrapbookMustBeFancy.Value) yield break;

				ScrapbookMBF_Skin_BGOnLoad(__instance);
			}
			__result = Fn();
		}
		#endregion
	}
}
