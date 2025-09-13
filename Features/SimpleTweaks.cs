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

		private static Helper.RECT? lastWindowRect = null;

		//////////////////////////////////////////////////////////////////////////////////////

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
				prefix: new HarmonyMethod(typeof(SimpleTweaks), nameof(SimpleTweaks.IsIgnoreWindowRest))
			);
			harmony.Patch( // prevent resetting window size & position after back from fullscreen
				AccessTools.Method(typeof(Screen), nameof(Screen.SetResolution), [typeof(int), typeof(int), typeof(FullScreenMode), typeof(int)]),
				prefix: new HarmonyMethod(typeof(SimpleTweaks), nameof(SimpleTweaks.Patch_Screen_SetResolution_Prefix)),
				postfix: new HarmonyMethod(typeof(SimpleTweaks), nameof(SimpleTweaks.Patch_Screen_SetResolution_Postfix))
			);

			// Story skip button patch
			harmony.Patch(
				AccessTools.Method(typeof(Panel_Base), "Update"),
				transpiler: new HarmonyMethod(typeof(SimpleTweaks_Patch), nameof(SimpleTweaks_Patch.Patch_PanelBase_Update))
			);

			// MuteOnBackgroundFix
			harmony.Patch(
				AccessTools.Method(typeof(WindowsGameManager), "OnApplicationPause"),
				prefix: new HarmonyMethod(typeof(SimpleTweaks), nameof(SimpleTweaks.OnApplicationPausePatch))
			);
			harmony.Patch(
				AccessTools.Method(typeof(WindowsGameManager), "OnApplicationFocus"),
				prefix: new HarmonyMethod(typeof(SimpleTweaks), nameof(SimpleTweaks.OnApplicationFocusPatch))
			);
			#endregion

			if (Helper.KeyCodeParse(Conf.SimpleTweaks.FullScreenKey.Value, out var kc)) {
				Plugin.Logger.LogInfo($"[Symphony::SimpleTweaks] > Key for Fullscreen toggle is '{Conf.SimpleTweaks.FullScreenKey.Value}', KeyCode is {kc}. This message will be logged once at first time.");
			}
		}

		public void Update() {
			Check_LobbyUIToggle();
			Check_FormationFix();
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

		private void Check_FormationFix() {
			if (!Conf.SimpleTweaks.UseFormationFix.Value) return;
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

			if (Conf.SimpleTweaks.LobbyUIHideKey.Value != "" && 
				Helper.KeyCodeParse(Conf.SimpleTweaks.LobbyUIHideKey.Value, out var kc) && 
				Input.GetKeyDown(kc)
			) { // Key downed?
				var panel_lobby = GameObject.FindObjectOfType<Panel_Lobby>();
				if (panel_lobby == null) {
					return;
				}

				panel_lobby.OnBtnExtend();
			}
		}

		private static int StoryViewerPatchKey() {
			if (Conf.SimpleTweaks.UsePatchStorySkip.Value) {
				if (Conf.SimpleTweaks.PatchStorySkipKey.Value != "" &&
					Helper.KeyCodeParse(Conf.SimpleTweaks.PatchStorySkipKey.Value, out var kc)
				) // Key valid?
					return (int)kc;
			}
			return (int)KeyCode.Space;
		}

		private static bool IsFullScreenKeyDowned() {
			var key = KeyCode.None;
			if (Helper.KeyCodeParse(Conf.SimpleTweaks.FullScreenKey.Value, out var kc)) {
				key = kc;
			}

			if (Conf.SimpleTweaks.Use_FullScreenKey.Value && key != KeyCode.None)
				return Input.GetKeyDown(key);
			return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter); // Game default value
		}
		private static bool IsIgnoreWindowRest(ref IEnumerator __result) {
			if (Conf.SimpleTweaks.Use_IgnoreWindowReset.Value) {
				__result = Enumerable.Empty<YieldInstruction>().GetEnumerator();
				return false;
			}
			return true;
		}
		private static void Patch_Screen_SetResolution_Prefix(int width, int height, FullScreenMode fullscreenMode, int preferredRefreshRate) {
			if (!Conf.SimpleTweaks.Use_IgnoreWindowReset.Value) return;

			if (fullscreenMode != FullScreenMode.Windowed) { // Going to fullscreen
				if (Helper.GetWindowRect(Plugin.hWnd, out var rc)) // Remember last window position
					lastWindowRect = rc;
			}
		}
		private static void Patch_Screen_SetResolution_Postfix(int width, int height, FullScreenMode fullscreenMode, int preferredRefreshRate) {
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

		private static void MuteOnBackgroundAction(bool paused) {
			if (paused)
				AudioListener.volume = 0.0f;
			else
				AudioListener.volume = 1.0f;
		}
		private static bool OnApplicationPausePatch(bool pause) {
			if (GameOption.BackGroundSoundOn) return false;
			if (!Conf.SimpleTweaks.MuteOnBackgroundFix.Value) return true;

			MuteOnBackgroundAction(pause);
			return false;
		}
		private static bool OnApplicationFocusPatch(bool focus) {
			if (GameOption.BackGroundSoundOn) return false;
			if (!Conf.SimpleTweaks.MuteOnBackgroundFix.Value) return true;

			MuteOnBackgroundAction(!focus);
			return false;
		}
	}
}
