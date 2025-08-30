using BepInEx;
using BepInEx.Configuration;

using LOEventSystem;
using LOEventSystem.Msg;

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Symphony.Features {
	internal class BattleHotkey : MonoBehaviour, Listener {
		internal static readonly ConfigFile config = new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.BattleHotkey.cfg"), true);

		internal static readonly ConfigEntry<bool> Use_SkillPanel = config.Bind("BattleHotkey", "Use_SkillPanel", true, "Use skill panel hotkeys");
		internal static readonly ConfigEntry<string>[] Key_SkillPanel = [
			config.Bind("BattleHotkey", "Skill1", "Alpha1", "Skill1 button hotkey"),
			config.Bind("BattleHotkey", "Skill2", "Alpha2", "Skill2 button hotkey"),
			config.Bind("BattleHotkey", "Move", "Alpha3", "Move button hotkey"),
			config.Bind("BattleHotkey", "Wait", "Alpha4", "Wait button hotkey"),
		];

		internal static readonly ConfigEntry<bool> Use_TeamGrid = config.Bind("BattleHotkey", "Use_TeamGrid", true, "Use team grid hotkeys");
		internal static readonly ConfigEntry<string>[] Key_TeamGrid = [
			config.Bind("BattleHotkey", "Team1", "Z", "Team grid 1 button hotkey"),
			config.Bind("BattleHotkey", "Team2", "X", "Team grid 2 button hotkey"),
			config.Bind("BattleHotkey", "Team3", "C", "Team grid 3 button hotkey"),
			config.Bind("BattleHotkey", "Team4", "A", "Team grid 4 button hotkey"),
			config.Bind("BattleHotkey", "Team5", "S", "Team grid 5 button hotkey"),
			config.Bind("BattleHotkey", "Team6", "D", "Team grid 6 button hotkey"),
			config.Bind("BattleHotkey", "Team7", "Q", "Team grid 7 button hotkey"),
			config.Bind("BattleHotkey", "Team8", "W", "Team grid 8 button hotkey"),
			config.Bind("BattleHotkey", "Team9", "E", "Team grid 9 button hotkey"),
		];
		internal static readonly ConfigEntry<bool> Use_EnemyGrid = config.Bind("BattleHotkey", "Use_EnemyGrid", true, "Use enemy grid hotkeys");
		internal static readonly ConfigEntry<string>[] Key_EnemyGrid = [
			config.Bind("BattleHotkey", "Enemy1", "Keypad1", "Enemy grid 1 button hotkey"),
			config.Bind("BattleHotkey", "Enemy2", "Keypad2", "Enemy grid 2 button hotkey"),
			config.Bind("BattleHotkey", "Enemy3", "Keypad3", "Enemy grid 3 button hotkey"),
			config.Bind("BattleHotkey", "Enemy4", "Keypad4", "Enemy grid 4 button hotkey"),
			config.Bind("BattleHotkey", "Enemy5", "Keypad5", "Enemy grid 5 button hotkey"),
			config.Bind("BattleHotkey", "Enemy6", "Keypad6", "Enemy grid 6 button hotkey"),
			config.Bind("BattleHotkey", "Enemy7", "Keypad7", "Enemy grid 7 button hotkey"),
			config.Bind("BattleHotkey", "Enemy8", "Keypad8", "Enemy grid 8 button hotkey"),
			config.Bind("BattleHotkey", "Enemy9", "Keypad9", "Enemy grid 9 button hotkey"),
		];
		internal static readonly ConfigEntry<bool> Use_PlayButton = config.Bind("BattleHotkey", "Use_PlayButton", true, "Use play button hotkeys");
		internal static readonly ConfigEntry<string> Key_Play = config.Bind("BattleHotkey", "Play", "KeypadPlus", "Play button hotkey");

		private bool inBattleScene = false;

		public void Awake() {
			StartCoroutine(LazyStart());
		}
		public void OnDestroy() {
			Handler.RemoveListner(this);
		}

		private IEnumerator LazyStart() {
			yield return new WaitForEndOfFrame();

			Plugin.Logger.LogInfo("[Symphony::BattleHotkey] Scene change detecting start");
			SceneManager.activeSceneChanged += (prev, _new) => {
				Plugin.Logger.LogDebug($"[Symphony::BattleHotkey] Scene change detected, new one is {_new.name}");

				inBattleScene = _new.name == "Scene_StageBattle";
				if (inBattleScene) {
					Plugin.Logger.LogInfo("[Symphony::BattleHotkey] Battle scene detected, load hotkeys");

					Handler.RegListner(this, eType.SKillConfirm); // Detect skill confirm button appears where.
				}
				else {
					Handler.RemoveListner(this);
				}
			};
		}

		public void Update() {
			if (!inBattleScene) return;

			CheckSkillPanel();
			//CheckTeamGridPanel();
			CheckEnemyGridPanel();
			CheckPlayPanel();
		}

		private int lastSelectedCellIndex = 0;
		public virtual void OnEvent(Base msg) {
			switch (msg.Type) {
				case eType.SKillConfirm:
					SKillConfirm cf = msg as SKillConfirm;
					Plugin.Logger.LogDebug($"OnEvent:SkillConfirm -> {(bool)cf.targetCreature} / {(bool)cf.targetLine}");
					if (cf.targetLine) {
						Plugin.Logger.LogDebug($"OnEvent:SkillConfirm.targetLine -> {cf.targetLine.Cell()} / {cf.targetLine.Row()}");
						lastSelectedCellIndex = cf.targetLine.Cell() + (3 - cf.targetLine.Row()) * 3;
					}
					break;
			}
		}

		private void CheckSkillPanel() {
			if (!Use_SkillPanel.Value) return;

			for (var i = 0; i < Key_SkillPanel.Length; i++) {
				var keyName = Key_SkillPanel[i];
				if (keyName.Value != "" && Helper.KeyCodeParse(keyName.Value, out var kc) && Input.GetKeyDown(kc)) { // Key downed?
					var panel = FindObjectOfType<Panel_StageBattle>();
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

		private void CheckTeamGridPanel() {
			if (!Use_TeamGrid.Value) return;

			for (int i = 0; i < 9; i++) {
				var idx = i % 3 + (2 - i / 3) * 3;
				var keyName = Key_TeamGrid[i];
				if (keyName.Value != "" && Helper.KeyCodeParse(keyName.Value, out var kc) && Input.GetKeyDown(kc)) { // Key downed?
					var panel_battle = FindObjectOfType<Panel_StageBattle>();
					if (panel_battle == null) {
						Plugin.Logger.LogWarning("[Symphony::BattleHotkey] In Battle scene, but Panel_StageBattle not found");
						return;
					}

					var btnSkillConfirm = (UIButton)typeof(Panel_StageBattle).GetField("_btnSkillConfirm", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel_battle);
					if (btnSkillConfirm != null && btnSkillConfirm.isActiveAndEnabled &&  // Confirm button alive
						lastSelectedCellIndex == i // pressed same cell button
						) {
						EventDelegate.Execute(btnSkillConfirm.onClick);
						return;
					}

					var panel_creature = FindObjectOfType<Panel_CreatureTouch>();
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

		private void CheckEnemyGridPanel() {
			if (!Use_EnemyGrid.Value) return;

			for (int i = 0; i < 9; i++) {
				var idx = i % 3 + (2 - i / 3) * 3;
				var keyName = Key_EnemyGrid[i];
				if (keyName.Value != "" && Helper.KeyCodeParse(keyName.Value, out var kc) && Input.GetKeyDown(kc)) { // Key downed?
					var panel_battle = FindObjectOfType<Panel_StageBattle>();
					if (panel_battle == null) {
						Plugin.Logger.LogWarning("[Symphony::BattleHotkey] In Battle scene, but Panel_StageBattle not found");
						return;
					}

					var btnSkillConfirm = (UIButton)typeof(Panel_StageBattle).GetField("_btnSkillConfirm", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel_battle);
					if (btnSkillConfirm != null && btnSkillConfirm.isActiveAndEnabled &&  // Confirm button alive
						lastSelectedCellIndex == i // pressed same cell button
						) {
						EventDelegate.Execute(btnSkillConfirm.onClick);
						return;
					}

					var panel_creature = FindObjectOfType<Panel_CreatureTouch>();
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
			if (!Use_PlayButton.Value) return;

			if (Key_Play.Value != "" && Helper.KeyCodeParse(Key_Play.Value, out var kc) && Input.GetKeyDown(kc)) { // Key downed?
				var panel_battle = FindObjectOfType<Panel_StageBattle>();
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
