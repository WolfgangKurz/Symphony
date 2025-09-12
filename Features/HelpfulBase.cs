using HarmonyLib;

using LO_ClientNetwork;

using Symphony.UI;

using System;
using System.Collections;
using System.Linq;

using UnityEngine;

namespace Symphony.Features {
	internal class HelpfulBase : MonoBehaviour {
		private static bool GettingAll = false;

		private WebGiveRewardInfo rewardTotal = null;
		private WebGiveRewardInfo costTotal = null;
		private bool DisplayGetAllResult = false;
		private long LastPacketFor = 0;

		public void Start() {
			var harmony = new Harmony("Symphony.HelpfulBase");
			harmony.Patch(
				AccessTools.Method(typeof(Panel_FacilityRewardResult), "Awake"),
				postfix: new HarmonyMethod(typeof(HelpfulBase), nameof(HelpfulBase.FacilityRewardResult_Awake))
			);

			EventManager.StartListening(this, 133U, new Action<WebResponseState>(this.OnFacilityWorkPakcet));
			EventManager.StartListening(this, 134U, new Action<WebResponseState>(this.OnFacilityRewardPacket));

			SceneListener.Instance.OnEnter("Scene_LivingStation", () => {
				Plugin.Logger.LogWarning("[Symphony.HelpfulBase] Scene_LivingStation detected");
				StartCoroutine(this.SetupBase());
			});
		}
		public void OnDestroy() {
			EventManager.StopListening(this);
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

				var facilities = GameObject.FindObjectsOfType<InstallationFacility>();
				Plugin.Logger.LogInfo("[Symphony.HelpfulBase] Facilities : " + facilities.Length.ToString());
				Plugin.Logger.LogInfo("[Symphony.HelpfulBase] Facilities to get : " + facilities.Where(x => x.GetState() == InstallationFacility.State.WorkComplete).Count().ToString());
				foreach (var fac in facilities) {
					if (fac.GetState() != InstallationFacility.State.WorkComplete) continue;

					Plugin.Logger.LogInfo("[Symphony.HelpfulBase] Click facility");
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
					//yield return new WaitForSecondsRealtime(0.155f);

					break;
				}
			} finally {
				GettingAll = false;

				if (this.rewardTotal.AddMetal > 0 || this.rewardTotal.AddNutrient > 0 || this.rewardTotal.AddPower > 0 ||
					this.rewardTotal.AddCash > 0 || this.rewardTotal.PCRewardList.Count > 0 ||
					this.costTotal.AddMetal > 0 || this.costTotal.AddNutrient > 0 || this.costTotal.AddPower > 0
				) {
					this.DisplayGetAllResult = true;
					InstantPanel.Wait(show: true);
				}
			}
		}

		private void OnFacilityRewardPacket(WebResponseState obj) {
			W2C_FACILITY_REWARD w2CFacilityReward = obj as W2C_FACILITY_REWARD;
			if (w2CFacilityReward.result.ErrorCode != 0) return;
			if (this.rewardTotal == null) return;
			if (!GettingAll) return;

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

			var x = (float)Screen.width / 2f - 300;
			var y = (float)Screen.height / 2f - 200;
			GUIX.ModalWindow(0, new Rect(x, y, 600, 400), this.PanelContent, "기지 - 일괄 수령 결과", false);
		}
		private void PanelContent(int id) {
			GUIX.Group(new Rect(4, 4, 300 - 8, 400 - 8 - 18 - 40), () => {
				GUIX.Heading(new Rect(0, 0, 292, 20), "획득");

				var offset = 24f;
				if (this.rewardTotal.AddMetal > 0) {
					GUIX.Label(
						new Rect(10, offset, 292, 20),
						$"부품 {this.rewardTotal.AddMetal.ToString("#,###")}",
						new Color(0.941f, 0.941f, 0.702f)
					);
					offset += 20f;
				}
				if (this.rewardTotal.AddNutrient > 0) {
					GUIX.Label(
						new Rect(10, offset, 292, 20),
						$"영양 {this.rewardTotal.AddNutrient.ToString("#,###")}",
						new Color(0.259f, 1f, 0.384f)
					);
					offset += 20f;
				}
				if (this.rewardTotal.AddPower > 0) {
					GUIX.Label(
						new Rect(10, offset, 292, 20),
						$"전력 {this.rewardTotal.AddPower.ToString("#,###")}",
						new Color(0.255f, 1f, 0.871f)
					);
					offset += 20f;
				}
				if (this.rewardTotal.AddCash > 0) {
					GUIX.Label(
						new Rect(10, offset, 292, 20),
						$"참치 {this.rewardTotal.AddCash.ToString("#,###")}"
					);
					offset += 20f;
				}
				if (this.rewardTotal.PCRewardList.Count > 0) {
					foreach (var pc in this.rewardTotal.PCRewardList) {
						var chr = SingleTon<DataManager>.Instance.GetTableCharCollection(pc.Index);
						GUIX.Label(
							new Rect(10, offset, 292, 20),
							chr?.Char_Name ?? pc.Index
						);
						offset += 20f;
					}
				}
			});
			GUIX.Group(new Rect(304, 4, 300 - 8, 400 - 8 - 18 - 40), () => {
				GUIX.Heading(new Rect(0, 0, 292, 20), "소비");

				var offset = 24f;
				if (this.costTotal.AddMetal > 0) {
					GUIX.Label(
						new Rect(10, offset, 292, 20),
						$"부품 {this.costTotal.AddMetal.ToString("#,###")}",
						new Color(0.941f, 0.941f, 0.702f)
					);
					offset += 20f;
				}
				if (this.costTotal.AddNutrient > 0) {
					GUIX.Label(
						new Rect(10, offset, 292, 20),
						$"영양 {this.costTotal.AddNutrient.ToString("#,###")}",
						new Color(0.259f, 1f, 0.384f)
					);
					offset += 20f;
				}
				if (this.costTotal.AddPower > 0) {
					GUIX.Label(
						new Rect(10, offset, 292, 20),
						$"전력 {this.costTotal.AddPower.ToString("#,###")}",
						new Color(0.255f, 1f, 0.871f)
					);
					offset += 20f;
				}
			});

			if(GUIX.Button(new Rect(250, 400 - 8 - 18 - 38, 100, 38), "닫기")) {
				this.DisplayGetAllResult = false;
				InstantPanel.Wait(show: false);
			}
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
