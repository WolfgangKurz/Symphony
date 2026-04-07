using HarmonyLib;

using LO_ClientNetwork;

using Symphony.UI;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEngine;

namespace Symphony.Features {
	[Feature("Automation")]
	internal class Automation : MonoBehaviour {
		private static bool GetAll_GettingAll = false;

		private static WebGiveRewardInfo GetAll_RewardTotal = null;
		private static WebGiveRewardInfo GetAll_CostTotal = null;
		private static Dictionary<string, int> GetAll_RewardItemDictionary = new();
		private static bool GetAll_DisplayGetAllResult = false;
		private static long GetAll_CurrentFacilityUid = 0;
		private static Action<Panel_FacilityRewardResult> GetAll_OnRewardPopup = null;
		private static Action<W2C_FACILITY_REWARD> GetAll_OnRewardPacket = null;
		private static Action<W2C_FACILITY_WORK> GetAll_OnWorkPacket = null;

		private static GameObject GetAll_BtnGetAll = null;

		public void Start() {
			var harmony = new Harmony("Symphony.Automation");

			#region Base GetAll
			harmony.Patch(
				AccessTools.Method(typeof(Panel_FacilityRewardResult), "ResourceUpdate"),
				postfix: new HarmonyMethod(typeof(Automation), nameof(Automation.FacilityRewardResult_Ready))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_LivingStation), "ShowSideMenu"),
				postfix: new HarmonyMethod(typeof(Automation), nameof(Automation.Panel_LivingStation_ShowSideMenu))
			);

			EventManager.StartListening(this, 133U, new Action<WebResponseState>(this.OnFacilityWorkPakcet));
			EventManager.StartListening(this, 134U, new Action<WebResponseState>(this.OnFacilityRewardPacket));

