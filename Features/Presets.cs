using HarmonyLib;

using Symphony.UI;

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using UnityEngine;

namespace Symphony.Features {
	internal class Presets : MonoBehaviour {
		private class CharMakingPresetData {
			private static readonly Regex reg = new("^([0-9]+),([0-9]+),([0-9]+),([0-9]+),([0-9]+),([0-9]+),(.+)$", RegexOptions.Compiled);

			public bool IsValid { get; }
			public int[] res;
			public string name;

			public CharMakingPresetData() {
				this.res = [0, 0, 0, 0, 0, 0];
				this.name = "New Preset";
				this.IsValid = true;
			}
			public CharMakingPresetData(int[] res, string name) {
				this.res = res.Concat([0, 0, 0, 0, 0, 0]).Take(6).ToArray(); // Fill and make copy
				this.name = name;
				this.IsValid = true;
			}

			public CharMakingPresetData(string data) {

				if (!reg.IsMatch(data)) {
					this.IsValid = false;
					this.res = [0, 0, 0, 0, 0, 0]; // to prevent error
					this.name = "";
					return;
				}

				var m = reg.Match(data);
				this.res = [
					int.Parse(m.Groups[1].Value), // metal
					int.Parse(m.Groups[2].Value), // power
					int.Parse(m.Groups[3].Value), // nutrient head
					int.Parse(m.Groups[4].Value), // nutrient breast
					int.Parse(m.Groups[5].Value), // nutrient legs
					int.Parse(m.Groups[6].Value) // adv module
				];
				this.name = m.Groups[7].Value;
				this.IsValid = true;
			}

			public string Serialize() {
				return string.Join(",", res) + "," + name;
			}
		}

		private static bool gui_DisplayCharMakingPreset = false;
		private static string gui_CharMakingPreset_Name = "";
		private static Vector2 gui_CharMakingPreset_Scroll = Vector2.zero;
		private static Rect gui_CharMakingPreset_Viewport = Rect.zero;

		private static CharMakingPresetData[] CharMakingPresets = [];
		private static int[] LastCharMakingData = [0, 0, 0, 0, 0, 0];

		private static UIAtlas asset_masterAtlas = null;

