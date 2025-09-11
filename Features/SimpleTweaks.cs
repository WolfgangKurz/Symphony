using BepInEx;
using BepInEx.Configuration;

using HarmonyLib;

using Symphony.UI;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Symphony.Features {
	internal class SimpleTweaks : MonoBehaviour {
		private class SimpleTweaks_Patch {
			public static IEnumerable<CodeInstruction> Patch_PanelBase_Update(MethodBase original, IEnumerable<CodeInstruction> instructions) {
				Plugin.Logger.LogInfo("[Symphony::SimpleTweaks] Start to patch Panel_Base.Update to patch auto-next");

				var Input_GetKey_KeyCode = AccessTools.Method(typeof(Input), "GetKey", [typeof(KeyCode)]);
				var Input_GetKeyUp_KeyCode = AccessTools.Method(typeof(Input), "GetKeyUp", [typeof(KeyCode)]);
				var Panel_Base_IsHolding = AccessTools.Field(typeof(Panel_Base), "isHolding");

				var new_inst = new CodeInstruction(
					OpCodes.Call,
					AccessTools.Method(typeof(SimpleTweaks), nameof(SimpleTweaks.StoryViewerPatchKey))
				);

				var ret = instructions;

				#region Press patch
				{
					var matcher = new CodeMatcher(ret);
					matcher.MatchForward(false,
						/* if (this.isHolding && Input.GetKey(KeyCode.Space) &&  ... */

						new CodeMatch(OpCodes.Ldarg_0), // this.
						new CodeMatch(OpCodes.Ldfld, Panel_Base_IsHolding), // isHolding
						new CodeMatch(OpCodes.Brfalse), // == false -> goto OPERAND

						new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)32), // 0x20, Space keycode
						new CodeMatch(OpCodes.Call, Input_GetKey_KeyCode), // Input.GetKey(KeyCode)
						new CodeMatch(OpCodes.Brfalse) // == false -> goto OPERAND
					);

					if (matcher.IsInvalid) {
						Plugin.Logger.LogWarning("[Symphony::SimpleTweaks] Failed to patch Panel_Base.Update, target instructions not found");
						return instructions;
					}

					matcher.Advance(3); // move to Space keycode
					new_inst.labels = matcher.Instruction.labels;
					matcher.RemoveInstruction(); // remove Space keycode
					matcher.Insert(new_inst); // insert calling
					ret = matcher.InstructionEnumeration();
				}
				#endregion

				#region Up patch
				{
					var matcher = new CodeMatcher(ret);
					matcher.MatchForward(false,
						/* if (this.isHolding && Input.GetKey(KeyCode.Space) &&  ... */
						new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)32), // 0x20, Space keycode
						new CodeMatch(OpCodes.Call, Input_GetKeyUp_KeyCode), // Input.GetKey(KeyCode)
						new CodeMatch(OpCodes.Brfalse) // == false -> goto OPERAND
					);

					if (matcher.IsInvalid) {
						Plugin.Logger.LogWarning("[Symphony::SimpleTweaks] Failed to patch Panel_Base.Update, target instructions not found");
						return instructions;
					}

					new_inst.labels = matcher.Instruction.labels;
					matcher.RemoveInstruction(); // remove Space keycode
					matcher.Insert(new_inst); // insert calling
					ret = matcher.InstructionEnumeration();
				}
				#endregion

				return ret;
			}

			public static IEnumerable<CodeInstruction> Patch_GameManager_Update(MethodBase original, IEnumerable<CodeInstruction> instructions) {
				Plugin.Logger.LogInfo("[Symphony::WindowedResize] Start to patch GameManager.Update");

				var Input_GetKeyDown_KeyCode = AccessTools.Method(typeof(Input), "GetKeyDown", [typeof(KeyCode)]);

				var matcher = new CodeMatcher(instructions);
				matcher.MatchForward(false,
					/* if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)) return; */
					new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)13), // Return KeyCode
					new CodeMatch(OpCodes.Call, Input_GetKeyDown_KeyCode), // Input.GetKeyDown(KeyCode)
					new CodeMatch(OpCodes.Brtrue), // == true -> goto OPERAND

					new CodeMatch(OpCodes.Ldc_I4, 271), // KeypadEnter KeyCode
					new CodeMatch(OpCodes.Call, Input_GetKeyDown_KeyCode), // Input.GetKeyDown(KeyCode)
					new CodeMatch(OpCodes.Brfalse) // == false -> goto OPERAND
				);

				if (matcher.IsInvalid) {
					Plugin.Logger.LogWarning("[Symphony::WindowedResize] Failed to patch GameManager.Update, target instructions not found");
					return instructions;
				}

				var new_inst = new CodeInstruction(
					OpCodes.Call,
					AccessTools.Method(typeof(SimpleTweaks), nameof(SimpleTweaks.IsFullScreenKeyDowned))
				);
				new_inst.labels = matcher.Instruction.labels;

				matcher.RemoveInstructions(5); // remove except 'return;'
				matcher.Insert(new_inst);
				return matcher.InstructionEnumeration();
			}
		}

		internal static ConfigFile config = new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.SimpleTweaks.cfg"), true);

		internal static ConfigEntry<bool> DisplayFPS = config.Bind("SimpleTweaks", "DisplayFPS", false, "Display FPS to screen");

		internal static ConfigEntry<bool> LimitFPS = config.Bind("SimpleTweaks", "LimitFPS", false, "Limits game framerate. Uses MaxFPS value");
		internal static ConfigEntry<bool> LimitBattleFPS = config.Bind("SimpleTweaks", "LimitBattleFPS", true, "Limits battle framerate. Uses MaxBattleFPS value");
		internal static ConfigEntry<int> MaxFPS = config.Bind("SimpleTweaks", "MaxFPS", 60, "Framerate");
		internal static ConfigEntry<int> MaxBattleFPS = config.Bind("SimpleTweaks", "MaxBattleFPS", 60, "Framerate");

		internal static ConfigEntry<bool> UseLobbyHide = config.Bind("SimpleTweaks", "UseLobbyHide", true, $"Use hotkey to toggle lobby UI");
		internal static ConfigEntry<string> LobbyUIHideKey = config.Bind("SimpleTweaks", "LobbyHideKey", "Tab", $"Key to toggle lobby UI");

		internal static readonly ConfigEntry<bool> Use_IgnoreWindowReset = config.Bind("SimpleTweaks", "Ignore_WindowReset", true, "Ignore window size aspect-ratio and position reset after resize");

		internal static readonly ConfigEntry<bool> Use_FullScreenKey = config.Bind("SimpleTweaks", "Use_FullScreenKey", true, "Use FullScreen mode key change");
		internal static readonly ConfigEntry<string> FullScreenKey = config.Bind("SimpleTweaks", "FullScreenKey", "F11", "Window mode change button replacement");

		internal static ConfigEntry<bool> MuteOnBackground = config.Bind("SimpleTweaks", "MuteOnBackground", false, $"Mute all sound when game go to background");

		internal static float VolumeBGM {
			get => GameOption.BgmVolume;
			set {
				if (value != GameOption.BgmVolume) {
					GameOption.BgmVolume = value;
					GameSoundManager.Instance.ChangeVolumeBGM();
					GameOption.SaveSetting();
				}
			}
		}
		internal static float VolumeSFX {
			get => GameOption.SfxVolume;
			set {
				if (value != GameOption.SfxVolume) {
					GameOption.SfxVolume = value;
					GameSoundManager.Instance.ChangeVolumeEffect();
					GameOption.SaveSetting();
				}
			}
		}
		internal static float VolumeVoice {
			get => GameOption.VoiceVolume;
			set {
				if (value != GameOption.VoiceVolume) {
					GameOption.VoiceVolume = value;
					GameSoundManager.Instance.ChangeVolumeVoice();
					GameOption.SaveSetting();
				}
			}
		}

		internal static ConfigEntry<bool> UsePatchStorySkip = config.Bind("SimpleTweaks", "UsePatchStorySkip", true, $"Prevent StoryViewer from proceeding automatically when the Space key is held down, and remap the key to PatchStorySkipKey");
		internal static ConfigEntry<string> PatchStorySkipKey = config.Bind("SimpleTweaks", "PatchStorySpacebar", "LeftControl", $"Key to remap for StoryViewer");

		internal static ConfigEntry<bool> UseFormationFix = config.Bind("SimpleTweaks", "UseFormationFix", true, $"Fix character selection bug on Formation scene");

		//////////////////////////////////////////////////////////////////////////////////////

		private FrameLimit DisplayFPSLimit = new(0.5f);

		private GUIStyle FPSStyle;
		private string lastFPS = "0";

		public void Start() {
			FPSStyle = new GUIStyle();
			FPSStyle.alignment = TextAnchor.MiddleCenter;
			FPSStyle.normal.textColor = Color.white;
			FPSStyle.fontSize = 13;
			FPSStyle.fontStyle = FontStyle.Bold;

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

					if (keyCodeName != "" && Helper.KeyCodeParse(keyCodeName, out var _)) {
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
			{ // from WindowedResize
				var path = Path.Combine(Paths.ConfigPath, "Symphony.WindowedResize.cfg");
				if (File.Exists(path)) {
					Plugin.Logger.LogMessage("[Symphony::SimpleTweaks] WindowedResize configuration detected, migration it.");
					var _old = new ConfigFile(path, false);

					var useFullScreenKey = _old.Bind("WindowedResize", "Use_FullScreenKey", true).Value;
					Use_FullScreenKey.Value = useFullScreenKey;

					var keyCodeName = _old.Bind("WindowedResize", "Key_Mode", "F11").Value;
					if (keyCodeName != "" && Helper.KeyCodeParse(keyCodeName, out var _)) {
						FullScreenKey.Value = keyCodeName;
					}

					File.Delete(path);
					config.Save();
				}
			}
			#endregion

			#region Patch
			var harmony = new Harmony("Symphony.SimpleTweaks");
			harmony.Patch(
				AccessTools.Method(typeof(Panel_Base), "Update"),
				transpiler: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_PanelBase_Update))
			);
			harmony.Patch(
				AccessTools.Method(typeof(GameManager), "Update"),
				transpiler: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_GameManager_Update))
			);
			harmony.Patch(
				AccessTools.Method(typeof(WindowsGameManager), "ApplyAspectRatioNextFrame"),
				prefix: new HarmonyMethod(typeof(SimpleTweaks), nameof(SimpleTweaks.IsIgnoreWindowRest))
			);
			#endregion

			if (Helper.KeyCodeParse(FullScreenKey.Value, out var kc)) {
				Plugin.Logger.LogInfo($"[Symphony::SimpleTweaks] > Key for Fullscreen toggle is '{FullScreenKey.Value}', KeyCode is {kc}. This message will be logged once at first time.");
			}
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
			foreach (var obj in objects) {
				var go = obj.gameObject;
				if (!go.TryGetComponent(typeof(UIButton), out var _)) {
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

			if (!LimitFPS.Value && !LimitBattleFPS.Value) {
				ResetFPS();
				return;
			}

			if (SceneManager.GetActiveScene().name == "Scene_StageBattle" && LimitBattleFPS.Value && MaxBattleFPS.Value > 0) {
				if (Application.targetFrameRate != MaxBattleFPS.Value || QualitySettings.vSyncCount != 0) { // should update
					Application.targetFrameRate = MaxBattleFPS.Value;
					QualitySettings.vSyncCount = 0;
					Plugin.Logger.LogInfo(
						$"[Symphony::SimpleTweak] Set battle framerate limit to {MaxBattleFPS.Value}" +
						(originalVSyncCount > 0 ? ", VSync also disabled" : "")
					);
				}
			}
			else if (LimitFPS.Value && MaxFPS.Value > 0) { // framerate has set
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

		private static int StoryViewerPatchKey() {
			if (UsePatchStorySkip.Value) {
				if (PatchStorySkipKey.Value != "" && Helper.KeyCodeParse(PatchStorySkipKey.Value, out var kc)) // Key valid?
					return (int)kc;
			}
			return (int)KeyCode.Space;
		}

		private static bool IsFullScreenKeyDowned() {
			var key = KeyCode.None;
			if (Helper.KeyCodeParse(FullScreenKey.Value, out var kc)) {
				key = kc;
			}

			if (Use_FullScreenKey.Value && key != KeyCode.None)
				return Input.GetKeyDown(key);
			return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter); // Game default value
		}
		private static bool IsIgnoreWindowRest(ref IEnumerator __result) {
			if (Use_IgnoreWindowReset.Value) {
				__result = Enumerable.Empty<YieldInstruction>().GetEnumerator();
				return false;
			}
			return true;
		}
	}
}
