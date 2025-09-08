using Symphony.Features;

using UnityEngine;

namespace Symphony.UI.Panels {
	internal class ConfigPanel : UIPanelBase {
		public override Rect rc { get; set; } = new Rect(10f, 30f, 408f, 500f);

		private const float WIDTH_FILL = 408f - 120f - 8f;
		private const float HALF_FILL = WIDTH_FILL / 2;

		private Rect panelViewport = new Rect(0, 0, 248, 0);
		private Vector2 panelScroll = Vector2.zero;

		private readonly string[] PluginFeatures = ["SimpleTweaks", "SimpleUI", "WindowedResize", "BattleHotkey"];
		private string SelectedFeature = "SimpleTweaks";

		public override void Update() { }
		public override void OnGUI() {
			rc = GUIX.ModalWindow(0, rc, this.PanelContent, "Symphony | LastOrigin QoL Plugin | F12", true);
		}

		private void PanelContent(int id) {
			var ec = Event.current;
			var goffset = 0;

			#region Plugin Name Section
			GUIX.Heading(new Rect(4, 2, 72, 18), "Symphony", Color.yellow);
			GUIX.Label(new Rect(76, 4, rc.width - 88, 16), Plugin.VersionTag);
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
						feat == SelectedFeature ? null : new Color(1f, 1f, 1f, 0f),
						feat == SelectedFeature ? null : new Color(1f, 1f, 1f, 0.2f),
						feat == SelectedFeature ? null : new Color(1f, 1f, 1f, 0.4f)
					)) {
						this.SelectedFeature = feat;
					}
					GUIX.Label(rc.Shrink(4), feat);
				}
			});
			#endregion

			var panelRect = new Rect(120, goffset, rc.width - 120 + 28, rc.height - goffset - 18 - 2);
			this.panelScroll = GUI.BeginScrollView(panelRect, this.panelScroll, this.panelViewport, false, true);

			var offset = 0f;
			GUIX.Group(new Rect(4, 4, WIDTH_FILL, rc.height - goffset - 8), () => {
				switch (this.SelectedFeature) {
					case "SimpleTweaks":
						#region SimpleTweaks Section
						GUIX.Heading(new Rect(0, offset, WIDTH_FILL, 20), "SimpleTweaks");
						offset += 20 + 4; {
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), SimpleTweaks.DisplayFPS.Value, "FPS 표시");
							if (value != SimpleTweaks.DisplayFPS.Value) {
								SimpleTweaks.DisplayFPS.Value = value;
								SimpleTweaks.config.Save();
							}
							offset += 20 + 4;
						}; {
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), SimpleTweaks.LimitFPS.Value, "FPS 제한하기");
							if (value != SimpleTweaks.LimitFPS.Value) {
								SimpleTweaks.LimitFPS.Value = value;
								SimpleTweaks.config.Save();
							}
							offset += 20 + 4;

							if (SimpleTweaks.LimitFPS.Value) {
								GUIX.Label(new Rect(0, offset, 80, 20), "최대 FPS");
								var input = GUIX.HorizontalSlider(new Rect(80, offset, WIDTH_FILL - 80, 20), SimpleTweaks.MaxFPS.Value, 1, 240);
								if (input != SimpleTweaks.MaxFPS.Value) {
									SimpleTweaks.MaxFPS.Value = input;
									SimpleTweaks.config.Save();
								}
								offset += 20 + 4;
							}
						}; {
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), SimpleTweaks.LimitBattleFPS.Value, "전투 FPS 제한하기");
							if (value != SimpleTweaks.LimitBattleFPS.Value) {
								SimpleTweaks.LimitBattleFPS.Value = value;
								SimpleTweaks.config.Save();
							}
							offset += 20 + 4;

							if (SimpleTweaks.LimitBattleFPS.Value) {
								GUIX.Label(new Rect(0, offset, 80, 20), "최대 FPS");
								var input = GUIX.HorizontalSlider(new Rect(80, offset, WIDTH_FILL - 80, 20), SimpleTweaks.MaxBattleFPS.Value, 1, 240);
								if (input != SimpleTweaks.MaxBattleFPS.Value) {
									SimpleTweaks.MaxBattleFPS.Value = input;
									SimpleTweaks.config.Save();
								}
								offset += 20 + 4;
							}
						}

						offset += 10; {
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), SimpleTweaks.UseLobbyHide.Value, "로비 UI 토글 사용");
							if (value != SimpleTweaks.UseLobbyHide.Value) {
								SimpleTweaks.UseLobbyHide.Value = value;
								SimpleTweaks.config.Save();
							}
							offset += 20 + 4;
						}
						if (SimpleTweaks.UseLobbyHide.Value) {
							GUIX.Label(new Rect(0, offset, HALF_FILL, 20), "로비 UI 토글 키");
							GUIX.KeyBinder("SimpleTweak:LobbyUIHideKey", new Rect(HALF_FILL, offset, HALF_FILL, 20), SimpleTweaks.LobbyUIHideKey.Value, KeyCode => {
								SimpleTweaks.LobbyUIHideKey.Value = KeyCode.ToString();
								SimpleTweaks.config.Save();
							});
							offset += 20 + 4;
						}

						offset += 10; {
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), SimpleTweaks.MuteOnBackground.Value, "백그라운드에서 음소거");
							if (value != SimpleTweaks.MuteOnBackground.Value) {
								SimpleTweaks.MuteOnBackground.Value = value;
								SimpleTweaks.config.Save();
							}
							offset += 20 + 4;
						}

						GUIX.Label(new Rect(0, offset, 80, 20), "BGM");
						SimpleTweaks.VolumeBGM = Mathf.Round(200f * GUIX.HorizontalSlider(
							new Rect(80, offset, WIDTH_FILL - 80, 20),
							SimpleTweaks.VolumeBGM, 0f, 1f,
							v => (v * 100f).ToString("0.0") + " %"
						)) / 200f;
						offset += 20 + 4;

						GUIX.Label(new Rect(0, offset, 80, 20), "SFX");
						SimpleTweaks.VolumeSFX = Mathf.Round(200f * GUIX.HorizontalSlider(
							new Rect(80, offset, WIDTH_FILL - 80, 20),
							SimpleTweaks.VolumeSFX, 0f, 1f,
							v => (v * 100f).ToString("0.0") + " %"
						)) / 200f;
						offset += 20 + 4;

						GUIX.Label(new Rect(0, offset, 80, 20), "Voice");
						SimpleTweaks.VolumeVoice = Mathf.Round(200f * GUIX.HorizontalSlider(
							new Rect(80, offset, WIDTH_FILL - 80, 20),
							SimpleTweaks.VolumeVoice, 0f, 1f,
							v => (v * 100f).ToString("0.0") + " %"
						)) / 200f;
						offset += 20 + 4;

						offset += 10; {
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), SimpleTweaks.UseFormationFix.Value, "편성 화면 선택 버그 수정");
							if (value != SimpleTweaks.UseFormationFix.Value) {
								SimpleTweaks.UseFormationFix.Value = value;
								SimpleTweaks.config.Save();
							}
							offset += 20 + 4;
						}
						#endregion
						break;

					//GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
					//offset += 1 + 4;

					case "SimpleUI":
						#region SimpleUI Section
						GUIX.Heading(new Rect(0, offset, WIDTH_FILL, 20), "SimpleUI");
						offset += 20 + 4; {
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), SimpleUI.Small_CharWarehouse.Value, "더 작은 전투원 목록 항목");
							if (value != SimpleUI.Small_CharWarehouse.Value) {
								SimpleUI.Small_CharWarehouse.Value = value;
								SimpleUI.config.Save();
							}
							offset += 20 + 4;
						}; {
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), SimpleUI.Small_CharSelection.Value, "더 작은 전투원 선택 항목");
							if (value != SimpleUI.Small_CharSelection.Value) {
								SimpleUI.Small_CharSelection.Value = value;
								SimpleUI.config.Save();
							}
							offset += 20 + 4;
						}; {
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), SimpleUI.Small_CharScrapbook.Value, "더 작은 전투원 도감 항목");
							if (value != SimpleUI.Small_CharScrapbook.Value) {
								SimpleUI.Small_CharScrapbook.Value = value;
								SimpleUI.config.Save();
							}
							offset += 20 + 4;
						}
						; {
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), SimpleUI.Small_ItemWarehouse.Value, "더 작은 장비 목록 항목");
							if (value != SimpleUI.Small_ItemWarehouse.Value) {
								SimpleUI.Small_ItemWarehouse.Value = value;
								SimpleUI.config.Save();
							}
							offset += 20 + 4;
						}
						; {
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), SimpleUI.Small_ItemSelection.Value, "더 작은 장비 선택 항목");
							if (value != SimpleUI.Small_ItemSelection.Value) {
								SimpleUI.Small_ItemSelection.Value = value;
								SimpleUI.config.Save();
							}
							offset += 20 + 4;
						}
						; {
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), SimpleUI.Small_TempInventory.Value, "더 작은 임시 창고 항목");
							if (value != SimpleUI.Small_TempInventory.Value) {
								SimpleUI.Small_TempInventory.Value = value;
								SimpleUI.config.Save();
							}
							offset += 20 + 4;
						}

						GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
						offset += 1 + 4;

						; {
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), SimpleUI.Small_Consumables.Value, "더 작은 소모품 목록 항목");
							if (value != SimpleUI.Small_Consumables.Value) {
								SimpleUI.Small_Consumables.Value = value;
								SimpleUI.config.Save();
							}
							offset += 20 + 4;
						}
						; {
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), SimpleUI.Sort_Consumables.Value, "소모품 목록 정렬");
							if (value != SimpleUI.Sort_Consumables.Value) {
								SimpleUI.Sort_Consumables.Value = value;
								SimpleUI.config.Save();
							}
							offset += 20 + 4;
						}
						;
						#endregion
						break;

					//GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
					//offset += 1 + 4;

					case "WindowedResize":
						#region WindowedResize Section
						GUIX.Heading(new Rect(0, offset, WIDTH_FILL, 20), "WindowedResize");
						offset += 20 + 4; {
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), WindowedResize.Use_FullScreenKey.Value, "전체화면 키 변경 사용");
							if (value != WindowedResize.Use_FullScreenKey.Value) {
								WindowedResize.Use_FullScreenKey.Value = value;
								WindowedResize.config.Save();
							}
							offset += 20 + 4;
						}
						if (WindowedResize.Use_FullScreenKey.Value) {
							GUIX.Label(new Rect(0, offset, HALF_FILL, 20), "전체화면 키");
							GUIX.KeyBinder("WindowedResize:Key_Mode", new Rect(HALF_FILL, offset, HALF_FILL, 20), WindowedResize.Key_Mode.Value, KeyCode => {
								WindowedResize.Key_Mode.Value = KeyCode.ToString();
								WindowedResize.config.Save();
							});
							offset += 20 + 4;
						}
						#endregion
						break;

					//GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
					//offset += 1 + 4;

					case "BattleHotkey":
						#region BattleHotkey Section
						GUIX.Heading(new Rect(0, offset, WIDTH_FILL, 20), "BattleHotkey");
						offset += 20 + 4; {
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), BattleHotkey.Use_SkillPanel.Value, "행동 단축키 사용");
							if (value != BattleHotkey.Use_SkillPanel.Value) {
								BattleHotkey.Use_SkillPanel.Value = value;
								BattleHotkey.config.Save();
							}
							offset += 20 + 4;
						}
						if (BattleHotkey.Use_SkillPanel.Value) {
							GUIX.Label(new Rect(20, offset, HALF_FILL - 20, 20), "액티브 스킬 1");
							GUIX.KeyBinder("BattleHotkey:Skill1", new Rect(HALF_FILL, offset, HALF_FILL, 20), BattleHotkey.Key_SkillPanel[0].Value, KeyCode => {
								BattleHotkey.Key_SkillPanel[0].Value = KeyCode.ToString();
								BattleHotkey.config.Save();
							});
							offset += 20 + 4;

							GUIX.Label(new Rect(20, offset, HALF_FILL - 20, 20), "액티브 스킬 2");
							GUIX.KeyBinder("BattleHotkey:Skill2", new Rect(HALF_FILL, offset, HALF_FILL, 20), BattleHotkey.Key_SkillPanel[1].Value, KeyCode => {
								BattleHotkey.Key_SkillPanel[1].Value = KeyCode.ToString();
								BattleHotkey.config.Save();
							});
							offset += 20 + 4;

							GUIX.Label(new Rect(20, offset, HALF_FILL - 20, 20), "이동");
							GUIX.KeyBinder("BattleHotkey:Move", new Rect(HALF_FILL, offset, HALF_FILL, 20), BattleHotkey.Key_SkillPanel[2].Value, KeyCode => {
								BattleHotkey.Key_SkillPanel[2].Value = KeyCode.ToString();
								BattleHotkey.config.Save();
							});
							offset += 20 + 4;

							GUIX.Label(new Rect(20, offset, HALF_FILL - 20, 20), "대기");
							GUIX.KeyBinder("BattleHotkey:Wait", new Rect(HALF_FILL, offset, HALF_FILL, 20), BattleHotkey.Key_SkillPanel[3].Value, KeyCode => {
								BattleHotkey.Key_SkillPanel[3].Value = KeyCode.ToString();
								BattleHotkey.config.Save();
							});
							offset += 20 + 4;
						} {
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), BattleHotkey.Use_PlayButton.Value, "행동 개시 단축키 사용");
							if (value != BattleHotkey.Use_PlayButton.Value) {
								BattleHotkey.Use_PlayButton.Value = value;
								BattleHotkey.config.Save();
							}
							offset += 20 + 4;
						}
						if (BattleHotkey.Use_PlayButton.Value) {
							GUIX.Label(new Rect(20, offset, HALF_FILL - 20, 20), "행동 개시");
							GUIX.KeyBinder("BattleHotkey:Play", new Rect(HALF_FILL, offset, HALF_FILL, 20), BattleHotkey.Key_Play.Value, KeyCode => {
								BattleHotkey.Key_Play.Value = KeyCode.ToString();
								BattleHotkey.config.Save();
							});
							offset += 20 + 4;
						}
						/*
						{
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), BattleHotkey.Use_TeamGrid.Value, "아군 선택 단축키 사용");
							if (value != BattleHotkey.Use_TeamGrid.Value) {
								BattleHotkey.Use_TeamGrid.Value = value;
								BattleHotkey.config.Save();
							}
							offset += 20 + 4;
						}
						if (BattleHotkey.Use_TeamGrid.Value) {
							for (var i = 0; i < 9; i++) {
								GUIX.Label(new Rect(20, offset, HALF_FILL - 20, 20), $"아군 위치 {i + 1}");
								GUIX.DrawGrid33(new Rect(HALF_FILL - 20, offset + 2, 16, 16), i);

								GUIX.KeyBinder($"BattleHotkey:TeamGrid{i + 1}", new Rect(HALF_FILL,  offset, HALF_FILL, 20), BattleHotkey.Key_TeamGrid[i].Value, keyCode => {
									BattleHotkey.Key_TeamGrid[i].Value = keyCode.ToString();
									BattleHotkey.config.Save();
								});
								offset += 20 + 4;
							}
						}
						*/
						{
							var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), BattleHotkey.Use_EnemyGrid.Value, "적군 선택 단축키 사용");
							if (value != BattleHotkey.Use_EnemyGrid.Value) {
								BattleHotkey.Use_EnemyGrid.Value = value;
								BattleHotkey.config.Save();
							}
							offset += 20 + 4;
						}
						if (BattleHotkey.Use_EnemyGrid.Value) {
							for (var i = 0; i < 9; i++) {
								GUIX.Label(new Rect(20, offset, HALF_FILL - 20, 20), $"적군 위치 {i + 1}");
								GUIX.DrawGrid33(new Rect(HALF_FILL - 20, offset + 2, 16, 16), i);

								GUIX.KeyBinder($"BattleHotkey:EnemyGrid{i + 1}", new Rect(HALF_FILL, offset, HALF_FILL, 20), BattleHotkey.Key_EnemyGrid[i].Value, keyCode => {
									BattleHotkey.Key_EnemyGrid[i].Value = keyCode.ToString();
									BattleHotkey.config.Save();
								});
								offset += 20 + 4;
							}
						}
						#endregion
						break;
				}
			});

			GUI.EndScrollView(true);
			this.panelViewport.height = offset + 4;
		}
	}
}
