using Colorful;

using GlobalDefines;

using HarmonyLib;

using LO_ClientNetwork;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.Networking;

namespace Symphony.Features {
	[Feature("Automation")]
	internal class Automation : MonoBehaviour {
		public void Start() {
			var harmony = new Harmony("Symphony.Automation");

			#region Base CollectAll Restart
			harmony.Patch(
				AccessTools.Method(typeof(Panel_LivingStation), nameof(Panel_LivingStation.OnCollectAllButton)),
				prefix: new HarmonyMethod(typeof(Automation), nameof(Automation.Panel_LivingStation_OnCollectAllButton))
			);

			harmony.Patch(
				AccessTools.Method(typeof(Panel_FacilityRewardResultAll), nameof(Panel_FacilityRewardResultAll.Init)),
				prefix: new HarmonyMethod(typeof(Automation), nameof(Automation.Panel_FacilityRewardResultAll_Init))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_FacilityRewardResultAll), "RewardView"),
				prefix: new HarmonyMethod(typeof(Automation), nameof(Automation.Panel_FacilityRewardResultAll_RewardView))
			);
			#endregion

			#region OfflineBattle Restart
			harmony.Patch(
				AccessTools.Method(typeof(Panel_OfflineBattlePopup), nameof(Panel_OfflineBattlePopup.OnOfflineBattleStartButton)),
				prefix: new HarmonyMethod(typeof(Automation), nameof(Automation.Patch_OfflineBattle_Restart_Memorize))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_OfflineBattleResult), nameof(Panel_OfflineBattleResult.InitOfflineBattleResult)),
				postfix: new HarmonyMethod(typeof(Automation), nameof(Automation.Patch_OfflineBattle_Restart))
			);
			#endregion

			#region Character Share
			harmony.Patch(
				AccessTools.Method(typeof(Panel_CharacterDetails), nameof(Panel_CharacterDetails.Start)),
				postfix: new HarmonyMethod(typeof(Automation), nameof(Automation.Patch_CharacterShare))
			);
			#endregion
		}
		public void OnDestroy() {
			EventManager.StopListening(this);
		}

		#region Base CollectAll Restart
		private static bool Panel_LivingStation_OnCollectAllButton(Panel_LivingStation __instance) {
			if (!Conf.Automation.Use_Base_CollectAll_Restart.Value) return true;

			if (!Scene_LivingStation.Instance.kStation.ChkCollectAll()) return false;

			IEnumerator Fn() {
				var targetFacilities = FindObjectsByType<InstallationFacility>(FindObjectsSortMode.None)
					.Where(x => x.mCurrentState == InstallationFacility.State.WorkComplete)
					.ToArray();

				yield return __instance.XGetMethod<IEnumerator>("Co_CollectAllProcess")?.Invoke();

				var insufficientRes = false;

				__instance.isCurrentCollectAllFlag = true;
				try {
					foreach (var fac in targetFacilities) {
						if (fac.mCurrentState == InstallationFacility.State.Prepare) {
							Scene_LivingStation.Instance.kStation.mCurrentFacility = fac;

							var costMetal = fac.Table.MetalCost;
							var costNutrient = fac.Table.NutrientCost;
							var costPower = fac.Table.PowerCost;
							if (
								(costMetal <= SingleTon<DataManager>.Instance.Metal) &&
								(costNutrient <= SingleTon<DataManager>.Instance.Nutrient) &&
								(costPower <= SingleTon<DataManager>.Instance.Power)
							) {
								/** Panel_FacilityOperation.SendWorkPacket() */
								InstantPanel.Wait(show: true);
								C2WPacket.Send_C2W_FACILITY_WORK( // send work packet
									SingleTon<DataManager>.Instance.AccessToken,
									SingleTon<DataManager>.Instance.WID,
									fac.Packet.Facility_uid
								);
							}
							else {
								insufficientRes = true;
								break;
							}
							
							yield return new WaitWhile(() => InstantPanel.IsWait());
						}
					}
				} finally {
					__instance.isCurrentCollectAllFlag = false;
				}

				if (insufficientRes) {
					InstantPanel.MessageBox("자원이 부족해 일부 시설을 재시작할 수 없었습니다.");
				}
			}
			__instance.StartCoroutine(Fn());

			return false;
		}

		private static void Panel_FacilityRewardResultAll_Init(Panel_FacilityRewardResultAll __instance) {
			if (!Conf.Automation.Use_Base_CollectAll_Restart.Value) return;

			var rewardList = Scene_LivingStation.Instance.CollectAllRewardList;
			var src = rewardList.ToArray();

			rewardList.Clear();
			try {
				rewardList.AddRange(src.Aggregate(
					new List<(WebGiveRewardInfo rewardInfo, Table_Facility facilityTable, long Facility_uid)>(),
					(p, c) => {
						if (!(c is object[])) return p;

						var o = c as object[];
						if (!(
							o.Length == 3 &&
							o[0] is WebGiveRewardInfo && o[1] is Table_Facility && o[2] is long
						)) return p;

						var rewardInfo = o[0] as WebGiveRewardInfo;
						var facilityTable = o[1] as Table_Facility;
						var Facility_uid = (long)o[2];

						if (rewardInfo.AddMetal > 0) {
							var idx = p.FindIndex(x => x.rewardInfo.AddMetal > 0);
							if (idx >= 0)
								p[idx].rewardInfo.AddMetal += rewardInfo.AddMetal;
							else
								p.Add((new() { AddMetal = rewardInfo.AddMetal }, facilityTable, Facility_uid));
						}
						if (rewardInfo.AddNutrient > 0) {
							var idx = p.FindIndex(x => x.rewardInfo.AddNutrient > 0);
							if (idx >= 0)
								p[idx].rewardInfo.AddNutrient += rewardInfo.AddNutrient;
							else
								p.Add((new() { AddNutrient = rewardInfo.AddNutrient }, facilityTable, Facility_uid));
						}
						if (rewardInfo.AddPower > 0) {
							var idx = p.FindIndex(x => x.rewardInfo.AddPower > 0);
							if (idx >= 0)
								p[idx].rewardInfo.AddPower += rewardInfo.AddPower;
							else
								p.Add((new() { AddPower = rewardInfo.AddPower }, facilityTable, Facility_uid));
						}

						if (rewardInfo.PCRewardList != null && rewardInfo.PCRewardList.Count > 0)
							// PCRewardList should be 1-lengthed list
							p.Add((new() { PCRewardList = rewardInfo.PCRewardList }, facilityTable, Facility_uid));

						if (rewardInfo.ItemRewardList != null && rewardInfo.ItemRewardList.Count > 0) {
							foreach (var item in rewardInfo.ItemRewardList) {
								if (item.Info.ItemType != 4) continue;

								var item2 = SingleTon<DataManager>.Instance.GetItem(item.Info.ItemSN);
								if (item2 == null) continue;

								var idx = p.FindIndex(x =>
									x.rewardInfo.ItemRewardList?.Count > 0 &&
									x.rewardInfo.ItemRewardList[0].Info.ItemKeyString == item.Info.ItemKeyString
								);

								if (idx >= 0)
									p[idx].rewardInfo.ItemRewardList[0].Info.StackCount += item2.StackCount - item2.BeforeStatckCount;
								else
									p.Add((new() {
										ItemRewardList = new() {
											new UpdateItemInfo {
												UpdateType = item.UpdateType,
												Info = new ItemInfo(
													item.Info.ItemUID,
													item.Info.ItemSN,
													item.Info.ItemType,
													item.Info.ItemKeyString,
													item2.StackCount - item2.BeforeStatckCount,
													item.Info.InvenCategory,
													item.Info.EnchantLevel,
													item.Info.IsLock,
													item.Info.EnchantPoint,
													item.Info.EquippedPCID,
													item.Info.EquipSlot
												)
											}
										}
									}, facilityTable, Facility_uid));
							}
						}

						return p;
					}
				).Select(x => new object[] { x.rewardInfo, x.facilityTable, x.Facility_uid }));
			} catch {
				// restore original list
				rewardList.Clear();
				rewardList.AddRange(src);
			}
		}
		private static bool Panel_FacilityRewardResultAll_RewardView(
			Panel_FacilityRewardResultAll __instance,
			Slot_FacilityRewardResultAllReward slot_FacilityRewardResultAllReward,
			WebGiveRewardInfo _webGiveReward,
			Table_Facility _table,
			long _facUID
		) {
			if (!Conf.Automation.Use_Base_CollectAll_Restart.Value) return true;
			if (
				(_webGiveReward.PCRewardList != null && _webGiveReward.PCRewardList.Count > 0) ||
				(_webGiveReward.AddMetal != 0) ||
				(_webGiveReward.AddNutrient != 0) ||
				(_webGiveReward.AddPower != 0) ||
				(_webGiveReward.AddCash != 0 || _webGiveReward.ItemRewardList == null)
			) return true;

			// No need to calculate StackCount, info.Info.StackCount already calculated
			// and, list should be 1-lengthed (but don't care)
			foreach (var info in _webGiveReward.ItemRewardList) {
				var item = SingleTon<DataManager>.Instance.GetItem(info.Info.ItemSN);
				if(item == null) continue;

				slot_FacilityRewardResultAllReward.SetData(_table, _facUID);
				slot_FacilityRewardResultAllReward.SetItem(item, info.Info.StackCount);
			}

			return false;
		}
		#endregion

		#region OfflineBattle_Restart
		private static void Patch_OfflineBattle_Restart_Memorize(Panel_OfflineBattlePopup __instance) {
			var enter = (OfflineBattleEnterClass)__instance.GetType()
				.GetField("offlineBattleEnter", BindingFlags.Instance | BindingFlags.NonPublic)
				.GetValue(__instance);

			Plugin.Logger.LogInfo($"[Symphony::Automation] Last OfflineBattle memorized, char: {enter.characterDiscompose}, equip: {enter.eqiupDiscompose}");
			Conf.Automation.OfflineBattle_Last_CharDiscomp.Value = enter.characterDiscompose;
			Conf.Automation.OfflineBattle_Last_EquipDiscomp.Value = enter.eqiupDiscompose;
		}
		private static void Patch_OfflineBattle_Restart(Panel_OfflineBattleResult __instance) {
			if (!Conf.Automation.Use_OfflineBattle_Restart.Value) return;

			var last = SingleTon<DataManager>.Instance.OfflineBattleInfo;
			if (last == null) {
				Plugin.Logger.LogWarning($"OfflineBattleInfo invalid 1");
				return; // no last offline battle info
			}

			#region Setup UI
			var prefab = SingleTon<ResourceManager>.Instance.LoadUIPrefab("Panel_OfflineBattle");
			var btn_src_bg = prefab.GetComponentsInChildren<UIButton>().FirstOrDefault(x => x.transform.parent?.name == "Btn_End");
			if (btn_src_bg == null) {
				Plugin.Logger.LogWarning("[Symphony::Automation] Btn_End in Panel_OfflineBattle not found");
				return; // Source not found
			}
			var btn_src = btn_src_bg.transform.parent.gameObject;

			var Panel_Result_TF = __instance.gameObject.transform.Find("OfflineBattleReward");

			var Btn_Restart = GameObject.Instantiate(btn_src, Panel_Result_TF);
			Btn_Restart.name = "Btn_Restart";
			Btn_Restart.transform.localPosition = new Vector3(740f, -440f, 0f);
			Btn_Restart.transform.localScale = Vector3.one;

			foreach (var x in Btn_Restart.GetComponentsInChildren<UILocalize>())
				DestroyImmediate(x);

			var labels = Btn_Restart.GetComponentsInChildren<UILabel>();
			foreach (var lbl in labels)
				lbl.text = "재시작";

			var btn = Btn_Restart.GetComponentInChildren<UIButton>();
			btn.onClick.Clear();

			prefab = SingleTon<ResourceManager>.Instance.LoadUIPrefab("Panel_OfflineBattlePopup");
			var resource = prefab.GetComponentsInChildren<UISprite>()
				.FirstOrDefault(x => x.transform.parent?.name == "Resource")
				.transform
				.parent.gameObject;
			var Panel_Resource = GameObject.Instantiate(resource, Panel_Result_TF);
			Panel_Resource.name = "Resource";
			Panel_Resource.transform.localPosition = new Vector3(180f, -440f, 0f);
			Panel_Resource.transform.localScale = Vector3.one;

			Panel_Resource.transform.GetComponentsInChildren<Transform>()
				.FirstOrDefault(x => x.name == "MetalCost")
				.GetComponent<UILabel>()
				.text = StringHelper.ConvertCommaNumber(last.Metal);
			Panel_Resource.transform.GetComponentsInChildren<Transform>()
				.FirstOrDefault(x => x.name == "NutrientCost")
				.GetComponent<UILabel>()
				.text = StringHelper.ConvertCommaNumber(last.Nutrient);
			Panel_Resource.transform.GetComponentsInChildren<Transform>()
				.FirstOrDefault(x => x.name == "PowerCost")
				.GetComponent<UILabel>()
				.text = StringHelper.ConvertCommaNumber(last.Power);

			Panel_Resource.transform.GetComponentsInChildren<Transform>()
				.FirstOrDefault(x => x.name == "MetalCurrentLb")
				.GetComponent<UILabel>()
				.text = StringHelper.ConvertCommaNumber(SingleTon<DataManager>.Instance.Metal);
			Panel_Resource.transform.GetComponentsInChildren<Transform>()
				.FirstOrDefault(x => x.name == "NutrientCurrentLb")
				.GetComponent<UILabel>()
				.text = StringHelper.ConvertCommaNumber(SingleTon<DataManager>.Instance.Nutrient);
			Panel_Resource.transform.GetComponentsInChildren<Transform>()
				.FirstOrDefault(x => x.name == "PowerCurrentLb")
				.GetComponent<UILabel>()
				.text = StringHelper.ConvertCommaNumber(SingleTon<DataManager>.Instance.Power);

			var label_src = prefab.GetComponentsInChildren<UILabel>()
				.FirstOrDefault(x => x.transform.name == "textLb1")
				.gameObject;

			var hours = (uint)Mathf.CeilToInt((float)(last.EndUnixTime - last.StartUnixTime) / 3600);
			var goHour = GameObject.Instantiate(label_src, Panel_Result_TF);
			goHour.name = "Lbl_Restart_Hour";
			goHour.transform.localPosition = new Vector3(180f, -340f, 0f);
			goHour.transform.localScale = Vector3.one;
			var lblHour = goHour.GetComponent<UILabel>();
			lblHour.text = $"※ 재시작 자율 전투 시간 : {hours} 시간";

			var goDiscompose = GameObject.Instantiate(label_src, Panel_Result_TF);
			goDiscompose.name = "Lbl_Restart_Discompose";
			goDiscompose.transform.localPosition = new Vector3(180f, -370f, 0f);
			goDiscompose.transform.localScale = Vector3.one;

			var lblDiscompose = goDiscompose.GetComponent<UILabel>();

			var charDiscomposeList = new List<string>();
			if ((Conf.Automation.OfflineBattle_Last_CharDiscomp.Value & 8) > 0) charDiscomposeList.Add("[c][fcf3a1]SS[-][/c]");
			if ((Conf.Automation.OfflineBattle_Last_CharDiscomp.Value & 4) > 0) charDiscomposeList.Add("[c][efd29c]S[-][/c]");
			if ((Conf.Automation.OfflineBattle_Last_CharDiscomp.Value & 2) > 0) charDiscomposeList.Add("[c][aecbf7]A[-][/c]");
			if ((Conf.Automation.OfflineBattle_Last_CharDiscomp.Value & 1) > 0) charDiscomposeList.Add("[c][c2f0a9]B[-][/c]");
			var charDiscomposeText = string.Join(", ", charDiscomposeList);

			var equipDiscomposeList = new List<string>();
			if ((Conf.Automation.OfflineBattle_Last_EquipDiscomp.Value & 8) > 0) equipDiscomposeList.Add("[c][fcf3a1]SS[-][/c]");
			if ((Conf.Automation.OfflineBattle_Last_EquipDiscomp.Value & 4) > 0) equipDiscomposeList.Add("[c][efe29c]S[-][/c]");
			if ((Conf.Automation.OfflineBattle_Last_EquipDiscomp.Value & 2) > 0) equipDiscomposeList.Add("[c][aecbf7]A[-][/c]");
			if ((Conf.Automation.OfflineBattle_Last_EquipDiscomp.Value & 1) > 0) equipDiscomposeList.Add("[c][c2f0a9]B[-][/c]");
			var equipDiscomposeText = string.Join(", ", equipDiscomposeList);

			lblDiscompose.text = $"※ 재시작 시 자동 분해될 전투원 : {charDiscomposeText}  /  장비 : {equipDiscomposeText}";
			#endregion

			#region Check Restart-able (event closed?)
			var mapRestartable = new Func<bool>(() => {
				var man = SingleTon<DataManager>.Instance;

				var map = man.GetTableChapterStage(last.StageKey);
				if (map == null) return false;

				var chapter = map != null ? man.GetTableMapChapter(map.ChapterIndex) : null;
				if (!string.IsNullOrEmpty(chapter?.Event_Category)) {
					var evChapter = man.GetTableEventChapter(chapter.Key);
					if (evChapter.Event_OpenType == 0) { // Closed event
						Plugin.Logger.LogWarning("[Symphony::Automation] Last offline map was event and closed, disable restart");
						return false;
					}
				}

				return true;
			}).Invoke();
			#endregion

			var enoughResource = SingleTon<DataManager>.Instance.Metal >= last.Metal &&
				SingleTon<DataManager>.Instance.Nutrient >= last.Nutrient &&
				SingleTon<DataManager>.Instance.Power >= last.Power;
			if (enoughResource && mapRestartable) {
				btn.onClick.Add(new(() => {
					var stage = SingleTon<DataManager>.Instance.GetTableChapterStage(last.StageKey);
					if(stage == null) {
						Plugin.Logger.LogWarning($"[Symphony::Automation] Stage info not found, key was {last.StageKey}");
						return;
					}

					__instance.XGetMethodVoid("UpdateCharacterInfo")?.Invoke();
					__instance.XGetMethodVoid("UpdateConsumeItemInfo")?.Invoke();

					try {
						var enter = new OfflineBattleEnterClass();
						enter.offlineBattleStage = stage;
						enter.offlineBattleSquad = new SquadInfo(0, last.SquadIndex, 0, [], ""); // dummy, just use SquadIndex
						enter.characterDiscompose = (byte)((Conf.Automation.OfflineBattle_Last_CharDiscomp.Value | 1) & 15);
						enter.eqiupDiscompose = (byte)((Conf.Automation.OfflineBattle_Last_EquipDiscomp.Value | 1) & 15);
						enter.offlineBattleMaxTime = new TimeSpan(99, 0, 0); // dummy value
						enter.offlineBattleMinTime = new TimeSpan(TimeSpan.TicksPerSecond * (long)last.ClearTime);
						enter.usingResources = [last.OnceMetal, last.OnceNutrient, last.OncePower]; // will be checked as `value * playCount` internally

						var go = new GameObject();
						go.transform.localScale = Vector3.zero;

						var popup = go
							.AddChild(SingleTon<ResourceManager>.Instance.LoadUIPrefab("Panel_OfflineBattlePopup"))
							.GetComponent<Panel_OfflineBattlePopup>();
						popup.InitOfflineBattlePopup(enter);
						popup.GetType().GetField("selectTimeHour", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(popup, hours);

						SingleTon<KeyListener>.Instance.AddPopupObj(popup.gameObject);

						popup.OnOfflineBattleStartButton();
					} catch (Exception e) {
						Plugin.Logger.LogError($"[Symphony::Automation] Error while trying to restart OfflineBattle");
						Plugin.Logger.LogError(e.ToString());
					}
				}));
			}
			else {
				Btn_Restart.SetActive(false); // hide restart button
			}
		}
		#endregion

		#region Character Share
		private const int CharShareCodeVersion = 1;
		private static void Patch_CharacterShare(Panel_CharacterDetails __instance) {
			if (!Conf.Automation.Use_CharacterShare.Value) return;

			var _goFavorMarriageIcon = __instance.XGetFieldValue<GameObject>("_goFavorMarriageIcon");
			if (_goFavorMarriageIcon == null) {
				Plugin.Logger.LogWarning("[Symphony::SimpleUI] Cannot get source button from scene");
				return;
			}

			var parent = __instance.XGetFieldValue<GameObject>("_goMarriageHide").transform;
			var hp_fill_box = parent.Find("hp_fill_box");
			var DefSp = parent.Find("DefSp");

			#region Export button
			{
				var go = GameObject.Instantiate(_goFavorMarriageIcon, parent, true);
				go.name = "Share_Export";
				Destroy(go.transform.Find("BtnMarriageVoice")?.gameObject);

				go.SetActive(true);
				go.transform.localPosition = new Vector3(
					DefSp.localPosition.x + 40f,
					hp_fill_box.localPosition.y + 20f,
					0f
				);

				var loc = go.GetComponentInChildren<UILocalize>();
				if (loc != null) {
					loc.enabled = false;
					Destroy(loc);
				}

				var lbl = go.GetComponentInChildren<UILabel>();
				if (lbl != null) lbl.text = "내보내기";

				if (!go.TryGetComponent<UISprite>(out var sp) || !go.TryGetComponent<UIButton>(out var btn)) {
					Plugin.Logger.LogWarning("[Symphony::SimpleUI] Cannot get component, failed to add favorite feature on CharacterDetail");
					Destroy(go);
					return;
				}

				btn.GetComponent<UISprite>()?.atlas = Atlas.atlas;
				btn.normalSprite = "UI_Export";
				sp.spriteName = btn.normalSprite;

				btn.hoverSprite = null;
				btn.pressedSprite = null;

				btn.onClick.Clear();
				btn.onClick.Add(new EventDelegate(() => {
					IEnumerator BuildShareText(bool upload) {
						var pc = __instance.XGetFieldValue<ClientPcInfo>("_SelectPCInfo");

						var sb = new StringBuilder();
						sb.AppendFormat("Symphony:Char:{0}:", CharShareCodeVersion);

						sb.AppendFormat(
							"{0}:{1}:{2}:{3}:{4}:{5}:",
							new Regex(@"^Char_(.+)_N$").Replace(pc.Index, "$1"),
							pc.Grade,
							pc.Level,
							pc.FavorPoint >= 20000 ? "Y" : "N",
							pc.CoreLinkBonus_KeyString,
							pc.GetTotalCoreValue() // CoreLink Suitability
						);

						// Stat levels
						ACTOR_ATTR_TYPE[] attrs = [
							ACTOR_ATTR_TYPE.HP,
								ACTOR_ATTR_TYPE.ATK,
								ACTOR_ATTR_TYPE.DEF,
								ACTOR_ATTR_TYPE.APPLY,
								ACTOR_ATTR_TYPE.EVADE,
								ACTOR_ATTR_TYPE.CRI
						];
						sb.AppendFormat("{0}:",
							string.Join(",", attrs.Select(
								attr => pc.PCEnchantAttrInfoList
									.FirstOrDefault(x => x.AttrType == (byte)attr)?.EnchantAfterCount ?? 0
							))
						);

						// Equip key & levels
						var equips = pc.XGetFieldValue<List<EquippedItemMok>>("equippedItemList");
						if (equips == null)
							sb.Append(":");
						else
							sb.AppendFormat("{0}:", string.Join(",", equips.Select(x => $"{x.Data.Key};{x.EnchantLevel}")));

						// Priority skill
						sb.AppendFormat("{0}:", pc.AIInfo.FirstSkillSlotType);

						// Skill levels
						sb.AppendFormat("{0}:", string.Join(",", pc.HaveSkillList.OrderBy(x => x.SkillKeyString).Select(x => $"{x.SkillLevel}")));

						sb.Append("END");

						var shareText = sb.ToString();
						Plugin.Logger.LogMessage($"[Symphony::Automation] Export unit data '{shareText}'");

						if(upload) {
							var form = new WWWForm();
							form.AddField("text", shareText);

							var req = UnityWebRequest.Post($"https://symphony-sharetext.swaytwig.com/write.php", form);
							yield return req.SendWebRequest();

							if (req.result != UnityWebRequest.Result.Success) {
								__instance.ShowMessage("swaytwig.com 서버에서 공유 텍스트를 불러오지 못했습니다.");
								yield break;
							}

							var res = req.downloadHandler.text.Trim();
							if (res.StartsWith("!")) {
								__instance.ShowMessageChoice(
									$"swaytwig.com 서버에서 오류가 발생했습니다.\n\n{res}\n\n원본을 복사하시겠습니까?",
									"전투원 공유",
									"예",
									"아니오",
									MessageType.YESNO_CHOICE,
									() => {
										try {
											GUIUtility.systemCopyBuffer = shareText;
											__instance.ShowMessage($"복사되었습니다");
										} catch {
											__instance.ShowMessage($"복사에 실패했습니다");
										}
									}
								);
								yield break;
							}

							shareText = res;
						}

						try {
							GUIUtility.systemCopyBuffer = shareText;
							if (shareText.Length == 11) // web share-text
								__instance.ShowMessage($"복사되었습니다\n\n{Common.COLOR_YELLOW}{shareText}{Common.COLOR_END}");
							else
								__instance.ShowMessage($"복사되었습니다");
						} catch {
							__instance.ShowMessage($"복사에 실패했습니다");
						}
					}

					__instance.ShowMessage(
						"클립보드로 복사하시겠습니까?",
						"전투원 공유하기",
						"예",
						"아니오",
						"원본으로 복사",
						GlobalDefines.MessageType.YESNOOK,
						() => __instance.StartCoroutine(BuildShareText(true)),
						null,
						() => __instance.StartCoroutine(BuildShareText(false))
					);
				}));
			}
			#endregion

			#region Import button
			{
				var go = GameObject.Instantiate(_goFavorMarriageIcon, parent, true);
				go.name = "Share_Import";
				Destroy(go.transform.Find("BtnMarriageVoice")?.gameObject);

				go.SetActive(true);
				go.transform.localPosition = new Vector3(
					DefSp.localPosition.x + 140f,
					hp_fill_box.localPosition.y + 20f,
					0f
				);

				var loc = go.GetComponentInChildren<UILocalize>();
				if (loc != null) {
					loc.enabled = false;
					Destroy(loc);
				}

				var lbl = go.GetComponentInChildren<UILabel>();
				if (lbl != null) lbl.text = "불러오기";

				if (!go.TryGetComponent<UISprite>(out var sp) || !go.TryGetComponent<UIButton>(out var btn)) {
					Plugin.Logger.LogWarning("[Symphony::SimpleUI] Cannot get component, failed to add favorite feature on CharacterDetail");
					Destroy(go);
					return;
				}

				btn.GetComponent<UISprite>()?.atlas = Atlas.atlas;
				btn.normalSprite = "UI_Import";
				sp.spriteName = btn.normalSprite;

				btn.hoverSprite = null;
				btn.pressedSprite = null;

				btn.onClick.Clear();
				btn.onClick.Add(new EventDelegate(() => {
					IEnumerator Fn() {
						var input = GUIUtility.systemCopyBuffer.Trim();
						if (input.Length == 11) { // is net-stored shared-text
							var b = input.Replace('-', '+').Replace('_', '/');
							if (b.Length % 4 != 0) b += new string('=', 4 - (b.Length % 4));

							var span = new Span<byte>(new byte[12]);
							if (Convert.TryFromBase64String(b, span, out _)) {
								var req = UnityWebRequest.Get($"https://symphony-sharetext.swaytwig.com/read.php?id={input}");
								yield return req.SendWebRequest();

								if (req.result != UnityWebRequest.Result.Success) {
									__instance.ShowMessage("swaytwig.com 서버에서 공유 텍스트를 불러오지 못했습니다.");
									yield break;
								}

								input = req.downloadHandler.text.Trim();
								if (input.StartsWith("!")) {
									__instance.ShowMessage($"swaytwig.com 서버에서 오류가 발생했습니다.\n\n{input}");
									yield break;
								}
							}
						}

						try {
							var dataManager = SingleTon<DataManager>.Instance;
							var pc = __instance.XGetFieldValue<ClientPcInfo>("_SelectPCInfo");

							string[] RARITIES = ["Ｂ", "Ａ", "Ｓ", "SS", "SSS"]; // use full-width to avoid BBCode
							string getRarity(int r) {
								var sb = new StringBuilder();
								sb.Append("[");
								if (r < 2 || r >= RARITIES.Length + 2)
									sb.Append("？");
								else
									sb.Append(RARITIES[r - 2]);
								sb.Append("]");
								return sb.ToString();
							}

							var SKILLS = pc.HaveSkillList.OrderBy(x => x.SkillKeyString).ToArray();
							SkillInfo getSkill(int i) => i < 0 || i >= SKILLS.Length ? null : SKILLS[i];

							ACTOR_ATTR_TYPE[] ATTRS = [
								ACTOR_ATTR_TYPE.HP,
								ACTOR_ATTR_TYPE.ATK,
								ACTOR_ATTR_TYPE.DEF,
								ACTOR_ATTR_TYPE.APPLY,
								ACTOR_ATTR_TYPE.EVADE,
								ACTOR_ATTR_TYPE.CRI
							];

							var ALL_EQUIPS = dataManager.GetAllEquipItem();
							var FREE_ITEMS = ALL_EQUIPS.Where(x => x.EquippedPCID == 0).ToList();
							var EQUIPPED_ITEMS = ALL_EQUIPS.Where(x => x.EquippedPCID != 0).ToList();
							var EQUIPPING = pc.XGetFieldValue<List<ClientItemInfo>>("equipClientItemInfo");

							var loaders = new List<Func<IEnumerator>>();

							var RED = Common.COLOR_RED;
							var GREEN = Common.COLOR_GREEN;
							var YELLOW = Common.COLOR_YELLOW;
							var CYAN = Common.COLOR_BLUE;
							var ORANGE = Common.COLOR_EXPANDLEVEL_MAX;
							var DEEP_YELLOW = Common.COLOR_DIALOG_CHOICE;
							var DARK_GREY = "[c][5e5e61]"; // Common.COLOR_GREY;
							var GREY = Common.COLOR_GREY;
							var END = Common.COLOR_END;

							var parts = input.Split(':');
							if (parts[0] != "Symphony") throw new FormatException($"Invalid header: '{parts[0]}'");
							if (parts[1] != "Char") throw new FormatException($"Invalid category: '{parts[1]}'");
							if (!int.TryParse(parts[2], out var codeVer) || codeVer < 0 || codeVer > CharShareCodeVersion)
								throw new FormatException($"Invalid version: '{parts[2]}'");
							if (parts[parts.Length - 1] != "END") throw new FormatException($"Invalid finalizer: '{parts[parts.Length - 1]}'");

							if (
								!dataManager.GetTableCharCollection($"Char_{parts[3]}_N").Try(out var chr) ||
								!dataManager.GetTablePC(chr.Char_Key).Try(out var tpc)
							) throw new FormatException($"Invalid character, '{parts[3]}'");

							if (tpc.Key != pc.PCTable.Key) {
								__instance.ShowMessage(string.Format(
									"복사된 공유 코드는 {0} 대상입니다",
									ORANGE + tpc.Name.Localize() + END
								));
								yield break;
							}

							if (codeVer == 1 && parts.Length != 14) throw new FormatException("Invalid parts length");

							var MaxGrade = tpc.StartGrade;
							while (true) {
								var promo = dataManager.GetTablePCPromotion(chr.Char_Key, MaxGrade);
								if (promo == null) break;

								MaxGrade = promo.PromotionGrade;
							}

							var sb = new StringBuilder();
							var sbWarn = new StringBuilder();
							if (codeVer < CharShareCodeVersion) {
								sb.AppendLine(YELLOW + "이전 버전에서 생성된 공유 코드입니다." + END);
								sb.AppendLine();
							}

							if (codeVer >= 1) {
								if (!int.TryParse(parts[4], out var rarity) || rarity < tpc.StartGrade || rarity > MaxGrade)
									throw new FormatException("Invalid Rarity");

								if (!int.TryParse(parts[5], out var lv) || lv <= 0 || lv > 120)
									throw new FormatException("Invalid Level");

								if (!parts[6].Try((x => x == "Y" || x == "N"), (x => x == "Y"), out var favor200))
									throw new FormatException("Invalid Favor");

								if (!dataManager.GetTableCoreLinkBonus(parts[7]).Try(x => parts[7].Length == 0 || x != null, out var fullLink))
									throw new FormatException("Invalid FullLinkBonus");

								if (!float.TryParse(parts[8], out var coreBonus) || coreBonus < 0 || coreBonus > 500)
									throw new FormatException("Invalid CoreLinkBonus");

								if (
									!parts[9].Split(',').Try(out var attrs_raw) || attrs_raw.Length != 6 ||
									!attrs_raw
										.Select(x => {
											if (!int.TryParse(x, out var attr)) return -1;
											if (attr < 0 || attr > lv * 3) return -1;
											return attr;
										})
										.Where(x => x >= 0)
										.ToArray()
										.Try(x => x.Length == 6, out var attrs)
								) throw new FormatException("Invalid Attrs");

								if (
									!parts[10].Split(',').Try(out var equips_raw) || equips_raw.Length > 4 ||
									!equips_raw
										.Select(x => {
											var p = x.Split(';');
											if (p.Length != 2 || !int.TryParse(p[1], out var elv)) return (null, 0);

											var eq = dataManager.GetTableItemEquip(p[0]);
											if (eq == null) return (null, 0);

											return (equip: eq, lv: elv);
										})
										.Where(x => x.equip != null)
										.ToArray()
										.Try(out var equips) ||
									equips.Count(x => (ITEM_TYPE)x.equip.ItemType == ITEM_TYPE.CHIP) > 2 ||
									equips.Count(x => (ITEM_TYPE)x.equip.ItemType == ITEM_TYPE.SPCHIP) > 1 || // OS
									equips.Count(x => (ITEM_TYPE)x.equip.ItemType == ITEM_TYPE.SUBEQ) > 1 // Gear
								) throw new FormatException("Invalid Equips");

								if (!int.TryParse(parts[11], out var prioSkill) || prioSkill < 0 || prioSkill > 1)
									throw new FormatException("Invalid PrioSkill");

								if (
									!parts[12].Split(',').Try(out var skills_raw) ||
									!skills_raw
										.Select(x => {
											if (!int.TryParse(x, out var slv)) return 0;
											if (slv < 0 || slv > 10) return 0;
											return slv;
										})
										.Where(x => x >= 0)
										.ToArray()
										.Try(out var skills) || skills.Length != rarity
								) throw new FormatException("Invalid Skill");


								sb.AppendLine(string.Format(
									"{0} {1} {2} {3}",
									(pc.Grade == rarity ? YELLOW : RED) + getRarity(rarity) + END,
									(pc.Level >= lv ? ORANGE : RED) + $"Lv.{lv}" + END,
									chr.Char_Name.Localize(),
									favor200
										? pc.FavorPoint < 20000
											? RED + "[호감도 200]" + END
											: ORANGE + "[호감도 200]" + END
										: DARK_GREY + "[호감도 200]" + END
								));
								sb.AppendLine(string.Format(
									"{0} {1} {2} {3}",
									GREY + "코어링크 :" + END,
									(pc.GetTotalCoreValue() >= coreBonus ? CYAN : END) + $"{coreBonus}%" + END,
									DARK_GREY + "/" + END,
									fullLink == null
										? DARK_GREY + "없음" + END
										: CYAN + StringHelper.GetFullLinkBonusMsg(fullLink.Key) + END
								));

								sb.AppendLine(string.Format(
									"{0}    {1}    {2}    {3}    {4}    {5}",
									GREY + "HP " + END + CYAN + $"+{attrs[0]}" + END,
									GREY + "공격력 " + END + CYAN + $"+{attrs[1]}" + END,
									GREY + "방어력 " + END + CYAN + $"+{attrs[2]}" + END,
									GREY + "적중률 " + END + CYAN + $"+{attrs[3]}" + END,
									GREY + "회피율 " + END + CYAN + $"+{attrs[4]}" + END,
									GREY + "치명타 " + END + CYAN + $"+{attrs[5]}" + END
								));

								sb.AppendLine(string.Format(
									"{0}{1}",
									GREY + "스킬 : " + END,
									string.Join(
										DARK_GREY + " / " + END,
										skills.Select((x, i) => {
											var y = getSkill(i)?.SkillLevel >= x
												? x.ToString()
												: RED + x + END;
											if (i == prioSkill)
												return GREEN + "<" + y + ">" + END;
											return y;
										})
									)
								));

								var eqi = 0;
								foreach (var eq in equips) {
									if (eqi > 0) {
										if (eqi % 2 == 0)
											sb.AppendLine();
										else
											sb.Append("    ");
									}
									sb.Append(string.Format(
										"{0} {1} {2}",
										YELLOW + getRarity(eq.equip.ItemGrade) + END,
										eq.equip.ItemName.Localize(),
										DEEP_YELLOW + $"+{eq.lv}" + END
									));

									eqi++;
								}
								sb.AppendLine();


								if (pc.Grade != rarity) sbWarn.AppendLine(ORANGE + "※ 등급이 일치하지 않습니다" + END);
								if (pc.Level < lv) sbWarn.AppendLine(ORANGE + "※ 레벨이 부족합니다" + END);
								if (favor200 && pc.FavorPoint < 20000) sbWarn.AppendLine(ORANGE + "※ 호감도 보너스가 부족합니다" + END);
								if (pc.GetTotalCoreValue() < coreBonus) sbWarn.AppendLine(ORANGE + "※ 코어링크 적합률이 부족합니다" + END);
								if (SKILLS.Any((x, i) => x.SkillLevel < (i >= skills.Length ? 0 : skills[i])))
									sbWarn.AppendLine(ORANGE + "※ 스킬 레벨이 부족합니다" + END);

								var groupedEquips = equips.Aggregate(
									new List<(Table_ItemEquip equip, int lv, int count)>(),
									(p, c) => {
										var idx = p.FindIndex(x => x.equip.Key == c.equip.Key && x.lv == c.lv);
										if (idx >= 0) {
											var v = p[idx];
											v.count++;
											p[idx] = v;
										}
										else
											p.Add((c.equip, c.lv, 1));

										return p;
									}
								);
								foreach (var geq in groupedEquips) {
									var FreeCount = FREE_ITEMS.Count(x => x.ItemKeyString == geq.equip.Key && x.EnchantLevel == geq.lv);
									var MatchCount = EQUIPPING.Count(x => x.ItemKeyString == geq.equip.Key && x.EnchantLevel == geq.lv);
									if (FreeCount < geq.count - MatchCount) {
										var EquipCount = EQUIPPED_ITEMS.Count(x => x.ItemKeyString == geq.equip.Key && x.EnchantLevel == geq.lv) - MatchCount;
										sbWarn.AppendLine(string.Format(
											"{0}{1}{2}  {3}{4}{5}",
											ORANGE + "※ 장착중이지 않은 " + END,
											string.Format(
												"{0} {1} {2}",
												YELLOW + getRarity(geq.equip.ItemGrade) + END,
												geq.equip.ItemName.Localize(),
												DEEP_YELLOW + $"+{geq.lv}" + END
											),
											ORANGE + " 이(가) 부족합니다" + END,
											DARK_GREY + "(" + END,
											CYAN + EquipCount + END,
											GREY + "개 장착중" + END + GREY + ")" + END
										));
									}
								}

								IEnumerator loader() {
									#region FullLinkBonus
									// Set when only char have enough corelink suitability
									if (pc.GetTotalCoreValue() == (float)Const.MAX_TOTAL_CORE_VALUE) {
										var key = fullLink?.Key ??
											dataManager.GetFullLinkBonusKey(pc.Index).FirstOrDefault(x => x.StartsWith("Core_Bonus_Cost_"));

										if (pc.CoreLinkBonus_KeyString != key) {
											__instance.ShowWaitMessage(show: true);
											C2WPacket.Send_C2W_SET_COREBONUS(dataManager.AccessToken, dataManager.WID, pc.PCId, key);
											yield return new WaitUntil(() => !InstantPanel.IsWait());
										}
									}
									#endregion

									#region Stats
									if (pc.MaxEnchantCount >= attrs.Sum()) { // enough stat value?
										var pCEnchantInfo = new PCEnchantInfo();
										pCEnchantInfo.HPValue = attrs[0];
										pCEnchantInfo.AtkValue = attrs[1];
										pCEnchantInfo.DefValue = attrs[2];
										pCEnchantInfo.AccValue = attrs[3];
										pCEnchantInfo.EvadeValue = attrs[4];
										pCEnchantInfo.CriValue = attrs[5];

										// Always resets
										__instance.ShowWaitMessage(show: true);
										C2WPacket.Send_C2W_PCENCHANT_RESET(dataManager.AccessToken, dataManager.WID, pc.PCId);
										yield return new WaitUntil(() => !InstantPanel.IsWait());

										__instance.ShowWaitMessage(show: true);
										C2WPacket.Send_C2W_PC_ENCHANT(dataManager.AccessToken, dataManager.WID, pc.PCId, pCEnchantInfo);
										yield return new WaitUntil(() => !InstantPanel.IsWait());
									}
									#endregion

									#region PrioSkill
									if (pc.AIInfo.FirstSkillSlotType != prioSkill) {
										var pCAIInfo = UtilityEx.DeepCopy(pc.AIInfo) as PCAIInfo;
										pCAIInfo.FirstSkillSlotType = (byte)prioSkill;

										__instance.ShowWaitMessage(show: true);
										C2WPacket.Send_C2W_PCAI_CHANGE(dataManager.AccessToken, dataManager.WID, pCAIInfo);
										yield return new WaitUntil(() => !InstantPanel.IsWait());
									}
									#endregion

									#region Equips
									{
										var slots = dataManager.GetTablePcEquipSlot(pc.PCId).GetPcEquipSlot()
											.Where(x => x != ITEM_TYPE.PCITEM)
											.ToArray();
										var occupiedSlots = pc.PCEquipSlotList
											.Where(x => x.EquippedItemInfo.ItemType != (byte)ITEM_TYPE.PCITEM)
											.Select(x => x.SlotNo)
											.ToHashSet();

										var equipCache = new HashSet<ulong>();
										var equips_to_equip = equips
											.Where(x => {
												var e = EQUIPPING.FirstOrDefault(y =>
													y.ItemKeyString == x.equip.Key &&
													y.EnchantLevel == x.lv &&
													!equipCache.Contains(y.ItemUID)
												);
												if (e != null) {
													equipCache.Add(e.ItemUID);
													return false;
												}
												return true;
											})
											.ToList();

										// Unequip not-matched
										foreach (var eq in EQUIPPING.Where(x => !equipCache.Contains(x.ItemUID)).ToList()) {
											__instance.ShowWaitMessage(show: true);
											C2WPacket.Send_C2W_UNSET_EQUIPITEM(
												dataManager.AccessToken, dataManager.WID,
												eq.EquippedPCID,
												eq.ItemSN
											);
											yield return new WaitUntil(() => !InstantPanel.IsWait());

											occupiedSlots.Remove(eq.EquipSlot);

											EQUIPPING.Remove(eq);
											EQUIPPED_ITEMS.Remove(eq);
											FREE_ITEMS.Add(eq);
										}

										var isPartial = false;
										foreach (var teq in equips_to_equip) {
											var eq = FREE_ITEMS.FirstOrDefault(x =>
												x.ItemKeyString == teq.equip.Key &&
												x.EnchantLevel == teq.lv
											);
											if (eq == null) {
												isPartial = true;
												continue;
											}

											var slot = -1;
											for (var i = 0; i < slots.Length; i++) {
												if (slots[i] != (ITEM_TYPE)eq.ItemType) continue;

												var slotNo = (byte)(1 + i * 2);
												if (occupiedSlots.Contains(slotNo)) continue;

												slot = slotNo;
												occupiedSlots.Add(slotNo);
												break;
											}
											if (slot == -1) {
												isPartial = true;
												continue;
											}

											__instance.ShowWaitMessage(show: true);
											C2WPacket.Send_C2W_SET_EQUIPITEM(
												dataManager.AccessToken, dataManager.WID,
												eq.ItemSN,
												eq.InvenCategory,
												eq.ItemKeyString,
												pc.PCId,
												pc.Index,
												(byte)slot
											);
											yield return new WaitUntil(() => !InstantPanel.IsWait());

											FREE_ITEMS.Remove(eq);
											EQUIPPED_ITEMS.Add(eq);
											EQUIPPING.Add(eq);
										}

										yield return new WaitUntil(() => !InstantPanel.IsWait());
										if (isPartial)
											__instance.ShowMessage("일부 장비를 장착하지 못했습니다.");
									}
									#endregion
								}
								loaders.Add(loader);
							}

							sb.AppendLine();
							sb.AppendLine(sbWarn.ToString());

							sb.AppendLine("이대로 불러오시겠습니까?");

							var msg = __instance.ShowMessageChoice(
								sb.ToString(),
								"전투원 불러오기",
								"예",
								"아니오",
								GlobalDefines.MessageType.YESNO_CHOICE,
								() => {
									IEnumerator fn() {
										foreach (var loader in loaders)
											yield return loader();
									}
									__instance.StartCoroutine(fn());
								}
							);

							var label = msg.XGetFieldValue<UILabel>("labelMessage");
							label.overflowMethod = UILabel.Overflow.ShrinkContent;
							label.spacingY = 5;
							label.width = 1500;
							label.height = 500;
							msg.subMsg = " ";
						} catch (FormatException ex) {
							__instance.ShowMessage($"올바르지 않은 공유 코드입니다\n\n{ex.Message}");
							Plugin.Logger.LogWarning(ex.ToString());
						} catch (Exception ex) {
							__instance.ShowMessage($"전투원을 불러오지 못했습니다\n\n{ex.Message}");
							Plugin.Logger.LogWarning(ex.ToString());
						}
					}
					__instance.StartCoroutine(Fn());
				}));
			}
			#endregion
		}
		#endregion
	}
}
