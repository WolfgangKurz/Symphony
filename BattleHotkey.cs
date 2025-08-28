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
	internal class BattleHotkey : MonoBehaviour, Listener {
		private ConfigEntry<string>[] Key_SkillPanel = null;
		private ConfigEntry<string>[] Key_Pad = null;
		private ConfigEntry<string> Key_Play = null;

		private bool inBattleScene = false;

		public void Awake() {
			// To make default config
			new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.BattleHotkey.cfg"), true);
			this.LoadKeys();

			this.Key_Pad = null;
			this.Key_SkillPanel = null;
			this.Key_Play = null;

			StartCoroutine(this.LazyStart());
		}
		public void OnDestroy() {
			Handler.RemoveListner(this);
		}

		private IEnumerator LazyStart() {
			yield return new WaitForEndOfFrame();

			Plugin.Logger.LogInfo("[Symphony::BattleHotkey] Scene change detecting start");
			SceneManager.activeSceneChanged += (prev, _new) => {
				Plugin.Logger.LogDebug($"[Symphony::BattleHotkey] Scene change detected, new one is {_new.name}");

				this.inBattleScene = _new.name == "Scene_StageBattle";
				if (this.inBattleScene) {
					Plugin.Logger.LogInfo("[Symphony::BattleHotkey] Battle scene detected, load hotkeys");

					Handler.RegListner(this, eType.SKillConfirm); // Detect skill confirm button appears where.
					this.LoadKeys();
				}
				else {
					Handler.RemoveListner(this);
					this.Key_Pad = null;
					this.Key_SkillPanel = null;
					this.Key_Play = null;
				}
			};
		}

		public void Update() {
			if (!this.inBattleScene) return;

			this.CheckSkillPanel();
			this.CheckGridPanel();
			this.CheckPlayPanel();
		}

		private int lastSelectedCellIndex = 0;
		public virtual void OnEvent(Base msg) {
			switch (msg.Type) {
				case eType.SKillConfirm:
					SKillConfirm cf = msg as SKillConfirm;
					Plugin.Logger.LogDebug($"OnEvent:SkillConfirm -> {(bool)cf.targetCreature} / {(bool)cf.targetLine}");
					if (cf.targetLine) {
						Plugin.Logger.LogDebug($"OnEvent:SkillConfirm.targetLine -> {cf.targetLine.Cell()} / {cf.targetLine.Row()}");
						this.lastSelectedCellIndex = cf.targetLine.Cell() + (3 - cf.targetLine.Row()) * 3;
					}
					break;
			}
		}

		private void LoadKeys() {
			var config = new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.BattleHotkey.cfg"), true);

			#region Skill1 Skill2 Move Wait
			var skillPanel_Keys = new string[] { "Skill1", "Skill2", "Move", "Wait" };
			this.Key_SkillPanel = new ConfigEntry<string>[4];
			for (var i = 0; i < skillPanel_Keys.Length; i++) {
				var keyName = skillPanel_Keys[i];
				var keyCodeName = this.Key_SkillPanel[i] = config.Bind("BattleHotkey", keyName, $"Alpha{i + 1}", $"{keyName} button hotkey. Clear will not regsiter hotkey");
				if (keyCodeName.Value != "") {
					if (Helper.KeyCodeParse(keyCodeName.Value, out var kc))
						Plugin.Logger.LogInfo($"[Symphony::BattleHotkey] > Key for {keyName} is '{keyCodeName.Value}', KeyCode is {kc}");
					else
						Plugin.Logger.LogInfo($"[Symphony::BattleHotkey] > Key for {keyName} is '{keyCodeName.Value}', KeyCode is not valid");
				}
			}
			#endregion

			#region Pad Keys
			this.Key_Pad = new ConfigEntry<string>[9];
			for (int i = 0; i < 9; i++) {
				var keyCodeName = this.Key_Pad[i] = config.Bind("BattleHotkey", $"Grid{i + 1}", $"Keypad{i + 1}", $"Grid {i + 1} (Keypad order) hotkey. Clear will not regsiter hotkey");
				if (keyCodeName.Value != "") {
					if (Helper.KeyCodeParse(keyCodeName.Value, out var kc))
						Plugin.Logger.LogInfo($"[Symphony::BattleHotkey] > Key for Grid{i + 1} is '{keyCodeName.Value}', KeyCode is {kc}");
					else
						Plugin.Logger.LogInfo($"[Symphony::BattleHotkey] > Key for Grid{i + 1} is '{keyCodeName.Value}', KeyCode is not valid");
				}
			}
			#endregion

			{
				var keyCodeName = this.Key_Play = config.Bind("BattleHotkey", "Play", "KeypadPlus", $"Play button hotkey. Clear will not regsiter hotkey");
				if (keyCodeName.Value != "") {
					if (Helper.KeyCodeParse(keyCodeName.Value, out var kc))
						Plugin.Logger.LogInfo($"[Symphony::BattleHotkey] > Key for Play is '{keyCodeName.Value}', KeyCode is {kc}");
					else
						Plugin.Logger.LogInfo($"[Symphony::BattleHotkey] > Key for Play is '{keyCodeName.Value}', KeyCode is not valid");
				}
			}
		}

		private void CheckSkillPanel() {
			if (this.Key_SkillPanel == null) return;

			for (var i = 0; i < this.Key_SkillPanel.Length; i++) {
				var keyName = this.Key_SkillPanel[i];
				if (keyName.Value != "" && Helper.KeyCodeParse(keyName.Value, out var kc) && Input.GetKeyDown(kc)) { // Key downed?
					var panel = GameObject.FindObjectOfType<Panel_StageBattle>();
					if (panel == null) {
						Plugin.Logger.LogWarning("[Symphony::BattleHotkey] In Battle scene, but Panel_StageBattle not found");
						return;
					}

					var btns = (UIExtendButton[])typeof(Panel_StageBattle).GetField("bottomSkill", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
					if (btns != null && btns[i] != null && btns[i].isActiveAndEnabled)
						EventDelegate.Execute(btns[i].onClick);
				}
			}
		}

		private void CheckGridPanel() {
			if (this.Key_Pad == null) return;

			for (int i = 0; i < 9; i++) {
				var idx = (i % 3) + (2 - i / 3) * 3;
				var keyName = this.Key_Pad[i];
				if (keyName.Value != "" && Helper.KeyCodeParse(keyName.Value, out var kc) && Input.GetKeyDown(kc)) { // Key downed?
					var panel_battle = GameObject.FindObjectOfType<Panel_StageBattle>();
					if (panel_battle == null) {
						Plugin.Logger.LogWarning("[Symphony::BattleHotkey] In Battle scene, but Panel_StageBattle not found");
						return;
					}

					var btnSkillConfirm = (UIButton)typeof(Panel_StageBattle).GetField("_btnSkillConfirm", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel_battle);
					if (btnSkillConfirm != null && btnSkillConfirm.isActiveAndEnabled &&  // Confirm button alive
						this.lastSelectedCellIndex == i // pressed same cell button
						) {
						EventDelegate.Execute(btnSkillConfirm.onClick);
						return;
					}

					var panel_creature = GameObject.FindObjectOfType<Panel_CreatureTouch>();
					if (panel_creature == null) {
						Plugin.Logger.LogWarning("[Symphony::BattleHotkey] In Battle scene, but Panel_CreatureTouch not found");
						return;
					}

					var enemies = (List<GameObject>)typeof(Panel_CreatureTouch).GetField("_listTouchUI", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel_creature);
					var enemy_button = enemies.FirstOrDefault(r => {
						if (r.TryGetComponent<UIChaSelect>(out var chaSelect)) {
							var enemy = chaSelect.SelectCreature as Enemy;
							if (enemy == null) return false;

							var idx = enemy.CellGrid + 3 * (3 - enemy.RowGrid);
							if (idx == i) return true;
						}
						return false;
					});
					if (enemy_button != null && enemy_button.TryGetComponent<UIButton>(out var button)) {
						// Enemy cell found
						EventDelegate.Execute(button.onClick);
						return;
					}

					// Enemy cell not found, maybe grid selection style skill
					var grids = (List<GameObject>)typeof(Panel_CreatureTouch).GetField("_listEnemyGird", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel_creature);
					if (grids[idx].TryGetComponent<UIChaSelect>(out var chaSelect)) {
						var ev = chaSelect.EventSelect;
						if (ev == null) return; // not selectable grid

						// selectable grid
						ev.Invoke();
					}
				}
			}
		}

		private void CheckPlayPanel() {
			if (this.Key_Play == null) return;

			if (this.Key_Play.Value != "" && Helper.KeyCodeParse(this.Key_Play.Value, out var kc) && Input.GetKeyDown(kc)) { // Key downed?
				var panel_battle = GameObject.FindObjectOfType<Panel_StageBattle>();
				if (panel_battle == null) {
					Plugin.Logger.LogWarning("[Symphony::BattleHotkey] In Battle scene, but Panel_StageBattle not found");
					return;
				}

				var btnPlay = (UIButton)typeof(Panel_StageBattle).GetField("_btnPlay", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel_battle);
				if (btnPlay != null && btnPlay.isActiveAndEnabled) {
					EventDelegate.Execute(btnPlay.onClick);
					return;
				}
			}
		}
	}
}
