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
	internal class HelpfulBase : MonoBehaviour {
		private static bool GettingAll = false;

		private WebGiveRewardInfo rewardTotal = null;
		private WebGiveRewardInfo costTotal = null;
		private Dictionary<string, int> rewardItemDictionary = new();
		private bool DisplayGetAllResult = false;
		private long LastPacketFor = 0;

		private static GameObject btnGetAll = null;

		public void Start() {
			var harmony = new Harmony("Symphony.HelpfulBase");
			harmony.Patch(
				AccessTools.Method(typeof(Panel_FacilityRewardResult), "Awake"),
				postfix: new HarmonyMethod(typeof(HelpfulBase), nameof(HelpfulBase.FacilityRewardResult_Awake))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_LivingStation), "ShowSideMenu"),
				postfix: new HarmonyMethod(typeof(HelpfulBase), nameof(HelpfulBase.Panel_LivingStation_ShowSideMenu))
			);

			EventManager.StartListening(this, 133U, new Action<WebResponseState>(this.OnFacilityWorkPakcet));
			EventManager.StartListening(this, 134U, new Action<WebResponseState>(this.OnFacilityRewardPacket));

			SceneListener.Instance.OnEnter("Scene_LivingStation", () => {
				if (!Conf.HelpfulBase.Use_GetAll.Value) return;

				Plugin.Logger.LogWarning("[Symphony.HelpfulBase] Scene_LivingStation detected");
				StartCoroutine(this.SetupBase());
			});
		}
		public void OnDestroy() {
			EventManager.StopListening(this);
			btnGetAll = null;
		}

		private IEnumerator SetupBase() {
			yield return new WaitForEndOfFrame();
			yield return new WaitForEndOfFrame(); // ensure scene load

			var src = GameObject.Find("FacilityEditButton");
			if (src == null) {
				Plugin.Logger.LogWarning("[Symphony.HelpfulBase] Failed to find Facility edit button");
				yield break;
			}

			var btn = GameObject.Instantiate(src);
			btnGetAll = btn;
			btn.name = "FacilityGetAllButton";
			if (!btn.TryGetComponent<UIButton>(out var uiBtn)) {
				Plugin.Logger.LogWarning("[Symphony.HelpfulBase] Failed to get UIButton component for cloned button");
				Destroy(btn);
				yield break;
			}

			btn.transform.SetParent(src.transform.parent);
			btn.transform.localPosition = new Vector3(-112f, 150f);
			btn.transform.localScale = Vector3.one;

			uiBtn.normalSprite = "ui_Character_Info_Icon_book";
			var label = btn.GetComponentInChildren<UILabel>(true);
			if (label == null) {
				Plugin.Logger.LogWarning("[Symphony.HelpfulBase] Failed to get UILabel component for cloned button");
				Destroy(btn);
				yield break;
			}

			label.text = "일괄 수령";

			uiBtn.onClick.Clear();
			uiBtn.onClick.Add(new EventDelegate(() => {
				StartCoroutine(this.OnClickGetAll());
			}));

			// ...
			yield return new WaitForEndOfFrame();
			label.text = "일괄 수령";
		}

		private IEnumerator OnClickGetAll() {
			try {
				this.rewardTotal = new WebGiveRewardInfo();
				this.costTotal = new WebGiveRewardInfo();
				GettingAll = true;
				this.DisplayGetAllResult = false;

				var scene = GameObject.FindObjectOfType<Scene_LivingStation>();

				var facilities = GameObject.FindObjectsOfType<InstallationFacility>();
				var facilitiesToWork = facilities.Where(x => x.GetState() == InstallationFacility.State.WorkComplete);
				Plugin.Logger.LogDebug($"[Symphony.HelpfulBase] Facilities : {facilities.Length}");
				Plugin.Logger.LogDebug($"[Symphony.HelpfulBase] Facilities to get : {facilitiesToWork.Count()}");
				foreach (var fac in facilitiesToWork) {
					if (fac.GetState() != InstallationFacility.State.WorkComplete) continue;

					Plugin.Logger.LogDebug("[Symphony.HelpfulBase] Select facility");
					scene.kStation.GetType()
						.GetField("mCurrentFacility", BindingFlags.Instance | BindingFlags.NonPublic)
						.SetValue(scene.kStation, fac);
					fac.OnSelected();

					this.LastPacketFor = 0;
					yield return new WaitUntil(() => LastPacketFor == fac.Packet.Facility_uid);

					if (fac.Table.MetalCost > SingleTon<DataManager>.Instance.Metal &&
						fac.Table.NutrientCost > SingleTon<DataManager>.Instance.Nutrient &&
						fac.Table.PowerCost > SingleTon<DataManager>.Instance.Power
					) {
						Plugin.Logger.LogMessage("[Symphony.HelpfulBase] Not enough resources to restart facility");
						continue; // Not enough resource to restart
					}

					this.costTotal.AddMetal += (uint)fac.Table.MetalCost;
					this.costTotal.AddNutrient += (uint)fac.Table.NutrientCost;
					this.costTotal.AddPower += (uint)fac.Table.PowerCost;

					this.LastPacketFor = 0;
					yield return new WaitUntil(() => LastPacketFor == fac.Packet.Facility_uid);
					//yield return new WaitForSecondsRealtime(1f);
					//yield return new WaitForSecondsRealtime(0.155f);

					scene.kStation.SelectFacilityRelease();
				}
			} finally {
				GettingAll = false;

				Plugin.Logger.LogDebug($"[Symphony.HelpfulBase] RewardTotal.AddMetal : {this.rewardTotal.AddMetal}");
				Plugin.Logger.LogDebug($"[Symphony.HelpfulBase] RewardTotal.AddNutrient : {this.rewardTotal.AddNutrient}");
				Plugin.Logger.LogDebug($"[Symphony.HelpfulBase] RewardTotal.AddPower : {this.rewardTotal.AddPower}");
				Plugin.Logger.LogDebug($"[Symphony.HelpfulBase] RewardTotal.AddCash : {this.rewardTotal.AddCash}");
				Plugin.Logger.LogDebug($"[Symphony.HelpfulBase] RewardTotal.PCRewardList :");
				foreach (var pc in this.rewardTotal.PCRewardList) {
					var chr = SingleTon<DataManager>.Instance.GetTableCharCollection(pc.Index);
					Plugin.Logger.LogDebug($"     {chr.Char_Name}");
				}
				Plugin.Logger.LogDebug($"[Symphony.HelpfulBase] CostTotal.AddMetal : {this.rewardTotal.AddMetal}");
				Plugin.Logger.LogDebug($"[Symphony.HelpfulBase] CostTotal.AddNutrient : {this.rewardTotal.AddNutrient}");
				Plugin.Logger.LogDebug($"[Symphony.HelpfulBase] CostTotal.AddPower : {this.rewardTotal.AddPower}");

				if (this.rewardTotal.AddMetal > 0 || this.rewardTotal.AddNutrient > 0 || this.rewardTotal.AddPower > 0 ||
					this.rewardTotal.AddCash > 0 || this.rewardTotal.PCRewardList.Count > 0 ||
					this.costTotal.AddMetal > 0 || this.costTotal.AddNutrient > 0 || this.costTotal.AddPower > 0
				) {
					this.resultViewport.height = 0f;
					this.resultScroll = Vector2.zero;

					var dict = new Dictionary<string, int>();
					foreach (var item in this.rewardTotal.ItemRewardList) {
						var info = SingleTon<DataManager>.Instance.GetItem(item.Info.ItemSN);
						if (info != null && info.ItemType != 0 && info.ItemType != 1 && info.ItemType != 2) {
							var target = SingleTon<DataManager>.Instance.GetTableItemConsumable(info.ItemKeyString);
							var itemName = Localization.Get(target?.ItemName ?? info.ItemKeyString);
							var itemCount = info.StackCount - info.BeforeStatckCount;
							if (dict.ContainsKey(itemName))
								dict[itemName] += itemCount;
							else
								dict.Add(itemName, itemCount);
						}
					}
					this.rewardItemDictionary = dict;

					this.DisplayGetAllResult = true;
					InstantPanel.Wait(true, true);
				}
			}
		}

		private void OnFacilityRewardPacket(WebResponseState obj) {
			W2C_FACILITY_REWARD w2CFacilityReward = obj as W2C_FACILITY_REWARD;
			if (w2CFacilityReward.result.ErrorCode != 0) return;
			if (!GettingAll) return;
			if (this.rewardTotal == null) return;

			// stack results
			var result = w2CFacilityReward.result;
			var res = result.RewardInfo;
			this.rewardTotal.PCRewardList.AddRange(res.PCRewardList ?? []);
			this.rewardTotal.AddMetal += res.AddMetal;
			this.rewardTotal.AddNutrient += res.AddNutrient;
			this.rewardTotal.AddPower += res.AddPower;
			this.rewardTotal.AddCash += res.AddCash;
			this.rewardTotal.ItemRewardList.AddRange(res.ItemRewardList ?? []);

			this.LastPacketFor = result.Facility_uid;
		}
		private void OnFacilityWorkPakcet(WebResponseState obj) {
			W2C_FACILITY_WORK w2CFacilityWork = obj as W2C_FACILITY_WORK;
			if (w2CFacilityWork.result.ErrorCode != 0) return;
			if (!GettingAll) return;

			var result = w2CFacilityWork.result;
			this.LastPacketFor = result.Facility_uid;
		}

		private void OnGUI() {
			if (!this.DisplayGetAllResult) return;

			var x = (float)Screen.width / 2f - 200;
			var y = (float)Screen.height / 2f - 150;
			GUIX.ModalWindow(0, new Rect(x, y, 400, 300), this.PanelContent, "기지 - 일괄 수령 결과", false);
		}

		private Rect resultViewport = new Rect(0, 24, 248, 0);
		private Vector2 resultScroll = Vector2.zero;
		private void PanelContent(int id) {
			var _offset = 0f;

			GUIX.Heading(new Rect(4, 4, 292, 20), "획득");
			GUIX.Heading(new Rect(204, 4, 292, 20), "소비");

			var panelRect = Rect.MinMaxRect(0, 24, 400, (300 - 18) - 40 - 10);
			this.resultScroll = GUIX.ScrollView(panelRect, this.resultScroll, this.resultViewport, false, false, () => {
				GUIX.Group(new Rect(4, 4, 200 - 8, this.resultViewport.height - 4), () => {
					var offset = 0f;

					if (this.rewardTotal.AddMetal > 0) {
						GUIX.Label(
							new Rect(10, offset, 292, 20),
							$"부품 {this.rewardTotal.AddMetal:#,##0}",
							new Color(0.941f, 0.941f, 0.702f)
						);
						offset += 20f;
					}
					if (this.rewardTotal.AddNutrient > 0) {
						GUIX.Label(
							new Rect(10, offset, 292, 20),
							$"영양 {this.rewardTotal.AddNutrient:#,##0}",
							new Color(0.259f, 1f, 0.384f)
						);
						offset += 20f;
					}
					if (this.rewardTotal.AddPower > 0) {
						GUIX.Label(
							new Rect(10, offset, 292, 20),
							$"전력 {this.rewardTotal.AddPower:#,##0}",
							new Color(0.255f, 1f, 0.871f)
						);
						offset += 20f;
					}
					if (this.rewardTotal.AddCash > 0) {
						GUIX.Label(
							new Rect(10, offset, 292, 20),
							$"참치 {this.rewardTotal.AddCash:#,##0}"
						);
						offset += 20f;
					}
					if (this.rewardTotal.PCRewardList.Count > 0) {
						foreach (var pc in this.rewardTotal.PCRewardList) {
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
					if (this.rewardTotal.ItemRewardList.Count > 0) {
						foreach (var kv in this.rewardItemDictionary) {
							GUIX.Label(new Rect(10, offset, 292, 20), $"{kv.Key} {kv.Value:#,##0}");
							offset += 20f;
						}
					}

					_offset = Mathf.Max(_offset, offset);
				});
				GUIX.Group(new Rect(204, 4, 200 - 8, this.resultViewport.height - 4), () => {
					var offset = 0f;

					if (this.costTotal.AddMetal > 0) {
						GUIX.Label(
							new Rect(10, offset, 292, 20),
							$"부품 {this.costTotal.AddMetal:#,##0}",
							new Color(0.941f, 0.941f, 0.702f)
						);
						offset += 20f;
					}
					if (this.costTotal.AddNutrient > 0) {
						GUIX.Label(
							new Rect(10, offset, 292, 20),
							$"영양 {this.costTotal.AddNutrient:#,##0}",
							new Color(0.259f, 1f, 0.384f)
						);
						offset += 20f;
					}
					if (this.costTotal.AddPower > 0) {
						GUIX.Label(
							new Rect(10, offset, 292, 20),
							$"전력 {this.costTotal.AddPower:#,##0}",
							new Color(0.255f, 1f, 0.871f)
						);
						offset += 20f;
					}

					_offset = Mathf.Max(_offset, offset);
				});
			});
			this.resultViewport.height = _offset;

			if (GUIX.Button(new Rect(200 - 50, (300 - 18) - 40 - 5, 100, 40), "닫기")) {
				this.DisplayGetAllResult = false;
				InstantPanel.Wait(show: false);
			}
		}

		private static void Panel_LivingStation_ShowSideMenu(bool _isEdit, bool _isGuide) {
			if(btnGetAll != null)
				btnGetAll.SetActive(_isEdit);
		}

		private static void FacilityRewardResult_Awake(Panel_FacilityRewardResult __instance) {
			if (!GettingAll) return;

			__instance.StartCoroutine(FacilityRewardResult_Awake_Coroutine(__instance));
		}
		private static IEnumerator FacilityRewardResult_Awake_Coroutine(Panel_FacilityRewardResult __instance) {
			var button = __instance.GetComponentsInChildren<UIButton>().FirstOrDefault(x => x.name == "RestartButton");
			if (button == null) yield break;

			yield return null; // safety wait

			EventDelegate.Execute(button.onClick);
		}
	}
}
