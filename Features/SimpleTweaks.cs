using HarmonyLib;

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Symphony.Features {
	internal class SimpleTweaks : MonoBehaviour {
		private class SimpleTweaks_Patch {
			private static Helper.RECT? lastWindowRect = null;

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
				Plugin.Logger.LogInfo("[Symphony::SimpleTweaks] Start to patch GameManager.Update");

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
					Plugin.Logger.LogWarning("[Symphony::SimpleTweaks] Failed to patch GameManager.Update, target instructions not found");
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

			public static bool IsIgnoreWindowReset(ref IEnumerator __result) {
				if (Conf.SimpleTweaks.Use_IgnoreWindowReset.Value) {
					__result = Enumerable.Empty<YieldInstruction>().GetEnumerator();
					return false;
				}
				return true;
			}
			public static void Patch_Screen_SetResolution_Prefix(int width, int height, FullScreenMode fullscreenMode, int preferredRefreshRate) {
				if (!Conf.SimpleTweaks.Use_IgnoreWindowReset.Value) return;

				if (fullscreenMode != FullScreenMode.Windowed) { // Going to fullscreen
					if (Helper.GetWindowRect(Plugin.hWnd, out var rc)) // Remember last window position
						lastWindowRect = rc;
				}
			}
			public static void Patch_Screen_SetResolution_Postfix(int width, int height, FullScreenMode fullscreenMode, int preferredRefreshRate) {
				if (!Conf.SimpleTweaks.Use_IgnoreWindowReset.Value) return;

				if (fullscreenMode == FullScreenMode.Windowed && lastWindowRect.HasValue) { // Restored to windowed
					IEnumerator Patch_Screen_SetResolution_Coroutine(Helper.RECT rc) {
						yield return null;
						Helper.ResizeWindow(Plugin.hWnd, rc);
					}

					var rc = lastWindowRect.Value;
					lastWindowRect = null;

					FindObjectOfType<MonoBehaviour>() // Use any MonoBehaviour
						.StartCoroutine(Patch_Screen_SetResolution_Coroutine(rc));
				}
			}

			public static void MuteOnBackgroundAction(bool paused) {
				if (paused)
					AudioListener.volume = 0.0f;
				else
					AudioListener.volume = 1.0f;
			}

			public static bool Patch_OnApplicationPause(bool pause) {
				if (GameOption.BackGroundSoundOn) return false;
				if (!Conf.SimpleTweaks.MuteOnBackgroundFix.Value) return true;

				MuteOnBackgroundAction(pause);
				return false;
			}
			public static bool Patch_OnApplicationFocus(bool focus) {
				if (GameOption.BackGroundSoundOn) return false;
				if (!Conf.SimpleTweaks.MuteOnBackgroundFix.Value) return true;

				MuteOnBackgroundAction(!focus);
				return false;
			}

			public static void Patch_OfflineBattle_Options_SquadSelect(Panel_SquadSelectOfflineBattle __instance) {
				if (!Conf.SimpleTweaks.Use_OfflineBattle_Memorize.Value) return;

				var toggleCharDisassemble = __instance.XGetFieldValue<UIToggle[]>("_toggleRewardCharacterDecomposeArr");
				var toggleItemDisassemble = __instance.XGetFieldValue<UIToggle[]>("_toggleRewardItemDecomposeArr");

				var conf_Char = (Conf.SimpleTweaks.OfflineBattle_Last_CharDiscomp.Value | 1) & 15;
				var conf_Equip = (Conf.SimpleTweaks.OfflineBattle_Last_EquipDiscomp.Value | 1) & 15;

				// B:1, A:2, S:4, SS:8
				string[] dbgName = ["B", "A", "S", "SS"];
				for (int i = 0; i < toggleCharDisassemble.Length; i++) {
					var v = conf_Char & (1 << i);
					toggleCharDisassemble[toggleCharDisassemble.Length - i - 1].Set(v != 0, false);
					Plugin.Logger.LogDebug($"[Symphony::SimpleTweaks] Char {dbgName[i]} = {v != 0}");
				}
				for (int i = 0; i < toggleItemDisassemble.Length; i++) {
					var v = conf_Equip & (1 << i);
					toggleItemDisassemble[toggleItemDisassemble.Length - i - 1].Set(v != 0, false);
					Plugin.Logger.LogDebug($"[Symphony::SimpleTweaks] Equip {dbgName[i]} = {v != 0}");
				}
				Plugin.Logger.LogInfo($"[Symphony::SimpleTweaks] Last OfflineBattle disassemble grades loaded");
			}
			public static void Patch_OfflineBattle_Options_TimePopup(Panel_OfflineBattlePopup __instance) {
				if (!Conf.SimpleTweaks.Use_OfflineBattle_Memorize.Value) return;

				var t = Conf.SimpleTweaks.OfflineBattle_Last_Time.Value;
				var inp = __instance.XGetFieldValue<UIInput>("selectTimeInput");
				inp.value = t.ToString();
				__instance.OnTimeInputFieldValueChange(inp);
				Plugin.Logger.LogInfo($"[Symphony::SimpleTweaks] Last OfflineBattle time loaded");
			}
			public static void Patch_OfflineBattle_Options_Memorize(Panel_OfflineBattlePopup __instance) {
				var enter = __instance.XGetFieldValue<OfflineBattleEnterClass>("offlineBattleEnter");

				Plugin.Logger.LogInfo($"[Symphony::SimpleTweaks] Last OfflineBattle memorized, char: {enter.characterDiscompose}, equip: {enter.eqiupDiscompose}");
				Conf.SimpleTweaks.OfflineBattle_Last_CharDiscomp.Value = enter.characterDiscompose;
				Conf.SimpleTweaks.OfflineBattle_Last_EquipDiscomp.Value = enter.eqiupDiscompose;
				Conf.SimpleTweaks.OfflineBattle_Last_Time.Value = (int)__instance.XGetFieldValue<uint>("selectTimeHour");
			}

			public static IEnumerable<CodeInstruction> Patch_PanelLogo_OnFinished(MethodBase original, IEnumerable<CodeInstruction> instructions) {
				if (!Conf.SimpleTweaks.Use_QuickLogo.Value) return instructions;

				Plugin.Logger.LogInfo("[Symphony::WindowedResize] Start to patch Panel_Logo.OnFinished");

				var MonoBehaviour_Invoke = AccessTools.Method(typeof(MonoBehaviour), "Invoke");

				var matcher = new CodeMatcher(instructions);
				matcher.MatchForward(false,
					/* this.Invoke("OnTexLogo", 4.2f) */
					new CodeMatch(OpCodes.Ldarg_0), // this
					new CodeMatch(OpCodes.Ldstr, "OnTexLogo"), // "OnTexLogo"
					new CodeMatch(OpCodes.Ldc_R4, 4.2f), // float 4.2
					new CodeMatch(OpCodes.Call, MonoBehaviour_Invoke) // Invoke
				);

				if (matcher.IsInvalid) {
					Plugin.Logger.LogWarning("[Symphony::WindowedResize] Failed to patch Panel_Logo.OnFinished, target instructions not found");
					return instructions;
				}

				matcher.Advance(2); // move to 4.2f
				matcher.Instruction.operand = 1f;
				return matcher.InstructionEnumeration();
			}
			public static IEnumerable<CodeInstruction> Patch_PanelLogo_OnTexLogo(MethodBase original, IEnumerable<CodeInstruction> instructions) {
				if (!Conf.SimpleTweaks.Use_QuickLogo.Value) return instructions;

				Plugin.Logger.LogInfo("[Symphony::WindowedResize] Start to patch Panel_Logo.OnTexLogo");

				var Mathf_Max = AccessTools.Method(typeof(Mathf), "Max", [typeof(float), typeof(float)]);

				var ret = instructions;

				{
					var matcher = new CodeMatcher(ret);
					matcher.MatchForward(false,
						/* var time = Mathf.Max(this.audioSource.clip.length + 1f, 4f) */
						new CodeMatch(OpCodes.Ldc_R4, 4f),
						new CodeMatch(OpCodes.Stloc_0),
						new CodeMatch(OpCodes.Ldarg_0), // this
						new CodeMatch(OpCodes.Ldfld), // audioSource
						new CodeMatch(OpCodes.Callvirt), // get_clip
						new CodeMatch(OpCodes.Callvirt), // get_length
						new CodeMatch(OpCodes.Ldc_R4, 1f), // 1f
						new CodeMatch(OpCodes.Add), // +
						new CodeMatch(OpCodes.Ldloc_0),
						new CodeMatch(OpCodes.Call, Mathf_Max), // Mathf.Max
						new CodeMatch(OpCodes.Stloc_1) // time =
					);

					if (matcher.IsInvalid) {
						Plugin.Logger.LogWarning("[Symphony::WindowedResize] Failed to patch Panel_Logo.OnTexLogo (1), target instructions not found");
						return instructions;
					}

					matcher.Instruction.operand = 1f;

					matcher.Advance(6);
					matcher.Instruction.operand = 0f;

					ret = matcher.InstructionEnumeration();
				}

				{
					var matcher = new CodeMatcher(ret);
					matcher.MatchForward(false,
						// this.audioSource.PlayDelayed(0.5f);
						new CodeMatch(OpCodes.Ldarg_0), // this
						new CodeMatch(OpCodes.Ldfld), // audioSource
						new CodeMatch(OpCodes.Ldc_R4, 0.5f), // 0.5f
						new CodeMatch(OpCodes.Callvirt) // PlayDelayed
					);

					if (matcher.IsInvalid) {
						Plugin.Logger.LogWarning("[Symphony::WindowedResize] Failed to patch Panel_Logo.OnTexLogo (2), target instructions not found");
						return instructions;
					}

					matcher.Advance(2);
					matcher.Instruction.operand = 0f;

					ret = matcher.InstructionEnumeration();
				}

				return ret;
			}
			public static void Patch_PanelLogo_OnFinished_Postfix() {
				if (!Conf.SimpleTweaks.Use_QuickLogo.Value) return;

				var rating = GameObject.FindObjectsOfType<UIPanel>().FirstOrDefault(x => x.name.StartsWith("Panel_Rating"));
				var tweens = rating.GetComponentsInChildren<TweenAlpha>(true);
				foreach (var t in tweens) {
					if (t.delay == 0f) {
						t.duration = 0.75f;
					}
					else if (t.delay == 4.2f) {
						t.delay = 0.75f;
						t.duration = 0.25f;
					}
				}
			}

			public static void Patch_PanelTitle_Start(Panel_Title __instance) {
				if (!Conf.SimpleTweaks.Use_QuickTitle.Value) return;

				// ignore animation waiting
				var fns = __instance.GetComponent<AnimatorAction>().onFinished.ToList();
				__instance.GetComponent<AnimatorAction>().onFinished.Clear();
				EventDelegate.Execute(fns);
			}
			public static void Patch_PanelTitle_RequestPermission(Panel_Title __instance) {
				if (!Conf.SimpleTweaks.Use_AutoLogin.Value) return; // TODO: Consider multi login-method

				IEnumerator Fn() {
					yield return null; // ensure next frame
					__instance.OnBtnTouch();
				}
				__instance.StartCoroutine(Fn());
			}

			public static float lastBGMTime = 0f;
			public static void Patch_BGM_ContinueBGM(GameSoundManager __instance, bool isDeviceChanged) {
				if (!Conf.SimpleTweaks.Use_ContinueBGM.Value) return;
				if (isDeviceChanged) return;

				var bgm = __instance.XGetFieldValue<GameObject>("_goBGM");
				if (bgm == null) return;

				if (!bgm.TryGetComponent<AudioSource>(out var audio)) return;

				audio.time = lastBGMTime;
			}
		}

		//////////////////////////////////////////////////////////////////////////////////////

		private FrameLimit frameBGMMemorize = new FrameLimit(0.1f);

		public void Start() {
			#region Patch
			var harmony = new Harmony("Symphony.SimpleTweaks");

			// FullScreen key remap
			harmony.Patch(
				AccessTools.Method(typeof(GameManager), "Update"),
				transpiler: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_GameManager_Update))
			);

			// Window resize fix
			harmony.Patch( // prevent minimum window size & forcing aspect ratio
				AccessTools.Method(typeof(WindowsGameManager), "ApplyAspectRatioNextFrame"),
				prefix: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.IsIgnoreWindowReset))
			);
			harmony.Patch( // prevent resetting window size & position after back from fullscreen
				AccessTools.Method(typeof(Screen), nameof(Screen.SetResolution), [typeof(int), typeof(int), typeof(FullScreenMode), typeof(int)]),
				prefix: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_Screen_SetResolution_Prefix)),
				postfix: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_Screen_SetResolution_Postfix))
			);

			// Story skip button patch
			harmony.Patch(
				AccessTools.Method(typeof(Panel_Base), "Update"),
				transpiler: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_PanelBase_Update))
			);

			// MuteOnBackgroundFix
			harmony.Patch(
				AccessTools.Method(typeof(WindowsGameManager), "OnApplicationPause"),
				prefix: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_OnApplicationPause))
			);
			harmony.Patch(
				AccessTools.Method(typeof(WindowsGameManager), "OnApplicationFocus"),
				prefix: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_OnApplicationFocus))
			);

			// OfflineBattle Disassemble grades & Time memorizing
			harmony.Patch(
				AccessTools.Method(typeof(Panel_SquadSelectOfflineBattle), nameof(Panel_SquadSelectOfflineBattle.Start)),
				postfix: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_OfflineBattle_Options_SquadSelect))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_OfflineBattlePopup), nameof(Panel_OfflineBattlePopup.Start)),
				postfix: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_OfflineBattle_Options_TimePopup))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_OfflineBattlePopup), nameof(Panel_OfflineBattlePopup.OnOfflineBattleStartButton)),
				prefix: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_OfflineBattle_Options_Memorize))
			);

			// Quick Logo
			harmony.Patch(
				AccessTools.Method(typeof(Panel_Logo), "OnFinished"),
				postfix: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_PanelLogo_OnFinished_Postfix)),
				transpiler: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_PanelLogo_OnFinished))
			);
			harmony.Patch(
				AccessTools.Method(typeof(Panel_Logo), "OnTexLogo"),
				transpiler: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_PanelLogo_OnTexLogo))
			);

			// Quick Title
			harmony.Patch(
				AccessTools.Method(typeof(Panel_Title), "Start"),
				postfix: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_PanelTitle_Start))
			);
			// Auto Login
			harmony.Patch(
				AccessTools.Method(typeof(Panel_Title), "RequestPermission"),
				postfix: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_PanelTitle_RequestPermission))
			);

			// Continue BGM
			harmony.Patch(
				AccessTools.Method(typeof(GameSoundManager), "OnAudioConfigurationChanged"),
				postfix: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_BGM_ContinueBGM))
			);
			#endregion

			if (Helper.KeyCodeParse(Conf.SimpleTweaks.FullScreenKey.Value, out var kc)) {
				Plugin.Logger.LogInfo($"[Symphony::SimpleTweaks] > Key for Fullscreen toggle is '{Conf.SimpleTweaks.FullScreenKey.Value}', KeyCode is {kc}. This message will be logged once at first time.");
			}
		}

		public void Update() {
			Check_LobbyUIToggle();
			Memorize_BGMStatus();
		}

		private void Check_LobbyUIToggle() {
			if (!Conf.SimpleTweaks.UseLobbyHide.Value) return;
			if (SceneManager.GetActiveScene().name != "Scene_Lobby") return;

			if (Conf.SimpleTweaks.LobbyUIHideKey.Value != "" &&
				Helper.KeyCodeParse(Conf.SimpleTweaks.LobbyUIHideKey.Value, out var kc) &&
				Input.GetKeyDown(kc)
			) { // Key downed?
				var panel_lobby = GameObject.FindObjectOfType<Panel_Lobby>();
				if (panel_lobby == null) {
					Plugin.Logger.LogWarning("[Symphony::SimpleTweak] In Lobby scene, but Panel_Lobby not found");
					return;
				}

				panel_lobby.OnBtnExtend();
			}
		}
		private void Memorize_BGMStatus() {
			if (!frameBGMMemorize.Valid()) return;
			if (!Conf.SimpleTweaks.Use_ContinueBGM.Value) return;

			var bgm = GameSoundManager.Instance.XGetFieldValue<GameObject>("_goBGM");
			if (bgm == null) return;

			if (!bgm.TryGetComponent<AudioSource>(out var audio)) return;

			SimpleTweaks_Patch.lastBGMTime = audio.time;
		}

		public static int StoryViewerPatchKey() {
			if (Conf.SimpleTweaks.UsePatchStorySkip.Value) {
				if (Conf.SimpleTweaks.PatchStorySkipKey.Value != "" &&
					Helper.KeyCodeParse(Conf.SimpleTweaks.PatchStorySkipKey.Value, out var kc)
				) // Key valid?
					return (int)kc;
			}
			return (int)KeyCode.Space;
		}

		public static bool IsFullScreenKeyDowned() {
			var key = KeyCode.None;
			if (Helper.KeyCodeParse(Conf.SimpleTweaks.FullScreenKey.Value, out var kc)) {
				key = kc;
			}

			if (Conf.SimpleTweaks.Use_FullScreenKey.Value && key != KeyCode.None)
				return Input.GetKeyDown(key);
			return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter); // Game default value
		}
	}
}
