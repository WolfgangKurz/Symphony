using BepInEx;
using BepInEx.Configuration;

using Symphony.UI;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Symphony.Features {
	internal class SimpleTweaks : MonoBehaviour {
		internal static ConfigFile config = new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.SimpleTweaks.cfg"), true);

		internal static ConfigEntry<bool> DisplayFPS = config.Bind("SimpleTweaks", "DisplayFPS", false, "Display FPS to screen");

		internal static ConfigEntry<bool> LimitFPS = config.Bind("SimpleTweaks", "LimitFPS", false, "Limits game framerate. Uses MaxFPS value");
		internal static ConfigEntry<int> MaxFPS = config.Bind("SimpleTweaks", "MaxFPS", 60, "Framerate");

		internal static ConfigEntry<bool> UseLobbyHide = config.Bind("SimpleTweaks", "UseLobbyHide", true, $"Use hotkey to toggle lobby UI");
		internal static ConfigEntry<string> LobbyUIHideKey = config.Bind("SimpleTweaks", "LobbyHideKey", "Tab", $"Key to toggle lobby UI");

		internal static ConfigEntry<bool> UseFormationFix = config.Bind("SimpleTweaks", "UseFormationFix", true, $"Fix character selection bug on Formation scene");

		internal static ConfigEntry<bool> MuteOnBackground = config.Bind("SimpleTweaks", "MuteOnBackground", false, $"Mute all sound when game go to background");

		private FrameLimit DisplayFPSLimit = new(0.5f);

		private GUIStyle FPSStyle;
		private string lastFPS = "0";

		public void Start() {
			#region Migration
			{ // from MaximumFrame
				var path = Path.Combine(Paths.ConfigPath, "Symphony.MaximumFrame.cfg");
				if (File.Exists(path)) {
					Plugin.Logger.LogMessage("[Symphony::SimpleTweaks] MaximumFrame configuration detected, migration it.");
					var _old = new ConfigFile(path, false);
					var frame = _old.Bind("MaximumFrame", "maximumFrame", -1).Value;

					LimitFPS.Value = frame > 0;
					MaxFPS.Value = Math.Max(frame, 1);

					File.Delete(path);
					config.Save();
				}
			}
			{ // from LobbyHide
				var path = Path.Combine(Paths.ConfigPath, "Symphony.LobbyHide.cfg");
				if (File.Exists(path)) {
					Plugin.Logger.LogMessage("[Symphony::SimpleTweaks] LobbyHide configuration detected, migration it.");
					var _old = new ConfigFile(path, false);
					var keyCodeName = _old.Bind("LobbyHide", "Toggle", "Tab").Value;

					if (keyCodeName != "" && Helper.KeyCodeParse(keyCodeName, out var kc)) {
						UseLobbyHide.Value = true;
						LobbyUIHideKey.Value = keyCodeName;
					}
					else {
						UseLobbyHide.Value = false;
					}

					File.Delete(path);
					config.Save();
				}
			}
			#endregion

			FPSStyle = new GUIStyle();
			FPSStyle.alignment = TextAnchor.MiddleCenter;
			FPSStyle.normal.textColor = Color.white;
			FPSStyle.fontSize = 13;
			FPSStyle.fontStyle = FontStyle.Bold;
		}

		public void Update() {
			if (DisplayFPS.Value && DisplayFPSLimit.Valid())
				lastFPS = (1.0f / Time.deltaTime).ToString("0.0");

			Check_LobbyUIToggle();
			Check_FormationFix();
			Check_FramerateLimit();
		}

		public void OnGUI() {
			if (DisplayFPS.Value) {
				GUIX.Fill(new Rect(5, 5, 50, 20), GUIX.Colors.WindowBG);
				GUI.Label(new Rect(5, 5, 50, 20), lastFPS, FPSStyle);
			}
		}

		public void OnApplicationFocus(bool hasFocus) {
			if (hasFocus)
				AudioListener.volume = 1.0f;
			else if (MuteOnBackground.Value)
				AudioListener.volume = 0.0f;
		}

		private void Check_LobbyUIToggle() {
			if (!UseLobbyHide.Value) return;
			if (SceneManager.GetActiveScene().name != "Scene_Lobby") return;

			if (LobbyUIHideKey.Value != "" && Helper.KeyCodeParse(LobbyUIHideKey.Value, out var kc) && Input.GetKeyDown(kc)) { // Key downed?
				var panel_lobby = GameObject.FindObjectOfType<Panel_Lobby>();
				if (panel_lobby == null) {
					Plugin.Logger.LogWarning("[Symphony::SimpleTweak] In Lobby scene, but Panel_Lobby not found");
					return;
				}

				panel_lobby.OnBtnExtend();
			}
		}

		private void Check_FormationFix() {
			if (!UseFormationFix.Value) return;
			if (SceneManager.GetActiveScene().name != "Scene_Formation2") return;

			var objects = GameObject.FindObjectsOfType<FormationCharacterPick>();
			foreach(var obj in objects) {
				var go = obj.gameObject;
				if(!go.TryGetComponent(typeof(UIButton), out var _)) {
					var btn = go.AddComponent<UIButton>();
					btn.onClick.Add(new EventDelegate(obj.Pick));

					var chr = go.GetComponent<Character>();
					Plugin.Logger.LogMessage($"[Symphony::SimpleTweak] Formation touch fixed for '{chr.PC.GetPCName()}'");
				}
			}

			if (LobbyUIHideKey.Value != "" && Helper.KeyCodeParse(LobbyUIHideKey.Value, out var kc) && Input.GetKeyDown(kc)) { // Key downed?
				var panel_lobby = GameObject.FindObjectOfType<Panel_Lobby>();
				if (panel_lobby == null) {
					return;
				}

				panel_lobby.OnBtnExtend();
			}
		}

		private FrameLimit FramerateLimit = new(1f);
		private int originalFramerate = -1;
		private int originalVSyncCount = 0;
		private void Check_FramerateLimit() {
			if (!FramerateLimit.Valid()) return;

			if (originalFramerate == -1) {
				originalFramerate = Application.targetFrameRate;
				originalVSyncCount = QualitySettings.vSyncCount;
			}

			if (!LimitFPS.Value) {
				ResetFPS();
				return;
			}

			if (MaxFPS.Value > 0) { // framerate has set
				if (Application.targetFrameRate != MaxFPS.Value || QualitySettings.vSyncCount != 0) { // should update
					Application.targetFrameRate = MaxFPS.Value;
					QualitySettings.vSyncCount = 0;
					Plugin.Logger.LogInfo(
						$"[Symphony::SimpleTweak] Set framerate limit to {MaxFPS.Value}" +
						(originalVSyncCount > 0 ? ", VSync also disabled" : "")
					);
				}
			}
			else // framerate has not set (use vanilla)
				ResetFPS();
		}
		private void ResetFPS() {
			if (Application.targetFrameRate != originalFramerate || QualitySettings.vSyncCount != originalVSyncCount) {
				Application.targetFrameRate = originalFramerate;
				QualitySettings.vSyncCount = originalVSyncCount;
				Plugin.Logger.LogInfo($"[Symphony::SimpleTweak] Set framerate limit to vanilla");
			}
		}
	}
}
