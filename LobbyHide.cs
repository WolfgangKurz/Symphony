using BepInEx;
using BepInEx.Configuration;

using LOEventSystem;
using LOEventSystem.Msg;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using UnityEngine;
using UnityEngine.SceneManagement;

using static UnityEngine.UIElements.UIR.Implementation.UIRStylePainter;

namespace Symphony {
	internal class LobbyHide : MonoBehaviour {
		private ConfigEntry<string> Key_HideToggle= null;

		private bool inLobyScene = false;

		public void Awake() {
			// To make default config
			new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.LobbyHide.cfg"), true);

			StartCoroutine(this.LazyStart());
		}

		private IEnumerator LazyStart() {
			yield return new WaitForEndOfFrame();

			Plugin.Logger.LogInfo("[Symphony::LobbyHide] Scene change detecting start");
			SceneManager.activeSceneChanged += (prev, _new) => {
				Plugin.Logger.LogDebug($"[Symphony::LobbyHide] Scene change detected, new one is {_new.name}");

				this.inLobyScene = _new.name == "Scene_Lobby";
				if (this.inLobyScene) {
					Plugin.Logger.LogInfo("[Symphony::LobbyHide] Lobby scene detected, load hotkeys");

					var config = new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.LobbyHide.cfg"), true);
					var keyCodeName = this.Key_HideToggle = config.Bind("LobbyHide", "Toggle", "Tab", $"Play button hotkey. Clear will not regsiter hotkey");
					if (keyCodeName.Value != "") {
						if (Helper.KeyCodeParse(keyCodeName.Value, out var kc))
							Plugin.Logger.LogInfo($"[Symphony::LobbyHide] > Key for Toggle is '{keyCodeName.Value}', KeyCode is {kc}");
						else
							Plugin.Logger.LogInfo($"[Symphony::LobbyHide] > Key for Toggle is '{keyCodeName.Value}', KeyCode is not valid");
					}
				}
				else {
					this.Key_HideToggle = null;
				}
			};
		}

		public void Update() {
			if (!this.inLobyScene) return;

			this.CheckToggle();
		}
		private void CheckToggle() {
			if (this.Key_HideToggle == null) return;

			

			if (this.Key_HideToggle.Value != "" && Helper.KeyCodeParse(this.Key_HideToggle.Value, out var kc) && Input.GetKeyDown(kc)) { // Key downed?
				var panel_lobby = GameObject.FindObjectOfType<Panel_Lobby>();
				if (panel_lobby == null) {
					Plugin.Logger.LogWarning("[Symphony::BattleHotkey] In Lobby scene, but Panel_Lobby not found");
					return;
				}

				panel_lobby.OnBtnExtend();
			}
		}
	}
}