		public void Start() {
			var harmony = new Harmony("Symphony.Presets");
			harmony.Patch(
				AccessTools.Method(typeof(Panel_Character_Creator), nameof(Panel_Character_Creator.Start)),
				postfix: new HarmonyMethod(typeof(Presets), nameof(Presets.Preset_CharacterCreate))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_Character_Creator), nameof(Panel_Character_Creator.OnBtnUnitMaking)),
				prefix: new HarmonyMethod(typeof(Presets), nameof(Presets.Preset_CharacterCreate_Memorize))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_Character_Creator), nameof(Panel_Character_Creator.OnBtnTotalMaking)),
				prefix: new HarmonyMethod(typeof(Presets), nameof(Presets.Preset_CharacterCreate_Memorize))
			);
		}

		private static void LazyInit() {
			if (asset_masterAtlas == null) {
				asset_masterAtlas = SingleTon<ResourceManager>.Instance.LoadAtlas("masterAtlas");
			}
		}

		#region Methods
		private static int Get_SpModule_Index(int Modules) {
			switch (Modules) {
				case 10: return 0;
				case 20: return 1;
				case 50: return 2;
				case 100: return 3;
				default:
					return 0;
			}
		}

		private static void SetCharacterMakingData_To_Panel(int Metal, int Power, int NutrientHead, int NutrientBreast, int NutrientLegs, int Modules = 0) {
			IEnumerator LogCallback(Panel_Character_Creator panel, int Modules) {
				yield return null;

				Plugin.Logger.LogDebug("[Symphony::Presets] Changing character making type");
				panel.XGetFieldValue<UIButton>("_btnCreate").isEnabled = true;
				panel.XGetFieldValue<UIToggle>("_toggleAndroidNormal").Set(Modules == 0);
				panel.XGetFieldValue<UIToggle>("_toggleAndroidSpecial").Set(Modules != 0);
				panel.XGetFieldValue<GameObject>("_godigitsSpecial").SetActive(Modules != 0);
				panel.OnBtnAndroidPos(panel.XGetFieldValue<PCMAKING_BIO_POS_TYPE>("_bioPosType"));

				if (Modules > 0) {
					yield return null;
					panel.LogMakingSpCallBack();
				}
				else {
					yield return null;
					panel.LogMakingCallBack();
				}
			}

			Plugin.Logger.LogDebug("[Symphony::Presets] Trying to load preset to making panel");

			if (Modules == 0) {
				SingleTon<DataManager>.Instance.AndroidResource[0] = (ushort)Metal;
				SingleTon<DataManager>.Instance.AndroidResource[4] = (ushort)Power;
				SingleTon<DataManager>.Instance.AndroidResource[1] = (ushort)NutrientHead;
				SingleTon<DataManager>.Instance.AndroidResource[2] = (ushort)NutrientBreast;
				SingleTon<DataManager>.Instance.AndroidResource[3] = (ushort)NutrientLegs;
			}
			else {
				SingleTon<DataManager>.Instance.SpecialAndroidResource[0] = (ushort)Metal;
				SingleTon<DataManager>.Instance.SpecialAndroidResource[4] = (ushort)Power;
				SingleTon<DataManager>.Instance.SpecialAndroidResource[1] = (ushort)NutrientHead;
				SingleTon<DataManager>.Instance.SpecialAndroidResource[2] = (ushort)NutrientBreast;
				SingleTon<DataManager>.Instance.SpecialAndroidResource[3] = (ushort)NutrientLegs;
				SingleTon<DataManager>.Instance.ModuleAmountIndex = Get_SpModule_Index(Modules);
			}

			var panel = FindObjectOfType<Panel_Character_Creator>();
			if (panel == null) return;

			Plugin.Logger.LogDebug("[Symphony::Presets] Show dummy message to prevent close making screen");
			panel.ShowMessage(
				"임시 메시지입니다.\n이 메시지가 자동으로 닫히지 않는다면, 직접 닫아도 됩니다.",
				"[Symphony::Presets]",
				"", ""
			);

			panel.StartCoroutine(LogCallback(panel, Modules));
		}
		#endregion

		#region Character Making Preset & Last Character Making Data
		private static void Preset_CharacterCreate(Panel_Character_Creator __instance) {
			LazyInit();

			if (Conf.Presets.Use_Last_CharMakingData.Value) {
				var r = LastCharMakingData = Conf.Presets.Last_CharMaking_Data.Value
					.Split(",")
					.Select(r => int.TryParse(r, out var v) ? v : 0)
					.Concat([0, 0, 0, 0, 0, 0])
					.Take(6)
					.ToArray();

				SetCharacterMakingData_To_Panel(r[0], r[1], r[2], r[3], r[4], r[5]);
			}

			if (!Conf.Presets.Use_CharMaking_Preset.Value) return;

			var bioroid_creation_panel = __instance.transform.Find("resource_slot_penal_android");

			var _normal = __instance.GetType()
				.GetField("_toggleAndroidNormal", BindingFlags.Instance | BindingFlags.NonPublic)
				.GetValue<UIToggle>(__instance);
			var btnPercentage = _normal.transform.Find("BtnPercentage").gameObject;

			var btn_preset = GameObject.Instantiate(btnPercentage, bioroid_creation_panel);
			btn_preset.name = "Btn_Preset";
			btn_preset.transform.localPosition = new Vector3(35.22f, -445.878f, 0f);
			{
				DestroyImmediate(btn_preset.GetComponentInChildren<UILocalize>()); // Remove localization

				var sp = btn_preset.GetComponentInChildren<UISprite>();
				sp.atlas = asset_masterAtlas;
				sp.spriteName = "CostInputBtn_N";
				sp.width = 180;
				sp.height = 70;

				var logLb = btn_preset.transform.Find("LogLb");
				logLb.localPosition = Vector3.zero;

				var lbl = logLb.GetComponent<UILabel>();
				lbl.text = "프리셋";
			}

			var BtnPreset = btn_preset.GetComponentInChildren<UIButton>();
			BtnPreset.onClick.Clear();
			BtnPreset.onClick.Add(new(() => {
				CharMakingPresets = Conf.Presets.CharMaking_Preset_Data.Value
					.Split("\n")
					.Select(r => new CharMakingPresetData(r))
					.Where(r => r.IsValid)
					.ToArray();
				LastCharMakingData = Conf.Presets.Last_CharMaking_Data.Value
					.Split(",")
					.Select(r => int.TryParse(r, out var v) ? v : 0)
					.Concat([0, 0, 0, 0, 0, 0])
					.Take(6)
					.ToArray();

				gui_CharMakingPreset_Name = "";
				gui_CharMakingPreset_Viewport = Rect.zero;
				gui_DisplayCharMakingPreset = true;
				InstantPanel.Wait(true, true);
			}));

			gui_CharMakingPreset_Scroll = Vector2.zero;
			Plugin.Logger.LogDebug("[Symphony::Presets] Preset button for Character Making has been generated");
		}
		private static void Preset_CharacterCreate_Memorize(Panel_Character_Creator __instance) {
			if (!__instance.XGetMethod<bool>("IsEnableUnitMaking").Invoke()) return;

			var inp = SingleTon<DataManager>.Instance.AndroidResource.Select(x => (int)x).Concat([0]);
			if (__instance.XGetFieldValue<byte>("_CreateTypeTab") == 1) { // TAB_CREATETYPE_SPECIAL
				inp = SingleTon<DataManager>.Instance.SpecialAndroidResource
					.Select(x => (int)x)
					.Concat([SingleTon<DataManager>.Instance.ModuleAmount[SingleTon<DataManager>.Instance.ModuleAmountIndex]]);
			}

			// Memorize
			Plugin.Logger.LogInfo("[Symphony::Presets] Last Character Making memorized");

			var arr = inp.ToArray();
			var ret = new int[] { arr[0], arr[4], arr[1], arr[2], arr[3], arr[5] };
			Conf.Presets.Last_CharMaking_Data.Value = string.Join(",", ret);
		}
		#endregion

		public void OnGUI() {
			#region Character Making Preset
			if (gui_DisplayCharMakingPreset) {
				void ClosePreset() {
					gui_DisplayCharMakingPreset = false;
					InstantPanel.Wait(false);
				}
				void PanelContent(int id) {
					var offset = 0f;

					var panelRect = Rect.MinMaxRect(0, 0, Mathf.Min(Screen.width - 20, 440), Screen.height - 34 - 20 - 18);
					gui_CharMakingPreset_Viewport = new Rect(
						panelRect.xMin, panelRect.yMin,
						panelRect.width,
						Mathf.Max(panelRect.height, gui_CharMakingPreset_Viewport.height) // overflow only
					);

					var w = panelRect.width;
					var h = Screen.height - 20;

					gui_CharMakingPreset_Scroll = GUIX.ScrollView(
						panelRect,
						gui_CharMakingPreset_Scroll,
						gui_CharMakingPreset_Viewport,
						false, true,
						() => {
							var lw = 0f;

							void DrawPreset(float y, string name, int[] res, Action action = null, bool deletable = true, Action delete = null) {
								if (res.Length != 6) return;

								var isNormal = res[5] == 0;
								Color? c1 = isNormal ? null : Color.HSVToRGB(0f, 0.6f, 0.6f);
								Color? c2 = isNormal ? null : Color.HSVToRGB(0f, 0.7f, 0.7f);
								Color? c3 = isNormal ? null : Color.HSVToRGB(0f, 0.8f, 0.8f);

								var _w = deletable ? w - 20 - 2 - 40 : w - 20;
								GUIX.Group(new Rect(2, y + 2, _w, 56), () => {
									if (GUIX.Button(new Rect(0, 0, _w, 48 + 4), "", c1, c2, c3)) action?.Invoke();

									var r1 = res[0].ToString();
									var r2 = res[1].ToString();
									var r3 = string.Join("  ", res.Skip(2).Take(3).Select(x => x.ToString()));
									var r4 = !isNormal ? res[5].ToString() : "";

									var tmpX = 48f;
									var tmpY = 0f;
									GUIX.Heading(
										new Rect(2, 2, 48, 48),
										isNormal ? "일반" : "특수",
										alignment: TextAnchor.MiddleCenter
									);

									GUIX.Heading(new Rect(2 + tmpX + 4, 2, _w - 2 - 48 - 8, 24), name);
									tmpY += 24f;

									GUIX.DrawAtlasSprite(new Rect(2 + tmpX + 4, 2 + tmpY, 24, 23), asset_masterAtlas, "UI_Icon_Gear");
									GUIX.Label(new Rect(4 + tmpX + 24 + 4, 2 + tmpY, 200, 20), r1);
									lw = GUIX.Label(r1).x;
									tmpX += 24 + lw + 10;

									GUIX.DrawAtlasSprite(new Rect(4 + tmpX, 2 + tmpY + 2, 24, 18), asset_masterAtlas, "UI_Icon_Power");
									GUIX.Label(new Rect(4 + tmpX + 24 + 4, 2 + tmpY, 200, 20), r2);
									lw = GUIX.Label(r2).x;
									tmpX += 24 + lw + 10;

									GUIX.DrawAtlasSprite(new Rect(4 + tmpX, 2 + tmpY + 2, 24, 19), asset_masterAtlas, "UI_Icon_Nutrient");
									GUIX.Label(new Rect(4 + tmpX + 24 + 4, 2 + tmpY, 200, 20), r3);

									if (r4.Length > 0) {
										lw = GUIX.Label(r3).x;
										tmpX += 24 + lw + 10;

										GUIX.DrawAtlasSprite(new Rect(4 + tmpX + 2, 2 + tmpY, 19, 24), asset_masterAtlas, "UI_Icon_Consumable_SpModule");
										GUIX.Label(new Rect(4 + tmpX + 24 + 4, 2 + tmpY, 200, 20), r4);
									}
								});

								if (deletable) {
									if (GUIX.Button(
										new Rect(_w + 4, y + 2, 40, 52),
										"X",
										Color.HSVToRGB(0f, 0.6f, 0.6f),
										Color.HSVToRGB(0f, 0.7f, 0.7f),
										Color.HSVToRGB(0f, 0.8f, 0.8f)
									))
										delete?.Invoke();
								}
							}

							DrawPreset(offset, "마지막 제조 기록", LastCharMakingData, () => {
								var r = LastCharMakingData;
								SetCharacterMakingData_To_Panel(r[0], r[1], r[2], r[3], r[4], r[5]);
								ClosePreset();
							}, false);
							offset += 56 + 2;

							if (LastCharMakingData[0] > 0) { // Only when last making data exists
								gui_CharMakingPreset_Name = GUIX.TextField(new Rect(2, offset, w - 20 - 80 - 2, 20), gui_CharMakingPreset_Name);

								if (GUIX.Button(new Rect(w - 20 - 80 + 2, offset, 80, 20), "프리셋 추가")) {
									var ret = CharMakingPresets
										.Concat([new CharMakingPresetData(LastCharMakingData, gui_CharMakingPreset_Name)])
										.ToArray();
									CharMakingPresets = ret;

									Conf.Presets.CharMaking_Preset_Data.Value = string.Join("\n", ret.Select(r => r.Serialize()));

									gui_CharMakingPreset_Name = "";
								}
								offset += 20;
							}

							offset += 4;
							GUIX.HLine(new Rect(2, offset, w - 4, 0));
							offset += 4;

							for (var i = 0; i < CharMakingPresets.Length; i++) {
								var pre = CharMakingPresets[i];
								DrawPreset(offset, pre.name, pre.res, () => {
									SetCharacterMakingData_To_Panel(pre.res[0], pre.res[1], pre.res[2], pre.res[3], pre.res[4], pre.res[5]);
									ClosePreset();
								}, true, () => {
									var ret = CharMakingPresets.Where((_, idx) => idx != i);
									CharMakingPresets = ret.ToArray();

									Conf.Presets.CharMaking_Preset_Data.Value =
										string.Join("\n", ret.Select(r => r.Serialize()));
								});
								offset += 56 + 2;
							}
						}
					);
					gui_CharMakingPreset_Viewport.height = offset;

					if (GUIX.Button(
						new Rect(w - 80, h - 32 - 18, 80, 32),
						"닫기",
						Color.HSVToRGB(0f, 0.6f, 0.6f),
						Color.HSVToRGB(0f, 0.7f, 0.7f),
						Color.HSVToRGB(0f, 0.8f, 0.8f)
					)) {
						ClosePreset();
					}
				}

				var w = Mathf.Min(Screen.width - 20, 440);
				var h = Screen.height - 20;
				var x = (float)Screen.width / 2f - w / 2;
				var y = (float)Screen.height / 2f - h / 2;
				GUIX.ModalWindow(0, new Rect(x, y, w, h), PanelContent, "전투원 제조 프리셋", false);
			}
		}
		#endregion
	}
}