			SceneListener.Instance.OnEnter("Scene_LivingStation", () => {
				if (!Conf.Automation.Use_Base_GetAll.Value) return;

				Plugin.Logger.LogWarning("[Symphony::Automation] Scene_LivingStation detected");
				StartCoroutine(this.SetupBase_GetAll());
			});
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
		}
		public void OnDestroy() {
			EventManager.StopListening(this);
			GetAll_BtnGetAll = null;
		}

		#region Base GetAll
		private IEnumerator SetupBase_GetAll() {
			yield return new WaitForEndOfFrame();
			yield return new WaitForEndOfFrame(); // ensure scene load

			var src = GameObject.Find("FacilityEditButton");
			if (src == null) {
				Plugin.Logger.LogWarning("[Symphony::Automation] Failed to find Facility edit button");
				yield break;
			}

			var _children = src.transform.parent.GetComponentsInChildren<Transform>(true);
			var btn = _children.FirstOrDefault(t => t.name == "CollectAllButton")?.gameObject;
			if (!btn) {
				Plugin.Logger.LogWarning("[Symphony::Automation] Failed to get CollectAllButton");
				Destroy(btn);
				yield break;
			}
			if(btn.activeSelf) {
				Plugin.Logger.LogWarning("[Symphony::Automation] CollectAllButton already active, seems already implemented, skip setup automation");
				yield break;
			}
			btn.SetActive(true);
			GetAll_BtnGetAll = btn;

			UIButton uiBtn;
			if (!btn.TryGetComponent<UIButton>(out uiBtn)) {
				var srcCollider = src.GetComponent<BoxCollider>();
				var collider = btn.AddComponent<BoxCollider>();
				collider.center = srcCollider.center;
				collider.size = srcCollider.size;

				uiBtn = btn.AddComponent<UIButton>();

				btn.transform.localPosition += new Vector3(0f, -160f);
			}
			if (!uiBtn) {
				Plugin.Logger.LogWarning("[Symphony::Automation] Failed to create UIButton component for CollectAllButton");
				Destroy(btn);
				yield break;
			}

			var deactivation = btn.transform.Find("Deactivation");
			if (deactivation) deactivation.gameObject.SetActive(false);

			uiBtn.onClick.Clear();
			uiBtn.onClick.Add(new EventDelegate(() => {
				StartCoroutine(this.OnClickGetAll());
			}));
		}
		private IEnumerator OnClickGetAll() {
			if (GetAll_GettingAll) yield break;

			try {
				GetAll_RewardTotal = new WebGiveRewardInfo();
				GetAll_CostTotal = new WebGiveRewardInfo();
				GetAll_GettingAll = true;
				GetAll_DisplayGetAllResult = false;

				var scene = GameObject.FindObjectOfType<Scene_LivingStation>();

				string[] charMakers = ["NukerMaking1", "NukerMaking2", "TankerMaking1", "TankerMaking2", "SupporterMaking1", "SupporterMaking2"];
				var facilities = GameObject.FindObjectsOfType<InstallationFacility>();
				var facilitiesToWork = facilities
					.Where(x => !charMakers.Contains(x.Packet.Facility_key))
					.Where(x => x.GetState() == InstallationFacility.State.WorkComplete);
				Plugin.Logger.LogDebug($"[Symphony::Automation] Facilities : {facilities.Length}");
				Plugin.Logger.LogDebug($"[Symphony::Automation] Facilities to get : {facilitiesToWork.Count()}");
				foreach (var fac in facilitiesToWork) {
					if (fac.GetState() != InstallationFacility.State.WorkComplete) continue;

					var facilityUid = fac.Packet.Facility_uid;
					Panel_FacilityRewardResult rewardPopup = null;
					bool rewardDone = false;
					bool workDone = false;
					bool restartRequested = false;
					bool restartTriggered = false;

					GetAll_CurrentFacilityUid = facilityUid;
					GetAll_OnRewardPacket = w2CFacilityReward => {
						var result = w2CFacilityReward.result;
						if (result.Facility_uid != GetAll_CurrentFacilityUid) return;

						var res = result.RewardInfo;
						GetAll_RewardTotal.PCRewardList.AddRange(res.PCRewardList ?? []);
						GetAll_RewardTotal.AddMetal += res.AddMetal;
						GetAll_RewardTotal.AddNutrient += res.AddNutrient;
						GetAll_RewardTotal.AddPower += res.AddPower;
						GetAll_RewardTotal.AddCash += res.AddCash;
						GetAll_RewardTotal.ItemRewardList.AddRange(res.ItemRewardList ?? []);

						rewardDone = true;
					};
					GetAll_OnWorkPacket = w2CFacilityWork => {
						var result = w2CFacilityWork.result;
						if (result.Facility_uid != GetAll_CurrentFacilityUid) return;

						GetAll_CostTotal.AddMetal += (uint)fac.Table.MetalCost;
						GetAll_CostTotal.AddNutrient += (uint)fac.Table.NutrientCost;
						GetAll_CostTotal.AddPower += (uint)fac.Table.PowerCost;

						workDone = true;
					};
					GetAll_OnRewardPopup = popup => {
						rewardPopup = popup;
						if (!restartRequested || restartTriggered) return;

						var button = popup.GetComponentsInChildren<UIButton>().FirstOrDefault(x => x.name == "RestartButton");
						restartTriggered = button != null;
						if (button != null) {
							EventDelegate.Execute(button.onClick);
						} else {
							Plugin.Logger.LogWarning($"[Symphony::Automation] Failed to trigger restart button for facility {facilityUid}");
							workDone = true;
						}
					};

					try {
						Plugin.Logger.LogDebug("[Symphony::Automation] Select facility");
						scene.kStation.GetType()
							.GetField("mCurrentFacility", BindingFlags.Instance | BindingFlags.NonPublic)
							.SetValue(scene.kStation, fac);

						Plugin.Logger.LogDebug("[Symphony::Automation] Click facility to get reward");
						fac.OnSelected();
						Plugin.Logger.LogDebug("[Symphony::Automation] Waiting reward packet");
						yield return new WaitUntil(() => rewardDone);
						Plugin.Logger.LogDebug("[Symphony::Automation] Waiting reward popup");
						yield return new WaitUntil(() => rewardPopup != null);

						if (fac.Table.MetalCost > SingleTon<DataManager>.Instance.Metal ||
							fac.Table.NutrientCost > SingleTon<DataManager>.Instance.Nutrient ||
							fac.Table.PowerCost > SingleTon<DataManager>.Instance.Power
						) {
							var closeButton = rewardPopup.GetComponentsInChildren<UIButton>().FirstOrDefault(x => x.name == "CloseButton");
							if (closeButton != null) EventDelegate.Execute(closeButton.onClick);

							Plugin.Logger.LogMessage("[Symphony::Automation] Not enough resources to restart facility");
							continue; // Not enough resource to restart
						}

						Plugin.Logger.LogDebug("[Symphony::Automation] Test restart");
						restartRequested = true;
						if (!restartTriggered) {
							var button = rewardPopup.GetComponentsInChildren<UIButton>().FirstOrDefault(x => x.name == "RestartButton");
							restartTriggered = button != null;
							if (button != null) {
								Plugin.Logger.LogDebug("[Symphony::Automation] Click restart button");
								EventDelegate.Execute(button.onClick);
							}
							else {
								Plugin.Logger.LogWarning($"[Symphony::Automation] Failed to trigger restart button for facility {facilityUid}");
								continue;
							}
						}

						Plugin.Logger.LogDebug("[Symphony::Automation] Waiting restart packet");
						yield return new WaitUntil(() => workDone);
						Plugin.Logger.LogDebug("[Symphony::Automation] OK");
						//yield return new WaitForSecondsRealtime(1f);
						//yield return new WaitForSecondsRealtime(0.155f);
					} finally {
						scene.kStation.SelectFacilityRelease();
						GetAll_CurrentFacilityUid = 0;
						GetAll_OnRewardPopup = null;
						GetAll_OnRewardPacket = null;
						GetAll_OnWorkPacket = null;
					}
				}
			} finally {
				GetAll_GettingAll = false;

				if (GetAll_RewardTotal.AddMetal > 0 || GetAll_RewardTotal.AddNutrient > 0 || GetAll_RewardTotal.AddPower > 0 ||
					GetAll_RewardTotal.AddCash > 0 || GetAll_RewardTotal.PCRewardList.Count > 0 ||
					GetAll_CostTotal.AddMetal > 0 || GetAll_CostTotal.AddNutrient > 0 || GetAll_CostTotal.AddPower > 0
				) {
					this.gui_GetAll_ResultViewport.height = 0f;
					this.gui_GetAll_ResultScroll = Vector2.zero;

					var dict = new Dictionary<string, int>();
					foreach (var item in GetAll_RewardTotal.ItemRewardList) {
						var info = SingleTon<DataManager>.Instance.GetItem(item.Info.ItemSN);
						if (info != null && info.ItemType != 0 && info.ItemType != 1 && info.ItemType != 2) {
							var target = SingleTon<DataManager>.Instance.GetTableItemConsumable(info.ItemKeyString);
							var itemName = SingleTon<DataManager>.Instance.GetLocalization(target?.ItemName ?? info.ItemKeyString) ?? info.ItemKeyString;
							var itemCount = info.StackCount - info.BeforeStatckCount;
							if (dict.ContainsKey(itemName))
								dict[itemName] += itemCount;
							else
								dict.Add(itemName, itemCount);
						}
					}
					GetAll_RewardItemDictionary = dict;

					GetAll_DisplayGetAllResult = true;
					InstantPanel.Wait(true, true);
				}
			}
		}
		private void OnFacilityRewardPacket(WebResponseState obj) {
			W2C_FACILITY_REWARD w2CFacilityReward = obj as W2C_FACILITY_REWARD;
			if (w2CFacilityReward.result.ErrorCode != 0) return;
			if (!GetAll_GettingAll) return;
			if (GetAll_OnRewardPacket == null) return;

			GetAll_OnRewardPacket(w2CFacilityReward);
		}
		private void OnFacilityWorkPakcet(WebResponseState obj) {
			W2C_FACILITY_WORK w2CFacilityWork = obj as W2C_FACILITY_WORK;
			if (w2CFacilityWork.result.ErrorCode != 0) return;
			if (!GetAll_GettingAll) return;
			if (GetAll_OnWorkPacket == null) return;

			GetAll_OnWorkPacket(w2CFacilityWork);
		}
		private static void Panel_LivingStation_ShowSideMenu(bool _isEdit, bool _isGuide) {
			if(GetAll_BtnGetAll != null)
				GetAll_BtnGetAll.SetActive(_isEdit);
		}
		private static void FacilityRewardResult_Ready(Panel_FacilityRewardResult __instance) {
			if (!GetAll_GettingAll) return;

			IEnumerator Fn() {
				yield return null; // safety wait

				GetAll_OnRewardPopup?.Invoke(__instance);
			}
			__instance.StartCoroutine(Fn());
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

			var enoughResource = SingleTon<DataManager>.Instance.Metal >= last.Metal &&
				SingleTon<DataManager>.Instance.Nutrient >= last.Nutrient &&
				SingleTon<DataManager>.Instance.Power >= last.Power;
			if (enoughResource) {
				btn.onClick.Add(new(() => {
					var stage = SingleTon<DataManager>.Instance.GetTableChapterStage(last.StageKey);
					if(stage == null) {
						Plugin.Logger.LogWarning($"[Symphony::Automation] Stage info not found, key was {last.StageKey}");
						return;
					}

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

		private Rect gui_GetAll_ResultViewport = new Rect(0, 24, 248, 0);
		private Vector2 gui_GetAll_ResultScroll = Vector2.zero;

		private void OnGUI() {
			#region GetAll Result Popup
			if (GetAll_DisplayGetAllResult) {
				void PanelContent(int id) {
					var _offset = 0f;

					GUIX.Heading(new Rect(4, 4, 292, 20), "획득");
					GUIX.Heading(new Rect(204, 4, 292, 20), "소비");

					var panelRect = Rect.MinMaxRect(0, 24, 400, (300 - 18) - 40 - 10);
					this.gui_GetAll_ResultScroll = GUIX.ScrollView(panelRect, this.gui_GetAll_ResultScroll, this.gui_GetAll_ResultViewport, false, false, () => {
						GUIX.Group(new Rect(4, 4, 200 - 8, this.gui_GetAll_ResultViewport.height - 4), () => {
							var offset = 0f;

							if (GetAll_RewardTotal.AddMetal > 0) {
								GUIX.Label(
									new Rect(10, offset, 292, 20),
									$"부품 {GetAll_RewardTotal.AddMetal:#,##0}",
									new Color(0.941f, 0.941f, 0.702f)
								);
								offset += 20f;
							}
							if (GetAll_RewardTotal.AddNutrient > 0) {
								GUIX.Label(
									new Rect(10, offset, 292, 20),
									$"영양 {GetAll_RewardTotal.AddNutrient:#,##0}",
									new Color(0.259f, 1f, 0.384f)
								);
								offset += 20f;
							}
							if (GetAll_RewardTotal.AddPower > 0) {
								GUIX.Label(
									new Rect(10, offset, 292, 20),
									$"전력 {GetAll_RewardTotal.AddPower:#,##0}",
									new Color(0.255f, 1f, 0.871f)
								);
								offset += 20f;
							}
							if (GetAll_RewardTotal.AddCash > 0) {
								GUIX.Label(
									new Rect(10, offset, 292, 20),
									$"참치 {GetAll_RewardTotal.AddCash:#,##0}"
								);
								offset += 20f;
							}
							if (GetAll_RewardTotal.PCRewardList.Count > 0) {
								foreach (var pc in GetAll_RewardTotal.PCRewardList) {
									var chr = SingleTon<DataManager>.Instance.GetTableCharCollection(pc.Index);
									if (chr != null) {
										GUIX.Label(
											new Rect(10, offset, 292, 20),
											chr?.Char_Name ?? pc.Index
										);
										offset += 20f;
									}
								}
							}
							if (GetAll_RewardItemDictionary.Count > 0) {
								foreach (var kv in GetAll_RewardItemDictionary) {
									GUIX.Label(new Rect(10, offset, 292, 20), $"{kv.Key} {kv.Value:#,##0}");
									offset += 20f;
								}
							}

							_offset = Mathf.Max(_offset, offset);
						});
						GUIX.Group(new Rect(204, 4, 200 - 8, this.gui_GetAll_ResultViewport.height - 4), () => {
							var offset = 0f;

							if (GetAll_CostTotal.AddMetal > 0) {
								GUIX.Label(
									new Rect(10, offset, 292, 20),
									$"부품 {GetAll_CostTotal.AddMetal:#,##0}",
									new Color(0.941f, 0.941f, 0.702f)
								);
								offset += 20f;
							}
							if (GetAll_CostTotal.AddNutrient > 0) {
								GUIX.Label(
									new Rect(10, offset, 292, 20),
									$"영양 {GetAll_CostTotal.AddNutrient:#,##0}",
									new Color(0.259f, 1f, 0.384f)
								);
								offset += 20f;
							}
							if (GetAll_CostTotal.AddPower > 0) {
								GUIX.Label(
									new Rect(10, offset, 292, 20),
									$"전력 {GetAll_CostTotal.AddPower:#,##0}",
									new Color(0.255f, 1f, 0.871f)
								);
								offset += 20f;
							}

							_offset = Mathf.Max(_offset, offset);
						});
					});
					this.gui_GetAll_ResultViewport.height = _offset;

					if (GUIX.Button(new Rect(200 - 50, (300 - 18) - 40 - 5, 100, 40), "닫기")) {
						GetAll_DisplayGetAllResult = false;
						InstantPanel.Wait(show: false);
					}
				}

				var x = (float)Screen.width / 2f - 200;
				var y = (float)Screen.height / 2f - 150;
				GUIX.ModalWindow(0, new Rect(x, y, 400, 300), PanelContent, "기지 - 일괄 수령 결과", false);
			}
			#endregion
		}
	}
}
