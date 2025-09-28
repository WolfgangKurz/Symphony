using LOEventSystem;
using LOEventSystem.Msg;

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEngine;

namespace Symphony.Features {
	[Feature("BattleHotkey")]
	internal class BattleHotkey : MonoBehaviour, Listener {
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
			SceneListener.Instance.OnEnter("Scene_StageBattle", () => {
				inBattleScene = true;
				Plugin.Logger.LogInfo("[Symphony::BattleHotkey] Battle scene detected, load hotkeys");
				Handler.RegListner(this, eType.SKillConfirm); // Detect skill confirm button appears where.
			});
			SceneListener.Instance.OnExit("Scene_StageBattle", () => {
				inBattleScene = false;
				Handler.RemoveListner(this);
			});
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
			if (!Conf.BattleHotkey.Use_SkillPanel.Value) return;

			for (var i = 0; i < Conf.BattleHotkey.Key_SkillPanel.Length; i++) {
				var keyName = Conf.BattleHotkey.Key_SkillPanel[i];
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
			if (!Conf.BattleHotkey.Use_TeamGrid.Value) return;

			for (int i = 0; i < 9; i++) {
				var idx = i % 3 + (2 - i / 3) * 3;
				var keyName = Conf.BattleHotkey.Key_TeamGrid[i];
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
			if (!Conf.BattleHotkey.Use_EnemyGrid.Value) return;

			for (int i = 0; i < 9; i++) {
				var idx = i % 3 + (2 - i / 3) * 3;
				var keyName = Conf.BattleHotkey.Key_EnemyGrid[i];
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
			if (!Conf.BattleHotkey.Use_PlayButton.Value) return;

			if (Conf.BattleHotkey.Key_Play.Value != "" && 
				Helper.KeyCodeParse(Conf.BattleHotkey.Key_Play.Value, out var kc) && 
				Input.GetKeyDown(kc)
			) { // Key downed?
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
