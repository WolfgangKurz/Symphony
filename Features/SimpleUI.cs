using Com.LuisPedroFonseca.ProCamera2D;

using GlobalDefines;

using HarmonyLib;

using LO_ClientNetwork;

using LOEventSystem;

using LOEventSystem.Msg;

using Symphony.Features.SimpleUIHelper;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using UnityEngine;

namespace Symphony.Features {
	[Feature("SimpleUI")]
	internal class SimpleUI : MonoBehaviour {
		private static ulong SquadClear_LastUnsetPC = 0;

		private static ButtonChangeSupport Disassemble_Char_All_Buttons = null;
		private static ButtonChangeSupport Disassemble_Equip_All_Buttons = null;

		private static UIAtlas asset_masterAtlas = null;

		public void Start() {
			var harmony = new Harmony("Symphony.SimpleUI");

			#region Battle
			#region Last Battle Map & Last Offline Battle
			harmony.Patch(
				AccessTools.Method(typeof(Panel_GameModeMenu), "Start"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.LastBattleMap_Panel_GameModeMenu_Start))
			);
			harmony.Patch(
				AccessTools.Method(typeof(SceneStageBattle), "Start"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Memorize_LastBattleMap))
			);
			harmony.Patch(
				AccessTools.PropertySetter(typeof(DataManager), nameof(DataManager.OfflineBattleInfo)),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Memorize_LastOfflineBattle))
			);
			#endregion

			#region Bypass World Button while Offline Battle
			harmony.Patch(
				AccessTools.Method(typeof(Panel_GameModeMenu), nameof(Panel_GameModeMenu.OnBtnOfflineBattleCheck)),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.OfflineBattleBypass_Patch))
			);
			#endregion

			#region Map Enemy Preview
			harmony.Patch(
				AccessTools.Method(typeof(Panel_StageDetail), nameof(Panel_StageDetail.SetMapStage)),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_MapEnemyPreview_SetMapStage))
			);
			#endregion
			#endregion

			#region ListItemDisplay + ListSearch
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

			#region Character List DoubleClick
			harmony.Patch(
				AccessTools.Method(typeof(Panel_PcWarehouse), nameof(Panel_PcWarehouse.ToogleChange)),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_PCWarehouse_DoubleClick))
			);
			#endregion

			#region Smaller List Items + Enter To Search (in XXX_post)
			harmony.Patch(
				AccessTools.Method(typeof(Panel_PcWarehouse), "Start"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_PCWarehouse_Start_pre)),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_PCWarehouse_Start_post))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_AideInventory), "Start"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_AideInventory_Start_pre)),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.GridItemsPatch_AideInventory_Start_post))
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
			#endregion
			#endregion

			#region ListFilter
			#region Consumable List Sorting
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

			#region Sort by XXX
			harmony.Patch(
				AccessTools.Method(typeof(Panel_PcWarehouse), "Awake"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Inject_SortByXXX_pre))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_AndroidInventory), "Awake"),
				prefix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Inject_SortByXXX_pre))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_BasePc), "Start"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Inject_SortByXXX_post))
			);
			#endregion

			#region Advanced Filter
			harmony.Patch(
				AccessTools.Method(typeof(Panel_PcWarehouse), "Start"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Inject_AdvancedFilter))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_AndroidInventory), "Start"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Inject_AdvancedFilter))
			);
			#endregion
			#endregion

			#region CharacterDetail
			harmony.Patch(
				AccessTools.Method(typeof(Panel_CharacterDetails), nameof(Panel_CharacterDetails.Start)),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_CharacterDetail_NextPre))
			);
			#endregion

			#region Workbench
			#region Preview Making
			harmony.Patch(
				AccessTools.Method(typeof(UIUnitMake), "SetMaking"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_Preview_UnitMaking_Display))
			);
			harmony.Patch(
				AccessTools.Method(typeof(UIUnitMake), "SetComplate"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_Preview_UnitMaking_Display))
			);
			harmony.Patch(
				AccessTools.Method(typeof(UIUnitMake), "SetReady"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_Preview_UnitMaking_Display_Clear))
			);

			harmony.Patch(
				AccessTools.Method(typeof(UIEquipMake), "SetMaking"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_Preview_EquipMaking_Display))
			);
			harmony.Patch(
				AccessTools.Method(typeof(UIEquipMake), "SetComplate"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_Preview_EquipMaking_Display))
			);
			harmony.Patch(
				AccessTools.Method(typeof(UIEquipMake), "SetReady"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_Preview_EquipMaking_Display_Clear))
			);
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
			#endregion

			#region Composite
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

			#region Exchange: No messy hand
			harmony.Patch(
				AccessTools.Method(typeof(Panel_ExShop), nameof(Panel_ExShop.Start)),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_Exchange_HideSoldOut))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_ExShop), "RefreshConsumableView"),
				transpiler: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_Exchange_ConsumableList))
			);
			#endregion
			#endregion

			#region Squad Clear Button
			harmony.Patch(
				AccessTools.Method(typeof(Panel_SquadInfo), "Start"),
				postfix: new HarmonyMethod(typeof(SimpleUI), nameof(SimpleUI.Patch_Squad_Clear))
			);
			EventManager.StartListening(this, 12U, new Action<WebResponseState>(this.HandlePacketUnsetPcToSquad));
			EventManager.StartListening(this, 197U, new Action<WebResponseState>(this.HandlePacketInfiniteWarUnsetPcToSquad));
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
		}

		private static void LazyInit() {
			if (asset_masterAtlas == null) {
				asset_masterAtlas = SingleTon<ResourceManager>.Instance.LoadAtlas("masterAtlas");
			}
		}

		#region Battle
		#region Last Battle Map & Last Offline Battle
		private static void LastBattleMap_Panel_GameModeMenu_Start(Panel_GameModeMenu __instance) {
			if (!Conf.SimpleUI.Use_LastBattleMap.Value && !Conf.SimpleUI.Use_LastOfflineBattle.Value) return;

			Atlas.Setup_Atlas();

			var use_lastBattle = Conf.SimpleUI.Use_LastBattleMap.Value;
			var use_lastOffline = Conf.SimpleUI.Use_LastOfflineBattle.Value;

			#region Resize and Move down `goMain`
			var goMain = (GameObject)__instance.GetType()
				.GetField("_goMain", BindingFlags.Instance | BindingFlags.NonPublic)
				.GetValue(__instance);

			{ // goBtn adjust
				var bgsp = goMain.transform.Find("BgSp");
				bgsp.localPosition = new Vector3(bgsp.localPosition.x, 15f, bgsp.localPosition.z);

				var box = goMain.GetComponent<BoxCollider>();
				box.center = new Vector3(-20.96f, -113.26f, 0f);
				box.size = new Vector3(646.504f, 247.321f, 0f);

				var sp = bgsp.GetComponent<UISprite>();
				sp.atlas = Atlas.atlas;
				sp.spriteName = "UI_SelectWorldBtn_MainStory_Small";
				sp.height = 280;

				var lb_Title = goMain.transform.Find("ChapterTitleLb");
				lb_Title.transform.localPosition -= new Vector3(0f, 70f, 0f);

				var lb_Num = goMain.transform.Find("ChapterNumLb");
				lb_Num.transform.localPosition -= new Vector3(0f, 70f, 0f);

				var lb_Text = goMain.transform.Find("TextSetPositionUiSprite");
				lb_Text.transform.localPosition -= new Vector3(0f, 70f, 0f);
			}
			#endregion

			if (use_lastBattle && use_lastOffline) {
				string[] names = ["LastBattle", "LastOfflineBattle"];
				string[] labels = ["마지막 전투 지역", "마지막 자율 전투"];
				Table_MapStage[] maps = [
					SingleTon<DataManager>.Instance.GetTableChapterStage(Conf.SimpleUI.LastBattleMapKey.Value),
					SingleTon<DataManager>.Instance.GetTableChapterStage(Conf.SimpleUI.LastOfflineBattleKey.Value),
				];
				for(var i=0; i<maps.Length;i++) {
					var map = maps[i];
					var chapter = map != null
						? SingleTon<DataManager>.Instance.GetTableMapChapter(map?.ChapterIndex)
						: null;

					if (!string.IsNullOrEmpty(chapter?.Event_Category)) {
						var evChapter = SingleTon<DataManager>.Instance.GetTableEventChapter(chapter.Key);
						if (evChapter.Event_OpenType == 0) { // Closed event
							Plugin.Logger.LogWarning("[Symphony::SimpleUI] Last visited map was event and closed, reset to none");

							if (use_lastBattle)
								Conf.SimpleUI.LastBattleMapKey.Value = "";
							else
								Conf.SimpleUI.LastOfflineBattleKey.Value = "";
							map = null;
							chapter = null;
						}
					}

					if (!string.IsNullOrEmpty(chapter?.Event_Category)) {
						var evChapter = SingleTon<DataManager>.Instance.GetTableEventChapter(chapter.Key);
						if (evChapter.Event_OpenType == 0) { // Closed event
							Plugin.Logger.LogWarning("[Symphony::SimpleUI] Last visited map was event and closed, reset to none");

							if (use_lastBattle)
								Conf.SimpleUI.LastBattleMapKey.Value = "";
							else
								Conf.SimpleUI.LastOfflineBattleKey.Value = "";
							map = null;
							chapter = null;
						}
					}

					var btn = GameObject.Instantiate(goMain);
					btn.name = names[i];
					btn.transform.SetParent(goMain.transform.parent);
					btn.transform.localScale = goMain.transform.localScale;
					btn.transform.localPosition = goMain.transform.localPosition
						+ new Vector3(323.252f * i, 0f);

					{ // btn adjust
						var bgsp = btn.transform.Find("BgSp");
						bgsp.localPosition = new Vector3(bgsp.localPosition.x, 270f, bgsp.localPosition.z);

						var box = btn.GetComponent<BoxCollider>();
						box.center = new Vector3(-20.96f - 323.252f / 2f, 141.74f, 0f);
						box.size = new Vector3(323.252f, 247.321f, 0f);

						var sp = bgsp.GetComponent<UISprite>();
						sp.atlas = Atlas.atlas;
						sp.spriteName = "UI_SelectWorldBtn_MainStory_Small_Half";
						sp.width = 350;
						sp.height = 280;

						var chapterName = !string.IsNullOrEmpty(chapter?.Event_Category)
							? SingleTon<DataManager>.Instance.GetTableEventChapterByCategory()
								.Find(x => x.Event_Category == chapter.Event_Category)
								.Event_CategoryName.Localize()
							: chapter?.ChapterString ?? "";

						var lb_Title = btn.transform.Find("ChapterTitleLb");
						lb_Title.transform.localPosition += new Vector3(-10f, 300f, 0f);
						{
							var cp = lb_Title.GetComponent<UILabel>();
							cp.fontSize = 32;
							cp.overflowMethod = UILabel.Overflow.ClampContent;
							cp.width = 224;
							cp.text = map?.StageName?.Localize() ?? "";
						}

						var lb_Num = btn.transform.Find("ChapterNumLb");
						lb_Num.transform.localPosition += new Vector3(-10f, 300f, 0f);
						{
							var cp = lb_Num.GetComponent<UILabel>();
							cp.pivot = UIWidget.Pivot.TopLeft;
							cp.fontSize = 28;
							cp.overflowMethod = UILabel.Overflow.ClampContent;
							cp.width = 184;
							cp.height = 56;
							cp.text = map != null ? $"{chapterName}. {map.StageIdxString}" : "";
						}

						var lb_Text = btn.transform.Find("TextSetPositionUiSprite");
						lb_Text.transform.localPosition += new Vector3(-10f, 270f, 0f);

						lb_Text.Find("!").gameObject.SetActive(false);

						var lb_Text_Title = lb_Text.Find("TitleLb");
						Destroy(lb_Text_Title.GetComponent<UILocalize>());
						{
							var cp = lb_Text_Title.GetComponent<UILabel>();
							cp.text = labels[i];
							cp.fontSize = 32;
						}

						var uiBtn = btn.GetComponent<UIButton>();
						uiBtn.onClick.Clear();
						uiBtn.onClick.Add(new(() => {
							if (map == null) return;

							SingleTon<GameManager>.Instance.MapInit();
							SingleTon<GameManager>.Instance.MapChapter = chapter;
							if (string.IsNullOrEmpty(chapter?.Event_Category)) {
								SingleTon<GameManager>.Instance.MapStage = map;
								SingleTon<GameManager>.Instance.GameMode = GAME_MODE.STORY;
								SingleTon<GameManager>.Instance.PlayMapStage = map;
							}
							else {
								SingleTon<GameManager>.Instance.MapEventChapter = SingleTon<DataManager>.Instance.GetTableEventChapter(chapter.Key);
								SingleTon<GameManager>.Instance.GameMode = GAME_MODE.EVENT;
								SingleTon<GameManager>.Instance.PlayMapStage = map;
							}

							Handler.Broadcast(new SceneChange(Const.Scene_World)); // __instance.ShowScene(Const.Scene_World);
						}));
					}
				}
			}
			else {
				var map = SingleTon<DataManager>.Instance.GetTableChapterStage(
					use_lastBattle
						? Conf.SimpleUI.LastBattleMapKey.Value
						: Conf.SimpleUI.LastOfflineBattleKey.Value
				);
				var chapter = map != null
					? SingleTon<DataManager>.Instance.GetTableMapChapter(map?.ChapterIndex)
					: null;

				if (!string.IsNullOrEmpty(chapter?.Event_Category)) {
					var evChapter = SingleTon<DataManager>.Instance.GetTableEventChapter(chapter.Key);
					if (evChapter.Event_OpenType == 0) { // Closed event
						Plugin.Logger.LogWarning("[Symphony::SimpleUI] Last visited map was event and closed, reset to none");

						if (use_lastBattle)
							Conf.SimpleUI.LastBattleMapKey.Value = "";
						else
							Conf.SimpleUI.LastOfflineBattleKey.Value = "";
						map = null;
						chapter = null;
					}
				}

				var btn = GameObject.Instantiate(goMain);
				btn.name = use_lastBattle ? "LastBattle" : "LastOfflineBattle";
				btn.transform.SetParent(goMain.transform.parent);
				btn.transform.localScale = goMain.transform.localScale;
				btn.transform.localPosition = goMain.transform.localPosition;

				{ // btn adjust
					var bgsp = btn.transform.Find("BgSp");
					bgsp.localPosition = new Vector3(bgsp.localPosition.x, 270f, bgsp.localPosition.z);

					var box = btn.GetComponent<BoxCollider>();
					box.center = new Vector3(-20.96f, 141.74f, 0f);
					box.size = new Vector3(646.504f, 247.321f, 0f);

					var sp = bgsp.GetComponent<UISprite>();
					sp.atlas = Atlas.atlas;
					sp.spriteName = "UI_SelectWorldBtn_MainStory_Small";
					sp.height = 280;

					var chapterName = !string.IsNullOrEmpty(chapter?.Event_Category)
						? SingleTon<DataManager>.Instance.GetTableEventChapterByCategory()
							.Find(x => x.Event_Category == chapter.Event_Category)
							.Event_CategoryName.Localize()
						: chapter?.ChapterString ?? "";

					var lb_Title = btn.transform.Find("ChapterTitleLb");
					lb_Title.transform.localPosition += new Vector3(0f, 260f, 0f);
					lb_Title.GetComponent<UILabel>().text = map?.StageName?.Localize() ?? "";

					var lb_Num = btn.transform.Find("ChapterNumLb");
					lb_Num.transform.localPosition += new Vector3(0f, 260f, 0f);
					lb_Num.GetComponent<UILabel>().text = map != null
						? $"{chapterName}. {map.StageIdxString}"
						: "";

					var lb_Text = btn.transform.Find("TextSetPositionUiSprite");
					lb_Text.transform.localPosition += new Vector3(0f, 260f, 0f);

					lb_Text.Find("!").gameObject.SetActive(false);
					var lb_Text_Title = lb_Text.Find("TitleLb");
					Destroy(lb_Text_Title.GetComponent<UILocalize>());
					lb_Text_Title.GetComponent<UILabel>().text = use_lastBattle ? "마지막 전투 지역" : "마지막 자율 전투";

					var uiBtn = btn.GetComponent<UIButton>();
					uiBtn.onClick.Clear();
					uiBtn.onClick.Add(new(() => {
						if (map == null) return;

						SingleTon<GameManager>.Instance.MapInit();
						if (string.IsNullOrEmpty(chapter?.Event_Category)) {
							SingleTon<GameManager>.Instance.MapStage = map;
							SingleTon<GameManager>.Instance.GameMode = GAME_MODE.STORY;
						}
						else {
							SingleTon<GameManager>.Instance.MapEventChapter = SingleTon<DataManager>.Instance.GetTableEventChapter(chapter.Key);
							SingleTon<GameManager>.Instance.GameMode = GAME_MODE.EVENT;
						}
						Handler.Broadcast(new SceneChange(Const.Scene_World)); // __instance.ShowScene(Const.Scene_World);
					}));
				}
			}
		}

		private static void Memorize_LastBattleMap() {
			var map = SingleTon<GameManager>.Instance.MapStage;
			if (map == null) return;

			if (map.GameModeType == (int)GAME_MODE.STORY || map.GameModeType == (int)GAME_MODE.EVENT) {
				if (Conf.SimpleUI.Use_LastBattleMap.Value)
					Plugin.Logger.LogInfo("[Symphony::SimpleUI] Last battle stage is " + map.Key);

				// Last visited battle map always be logged
				Conf.SimpleUI.LastBattleMapKey.Value = map.Key;
			}
		}
		private static void Memorize_LastOfflineBattle(AutoRepeatInfo value) {
			if (Conf.SimpleUI.Use_LastOfflineBattle.Value)
				Plugin.Logger.LogInfo("[Symphony::SimpleUI] Last offline battle stage is " + value.StageKey);

			if (value != null && !string.IsNullOrEmpty(value.StageKey))
				Conf.SimpleUI.LastOfflineBattleKey.Value = value.StageKey;
		}
		#endregion

		#region Bypass World Button while Offline Battle
		private static bool OfflineBattleBypass_Patch(Panel_GameModeMenu __instance) {
			if (!Conf.SimpleUI.Use_OfflineBattle_Bypass.Value) return true;

			__instance.OnBtnMainStroyMode(); // OnBtnOfflineBattleCheck
			return false;
		}
		#endregion

		#region Map Enemy Preview
		private static void Patch_MapEnemyPreview_SetMapStage(Panel_StageDetail __instance, Table_MapStage mapStage) {
			if (!Conf.SimpleUI.Use_MapEnemyPreview.Value) return;

			var goCommon = __instance.XGetFieldValue<GameObject>("_goCommonStage");
			if (goCommon == null) return;

			try {
				var SquadSelectEW = SingleTon<ResourceManager>.Instance.LoadUIPrefab("Panel_SquadSelectEW");
				var RightMenuParent = GameObject.Instantiate(
					SquadSelectEW.transform.Find("rightmenu").gameObject,
					goCommon.transform
				);

				Destroy(RightMenuParent.transform.Find("Battle_Title").gameObject);
				Destroy(RightMenuParent.transform.Find("Battle_Option").gameObject);

				var goTabs = RightMenuParent.transform.Find("Tabs").gameObject;
				var goTabInfos = RightMenuParent.transform.Find("TabInfos").gameObject;

				var TabInfos = new GameObject[] {
					goTabInfos.transform.GetChild(0).gameObject,
					goTabInfos.transform.GetChild(1).gameObject,
				};
				TabInfos[1].transform.DestroyChildren();
				Destroy(goTabInfos.transform.GetChild(2).gameObject);

				var toggles = new UIToggle[] {
					goTabs.transform.GetChild(0).GetComponent<UIToggle>(),
					goTabs.transform.GetChild(1).GetComponent<UIToggle>(),
				};
				Destroy(goTabs.transform.GetChild(2).gameObject);

				RightMenuParent.transform.localPosition = new Vector3(-540f, 420f, 0f);
				TabInfos[0].transform.localPosition += new Vector3(120f, -50f, 0f);
				TabInfos[1].transform.localPosition = new Vector3(660f, 180f, 0f);

				for (var btnIdx = 0; btnIdx < toggles.Length; btnIdx++) {
					var _btnIdx = btnIdx;
					var go_TabButton = toggles[btnIdx].gameObject;
					var btn = go_TabButton.GetComponent<UIButton>();
					btn.onClick.Clear();
					btn.onClick.Add(new(() => {
						for (var i = 0; i < TabInfos.Length; i++)
							TabInfos[i].SetActive(i == _btnIdx);
					}));
				}

				goCommon.transform.Find("DecoSp (1)").gameObject.SetActive(false);

				var pcList = goCommon.transform.Find("MissingPCList");
				pcList.SetParent(TabInfos[1].transform);
				pcList.localPosition = Vector3.zero;

				var itemList = goCommon.transform.Find("ItemList");
				itemList.SetParent(TabInfos[1].transform);
				itemList.localPosition = Vector3.zero;

				var clearLb = goCommon.transform.Find("ClearLb");
				clearLb.SetParent(TabInfos[1].transform);
				clearLb.localPosition = new Vector3(-850f, -345f, 0f);

				var clearCondition = goCommon.transform.Find("ClearCondition");
				clearCondition.SetParent(TabInfos[1].transform);
				clearCondition.localPosition = new Vector3(-120f, -595f, 0f);

				// Make grid
				var _gridMonsterInfo = TabInfos[0].GetComponentInChildren<UIGrid>(true);
				var _uiCenterOnMonster = TabInfos[0].GetComponentInChildren<UICenterOnChild>(true);

				{
					var btnLeft = TabInfos[0].transform.Find("LeftArrow").GetComponent<UIButton>();
					btnLeft.onClick.Clear();
					btnLeft.onClick.Add(new(() => _uiCenterOnMonster.Previous(false)));
					btnLeft.GetComponent<UISprite>().depth = 3;

					var btnRight = TabInfos[0].transform.Find("RightArrow").GetComponent<UIButton>();
					btnRight.onClick.Clear();
					btnRight.onClick.Add(new(() => _uiCenterOnMonster.Next(false)));
					btnRight.GetComponent<UISprite>().depth = 3;
				}

				_gridMonsterInfo.transform.DestroyChildren();

				void GridFn(List<string> grp, int id) {
					if (grp != null && grp.Count > 0) {
						var tmg = SingleTon<DataManager>.Instance.GetTableMobGroup(grp[0]);
						if (tmg != null)
							_gridMonsterInfo.gameObject.AddChild(SingleTon<ResourceManager>.Instance.LoadUIPrefab("UIStageMonsterInfo"))
								.GetComponent<UIStageMonsterInfo>()
								.SetData(tmg, id);
					}
				}
				GridFn(mapStage.WaveMobGroup1, 1);
				GridFn(mapStage.WaveMobGroup2, 2);
				GridFn(mapStage.WaveMobGroup3, 3);
				GridFn(mapStage.WaveMobGroup4, 4);
				GridFn(mapStage.WaveMobGroup5, 5);

				_gridMonsterInfo.repositionNow = true;

				toggles[0].GetComponent<UIWidget>().depth = 2;
				toggles[1].GetComponent<UIWidget>().depth = 2;

				foreach (var lbl in toggles[1].GetComponentsInChildren<UILabel>(true)) {
					var loc = lbl.GetComponent<UILocalize>();
					if (loc != null) Destroy(loc);
					lbl.text = "스테이지 정보";
				}

				IEnumerator LazyStart() {
					yield return null;
					EventDelegate.Execute(toggles[1].GetComponent<UIButton>().onClick);
					toggles[1].XGetMethodVoid("OnClick").Invoke();
				}
				__instance.StartCoroutine(LazyStart());
			} catch (Exception e) {
				Plugin.Logger.LogError(e);
			}
		}
		#endregion
		#endregion

		#region ListItemDisplay + ListSearch
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

		#region Character List DoubleClick
		private static void Patch_PCWarehouse_DoubleClick(Panel_PcWarehouse __instance) {
			// Transpiling make broken IL codes
			if (!Conf.SimpleUI.DblClick_CharWarehouse.Value) return;

			// 2 = Panel_PcWarehouse.WAREHOUSETYPE.INVENTYPE_WAREHOUSE
			if (__instance.XGetFieldValue<byte>("_invenType") == 2) {
				var _grid = __instance.XGetFieldValue<UIReuseGrid>("_reUseGrid");
				var m_listData = _grid.XGetFieldValue<List<IReuseCellData>>("m_listData");
				foreach (var _cell in m_listData) {
					var cell = _cell as ItemCellInvenCharacter;
					cell.callbackDoubleClick = () => {
						var _selected = __instance.XGetFieldValue<ClientPcInfo>("_SelectPCInfo");
						if (!_selected.IsModulePc()) __instance.OnBtnDetailGo();
					};
				}
			}
		}
		#endregion

		#region Smaller List Items
		private const float SMALL_ORIGINAL6_RATIO = (1f / 8f * 6f); // 6 -> 8
		private const float SMALL_ORIGINAL5_RATIO = (1f / 7f * 5f); // 5 -> 7
		private const float SMALL_CONSUMABLE_RATIO = (1f / 7f * 6f); // 6 -> 7
		private static void GridItemsPatch_PCWarehouse_Start_pre(Panel_PcWarehouse __instance) {
			if (!Conf.SimpleUI.Small_CharWarehouse.Value) return;

			var _reUseGrid = __instance.XGetFieldValue<UIReuseGrid>("_reUseGrid");

			_reUseGrid.m_Column = 8;
			_reUseGrid.m_cellWidth = (int)(_reUseGrid.m_cellWidth * SMALL_ORIGINAL6_RATIO);
			_reUseGrid.m_cellHeight = (int)(_reUseGrid.m_cellHeight * SMALL_ORIGINAL6_RATIO);

			__instance.HeightInvenSquad = (int)(__instance.HeightInvenSquad * SMALL_ORIGINAL6_RATIO);
		}
		private static void GridItemsPatch_PCWarehouse_Start_post(Panel_PcWarehouse __instance) {
			if (Conf.SimpleUI.Small_CharWarehouse.Value) {
				var _reUseGrid = __instance.XGetFieldValue<UIReuseGrid>("_reUseGrid");
				var m_cellList = _reUseGrid.XGetFieldValue<UIReuseScrollViewCell[]>("m_cellList");

				foreach (var cell in m_cellList) {
					cell.transform.localScale = new Vector3(SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO);
				}
			}

			if (Conf.SimpleUI.EnterToSearch_CharWarehouse.Value) {
				var _inputSearch = __instance.XGetFieldValue<UIInput>("_inputSearch");
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
		private static void GridItemsPatch_AideInventory_Start_pre(Panel_AideInventory __instance) {
			if (!Conf.SimpleUI.Small_CharSelection.Value) return;

			var _reUseGrid = __instance.XGetFieldValue<UIReuseGrid>("_reUseGrid");

			_reUseGrid.m_Column = 8;
			_reUseGrid.m_cellWidth = (int)(_reUseGrid.m_cellWidth * SMALL_ORIGINAL6_RATIO);
			_reUseGrid.m_cellHeight = (int)(_reUseGrid.m_cellHeight * SMALL_ORIGINAL6_RATIO);
		}
		private static void GridItemsPatch_AideInventory_Start_post(Panel_AideInventory __instance) {
			if (Conf.SimpleUI.Small_CharSelection.Value) {
				var _reUseGrid = __instance.XGetFieldValue<UIReuseGrid>("_reUseGrid");
				var m_cellList = _reUseGrid.XGetFieldValue<UIReuseScrollViewCell[]>("m_cellList");

				foreach (var cell in m_cellList) {
					cell.transform.localScale = new Vector3(SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO);
				}
			}

			if (Conf.SimpleUI.EnterToSearch_CharSelection.Value) {
				var _inputSearch = __instance.XGetFieldValue<UIInput>("_inputSearch");
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

			var _reUseGrid = __instance.XGetFieldValue<UIReuseGrid[]>("_reUseGrid");
			foreach (var grid in _reUseGrid) {
				grid.m_Column = 8;
				grid.m_cellWidth = (int)(grid.m_cellWidth * SMALL_ORIGINAL6_RATIO);
				grid.m_cellHeight = (int)(grid.m_cellHeight * SMALL_ORIGINAL6_RATIO);
			}
		}
		private static void GridItemsPatch_AndroidInventory_Start_post(Panel_AndroidInventory __instance) {
			if (Conf.SimpleUI.Small_CharSelection.Value) {
				var _reUseGrid = __instance.XGetFieldValue<UIReuseGrid[]>("_reUseGrid");
				foreach (var grid in _reUseGrid) {
					var m_cellList = grid.XGetFieldValue<UIReuseScrollViewCell[]>("m_cellList");
					foreach (var cell in m_cellList) {
						cell.transform.localScale = new Vector3(SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO);
					}
				}
			}

			if (Conf.SimpleUI.EnterToSearch_CharSelection.Value) {
				var _inputSearch = __instance.XGetFieldValue<UIInput>("_inputSearch");
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

			var _grid = __instance.XGetFieldValue<UIReuseGrid>("_grid");
			_grid.m_Column = 8;
			_grid.m_cellWidth = (int)(_grid.m_cellWidth * SMALL_ORIGINAL6_RATIO);
			_grid.m_cellHeight = (int)(_grid.m_cellHeight * SMALL_ORIGINAL6_RATIO);
		}
		private static void GridItemsPatch_CharacterBook_Start_post(Panel_CharacterBook __instance) {
			if (!Conf.SimpleUI.Small_ItemWarehouse.Value) return;

			var _grid = __instance.XGetFieldValue<UIReuseGrid>("_grid");
			var m_cellList = _grid.XGetFieldValue<UIReuseScrollViewCell[]>("m_cellList");
			foreach (var cell in m_cellList) {
				cell.transform.localScale = new Vector3(SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO);
			}
		}
		private static void GridItemsPatch_ItemInventory_Start_pre(Panel_ItemInventory __instance) {
			if (!Conf.SimpleUI.Small_ItemWarehouse.Value) return;

			var _gridItemList = __instance.XGetFieldValue<UIReuseGrid>("_gridItemList");
			_gridItemList.m_Column = 7;
			_gridItemList.m_cellWidth = (int)(_gridItemList.m_cellWidth * SMALL_ORIGINAL5_RATIO);
			_gridItemList.m_cellHeight = (int)(_gridItemList.m_cellHeight * SMALL_ORIGINAL5_RATIO);
		}
		private static void GridItemsPatch_ItemInventory_Start_post(Panel_ItemInventory __instance) {
			if (Conf.SimpleUI.Small_ItemWarehouse.Value) {
				var _gridItemList = __instance.XGetFieldValue<UIReuseGrid>("_gridItemList");
				var m_cellList = _gridItemList.XGetFieldValue<UIReuseScrollViewCell[]>("m_cellList");
				foreach (var cell in m_cellList) {
					cell.transform.localScale = new Vector3(SMALL_ORIGINAL5_RATIO, SMALL_ORIGINAL5_RATIO, SMALL_ORIGINAL5_RATIO);
				}
			}

			if (Conf.SimpleUI.EnterToSearch_ItemWarehouse.Value) {
				var _inputSearch = __instance.XGetFieldValue<UIInput>("_inputSearch");
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

			var _gridItemList = __instance.XGetFieldValue<UIReuseGrid>("_gridItemList");
			_gridItemList.m_Column = 7;
			_gridItemList.m_cellWidth = (int)(_gridItemList.m_cellWidth * SMALL_ORIGINAL5_RATIO);
			_gridItemList.m_cellHeight = (int)(_gridItemList.m_cellHeight * SMALL_ORIGINAL5_RATIO);
		}
		private static void GridItemsPatch_ItemEquipInventory_Start_post(Panel_ItemEquipInventory __instance) {
			if (Conf.SimpleUI.Small_ItemSelection.Value) {
				var _gridItemList = __instance.XGetFieldValue<UIReuseGrid>("_gridItemList");
				var m_cellList = _gridItemList.XGetFieldValue<UIReuseScrollViewCell[]>("m_cellList");
				foreach (var cell in m_cellList) {
					cell.transform.localScale = new Vector3(SMALL_ORIGINAL5_RATIO, SMALL_ORIGINAL5_RATIO, SMALL_ORIGINAL5_RATIO);
				}
			}

			if (Conf.SimpleUI.EnterToSearch_ItemSelection.Value) {
				var _inputSearch = __instance.XGetFieldValue<UIInput>("_inputSearch");
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

			var _gridItemList = __instance.XGetFieldValue<UIReuseGrid>("_gridItemList");
			_gridItemList.m_Column = 8;
			_gridItemList.m_cellWidth = (int)(_gridItemList.m_cellWidth * SMALL_ORIGINAL6_RATIO);
			_gridItemList.m_cellHeight = (int)(_gridItemList.m_cellHeight * SMALL_ORIGINAL6_RATIO);
		}
		private static void GridItemsPatch_ItemSelectInventory_Start_post(Panel_ItemSelectInventory __instance) {
			if (Conf.SimpleUI.Small_ItemSelection.Value) {
				var _gridItemList = __instance.XGetFieldValue<UIReuseGrid>("_gridItemList");
				var m_cellList = _gridItemList.XGetFieldValue<UIReuseScrollViewCell[]>("m_cellList");
				foreach (var cell in m_cellList) {
					cell.transform.localScale = new Vector3(SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO);
				}
			}

			if (Conf.SimpleUI.EnterToSearch_ItemSelection.Value) {
				var _inputSearch = __instance.XGetFieldValue<UIInput>("_inputSearch");
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

			var _reUseGridPc = __instance.XGetFieldValue<UIReuseGrid>("_reUseGridPc");
			_reUseGridPc.m_Column = 8;
			_reUseGridPc.m_cellWidth = (int)(_reUseGridPc.m_cellWidth * SMALL_ORIGINAL6_RATIO);
			_reUseGridPc.m_cellHeight = (int)(_reUseGridPc.m_cellHeight * SMALL_ORIGINAL6_RATIO);

			var _reUseGridEquip = __instance.XGetFieldValue<UIReuseGrid>("_reUseGridEquip");
			_reUseGridEquip.m_Column = 8;
			_reUseGridEquip.m_cellWidth = (int)(_reUseGridEquip.m_cellWidth * SMALL_ORIGINAL6_RATIO);
			_reUseGridEquip.m_cellHeight = (int)(_reUseGridEquip.m_cellHeight * SMALL_ORIGINAL6_RATIO);
		}
		private static void GridItemsPatch_TempInventory_Start_post(Panel_TempInventory __instance) {
			if (!Conf.SimpleUI.Small_TempInventory.Value) return;

			{
				var _reUseGridPc = __instance.XGetFieldValue<UIReuseGrid>("_reUseGridPc");
				var m_cellList = _reUseGridPc.XGetFieldValue<UIReuseScrollViewCell[]>("m_cellList");
				foreach (var cell in m_cellList) {
					cell.transform.localScale = new Vector3(SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO);
				}
			}
			{
				var _reUseGridEquip = __instance.XGetFieldValue<UIReuseGrid>("_reUseGridEquip");
				var m_cellList = _reUseGridEquip.XGetFieldValue<UIReuseScrollViewCell[]>("m_cellList");
				foreach (var cell in m_cellList) {
					cell.transform.localScale = new Vector3(SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO, SMALL_ORIGINAL6_RATIO);
				}
			}
		}
		private static void GridItemsPatch_Consumable_Start_pre(Panel_MaterialWarehouse __instance) {
			if (!Conf.SimpleUI.Small_Consumables.Value) return;

			var _reGrid = __instance.XGetFieldValue<UIReuseGrid>("_reGrid");

			_reGrid.m_Column = 7;
			_reGrid.m_cellWidth = (int)(_reGrid.m_cellWidth * SMALL_CONSUMABLE_RATIO);
			_reGrid.m_cellHeight = (int)(_reGrid.m_cellHeight * SMALL_CONSUMABLE_RATIO);
		}
		private static void GridItemsPatch_Consumable_Start_post(Panel_MaterialWarehouse __instance) {
			if (!Conf.SimpleUI.Small_Consumables.Value) return;

			var _reGrid = __instance.XGetFieldValue<UIReuseGrid>("_reGrid");

			var m_cellList = (UIReuseScrollViewCell[])_reGrid.GetType()
				.GetField("m_cellList", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue(_reGrid);

			foreach (var cell in m_cellList) {
				cell.transform.localScale = new Vector3(SMALL_CONSUMABLE_RATIO, SMALL_CONSUMABLE_RATIO, SMALL_CONSUMABLE_RATIO);
			}
		}
		#endregion
		#endregion

		#region ListFilter
		#region Consumable List Sorting
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

		#region Sort by XXX
		private static readonly (string id, string name, Comparison<IReuseCellData> fn)[] Patch_SortBy_Extra_List = [
			("Name",  "이름",      Patch_SortBy_Name),
			("Group", "소속 부대", Patch_SortBy_Group),
			("Links", "링크 수",   Patch_SortBy_Links),
		];

		private static void Inject_SortByXXX_pre(Panel_BasePc __instance) {
			if (!Conf.SimpleUI.Use_SortBy_Extra.Value) return;

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
				//e.localPosition = e.localPosition - new Vector3(0, -111, 0);
				var c1 = e.GetComponentsInChildren<UIWidget>(true);
				foreach (var c in c1) c.width -= 80;

				var c2 = e.GetComponentsInChildren<BoxCollider>(true);
				foreach (var c in c2) c.size -= new Vector3(80f, 0f);

				var c3 = e.GetComponentsInChildren<UILabel>(true);
				foreach (var c in c3) c.width += 20;

				e.transform.localPosition += new Vector3(40f, 0f);
			}

			var els = menu.GetComponentsInChildren<Transform>(true);

			var _sep = els.FirstOrDefault(x => x.name == "DecoSp01");
			if (_sep == null) {
				Plugin.Logger.LogWarning("[Symphony::SimpleUI] Failed to find Separator on SortPanel Menu");
				return;
			}

			var _btn = els.FirstOrDefault(x => x.name == "Grade");
			if (_btn == null) {
				Plugin.Logger.LogWarning("[Symphony::SimpleUI] Failed to find Grade button on SortPanel Menu");
				return;
			}

			var buttonOffset = new Vector3(
				_btn.GetComponentInChildren<UISprite>(true).width + 10f,
				0f
			);

			var _toggleSort = __instance.XGetFieldValue<UIToggle[]>("_toggleSort").ToList();
			foreach (var ex in Patch_SortBy_Extra_List) { 
				var sep = GameObject.Instantiate(_sep.gameObject);
				sep.name = $"DecoSp_{ex.id}";
				sep.transform.SetParent(_sep.parent);
				sep.transform.localPosition = _sep.localPosition - buttonOffset;
				sep.transform.localScale = Vector3.one;

				var btn = GameObject.Instantiate(_btn.gameObject);
				btn.name = ex.id;
				btn.transform.SetParent(_btn.parent);
				btn.transform.localPosition = _btn.localPosition - buttonOffset;
				btn.transform.localScale = Vector3.one;

				btn.GetComponentsInChildren<UILocalize>(true).ToList().ForEach(DestroyImmediate);

				var lbl = btn.GetComponentsInChildren<UILabel>(true);
				foreach (var lb in lbl) lb.text = ex.name;

				var btnOff = btn.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "btn_OFF");
				var uiButton = btnOff.GetComponent<UIButton>();
				uiButton.onClick.Clear();
				uiButton.onClick.Add(new(() => {
					void OnSort(Panel_Base instance, UILabel lbl) {
						if (instance == null) {
							Plugin.Logger.LogWarning("[Symphony::SimpleUI] instance is null");
							return;
						}

						var Sorting = instance.XGetMethodVoid<Comparison<IReuseCellData>>("Sorting");
						if (Sorting == null) {
							Plugin.Logger.LogWarning("[Symphony::SimpleUI] Failed to find Sorting method");
							return;
						}
						Sorting(ex.fn);

						var label = instance.XGetFieldValue<UILabel>("_lblSort");
						if (label != null) label.text = lbl?.text ?? ex.name;
					}

					try {
						var lbl = btnOff.GetComponentInChildren<UILabel>(true);
						OnSort(__instance, lbl);
					} catch (Exception e) {
						Plugin.Logger.LogError(e);
					}
				}));

				_toggleSort.Add(btn.GetComponentInChildren<UIToggle>(true));

				buttonOffset += new Vector3(0, 72, 0);
			}
			__instance.XSetFieldValue("_toggleSort", _toggleSort.ToArray());
		}
		private static void Inject_SortByXXX_post(Panel_BasePc __instance) {
			if (!Conf.SimpleUI.Use_SortBy_Extra.Value) return;

			var _charSortFunc = __instance.XGetFieldValue<Comparison<IReuseCellData>[]>("_charSortFunc");
			if (_charSortFunc != null) {
				var charSortFunc = _charSortFunc.ToList();
				foreach (var ex in Patch_SortBy_Extra_List)
					charSortFunc.Add(ex.fn);
				__instance.XSetFieldValue("_charSortFunc", charSortFunc.ToArray());
			}
		}

		static int Patch_SortBy_Name(IReuseCellData a, IReuseCellData b) {
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
		static int Patch_SortBy_Group(IReuseCellData a, IReuseCellData b) {
			if (a.IsFirst() && !b.IsFirst()) return -1;
			if (!a.IsFirst() && b.IsFirst()) return 1;
			if (a.IsLast() && !b.IsLast()) return 1;
			if (!a.IsLast() && b.IsLast()) return -1;

			var _a = SingleTon<DataManager>.Instance.GetTableCharCollection(
				SingleTon<DataManager>.Instance.GetMyPCClient(
					a.GetPCID()
				).GetTablePC().Key
			);
			var _b = SingleTon<DataManager>.Instance.GetTableCharCollection(
				SingleTon<DataManager>.Instance.GetMyPCClient(
					b.GetPCID()
				).GetTablePC().Key
			);
			if (_a != null && _b != null) {
				var __a = SingleTon<DataManager>.Instance.GetTableTroopCategory(_a.Troop_Category).Squad_Name.Localize();
				var __b = SingleTon<DataManager>.Instance.GetTableTroopCategory(_b.Troop_Category).Squad_Name.Localize();

				if (string.Compare(__a, __b, Common.GetCultureInfo(), CompareOptions.StringSort) > 0)
					return -SingleTon<GameManager>.Instance.InvertSort;
				if (string.Compare(__a, __b, Common.GetCultureInfo(), CompareOptions.StringSort) < 0)
					return SingleTon<GameManager>.Instance.InvertSort;
			}
			else if (_a == null && _b == null) {
				// pass through
			}
			else if (_a == null)
				return 1; // module should be last (even inverted)
			else if (_b == null)
				return -1;

			if (string.Compare(a.GetName(), b.GetName(), Common.GetCultureInfo(), CompareOptions.StringSort) > 0)
				return -SingleTon<GameManager>.Instance.InvertSort;
			if (string.Compare(a.GetName(), b.GetName(), Common.GetCultureInfo(), CompareOptions.StringSort) < 0)
				return SingleTon<GameManager>.Instance.InvertSort;

			return a.GetPCID().CompareTo(b.GetPCID());
		}
		static int Patch_SortBy_Links(IReuseCellData a, IReuseCellData b) {
			if (a.IsFirst() && !b.IsFirst()) return -1;
			if (!a.IsFirst() && b.IsFirst()) return 1;
			if (a.IsLast() && !b.IsLast()) return 1;
			if (!a.IsLast() && b.IsLast()) return -1;

			var _a = SingleTon<DataManager>.Instance.GetMyPCClient(a.GetPCID()).CoreState.Sum();
			var _b = SingleTon<DataManager>.Instance.GetMyPCClient(b.GetPCID()).CoreState.Sum();

			if (_a < _b)
				return -SingleTon<GameManager>.Instance.InvertSort;
			else if (_a > _b)
				return SingleTon<GameManager>.Instance.InvertSort;

			if (string.Compare(a.GetName(), b.GetName(), Common.GetCultureInfo(), CompareOptions.StringSort) > 0)
				return -SingleTon<GameManager>.Instance.InvertSort;
			if (string.Compare(a.GetName(), b.GetName(), Common.GetCultureInfo(), CompareOptions.StringSort) < 0)
				return SingleTon<GameManager>.Instance.InvertSort;

			return a.GetPCID().CompareTo(b.GetPCID());
		}
		#endregion

		#region Advanced Filter
		private static void Inject_AdvancedFilter(Panel_BasePc __instance) {
			if (!Conf.SimpleUI.Use_AdvancedFilter.Value) return;

			var btnChangeSource = __instance.XGetFieldValue<UIToggle[]>("_toggleRole");
			if (btnChangeSource == null) {
				Plugin.Logger.LogWarning("[Symphony::SimpleUI] Cannot find reference button for Advanced Filter creating swap button");
				return;
			}

			var goType = __instance.XGetFieldValue<GameObject[]>("_goTypeWrapper").FirstOrDefault(x => x.activeSelf);
			var btnSwap = Instantiate<GameObject>(btnChangeSource[2].gameObject, goType.transform);
			btnSwap.name = "btnModeSwap";
			btnSwap.transform.localPosition = goType.transform.Find("TitleLb").localPosition - new Vector3(224f, 0f);
			{ // Remove except sprites, setup Button
				var comps = ((MonoBehaviour[])btnSwap.GetComponentsInChildren<UILabel>(true))
					.Concat(btnSwap.GetComponentsInChildren<UILocalize>(true))
					.Select(x => x.gameObject)
					.Distinct();
				foreach (var c in comps) c?.gameObject?.Destroy();

				btnSwap.transform.Find("btnSortRankSp")?.gameObject?.Destroy();
				btnSwap.transform.Find("btnSortRank-ON").gameObject.name = "btnFilterSwap";

				var icon = btnSwap.transform.Find("btnFilterSwap", "btnSortRank-label");
				icon.gameObject.name = "btnFilterSwap-icon";
				icon.localPosition = Vector3.zero;

				btnSwap.GetComponent<UIToggle>()?.Destroy();

				var comp = __instance.gameObject.AddComponent<AdvancedSearchComponent>();
				comp.enabled = false;

				var btn = btnSwap.AddComponent<UIButton>();
				btn.onClick.Add(new(() => {
					comp.enabled = !comp.enabled; // not advanced mode

					var filter = __instance.kDynamicWidget.transform.Find("Anchor_CenterRight", "FilterFolder", "Filter");
					filter?.gameObject.SetActive(!comp.enabled);
				}));
			}
		}
		#endregion
		#endregion

		#region CharacterDetail
		private static void Patch_CharacterDetail_NextPre(Panel_CharacterDetails __instance) {
			if (!Conf.SimpleUI.Use_CharacterDetail_NextPrev.Value) return;
			if (SingleTon<GameManager>.Instance.CharacterDetailOpeType != 1) return; // From Warehouse

			var _list = SingleTon<DataManager>.Instance.GetAllPc();
			var toggles = NativeSingleton<ToggleManager>.Instance.Char_Lobby;
			
			bool IsFilterGrade(ClientPcInfo pc) => toggles.Grade[pc.Grade - 2];
			bool IsFilterActorClass(ClientPcInfo pc) => toggles.Type[pc.PCTable.ActorClassType];
			bool IsFilterRoleType(ClientPcInfo pc) => toggles.Type[pc.PCTable.RoleType];
			bool IsFilterBodyType(ClientPcInfo pc) => toggles.Body[pc.PCTable.ActorBodyType];

			Comparison<IReuseCellData> sortFunc = toggles.Sort switch {
				0 => Common.SortGrade,
				1 => Common.SortLevel,
				2 => Common.SortAtt,
				3 => Common.SortHp,
				4 => Common.SortDef,
				5 => Common.SortApply,
				6 => Common.SortEvade,
				7 => Common.SortCri,
				8 => Common.SortActivePoint,
				9 => Common.SortFavor,
				10 => Common.SortGetTime,
				11 => Common.SortMarriage,
				_ => null
			};
			if(sortFunc == null && Conf.SimpleUI.Use_SortBy_Extra.Value) {
				sortFunc = toggles.Sort switch {
					12 => Patch_SortBy_Name,
					13 => Patch_SortBy_Group,
					14 => Patch_SortBy_Links,
					_ => null
				};
			}
			if(sortFunc == null) {
				Plugin.Logger.LogInfo($"[Symphony::SimpleUI] Unable to find Sort function, value was {toggles.Sort}");
				return; // No need to move
			}

			// Name search unavailable
			var _filtered = _list
				.Where(pc => !pc.IsModulePc() && IsFilterGrade(pc) && IsFilterActorClass(pc) && IsFilterRoleType(pc) && IsFilterBodyType(pc))
				.Select(x => new ItemCellInvenCharacter {
					pcInfo = x,
					coreTargetPcInfo = x,
					IsCostView = false,
					IsCharUpGradeView = false,
					IsCoreStateView = true
				})
				.ToList();
			_filtered.Sort(sortFunc);
			var filtered = _filtered.ToArray();

			if (filtered.Length <= 1) {
				Plugin.Logger.LogInfo($"[Symphony::SimpleUI] Filtered unit list length was {filtered.Length}");
				return; // No need to move
			}

			var goPrev = __instance.XGetFieldValue<GameObject>("_goPcRight");
			goPrev.SetActive(true);

			var btnPrev = goPrev.GetComponentInChildren<UIButton>();
			btnPrev.onClick.Clear();
			btnPrev.onClick.Add(new(() => Patch_CharacterDetail_OnBtnNextPrev(__instance, filtered, -1)));

			var goNext = __instance.XGetFieldValue<GameObject>("_goPcLeft");
			goNext.SetActive(true);

			var btnNext = goNext.GetComponentInChildren<UIButton>();
			btnNext.onClick.Clear();
			btnNext.onClick.Add(new(() => Patch_CharacterDetail_OnBtnNextPrev(__instance, filtered, +1)));
		}
		private static void Patch_CharacterDetail_OnBtnNextPrev(
			Panel_CharacterDetails __instance, ItemCellInvenCharacter[] list, int offset
		) {
			__instance.XGetMethodVoid<Action>("SendReqSetSkin").Invoke(null);

			var cur = SingleTon<GameManager>.Instance.SelectPCID;
			var idx = Array.FindIndex(list, x => x.pcInfo.PCId == cur) - offset; // list are reversed
			if (idx < 0) idx = list.Length - 1;
			else if (idx >= list.Length) idx = 0;

			var to = list[idx];
			var fn = () => {
				__instance.XSetFieldValue("_selectPCID", to.pcInfo.PCId);
				SingleTon<GameManager>.Instance.SelectPCID = to.pcInfo.PCId;
				__instance.XGetMethodVoid("RefreshPCInfo").Invoke();
			};

			void HandlePacketResponsePcInfo(WebResponseState p) {
				var res = p as W2C_RESPONSE_PCINFO;
				__instance.ShowWaitMessage(show: false);
				if (InstantPanel.IsWait()) InstantPanel.Wait(show: false);

				EventManager.StopListening(__instance, 204u, HandlePacketResponsePcInfo);
				if (res.result.ErrorCode == 0) fn();
			}

			EventManager.StartListening(__instance, 204u, HandlePacketResponsePcInfo);
			SingleTon<DataManager>.Instance.RefreshPCInfo(to.pcInfo.PCId, new(() => {
				EventManager.StopListening(__instance, 204u, HandlePacketResponsePcInfo);
				fn();
			}));
		}
		#endregion

		#region Workbench
		#region Preview Making
		private static void Patch_Preview_UnitMaking_Label(UIUnitMake __instance) {
			var p_label = __instance.GetComponentsInChildren<UILabel>().FirstOrDefault(x => x.gameObject.name == "Label_Preview");
			if (p_label != null) return;

			var lbl_src = __instance.XGetFieldValue<UILabel>("_lblMakingTime");

			var active = lbl_src.gameObject.activeSelf;
			lbl_src.gameObject.SetActive(true);

			{
				var lbl = GameObject.Instantiate(lbl_src.gameObject);
				lbl.name = "Label_Preview";
				lbl.transform.SetParent(lbl_src.transform.parent.parent.parent);
				lbl.transform.localScale = Vector3.one;
				lbl.transform.localPosition = lbl_src.transform.localPosition + new Vector3(280f, 0f, 0f);

				var label = lbl.GetComponent<UILabel>();
				label.height += 20;
				label.fontSize = 26;
				label.text = "";
			}
			{
				var lbl = GameObject.Instantiate(lbl_src.gameObject);
				lbl.name = "Label_Result_New";
				lbl.transform.SetParent(lbl_src.transform.parent.parent.parent);
				lbl.transform.localScale = Vector3.one;
				lbl.transform.localPosition = lbl_src.transform.localPosition + new Vector3(280f, -156f, 0f);

				var label = lbl.GetComponent<UILabel>();
				label.fontSize = 28;
				label.color = new Color(1f, 0.73f, 0.27f);
				label.text = "NEW !";
				label.enabled = false;
			}

			lbl_src.gameObject.SetActive(active);
		}
		private static void Patch_Preview_UnitMaking_Display(UIUnitMake __instance, PCMakingIngInfo makingInfo) {
			Patch_Preview_UnitMaking_Label(__instance);

			if (!Conf.SimpleUI.Use_CharacterMakingPreview.Value) {
				Patch_Preview_UnitMaking_Display_Clear(__instance);
				return;
			}

			var info = makingInfo;
			var label = __instance.GetComponentsInChildren<UILabel>().FirstOrDefault(x => x.gameObject.name == "Label_Preview");
			if (info == null) {
				if (label != null) label.text = "";
				return;
			}

			var pcMaking = SingleTon<DataManager>.Instance.TableLastOne?._TableManager?._Table_PCMaking;
			if (pcMaking == null) {
				if (label != null) label.text = string.Empty;
				return;
			}

			var scrapbook = SingleTon<DataManager>.Instance.GetCollectionHero();

			var units = pcMaking
				.Where(x => x.Value.PCMakingTime == (int)(info.EndTime - info.StartTime))
				.Select(x => x.Value.Key)
				.ToArray();
			var unitNames = string.Join(
				"\n",
				units.Select(x => (SingleTon<DataManager>.Instance.GetTablePC(x)?.Name ?? $"${x}").Localize())
			);
			if (label != null)
				label.text = unitNames;
			else
				Plugin.Logger.LogWarning($"[Symphony::SimpleUI] Workbench Unit Slot {info.SlotNo} result is {unitNames.Replace("\n", " / ")}, but label not found");

			var label_new = __instance.GetComponentsInChildren<UILabel>(true)
				.FirstOrDefault(x => x.gameObject.name == "Label_Result_New");

			if (units.All(x => !scrapbook.Contains(x))) { // all results are not in scrapbook
				if (label_new != null) {
					label_new.text = "NEW !";
					label_new.enabled = true;
				}
			}
			else if (units.Any(x => !scrapbook.Contains(x))) { // some result are not in scrapbook
				if (label_new != null) {
					label_new.text = "NEW ?";
					label_new.enabled = true;
				}
			}
			else { // all result are in scrapbook
				if (label_new != null)
					label_new.enabled = false;
			}
		}
		private static void Patch_Preview_UnitMaking_Display_Clear(UIUnitMake __instance) {
			Patch_Preview_UnitMaking_Label(__instance);

			var label = __instance.GetComponentsInChildren<UILabel>().FirstOrDefault(x => x.gameObject.name == "Label_Preview");
			if (label != null) label.text = "";

			var label_new = __instance.GetComponentsInChildren<UILabel>(true)
				.FirstOrDefault(x => x.gameObject.name == "Label_Result_New");
			if (label_new != null) label_new.enabled = false;
		}

		private static void Patch_Preview_EquipMaking_Label(UIEquipMake __instance) {
			var p_label = __instance.GetComponentsInChildren<UILabel>().FirstOrDefault(x => x.gameObject.name == "Label_Preview");
			if (p_label != null) return;

			var lbl_src = __instance.XGetFieldValue<UILabel>("_lblMakingTime");

			var active = lbl_src.gameObject.activeSelf;
			lbl_src.gameObject.SetActive(true);

			var lbl = GameObject.Instantiate(lbl_src.gameObject);
			lbl.name = "Label_Preview";
			lbl.transform.SetParent(lbl_src.transform.parent.parent.parent);
			lbl.transform.localScale = Vector3.one;
			lbl.transform.localPosition = lbl_src.transform.localPosition + new Vector3(280f, 0f, 0f);

			var label = lbl.GetComponent<UILabel>();
			label.height += 20;
			label.fontSize = 26;
			label.text = "";

			lbl_src.gameObject.SetActive(active);
		}
		private static void Patch_Preview_EquipMaking_Display(UIEquipMake __instance, EquipMakingIngInfo makingInfo) {
			Patch_Preview_EquipMaking_Label(__instance);

			if (!Conf.SimpleUI.Use_EquipMakingPreview.Value) {
				Patch_Preview_EquipMaking_Display_Clear(__instance);
				return;
			}

			var info = makingInfo;
			var label = __instance.GetComponentsInChildren<UILabel>().FirstOrDefault(x => x.gameObject.name == "Label_Preview");
			if (info == null) {
				if (label != null) label.text = "";
				return;
			}

			var eqMaking = SingleTon<DataManager>.Instance.TableLastOne?._TableManager?._Table_EquipMaking;
			if (eqMaking == null) {
				if (label != null) label.text = string.Empty;
				return;
			}

			var units = string.Join(
				"\n",
				eqMaking
					.Where(x => x.Value.EquipMakingTime == (int)(info.EndTime - info.StartTime))
					.Select(x => SingleTon<DataManager>.Instance.GetTableItemEquip(x.Value.Key)?.ItemName ?? $"${x.Value.Key}")
					.Select(x => x.Localize())
			);
			if (label != null)
				label.text = units;
			else
				Plugin.Logger.LogWarning($"[Symphony::SimpleUI] Workbench Equip Slot {info.SlotNo} result is {units}, but label not found");
		}
		private static void Patch_Preview_EquipMaking_Display_Clear(UIEquipMake __instance) {
			Patch_Preview_EquipMaking_Label(__instance);

			var label = __instance.GetComponentsInChildren<UILabel>().FirstOrDefault(x => x.gameObject.name == "Label_Preview");
			if (label != null) label.text = "";
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
		#endregion

		#region Composite
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
			);

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

		#region Exchange: No messy hand
		private static void Patch_Exchange_HideSoldOut(Panel_ExShop __instance) {
			if (!Conf.SimpleUI.Use_Exchange_NoMessyHand.Value) return;

			__instance.XGetFieldValue<UIToggle>("_toggleSoldOutHide").value = true;
		}
		private static IEnumerable<CodeInstruction> Patch_Exchange_ConsumableList(MethodBase original, IEnumerable<CodeInstruction> instructions) {
			Plugin.Logger.LogInfo("[Symphony::SimpleUI] Start to patch Panel_ExShop.RefreshConsumableView");

			var matcher = new CodeMatcher(instructions);
			matcher.MatchForward(false,
				/* for (int index = 0; */
				new CodeMatch(OpCodes.Ldc_I4_0), // 0
				new CodeMatch(OpCodes.Stloc_S), // index =

				new CodeMatch(OpCodes.Br), // start of loop

				new CodeMatch(OpCodes.Ldloc_0), // start of body of loop
				new CodeMatch(OpCodes.Ldloc_S)
			);

			if (matcher.IsInvalid) {
				Plugin.Logger.LogWarning("[Symphony::SimpleUI] Failed to patch Panel_ExShop.RefreshConsumableView, target instructions not found");
				return instructions;
			}

			matcher.Insert(
				new CodeInstruction(OpCodes.Ldarg_0), // this
				new CodeInstruction(OpCodes.Ldloc_0), // clientItemInfoList
				new CodeInstruction(
					OpCodes.Call,
					AccessTools.Method(typeof(SimpleUI), nameof(SimpleUI.Patch_Exchange_ConsumableList_Filter))
				),
				new CodeInstruction(OpCodes.Stloc_0) // clientItemInfoList = [return of call]
			);

			return matcher.InstructionEnumeration();
		}
		private static List<ClientItemInfo> Patch_Exchange_ConsumableList_Filter(Panel_ExShop __instance, List<ClientItemInfo> list) {
			if (!Conf.SimpleUI.Use_Exchange_NoMessyHand.Value) return list;

			var IsEnableConsume = __instance.XGetMethod<Table_ExShop, bool>("IsEnableConsume");
			var IsHideSoldOut = __instance.XGetMethod<Table_ExShop, bool>("IsHideSoldOut");
			var shopItems = SingleTon<DataManager>.Instance.GetExShopData(
				__instance.XGetFieldValue<EXSHOP_CATEGORY>("_curCategory")
			)
				.Where(x => x.IsSale == 1 && IsEnableConsume.Invoke(x) && !IsHideSoldOut.Invoke(x))
				.SelectMany(x => x.NeedItem)
				.ToHashSet();

			var ret = new List<ClientItemInfo>();
			ret.AddRange(list.Where(x => shopItems.Contains(x.ItemKeyString)));
			return ret.ToList();
		}
		#endregion
		#endregion

		#region Squad Clear Button
		private static void Patch_Squad_Clear(Panel_SquadInfo __instance) {
			if (!Conf.SimpleUI.Use_Squad_Clear.Value) return;

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
					var squadType = SingleTon<GameManager>.Instance.SquadType;
					var normalMasterSquad = SingleTon<DataManager>.Instance.GetUserInfo().MasterSquadIndex;

					var squad = SingleTon<DataManager>.Instance.GetCurrentSquad(squadType);
					var chars = squad.SquadSlotList // move leader to last of list
						.Where(r => r.PCId != 0 && r.PCId != squad.LeaderPCID)
						.Concat(squadType == GlobalDefines.SQUAD_TYPE.NORMAL && normalMasterSquad == squad.SquadIndex
							? [] // exclude leader for master squad
							: squad.SquadSlotList.Where(r => r.PCId == squad.LeaderPCID)
						)
						.ToArray();

					foreach (var chr in chars) {
						SquadClear_LastUnsetPC = 0;

						// FormationCharacterPick.OnPick
						MonoSingleton<SceneBase>.Instance.ShowWaitMessage(true);
						if (squadType == GlobalDefines.SQUAD_TYPE.NORMAL) {
							C2WPacket.Send_C2W_UNSET_PC_TO_SQUAD(
							SingleTon<DataManager>.Instance.AccessToken,
							SingleTon<DataManager>.Instance.WID,
							chr.PCId,
							squad.SquadIndex,
							SingleTon<DataManager>.Instance.GetSquadSlotNumber(chr.PCId, GlobalDefines.SQUAD_TYPE.NORMAL)
						);
						}
						else {
							C2WPacket.Send_C2W_INFINITEWAR_UNSET_PC_TO_SQUAD(
								SingleTon<DataManager>.Instance.AccessToken,
								SingleTon<DataManager>.Instance.WID,
								chr.PCId,
								squad.SquadIndex,
								SingleTon<DataManager>.Instance.GetSquadSlotNumber(chr.PCId, GlobalDefines.SQUAD_TYPE.INFINITE_WAR)
							);
						}

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
		private void HandlePacketInfiniteWarUnsetPcToSquad(WebResponseState obj) {
			W2C_INFINITEWAR_UNSET_PC_TO_SQUAD data = obj as W2C_INFINITEWAR_UNSET_PC_TO_SQUAD;
			if (data.result.ErrorCode != 0) return;

			SquadClear_LastUnsetPC = data.result.PCID;
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
	}
}
