using BepInEx.Configuration;

using LOEventSystem;
using LOEventSystem.Msg;

using Symphony.Features;
using Symphony.Features.KeyMapping;

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace Symphony.UI.Panels {
	internal partial class ConfigPanel : UIPanelBase {
		private delegate void VoidDelegate();
		private delegate string SliderTemplateDelegate(float value);

		private readonly Color Color_description = new(0.9f, 0.9f, 0.9f, 0.9f);

		public override Rect rc { get; set; } = new Rect(10f, 30f, 422f, 500f);

		private const float WIDTH_FILL = 408f - 120f - 8f;
		private const float HALF_FILL = WIDTH_FILL / 2;

		private Rect panelViewport = new Rect(0, 0, 248, 0);
		private Vector2 panelScroll = Vector2.zero;

		private string experimental_KeyMapping_NewName = "";

		private enum IconKey : int {
			None,
			Bell,
			Brush,
			Carrot,
			Construction,
			Gear,
			Keyboard,
			Presets,
			Robot,
			TrafficLight,
			TV,
		}
		private static Dictionary<IconKey, Texture2D> Icons = new();

		private readonly (IconKey icon, string key, string disp)[] PluginFeatures = [
			(IconKey.Gear, "QuickConfig", null),
			(IconKey.TV, "GracefulFPS", null),
			(IconKey.Carrot, "SimpleTweaks", null),
			(IconKey.Brush, "SimpleUI", null),
			(IconKey.Bell, "Notification", null),
			(IconKey.Presets, "Presets", null),
			(IconKey.Robot, "Automation", null),
			(IconKey.Construction, "Experimental", null)
		];
		private string SelectedFeature = "QuickConfig";

		public bool locked = false;

		static ConfigPanel() {
			void LoadIcon(IconKey key, byte[] data) {
				Icons.Add(key, new Texture2D(1, 1, TextureFormat.ARGB32, false));
				Icons[key].LoadImage(data);
			}

			LoadIcon(IconKey.Bell, Resource.icon_bell);
			LoadIcon(IconKey.Brush, Resource.icon_brush);
			LoadIcon(IconKey.Carrot, Resource.icon_carrot);
			LoadIcon(IconKey.Construction, Resource.icon_construction);
			LoadIcon(IconKey.Gear, Resource.icon_gear);
			LoadIcon(IconKey.Keyboard, Resource.icon_keyboard);
			LoadIcon(IconKey.Presets, Resource.icon_presets);
			LoadIcon(IconKey.Robot, Resource.icon_robot);
			LoadIcon(IconKey.TrafficLight, Resource.icon_traffic_light);
			LoadIcon(IconKey.TV, Resource.icon_tv);
		}

		public ConfigPanel(MonoBehaviour instance) : base(instance) { }

		public override void Update() { }
		public override void OnGUI() {
			if (this.locked) return; // skip if locked
			rc = GUIX.ModalWindow(0, rc, this.PanelContent, "Symphony | LastOrigin QoL Plugin | F1", true);
		}

		#region Config Element Shorthand
		private void KeepOffset(ref float offset, VoidDelegate render) {
			var prev = offset;
			render?.Invoke();
			offset = prev;
		}
		private void DrawLabel(ref float offset, string text, Color? color = null, float leftMargin = 0f, float rightMargin = 0f) {
			var h = GUIX.Label(text, WIDTH_FILL - leftMargin - rightMargin, wrap: true).y;
			GUIX.Label(
				new Rect(leftMargin, offset, WIDTH_FILL - leftMargin - rightMargin, h),
				text,
				color,
				wrap: true
			);
			offset += h + (10 - (h % 10)) + 4;
		}
		private void DrawToggle(ref float offset, string name, ConfigEntry<bool> config, float leftMargin = 0f, float rightMargin = 0f) {
			var value = GUIX.Toggle(
					new Rect(leftMargin, offset, WIDTH_FILL - leftMargin - rightMargin, 20),
					config.Value,
					name
				);
			if (value != config.Value) {
				config.Value = value;
				Conf.config.Save();
			}
			offset += 20 + 4;
		}
		private void DrawToggle(ref float offset, string name, ref bool value, VoidDelegate onChecked, float leftMargin = 0f, float rightMargin = 0f) {
			var prev = value;
			value = GUIX.Toggle(
					new Rect(leftMargin, offset, WIDTH_FILL - leftMargin - rightMargin, 20),
					value,
					name
				);
			if (value != prev) onChecked?.Invoke();
			offset += 20 + 4;
		}
		private void DrawSeparator(ref float offset) {
			GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
			offset += 1 + 4;
		}
		private void DrawLineButton(ref float offset, string text, VoidDelegate onClick, float leftMargin = 0f, float rightMargin = 0f) {
			if(GUIX.Button(
				new Rect(leftMargin, offset, WIDTH_FILL - leftMargin - rightMargin, 20),
				text
			)) onClick?.Invoke();
			offset += 20 + 4;
		}
		private void DrawSlider(
			ref float offset, string text, ConfigEntry<float> config, VoidDelegate onChange = null,
			float min = 0f, float max = 1f, float step = 0.005f, SliderTemplateDelegate template = null,
			float labelWidth = 80f, float leftMargin = 0f, float rightMargin = 0f) {

			GUIX.Label(new Rect(0, offset, labelWidth, 20), text);
			var prev = config.Value;
			float v;
			if (step == 0f) {
				v = GUIX.HorizontalSlider(
					new Rect(labelWidth + leftMargin, offset, WIDTH_FILL - labelWidth - leftMargin - rightMargin, 20),
					config.Value, min, max,
					v => template == null ? (v * 100f).ToString("0.0") + " %" : template.Invoke(v)
				);
			}
			else {
				var output = GUIX.HorizontalSlider(
					new Rect(labelWidth + leftMargin, offset, WIDTH_FILL - labelWidth - leftMargin - rightMargin, 20),
					config.Value, min, max,
					v => template == null ? (v * 100f).ToString("0.0") + " %" : template.Invoke(v)
				);
				v = Mathf.Round(output / step) * step;
			}

			if (v != prev) {
				config.Value = v;
				Conf.config.Save();
				onChange?.Invoke();
			}
			offset += 20 + 4;
		}
		private void DrawSlider(
			ref float offset, string text, ref float value, VoidDelegate onChange,
			float min = 0f, float max = 1f, float step = 0.005f, SliderTemplateDelegate template = null,
			float labelWidth = 80f, float leftMargin = 0f, float rightMargin = 0f) {

			GUIX.Label(new Rect(0, offset, labelWidth, 20), text);
			var prev = value;
			if (step == 0f) {
				value = GUIX.HorizontalSlider(
					new Rect(labelWidth + leftMargin, offset, WIDTH_FILL - labelWidth - leftMargin - rightMargin, 20),
					value, min, max,
					v => template == null ? (v * 100f).ToString("0.0") + " %" : template.Invoke(v)
				);
			}
			else {
				var output = GUIX.HorizontalSlider(
					new Rect(labelWidth + leftMargin, offset, WIDTH_FILL - labelWidth - leftMargin - rightMargin, 20),
					value, min, max,
					v => template == null ? (v * 100f).ToString("0.0") + " %" : template.Invoke(v)
				);
				output = Mathf.Round(output / step) * step;
				value = output;
			}

			if (value != prev) onChange?.Invoke();
			offset += 20 + 4;
		}
		private void DrawRadio<T>(
			ref float offset, Dictionary<T, string> options, ConfigEntry<T> config, VoidDelegate onChange = null,
			float leftMargin = 0f, float rightMargin = 0f) where T : IEquatable<T> {

			var x = 0f;
			foreach(var opt in options) {
				var w = GUIX.Label(opt.Value).x + 20f + 5f;
				if (GUIX.Radio(new Rect(leftMargin + x, offset, w, 20), config.Value.Equals(opt.Key), opt.Value)) {
					config.Value = opt.Key;
					Conf.config.Save();
					onChange?.Invoke();
				}
				x += w + 10f;
			}
			offset += 20 + 4;
		}
		private void DrawKeyBinder(
			ref float offset, string text, ConfigEntry<string> config,
			VoidDelegate onChange = null, float leftMargin = 0f, float rightMargin = 0f) {

			var uuid = Guid.NewGuid().ToString();
			GUIX.Label(new Rect(leftMargin, offset, HALF_FILL - leftMargin, 20), text);
			GUIX.KeyBinder(
				$"DrawKeyBinder:{uuid}",
				new Rect(HALF_FILL, offset, HALF_FILL - rightMargin, 20),
				config.Value,
				KeyCode => {
					config.Value = KeyCode.ToString();
					Conf.config.Save();
					onChange?.Invoke();
				}
			);
			offset += 20 + 4;
		}
		#endregion

		private void PanelContent(int id) {
			var ec = Event.current;
			var goffset = 0;

			float float_temp = 0f;
			bool bool_temp = false;

			#region Plugin Name Section
			GUIX.Heading(new Rect(4, 2, 72, 18), "Symphony", Color.yellow);
			GUIX.Label(new Rect(76, 4, rc.width - 84 - 120, 16), Plugin.VersionTag);

			if (GUIX.Button(new Rect(4 + rc.width - 8 - 120, 1, 120, 20), "Release Note"))
				UIManager.Instance.AddPanel(new ReleaseNotePanel(this.instance));
			goffset += 20 + 4;
			#endregion

			GUIX.HLine(new Rect(4, 24, rc.width - 8, 0));
			goffset += 1;

			#region BepInEx console disable
			var consoleUsing = BepInEx.ConsoleManager.ConfigConsoleEnabled.Value;
			if (consoleUsing) {
				goffset += 4;

				if (GUIX.Button(
					new Rect(4, 29, rc.width - 8, 20),
					"BepInEx 콘솔 끄기",
					Color.HSVToRGB(0f, 0.6f, 0.6f),
					Color.HSVToRGB(0f, 0.7f, 0.7f),
					Color.HSVToRGB(0f, 0.8f, 0.8f)
				)) {
					BepInEx.ConsoleManager.ConfigConsoleEnabled.Value = false;
					BepInEx.ConsoleManager.DetachConsole();
				}
				goffset += 20 + 4;

				GUIX.HLine(new Rect(4, 54, rc.width - 8, 0));
				goffset += 1;
			}
			#endregion

			goffset++;

			#region Feature selector
			GUIX.Group(new Rect(0, goffset, 120, rc.height - goffset), () => {
				GUIX.VLine(new Rect(120 - 1, 0, 1, rc.height - goffset));

				for (var i = 0; i < PluginFeatures.Length; i++) {
					var feat = PluginFeatures[i];

					var rc = new Rect(0, i * 23, 120 - 1, 22);
					if (GUIX.Button(
						rc, "",
						feat.key == SelectedFeature ? null : new Color(1f, 1f, 1f, 0f),
						feat.key == SelectedFeature ? null : new Color(1f, 1f, 1f, 0.2f),
						feat.key == SelectedFeature ? null : new Color(1f, 1f, 1f, 0.4f)
					)) {
						this.SelectedFeature = feat.key;
					}

					if (feat.icon == IconKey.None) {
						GUIX.Label(rc.Shrink(4), feat.disp ?? feat.key);
					} else { 
						GUI.DrawTexture(rc.Shrink(4).Width(14), Icons[feat.icon]);
						GUIX.Label(rc.Shrink(4).Shrink(18, 0, 0, 0), feat.disp ?? feat.key);
					}
				}
			});
			#endregion

			var offset = 0f;
			var panelRect = new Rect(120, goffset, rc.width - 120, rc.height - goffset - 18 - 2);
			this.panelScroll = GUIX.ScrollView(panelRect, this.panelScroll, this.panelViewport, false, false, () => {
				GUIX.Group(new Rect(4, 4, WIDTH_FILL, this.panelViewport.height - 4), () => {
					switch (this.SelectedFeature) {
						case "QuickConfig":
							#region QuickConfig Section
							float_temp = GameOption.BgmVolume;
							DrawSlider(ref offset, "배경 음악", ref float_temp, () => {
								GameOption.BgmVolume = float_temp;
								GameSoundManager.Instance.ChangeVolumeBGM();
								GameOption.SaveSetting();
							});

							float_temp = GameOption.SfxVolume;
							DrawSlider(ref offset, "효과음", ref float_temp, () => {
								GameOption.SfxVolume = float_temp;
								GameSoundManager.Instance.ChangeVolumeEffect();
								GameOption.SaveSetting();
							});

							float_temp = GameOption.VoiceVolume;
							DrawSlider(ref offset, "음성", ref float_temp, () => {
								GameOption.VoiceVolume = float_temp;
								GameSoundManager.Instance.ChangeVolumeVoice();
								GameOption.SaveSetting();
							});

							bool_temp = GameOption.BackGroundSoundOn;
							DrawToggle(ref offset, "백그라운드 재생", ref bool_temp, () => {
								GameOption.BackGroundSoundOn = bool_temp;
								GameOption.SaveSetting();
							});

							DrawSeparator(ref offset);

							bool_temp = GameOption.SubwayMode;
							DrawToggle(ref offset, "실루엣 모드", ref bool_temp, () => {
								GameOption.SubwayMode = bool_temp;
								GameOption.SaveSetting();
								Handler.Broadcast((Base)new SubwayMode());
							});

							DrawSeparator(ref offset);

							KeepOffset(ref offset, () => {
								float_temp = GameOption.LobbyBubbleText;
								DrawSlider(ref offset, "말풍선", ref float_temp, () => {
									GameOption.LobbyBubbleText = float_temp;
									GameOption.SaveSetting();
								}, rightMargin: 30f);
							});

							bool_temp = GameOption.LobbyBubbleText > 0f;
							DrawToggle(ref offset, "", ref bool_temp, () => {
								GameOption.LobbyBubbleText = bool_temp ? 1f : 0f;
								GameOption.SaveSetting();
							}, leftMargin: WIDTH_FILL - 20);
							#endregion
							break;

						//GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
						//offset += 1 + 4;

						case "GracefulFPS":
							#region GracefulFPS Section
							GUI.DrawTexture(new Rect(0, offset, 20, 20), Icons[IconKey.TV]);
							GUIX.Heading(new Rect(24, offset, WIDTH_FILL, 20), "GracefulFPS");
							offset += 20 + 8;

							DrawToggle(ref offset, "FPS 표시", Conf.GracefulFPS.DisplayFPS);

							offset += 10;

							DrawLabel(ref offset, "FPS 제한하기");
							offset -= 4;
							DrawRadio(ref offset, new() {
								{ "None",  "바닐라" },
								{ "Fixed", "고정" },
								{ "VSync", "수직동기화" }
							}, Conf.GracefulFPS.LimitFPS, () => GracefulFPS.ApplyFPS());

							if (Conf.GracefulFPS.LimitFPS.Value == "Fixed") {
								float_temp = Conf.GracefulFPS.MaxFPS.Value;
								DrawSlider(
									ref offset, "최대 FPS", ref float_temp, () => {
										Conf.GracefulFPS.MaxFPS.Value = (int)float_temp;
										Conf.config.Save();
										GracefulFPS.ApplyFPS();
									},
									1f, 240f, 1f
								);
							}

							offset += 10;

							DrawLabel(ref offset, "전투 FPS 제한하기");
							offset -= 4;
							DrawRadio(ref offset, new() {
								{ "None",  "설정 안함" },
								{ "Fixed", "고정" },
								{ "VSync", "수직동기화" }
							}, Conf.GracefulFPS.LimitBattleFPS, () => GracefulFPS.ApplyFPS());

							if (Conf.GracefulFPS.LimitBattleFPS.Value == "Fixed") {
								float_temp = (float)Conf.GracefulFPS.MaxBattleFPS.Value;
								DrawSlider(
									ref offset, "최대 FPS", ref float_temp, () => {
										Conf.GracefulFPS.MaxBattleFPS.Value = (int)float_temp;
										Conf.config.Save();
										GracefulFPS.ApplyFPS();
									},
									1f, 240f, 1f
								);
							}

							DrawLabel(ref offset, "'설정 안함'으로 설정하는 경우,\n위 'FPS 제한하기'의 설정을 따릅니다.", Color_description, 20);
							break;

						#endregion

						//GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
						//offset += 1 + 4;

						case "SimpleTweaks":
							#region SimpleTweaks Section
							GUI.DrawTexture(new Rect(0, offset, 20, 20), Icons[IconKey.Carrot]);
							GUIX.Heading(new Rect(24, offset, WIDTH_FILL, 20), "SimpleTweaks");
							offset += 20 + 8;

							DrawToggle(ref offset, "로비 UI 숨기기/보이기 단축키 사용", Conf.SimpleTweaks.UseLobbyHide);
							if (Conf.SimpleTweaks.UseLobbyHide.Value)
								DrawKeyBinder(ref offset, "단축키", Conf.SimpleTweaks.LobbyUIHideKey, leftMargin: 10);

							DrawSeparator(ref offset);

							DrawToggle(ref offset, "창 비율 및 위치 초기화 무시", Conf.SimpleTweaks.Use_IgnoreWindowReset);

							offset += 10;

							DrawToggle(ref offset, "전체화면 키 변경하기", Conf.SimpleTweaks.Use_FullScreenKey);
							if (Conf.SimpleTweaks.Use_FullScreenKey.Value)
								DrawKeyBinder(ref offset, "전체화면 키", Conf.SimpleTweaks.FullScreenKey, leftMargin: 10);

							DrawSeparator(ref offset);

							DrawToggle(ref offset, "백그라운드 재생 동작 변경", Conf.SimpleTweaks.MuteOnBackgroundFix);
							DrawLabel(ref offset, "사운드 설정에서 '백그라운드 재생'을 켰을 경우, 백그라운드에서 오디오가 일시정지 되는 동작 대신 음소거가 되도록 하는 옵션입니다.\n'백그라운드 재생'이 꺼져있을 경우, 동작하지 않습니다.", Color_description, 20);

							DrawSeparator(ref offset);

							DrawToggle(ref offset, "마지막 자율 전투 설정 기억하기", Conf.SimpleTweaks.Use_OfflineBattle_Memorize);
							DrawLabel(ref offset, "전투원 및 장비 분해 설정과 시간을 기억합니다.", Color_description, 20);

							DrawSeparator(ref offset);

							DrawToggle(ref offset, "스토리 뷰어 스킵 키 변경", Conf.SimpleTweaks.UsePatchStorySkip);
							if (Conf.SimpleTweaks.UsePatchStorySkip.Value)
								DrawKeyBinder(ref offset, "스킵 키", Conf.SimpleTweaks.PatchStorySkipKey, leftMargin: 10);

							DrawSeparator(ref offset);

							DrawToggle(ref offset, "빠른 로고 화면", Conf.SimpleTweaks.Use_QuickLogo);
							DrawToggle(ref offset, "바로 로그인 가능", Conf.SimpleTweaks.Use_QuickTitle);
							DrawToggle(ref offset, "자동 로그인", Conf.SimpleTweaks.Use_AutoLogin);

							DrawSeparator(ref offset);

							DrawToggle(ref offset, "BGM 초기화 방지하기", Conf.SimpleTweaks.Use_ContinueBGM);
							DrawLabel(ref offset, "사운드 장치 등이 변경되었을 때, BGM이 초기화되어 처음부터 재생되는 것을 방지하고, 재생되던 위치부터 이어서 재생되도록 합니다.", Color_description, 20);
							#endregion
							break;

						//GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
						//offset += 1 + 4;

						case "SimpleUI":
							#region SimpleUI Section
							GUI.DrawTexture(new Rect(0, offset, 20, 20), Icons[IconKey.Brush]);
							GUIX.Heading(new Rect(24, offset, WIDTH_FILL, 20), "SimpleUI");
							offset += 20 + 8;

							this.Conf_SimpleUI(ref offset);
							#endregion
							break;

						//GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
						//offset += 1 + 4;

						case "Notification":
							#region Notification Section
							GUI.DrawTexture(new Rect(0, offset, 20, 20), Icons[IconKey.Bell]);
							GUIX.Heading(new Rect(24, offset, WIDTH_FILL, 20), "Notification");
							offset += 20 + 8;

							DrawToggle(ref offset, "인게임 알림을 윈도우로 받기", Conf.Notification.Handle_Notification);
							#endregion
							break;

						//GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
						//offset += 1 + 4;

						case "Presets":
							#region Presets Section
							GUI.DrawTexture(new Rect(0, offset, 20, 20), Icons[IconKey.Presets]);
							GUIX.Heading(new Rect(24, offset, WIDTH_FILL, 20), "Presets");
							offset += 20 + 8;

							DrawToggle(ref offset, "전투원 제조 프리셋 사용하기", Conf.Presets.Use_CharMaking_Preset);

							DrawSeparator(ref offset);

							DrawToggle(ref offset, "마지막 전투원 제조 수치 불러오기", Conf.Presets.Use_Last_CharMakingData);
							DrawToggle(ref offset, "마지막 장비 제조 수치 불러오기", Conf.Presets.Use_Last_EquipMakingData);
							DrawToggle(ref offset, "마지막 시설 부품 제조 수치 불러오기", Conf.Presets.Use_Last_FacPartsMakingData);
							#endregion
							break;

						//GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
						//offset += 1 + 4;

						case "Automation":
							#region Automation Section
							GUI.DrawTexture(new Rect(0, offset, 20, 20), Icons[IconKey.Robot]);
							GUIX.Heading(new Rect(24, offset, WIDTH_FILL, 20), "Automation");
							offset += 20 + 8;

							GUIX.Heading(new Rect(0, offset, WIDTH_FILL, 20), "! 주의 !", Color.yellow);
							offset += 20;
							DrawLabel(ref offset, "이 기능은 매크로 동작을 포함합니다.\n사용 시 운영 주체에 의해 이용 제한에 이를 수 있습니다.\n신중하게 사용해 주세요.", Color.yellow);

							DrawToggle(ref offset, "기지 일괄 수령 사용하기", Conf.Automation.Use_Base_GetAll);
							DrawToggle(ref offset, "자율 전투 재시작 사용하기", Conf.Automation.Use_OfflineBattle_Restart);
							#endregion
							break;

						//GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
						//offset += 1 + 4;

						case "Experimental":
							#region Experimental Section
							GUI.DrawTexture(new Rect(0, offset, 20, 20), Icons[IconKey.Construction]);
							GUIX.Heading(new Rect(24, offset, WIDTH_FILL, 20), "Experimental");
							offset += 20 + 8;

							GUIX.Heading(new Rect(0, offset, WIDTH_FILL, 20), "! 주의 !", Color.yellow);
							offset += 20;
							DrawLabel(ref offset, "이 기능은 완전히 검증되지 않은 동작을 포함합니다.\n사용 시 게임 동작에 문제가 발생할 수 있습니다.\n위 내용을 충분히 숙지 후 사용해 주세요.", Color.yellow);

							DrawSeparator(ref offset);

							DrawToggle(ref offset, "전투 프리징 수정", Conf.Experimental.Fix_BattleFreezing);
							DrawLabel(ref offset, "특정 전투 상황에서 캐릭터/적의 움직임이 멈추고 다음으로 진행되지 않는 문제를 수정하는 기능입니다.\n모든 프리징이 수정되지 않을 수 있습니다.\n다음 프리징 문제가 해결됩니다.", Color_description, 20);

							DrawLabel(ref offset, " * 프레데터, 블라인드 프린세스 등 파티클 관련 프리징 문제", Color_description, 20);
							DrawLabel(ref offset, " * 치이 아루엘 회피 프리징 문제", Color_description, 20);
							DrawLabel(ref offset, " * 이나비 스킨 2스킬 프리징 문제", Color_description, 20);

							DrawSeparator(ref offset);

							DrawToggle(ref offset, "키 맵핑 사용하기", Conf.Experimental.Use_KeyMapping, rightMargin: 90);
							DrawSlider(ref offset, "키 맵 불투명도", Conf.Experimental.KeyMapping_Opacity, labelWidth: 100f);
							DrawLabel(ref offset, "설정한 키를 누르면 화면의 특정 영역을 클릭한 것과 같은 동작을 만드는 기능입니다.\n앱플레이어 등에서 '가상키'로 불리는 동작입니다.", Color_description, 20);

							var groupToRemove = "";
							foreach (var g in KeyMappingConf.KeyMaps) {
								KeepOffset(ref offset, () => {
									DrawRadio(ref offset, new() { { g.Key, "" } }, Conf.Experimental.KeyMapping_Active);
								});

								if (g.Key != "Default") {
									KeepOffset(ref offset, () => {
										DrawLineButton(ref offset, "X", () => {
											if (g.Key == Conf.Experimental.KeyMapping_Active.Value)
												Conf.Experimental.KeyMapping_Active.Value = "Default";

											groupToRemove = g.Key;
										}, WIDTH_FILL - 20);
									});
								}

								if (Conf.Experimental.KeyMapping_Active.Value == g.Key) {
									KeepOffset(ref offset, () => {
										DrawLineButton(ref offset, "편집하기", () => {
											UIManager.Instance.AddPanel(new KeyMapPanel(this.instance));
										}, WIDTH_FILL - 105, 25);
									});
								}

								GUIX.Label(new Rect(25, offset, WIDTH_FILL - 105, 20), g.Key);
								offset += 20 + 4;
							}
							if (groupToRemove.Length > 0) KeyMappingConf.RemoveGroup(groupToRemove);

							this.experimental_KeyMapping_NewName = GUIX.TextField(
								new Rect(0, offset, WIDTH_FILL - 85, 20),
								this.experimental_KeyMapping_NewName,
								fontStyle: KeyMappingConf.KeyMaps.Keys.Contains(this.experimental_KeyMapping_NewName)
									? FontStyle.Italic
									: FontStyle.Normal
							);
							DrawLineButton(ref offset, "추가하기", () => {
								var n = this.experimental_KeyMapping_NewName;
								if (
									string.IsNullOrWhiteSpace(n) ||
									KeyMappingConf.KeyMaps.Keys.Contains(n)
								) return;

								KeyMappingConf.Save(n, []);
								Conf.Experimental.KeyMapping_Active.Value = n;
								this.experimental_KeyMapping_NewName = "";
							}, WIDTH_FILL - 80);

							#endregion
							break;
					}
				});
			});
			this.panelViewport.height = offset + 4;
		}
	}
}
