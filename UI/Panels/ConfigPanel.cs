using Symphony.Features;

using UnityEngine;

namespace Symphony.UI.Panels {
	internal class ConfigPanel : UIPanelBase {
		public override Rect rc { get; set; } = new Rect(10f, 30f, 250f, 500f);

		private Rect panelRect => new Rect(0, 0, 268, rc.height - 18 - 2);
		private Rect panelViewport = new Rect(0, 0, 248, 0);
		private Vector2 panelScroll = Vector2.zero;

		public override void Update() { }
		public override void OnGUI() {
			rc = GUIX.ModalWindow(0, rc, this.PanelContent, "Symphony | LastOrigin QoL Plugin | F12", true);
		}

		private void PanelContent(int id) {
			var offset = 4f;
			var ec = Event.current;

			this.panelScroll = GUI.BeginScrollView(this.panelRect, this.panelScroll, this.panelViewport, false, true);

			#region Plugin Name Section
			GUIX.Heading(new Rect(4, offset, 80, 20), "Symphony", Color.yellow);
			GUIX.Label(new Rect(84, offset, 160, 20), Plugin.VersionTag);
			offset += 20 + 4;
			#endregion

			GUIX.HLine(new Rect(4, offset, 240, 0));
			offset += 1 + 4;

			#region BepInEx console disable
			var consoleUsing = BepInEx.ConsoleManager.ConfigConsoleEnabled.Value;
			if (consoleUsing) {
				if (GUIX.Button(
					new Rect(4, offset, 240, 20),
					"BepInEx 콘솔 끄기",
					Color.HSVToRGB(0f, 0.6f, 0.6f),
					Color.HSVToRGB(0f, 0.7f, 0.7f),
					Color.HSVToRGB(0f, 0.8f, 0.8f)
				)) {
					BepInEx.ConsoleManager.ConfigConsoleEnabled.Value = false;
					BepInEx.ConsoleManager.DetachConsole();
				}
				offset += 20 + 4;

				GUIX.HLine(new Rect(4, offset, 240, 0));
				offset += 1 + 4;
			}
			#endregion

			#region SimpleTweaks Section
			GUIX.Heading(new Rect(4, offset, 240, 20), "SimpleTweaks");
			offset += 20 + 4;

			{
				var value = GUIX.Toggle(new Rect(4, offset, 240, 20), SimpleTweaks.DisplayFPS.Value, "FPS 표시");
				if (value != SimpleTweaks.DisplayFPS.Value) {
					SimpleTweaks.DisplayFPS.Value = value;
					SimpleTweaks.config.Save();
				}
				offset += 20 + 4;
			}

			{
				var value = GUIX.Toggle(new Rect(4, offset, 240, 20), SimpleTweaks.LimitFPS.Value, "FPS 제한하기");
				if (value != SimpleTweaks.LimitFPS.Value) {
					SimpleTweaks.LimitFPS.Value = value;
					SimpleTweaks.config.Save();
				}
				offset += 20 + 4;

				if (SimpleTweaks.LimitFPS.Value) {
					GUIX.Label(new Rect(4, offset, 88, 20), "최대 FPS");
					var input = GUIX.TextField(new Rect(96, offset, 50, 20), SimpleTweaks.MaxFPS.Value.ToString());
					if (int.TryParse(input, out var input_i)) {
						SimpleTweaks.MaxFPS.Value = input_i;
						SimpleTweaks.config.Save();
					}
					offset += 20 + 4;
				}
			}

			{
				var value = GUIX.Toggle(new Rect(4, offset, 240, 20), SimpleTweaks.UseLobbyHide.Value, "로비 UI 토글 사용");
				if (value != SimpleTweaks.UseLobbyHide.Value) {
					SimpleTweaks.UseLobbyHide.Value = value;
					SimpleTweaks.config.Save();
				}
				offset += 20 + 4;
			}
			if (SimpleTweaks.UseLobbyHide.Value) {
				GUIX.Label(new Rect(4, offset, 88, 20), "로비 UI 토글 키");
				GUIX.KeyBinder("SimpleTweak:LobbyUIHideKey", new Rect(120, offset, 120, 20), SimpleTweaks.LobbyUIHideKey.Value, KeyCode => {
					SimpleTweaks.LobbyUIHideKey.Value = KeyCode.ToString();
					SimpleTweaks.config.Save();
				});
				offset += 20 + 4;
			}

			{
				var value = GUIX.Toggle(new Rect(4, offset, 240, 20), SimpleTweaks.UseFormationFix.Value, "편성 화면 선택 버그 수정");
				if (value != SimpleTweaks.UseFormationFix.Value) {
					SimpleTweaks.UseFormationFix.Value = value;
					SimpleTweaks.config.Save();
				}
				offset += 20 + 4;
			}
			#endregion

			GUIX.HLine(new Rect(4, offset, 240, 0));
			offset += 1 + 4;

			#region WindowedResize Section
			GUIX.Heading(new Rect(4, offset, 240, 20), "WindowedResize");
			offset += 20 + 4;

			GUIX.Label(new Rect(4, offset, 88, 20), "전체화면 키");
			GUIX.KeyBinder("WindowedResize:Key_Mode", new Rect(120, offset, 120, 20), WindowedResize.Key_Mode.Value, KeyCode => {
				WindowedResize.Key_Mode.Value = KeyCode.ToString();
				WindowedResize.config.Save();
			});
			offset += 20 + 4;
			#endregion

			GUIX.HLine(new Rect(4, offset, 240, 0));
			offset += 1 + 4;

			#region BattleHotkey Section
			GUIX.Heading(new Rect(4, offset, 240, 20), "BattleHotkey");
			offset += 20 + 4;

			{
				var value = GUIX.Toggle(new Rect(4, offset, 240, 20), BattleHotkey.Use_SkillPanel.Value, "행동 단축키 사용");
				if (value != BattleHotkey.Use_SkillPanel.Value) {
					BattleHotkey.Use_SkillPanel.Value = value;
					BattleHotkey.config.Save();
				}
				offset += 20 + 4;
			}
			if (BattleHotkey.Use_SkillPanel.Value) {
				GUIX.Label(new Rect(24, offset, 70, 20), "액티브 스킬 1");
				GUIX.KeyBinder("BattleHotkey:Skill1", new Rect(120, offset, 120, 20), BattleHotkey.Key_SkillPanel[0].Value, KeyCode => {
					BattleHotkey.Key_SkillPanel[0].Value = KeyCode.ToString();
					BattleHotkey.config.Save();
				});
				offset += 20 + 4;

				GUIX.Label(new Rect(24, offset, 70, 20), "액티브 스킬 2");
				GUIX.KeyBinder("BattleHotkey:Skill2", new Rect(120, offset, 120, 20), BattleHotkey.Key_SkillPanel[1].Value, KeyCode => {
					BattleHotkey.Key_SkillPanel[1].Value = KeyCode.ToString();
					BattleHotkey.config.Save();
				});
				offset += 20 + 4;

				GUIX.Label(new Rect(24, offset, 70, 20), "이동");
				GUIX.KeyBinder("BattleHotkey:Move", new Rect(120, offset, 120, 20), BattleHotkey.Key_SkillPanel[2].Value, KeyCode => {
					BattleHotkey.Key_SkillPanel[2].Value = KeyCode.ToString();
					BattleHotkey.config.Save();
				});
				offset += 20 + 4;

				GUIX.Label(new Rect(24, offset, 70, 20), "대기");
				GUIX.KeyBinder("BattleHotkey:Wait", new Rect(120, offset, 120, 20), BattleHotkey.Key_SkillPanel[3].Value, KeyCode => {
					BattleHotkey.Key_SkillPanel[3].Value = KeyCode.ToString();
					BattleHotkey.config.Save();
				});
				offset += 20 + 4;
			}

			{
				var value = GUIX.Toggle(new Rect(4, offset, 240, 20), BattleHotkey.Use_PlayButton.Value, "행동 개시 단축키 사용");
				if (value != BattleHotkey.Use_PlayButton.Value) {
					BattleHotkey.Use_PlayButton.Value = value;
					BattleHotkey.config.Save();
				}
				offset += 20 + 4;
			}
			if (BattleHotkey.Use_PlayButton.Value) {
				GUIX.Label(new Rect(24, offset, 70, 20), "행동 개시");
				GUIX.KeyBinder("BattleHotkey:Play", new Rect(120, offset, 120, 20), BattleHotkey.Key_Play.Value, KeyCode => {
					BattleHotkey.Key_Play.Value = KeyCode.ToString();
					BattleHotkey.config.Save();
				});
				offset += 20 + 4;
			}
			/*
			{
				var value = GUIX.Toggle(new Rect(4, offset, 240, 20), BattleHotkey.Use_TeamGrid.Value, "아군 선택 단축키 사용");
				if (value != BattleHotkey.Use_TeamGrid.Value) {
					BattleHotkey.Use_TeamGrid.Value = value;
					BattleHotkey.config.Save();
				}
				offset += 20 + 4;
			}
			if (BattleHotkey.Use_TeamGrid.Value) {
				for (var i = 0; i < 9; i++) {
					GUIX.Label(new Rect(24, offset, 70, 20), $"아군 위치 {i + 1}");
					GUIX.DrawGrid33(new Rect(98, offset + 2, 16, 16), i);

					GUIX.KeyBinder($"BattleHotkey:TeamGrid{i + 1}", new Rect(120, offset, 120, 20), BattleHotkey.Key_TeamGrid[i].Value, keyCode => {
						BattleHotkey.Key_TeamGrid[i].Value = keyCode.ToString();
						BattleHotkey.config.Save();
					});
					offset += 20 + 4;
				}
			}
			*/
			{
				var value = GUIX.Toggle(new Rect(4, offset, 240, 20), BattleHotkey.Use_EnemyGrid.Value, "적군 선택 단축키 사용");
				if (value != BattleHotkey.Use_EnemyGrid.Value) {
					BattleHotkey.Use_EnemyGrid.Value = value;
					BattleHotkey.config.Save();
				}
				offset += 20 + 4;
			}
			if (BattleHotkey.Use_EnemyGrid.Value) {
				for (var i = 0; i < 9; i++) {
					GUIX.Label(new Rect(24, offset, 70, 20), $"적군 위치 {i + 1}");
					GUIX.DrawGrid33(new Rect(98, offset + 2, 16, 16), i);

					GUIX.KeyBinder($"BattleHotkey:EnemyGrid{i + 1}", new Rect(120, offset, 120, 20), BattleHotkey.Key_EnemyGrid[i].Value, keyCode => {
						BattleHotkey.Key_EnemyGrid[i].Value = keyCode.ToString();
						BattleHotkey.config.Save();
					});
					offset += 20 + 4;
				}
			}
			#endregion

			GUI.EndScrollView(true);
			this.panelViewport.height = offset;
		}
	}
}
