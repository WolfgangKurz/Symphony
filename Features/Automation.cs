using HarmonyLib;

using LO_ClientNetwork;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEngine;

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
		}

		#region Base CollectAll Restart
		private static bool Panel_LivingStation_OnCollectAllButton(Panel_LivingStation __instance) {
			if (!Conf.Automation.Use_Base_CollectAll_Restart.Value) return true;

			IEnumerator Fn() {
				var targetFacilities = FindObjectsByType<InstallationFacility>(FindObjectsSortMode.None)
					.Where(x => x.mCurrentState == InstallationFacility.State.WorkComplete
					|| x.mCurrentState == InstallationFacility.State.Prepare)
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

				if(insufficientRes) {
					InstantPanel.MessageBox("자원이 부족해 일부 시설을 재시작할 수 없었습니다.");
				}
			}
			__instance.StartCoroutine(Fn());

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
	}
}
