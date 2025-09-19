using Symphony.Features;

using UnityEngine;

namespace Symphony.UI.Panels {
	internal class ConfigPanel : UIPanelBase {
		public override Rect rc { get; set; } = new Rect(10f, 30f, 422f, 500f);

		private const float WIDTH_FILL = 408f - 120f - 8f;
		private const float HALF_FILL = WIDTH_FILL / 2;

		private Rect panelViewport = new Rect(0, 0, 248, 0);
		private Vector2 panelScroll = Vector2.zero;

		private readonly string[] PluginFeatures = ["GracefulFPS", "SimpleTweaks", "SimpleUI", "BattleHotkey", "LastBattle", "Notification", "Presets", "Automation"];
		private string SelectedFeature = "GracefulFPS";

		public ConfigPanel(MonoBehaviour instance) : base(instance) { }

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

			var offset = 0f;
			var panelRect = new Rect(120, goffset, rc.width - 120, rc.height - goffset - 18 - 2);
			this.panelScroll = GUIX.ScrollView(panelRect, this.panelScroll, this.panelViewport, false, false, () => {
				GUIX.Group(new Rect(4, 4, WIDTH_FILL, this.panelViewport.height - 4), () => {
					switch (this.SelectedFeature) {
						case "GracefulFPS":
							#region GracefulFPS Section
							GUIX.Heading(new Rect(0, offset, WIDTH_FILL, 20), "GracefulFPS");
							offset += 20 + 4;

							; {
								var value = GUIX.Toggle(new Rect(0, offset, WIDTH_FILL, 20), Conf.GracefulFPS.DisplayFPS.Value, "FPS 표시");
								if (value != Conf.GracefulFPS.DisplayFPS.Value) {
									Conf.GracefulFPS.DisplayFPS.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							offset += 10;
							; {
								GUIX.Label(new Rect(0, offset, WIDTH_FILL, 20), "FPS 제한하기");
								offset += 20 + 4;

								var x = 0f;
								var w = GUIX.Label("기본").x + 20f + 5f;
								if (GUIX.Radio(new Rect(x, offset, w, 20), Conf.GracefulFPS.LimitFPS.Value == "None", "기본")) {
									Conf.GracefulFPS.LimitFPS.Value = "None";
									GracefulFPS.ApplyFPS();
									Conf.config.Save();
								}
								x += w + 10f;
								w = GUIX.Label("고정").x + 20f + 5f;
								if (GUIX.Radio(new Rect(x, offset, w, 20), Conf.GracefulFPS.LimitFPS.Value == "Fixed", "고정")) {
									Conf.GracefulFPS.LimitFPS.Value = "Fixed";
									GracefulFPS.ApplyFPS();
									Conf.config.Save();
								}
								x += w + 10f;
								w = GUIX.Label("수직동기화").x + 20f + 5f;
								if (GUIX.Radio(new Rect(x, offset, w, 20), Conf.GracefulFPS.LimitFPS.Value == "VSync", "수직동기화")) {
									Conf.GracefulFPS.LimitFPS.Value = "VSync";
									GracefulFPS.ApplyFPS();
									Conf.config.Save();
								}
								offset += 20 + 4;

								if (Conf.GracefulFPS.LimitFPS.Value == "Fixed") {
									GUIX.Label(new Rect(0, offset, 80, 20), "최대 FPS");
									try {
										var input = (int)GUIX.HorizontalSlider(
											new Rect(80, offset, WIDTH_FILL - 80, 20),
											Conf.GracefulFPS.MaxFPS.Value,
											1, 240,
											v => ((int)Mathf.Round(v)).ToString()
										);
										if (input != Conf.GracefulFPS.MaxFPS.Value) {
											Conf.GracefulFPS.MaxFPS.Value = input;
											GracefulFPS.ApplyFPS();
											Conf.config.Save();
										}
										offset += 20 + 4;
									} catch (System.Exception e) {
										Plugin.Logger.LogWarning(e);
									}
								}
							}
							offset += 10;
							; {
								GUIX.Label(new Rect(0, offset, WIDTH_FILL, 20), "전투 FPS 제한하기");
								offset += 20 + 4;

								var x = 0f;
								var w = GUIX.Label("기본").x + 20f + 5f;
								if (GUIX.Radio(new Rect(x, offset, w, 20), Conf.GracefulFPS.LimitBattleFPS.Value == "None", "기본")) {
									Conf.GracefulFPS.LimitBattleFPS.Value = "None";
									GracefulFPS.ApplyFPS();
									Conf.config.Save();
								}
								x += w + 10f;
								w = GUIX.Label("고정").x + 20f + 5f;
								if (GUIX.Radio(new Rect(x, offset, w, 20), Conf.GracefulFPS.LimitBattleFPS.Value == "Fixed", "고정")) {
									Conf.GracefulFPS.LimitBattleFPS.Value = "Fixed";
									GracefulFPS.ApplyFPS();
									Conf.config.Save();
								}
								x += w + 10f;
								w = GUIX.Label("수직동기화").x + 20f + 5f;
								if (GUIX.Radio(new Rect(x, offset, w, 20), Conf.GracefulFPS.LimitBattleFPS.Value == "VSync", "수직동기화")) {
									Conf.GracefulFPS.LimitBattleFPS.Value = "VSync";
									GracefulFPS.ApplyFPS();
									Conf.config.Save();
								}
								offset += 20 + 4;

								if (Conf.GracefulFPS.LimitBattleFPS.Value == "Fixed") {
									GUIX.Label(new Rect(0, offset, 80, 20), "최대 FPS");
									try {
										var input = (int)GUIX.HorizontalSlider(
											new Rect(80, offset, WIDTH_FILL - 80, 20),
											Conf.GracefulFPS.MaxBattleFPS.Value,
											1, 240,
											v => ((int)Mathf.Round(v)).ToString()
										);
										if (input != Conf.GracefulFPS.MaxBattleFPS.Value) {
											Conf.GracefulFPS.MaxBattleFPS.Value = input;
											GracefulFPS.ApplyFPS();
											Conf.config.Save();
										}
										offset += 20 + 4;
									} catch (System.Exception e) {
										Plugin.Logger.LogWarning(e);
									}
								}
							}
							break;

						#endregion

						//GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
						//offset += 1 + 4;

						case "SimpleTweaks":
							#region SimpleTweaks Section
							GUIX.Heading(new Rect(0, offset, WIDTH_FILL, 20), "SimpleTweaks");
							offset += 20 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleTweaks.UseLobbyHide.Value,
									"로비 UI 토글 사용"
								);
								if (value != Conf.SimpleTweaks.UseLobbyHide.Value) {
									Conf.SimpleTweaks.UseLobbyHide.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							if (Conf.SimpleTweaks.UseLobbyHide.Value) {
								GUIX.Label(new Rect(0, offset, HALF_FILL, 20), "로비 UI 토글 키");
								GUIX.KeyBinder(
									"SimpleTweak:LobbyUIHideKey",
									new Rect(HALF_FILL, offset, HALF_FILL, 20),
									Conf.SimpleTweaks.LobbyUIHideKey.Value,
									KeyCode => {
										Conf.SimpleTweaks.LobbyUIHideKey.Value = KeyCode.ToString();
										Conf.config.Save();
									}
								);
								offset += 20 + 4;
							}

							GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
							offset += 1 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleTweaks.Use_IgnoreWindowReset.Value,
									"창 비율 및 위치 초기화 무시"
								);
								if (value != Conf.SimpleTweaks.Use_IgnoreWindowReset.Value) {
									Conf.SimpleTweaks.Use_IgnoreWindowReset.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}

							offset += 10; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleTweaks.Use_FullScreenKey.Value,
									"전체화면 키 변경 사용"
								);
								if (value != Conf.SimpleTweaks.Use_FullScreenKey.Value) {
									Conf.SimpleTweaks.Use_FullScreenKey.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							if (Conf.SimpleTweaks.Use_FullScreenKey.Value) {
								GUIX.Label(new Rect(0, offset, HALF_FILL, 20), "전체화면 키");
								GUIX.KeyBinder(
									"SimpleTweaks:FullScreenKey",
									new Rect(HALF_FILL, offset, HALF_FILL, 20),
									Conf.SimpleTweaks.FullScreenKey.Value,
									KeyCode => {
										Conf.SimpleTweaks.FullScreenKey.Value = KeyCode.ToString();
										Conf.config.Save();
									}
								);
								offset += 20 + 4;
							}

							GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
							offset += 1 + 4;

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

							GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
							offset += 1 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleTweaks.MuteOnBackgroundFix.Value,
									"백그라운드에서 음소거 동작 변경"
								);
								if (value != Conf.SimpleTweaks.MuteOnBackgroundFix.Value) {
									Conf.SimpleTweaks.MuteOnBackgroundFix.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}

							GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
							offset += 1 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleTweaks.Use_OfflineBattle_Memorize.Value,
									"마지막 자율 전투 옵션 기억하기"
								);
								if (value != Conf.SimpleTweaks.Use_OfflineBattle_Memorize.Value) {
									Conf.SimpleTweaks.Use_OfflineBattle_Memorize.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}

							GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
							offset += 1 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleTweaks.UsePatchStorySkip.Value,
									"스토리 뷰어 스킵 키 변경"
								);
								if (value != Conf.SimpleTweaks.UsePatchStorySkip.Value) {
									Conf.SimpleTweaks.UsePatchStorySkip.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							if (Conf.SimpleTweaks.UseLobbyHide.Value) {
								GUIX.Label(new Rect(0, offset, HALF_FILL, 20), "스킵 키");
								GUIX.KeyBinder(
									"SimpleTweak:PatchStorySkipKey",
									new Rect(HALF_FILL, offset, HALF_FILL, 20),
									Conf.SimpleTweaks.PatchStorySkipKey.Value, KeyCode => {
										Conf.SimpleTweaks.PatchStorySkipKey.Value = KeyCode.ToString();
										Conf.config.Save();
									}
								);
								offset += 20 + 4;
							}

							GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
							offset += 1 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleTweaks.Use_QuickLogo.Value,
									"빠른 로고 화면"
								);
								if (value != Conf.SimpleTweaks.Use_QuickLogo.Value) {
									Conf.SimpleTweaks.Use_QuickLogo.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleTweaks.Use_QuickTitle.Value,
									"바로 로그인 가능"
								);
								if (value != Conf.SimpleTweaks.Use_QuickTitle.Value) {
									Conf.SimpleTweaks.Use_QuickTitle.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleTweaks.Use_AutoLogin.Value,
									"자동 로그인"
								);
								if (value != Conf.SimpleTweaks.Use_AutoLogin.Value) {
									Conf.SimpleTweaks.Use_AutoLogin.Value = value;
									Conf.config.Save();
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
							offset += 20 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Use_OfflineBattle_Bypass.Value,
									"자율 전투 확인 대신 맵으로"
								);
								if (value != Conf.SimpleUI.Use_OfflineBattle_Bypass.Value) {
									Conf.SimpleUI.Use_OfflineBattle_Bypass.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}

							GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
							offset += 1 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Small_CharWarehouse.Value,
									"더 작은 전투원 목록 항목"
								);
								if (value != Conf.SimpleUI.Small_CharWarehouse.Value) {
									Conf.SimpleUI.Small_CharWarehouse.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Small_CharSelection.Value,
									"더 작은 전투원 선택 항목"
								);
								if (value != Conf.SimpleUI.Small_CharSelection.Value) {
									Conf.SimpleUI.Small_CharSelection.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Small_CharScrapbook.Value,
									"더 작은 전투원 도감 항목"
								);
								if (value != Conf.SimpleUI.Small_CharScrapbook.Value) {
									Conf.SimpleUI.Small_CharScrapbook.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Small_ItemWarehouse.Value,
									"더 작은 장비 목록 항목"
								);
								if (value != Conf.SimpleUI.Small_ItemWarehouse.Value) {
									Conf.SimpleUI.Small_ItemWarehouse.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Small_ItemSelection.Value,
									"더 작은 장비 선택 항목"
								);
								if (value != Conf.SimpleUI.Small_ItemSelection.Value) {
									Conf.SimpleUI.Small_ItemSelection.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Small_TempInventory.Value,
									"더 작은 임시 창고 항목"
								);
								if (value != Conf.SimpleUI.Small_TempInventory.Value) {
									Conf.SimpleUI.Small_TempInventory.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}

							GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
							offset += 1 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Small_Consumables.Value,
									"더 작은 소모품 목록 항목"
								);
								if (value != Conf.SimpleUI.Small_Consumables.Value) {
									Conf.SimpleUI.Small_Consumables.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Sort_Consumables.Value,
									"소모품 목록 정렬"
								);
								if (value != Conf.SimpleUI.Sort_Consumables.Value) {
									Conf.SimpleUI.Sort_Consumables.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							;

							GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
							offset += 1 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.EnterToSearch_CharWarehouse.Value,
									"전투원 목록에서 Enter로 검색"
								);
								if (value != Conf.SimpleUI.EnterToSearch_CharWarehouse.Value) {
									Conf.SimpleUI.EnterToSearch_CharWarehouse.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.EnterToSearch_CharSelection.Value,
									"전투원 선택에서 Enter로 검색"
								);
								if (value != Conf.SimpleUI.EnterToSearch_CharSelection.Value) {
									Conf.SimpleUI.EnterToSearch_CharSelection.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.EnterToSearch_ItemWarehouse.Value,
									"장비 목록에서 Enter로 검색"
								);
								if (value != Conf.SimpleUI.EnterToSearch_ItemWarehouse.Value) {
									Conf.SimpleUI.EnterToSearch_ItemWarehouse.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.EnterToSearch_ItemSelection.Value,
									"장비 선택에서 Enter로 검색"
								);
								if (value != Conf.SimpleUI.EnterToSearch_ItemSelection.Value) {
									Conf.SimpleUI.EnterToSearch_ItemSelection.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							;

							GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
							offset += 1 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Use_AccelerateScrollDelta.Value,
									"스크롤/패닝 가속, 줌 반전하기"
								);
								if (value != Conf.SimpleUI.Use_AccelerateScrollDelta.Value) {
									Conf.SimpleUI.Use_AccelerateScrollDelta.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							;

							GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
							offset += 1 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Use_SortByName.Value,
									"전투원 이름 정렬 추가"
								);
								if (value != Conf.SimpleUI.Use_SortByName.Value) {
									Conf.SimpleUI.Use_SortByName.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Default_CharacterCost_Off.Value,
									"전투원 소모 자원 표기 기본 끄기"
								);
								if (value != Conf.SimpleUI.Default_CharacterCost_Off.Value) {
									Conf.SimpleUI.Default_CharacterCost_Off.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							;

							GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
							offset += 1 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Use_Squad_Clear.Value,
									"편성에 전체 해제 추가"
								);
								if (value != Conf.SimpleUI.Use_Squad_Clear.Value) {
									Conf.SimpleUI.Use_Squad_Clear.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}

							GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
							offset += 1 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Use_Disassemble_SelectAll_Character.Value,
									"분해에 모든 전투원 선택 추가"
								);
								if (value != Conf.SimpleUI.Use_Disassemble_SelectAll_Character.Value) {
									Conf.SimpleUI.Use_Disassemble_SelectAll_Character.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Use_Disassemble_SelectAll_Equip.Value,
									"분해에 모든 장비 선택 추가"
								);
								if (value != Conf.SimpleUI.Use_Disassemble_SelectAll_Equip.Value) {
									Conf.SimpleUI.Use_Disassemble_SelectAll_Equip.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}

							GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
							offset += 1 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Use_ScrapbookMustBeFancy.Value,
									"도감은 멋져야 한다"
								);
								if (value != Conf.SimpleUI.Use_ScrapbookMustBeFancy.Value) {
									Conf.SimpleUI.Use_ScrapbookMustBeFancy.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}

							GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
							offset += 1 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Use_CharacterMakingPreview.Value,
									"전투원 제조 결과 미리보기"
								);
								if (value != Conf.SimpleUI.Use_CharacterMakingPreview.Value) {
									Conf.SimpleUI.Use_CharacterMakingPreview.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.SimpleUI.Use_EquipMakingPreview.Value,
									"장비 제조 결과 미리보기"
								);
								if (value != Conf.SimpleUI.Use_EquipMakingPreview.Value) {
									Conf.SimpleUI.Use_EquipMakingPreview.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							#endregion
							break;

						//GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
						//offset += 1 + 4;

						case "BattleHotkey":
							#region BattleHotkey Section
							GUIX.Heading(new Rect(0, offset, WIDTH_FILL, 20), "BattleHotkey");
							offset += 20 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.BattleHotkey.Use_SkillPanel.Value,
									"행동 단축키 사용"
								);
								if (value != Conf.BattleHotkey.Use_SkillPanel.Value) {
									Conf.BattleHotkey.Use_SkillPanel.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							if (Conf.BattleHotkey.Use_SkillPanel.Value) {
								GUIX.Label(new Rect(20, offset, HALF_FILL - 20, 20), "액티브 스킬 1");
								GUIX.KeyBinder(
									"BattleHotkey:Skill1",
									new Rect(HALF_FILL, offset, HALF_FILL, 20),
									Conf.BattleHotkey.Key_SkillPanel[0].Value,
									KeyCode => {
										Conf.BattleHotkey.Key_SkillPanel[0].Value = KeyCode.ToString();
										Conf.config.Save();
									}
								);
								offset += 20 + 4;

								GUIX.Label(new Rect(20, offset, HALF_FILL - 20, 20), "액티브 스킬 2");
								GUIX.KeyBinder(
									"BattleHotkey:Skill2",
									new Rect(HALF_FILL, offset, HALF_FILL, 20),
									Conf.BattleHotkey.Key_SkillPanel[1].Value,
									KeyCode => {
										Conf.BattleHotkey.Key_SkillPanel[1].Value = KeyCode.ToString();
										Conf.config.Save();
									}
								);
								offset += 20 + 4;

								GUIX.Label(new Rect(20, offset, HALF_FILL - 20, 20), "이동");
								GUIX.KeyBinder(
									"BattleHotkey:Move",
									new Rect(HALF_FILL, offset, HALF_FILL, 20),
									Conf.BattleHotkey.Key_SkillPanel[2].Value,
									KeyCode => {
										Conf.BattleHotkey.Key_SkillPanel[2].Value = KeyCode.ToString();
										Conf.config.Save();
									}
								);
								offset += 20 + 4;

								GUIX.Label(new Rect(20, offset, HALF_FILL - 20, 20), "대기");
								GUIX.KeyBinder(
									"BattleHotkey:Wait",
									new Rect(HALF_FILL, offset, HALF_FILL, 20),
									Conf.BattleHotkey.Key_SkillPanel[3].Value,
									KeyCode => {
										Conf.BattleHotkey.Key_SkillPanel[3].Value = KeyCode.ToString();
										Conf.config.Save();
									}
								);
								offset += 20 + 4;
							} {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.BattleHotkey.Use_PlayButton.Value,
									"행동 개시 단축키 사용"
								);
								if (value != Conf.BattleHotkey.Use_PlayButton.Value) {
									Conf.BattleHotkey.Use_PlayButton.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							if (Conf.BattleHotkey.Use_PlayButton.Value) {
								GUIX.Label(new Rect(20, offset, HALF_FILL - 20, 20), "행동 개시");
								GUIX.KeyBinder(
									"BattleHotkey:Play",
									new Rect(HALF_FILL, offset, HALF_FILL, 20),
									Conf.BattleHotkey.Key_Play.Value,
									KeyCode => {
										Conf.BattleHotkey.Key_Play.Value = KeyCode.ToString();
										Conf.config.Save();
									}
								);
								offset += 20 + 4;
							}
							/*
							{
								var value = GUIX.Toggle
									(new Rect(0, offset, WIDTH_FILL, 20),
									Conf.BattleHotkey.Use_TeamGrid.Value,
									"아군 선택 단축키 사용"
								);
								if (value != Conf.BattleHotkey.Use_TeamGrid.Value) {
									Conf.BattleHotkey.Use_TeamGrid.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							if (Conf.BattleHotkey.Use_TeamGrid.Value) {
								for (var i = 0; i < 9; i++) {
									GUIX.Label(new Rect(20, offset, HALF_FILL - 20, 20), $"아군 위치 {i + 1}");
									GUIX.DrawGrid33(new Rect(HALF_FILL - 20, offset + 2, 16, 16), i);

									GUIX.KeyBinder(
										$"BattleHotkey:TeamGrid{i + 1}",
										new Rect(HALF_FILL,  offset, HALF_FILL, 20),
										Conf.BattleHotkey.Key_TeamGrid[i].Value,
										keyCode => {
											Conf.BattleHotkey.Key_TeamGrid[i].Value = keyCode.ToString();
											Conf.config.Save();
										}
									);
									offset += 20 + 4;
								}
							}
							*/
							{
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.BattleHotkey.Use_EnemyGrid.Value,
									"적군 선택 단축키 사용"
								);
								if (value != Conf.BattleHotkey.Use_EnemyGrid.Value) {
									Conf.BattleHotkey.Use_EnemyGrid.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							if (Conf.BattleHotkey.Use_EnemyGrid.Value) {
								for (var i = 0; i < 9; i++) {
									GUIX.Label(new Rect(20, offset, HALF_FILL - 20, 20), $"적군 위치 {i + 1}");
									GUIX.DrawGrid33(new Rect(HALF_FILL - 20, offset + 2, 16, 16), i);

									GUIX.KeyBinder(
										$"BattleHotkey:EnemyGrid{i + 1}",
										new Rect(HALF_FILL, offset, HALF_FILL, 20),
										Conf.BattleHotkey.Key_EnemyGrid[i].Value,
										keyCode => {
											Conf.BattleHotkey.Key_EnemyGrid[i].Value = keyCode.ToString();
											Conf.config.Save();
										}
									);
									offset += 20 + 4;
								}
							}
							#endregion
							break;

						//GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
						//offset += 1 + 4;

						case "LastBattle":
							#region LastBattle Section
							GUIX.Heading(new Rect(0, offset, WIDTH_FILL, 20), "LastBattle");
							offset += 20 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.LastBattle.Use_LastBattleMap.Value,
									"마지막 방문 전투 지역 버튼 추가"
								);
								if (value != Conf.LastBattle.Use_LastBattleMap.Value) {
									Conf.LastBattle.Use_LastBattleMap.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							#endregion
							break;

						//GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
						//offset += 1 + 4;

						case "Notification":
							#region Notification Section
							GUIX.Heading(new Rect(0, offset, WIDTH_FILL, 20), "Notification");
							offset += 20 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.Notification.Handle_Notification.Value,
									"인게임 알림을 윈도우 알림으로 받기"
								);
								if (value != Conf.Notification.Handle_Notification.Value) {
									Conf.Notification.Handle_Notification.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							#endregion
							break;

						//GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
						//offset += 1 + 4;

						case "Presets":
							#region Presets Section
							GUIX.Heading(new Rect(0, offset, WIDTH_FILL, 20), "Presets");
							offset += 20 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.Presets.Use_CharMaking_Preset.Value,
									"전투원 제조 프리셋 사용하기"
								);
								if (value != Conf.Presets.Use_CharMaking_Preset.Value) {
									Conf.Presets.Use_CharMaking_Preset.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.Presets.Use_Last_CharMakingData.Value,
									"마지막 전투원 제조 수치 불러오기"
								);
								if (value != Conf.Presets.Use_Last_CharMakingData.Value) {
									Conf.Presets.Use_Last_CharMakingData.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							#endregion
							break;

						//GUIX.HLine(new Rect(0, offset, WIDTH_FILL, 0));
						//offset += 1 + 4;

						case "Automation":
							#region Automation Section
							GUIX.Heading(new Rect(0, offset, WIDTH_FILL, 20), "Automation");
							offset += 20 + 4;

							GUIX.Heading(new Rect(0, offset, WIDTH_FILL, 20), "! 주의 !", Color.yellow);
							offset += 20;

							GUIX.Label(new Rect(0, offset, WIDTH_FILL, 20), "이 기능은 매크로 동작을 포함합니다.", Color.yellow);
							offset += 20;
							GUIX.Label(new Rect(0, offset, WIDTH_FILL, 20), "사용 시 운영 주체에 의해 이용 제한에 이를 수 있습니다.", Color.yellow);
							offset += 20;
							GUIX.Label(new Rect(0, offset, WIDTH_FILL, 20), "신중하게 사용해 주세요.", Color.yellow);
							offset += 20 + 4;

							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.Automation.Use_Base_GetAll.Value,
									"기지 일괄 수령 사용하기"
								);
								if (value != Conf.Automation.Use_Base_GetAll.Value) {
									Conf.Automation.Use_Base_GetAll.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							; {
								var value = GUIX.Toggle(
									new Rect(0, offset, WIDTH_FILL, 20),
									Conf.Automation.Use_OfflineBattle_Restart.Value,
									"자율 전투 재시작 사용하기"
								);
								if (value != Conf.Automation.Use_OfflineBattle_Restart.Value) {
									Conf.Automation.Use_OfflineBattle_Restart.Value = value;
									Conf.config.Save();
								}
								offset += 20 + 4;
							}
							#endregion
							break;
					}
				});
			});
			this.panelViewport.height = offset + 4;
		}
	}
}
