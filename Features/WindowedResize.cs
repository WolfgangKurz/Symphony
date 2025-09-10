using BepInEx;
using BepInEx.Configuration;

using HarmonyLib;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

using UnityEngine;

namespace Symphony.Features {
	internal class WindowedResize : MonoBehaviour {
		private class WindowedResize_Patch {
			public static IEnumerable<CodeInstruction> Patch_Update(MethodBase original, IEnumerable<CodeInstruction> instructions) {
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
					AccessTools.Method(typeof(WindowedResize), nameof(IsFullScreenKeyDowned))
				);
				new_inst.labels = matcher.Instruction.labels;

				matcher.RemoveInstructions(5); // remove except 'return;'
				matcher.Insert(new_inst);
				return matcher.InstructionEnumeration();
			}
		}
		private enum WindowType : int {
			Window = 0,
			Maximized = 1,
			FullScreen = 2,
		}

		private bool ready = false;

		internal static readonly ConfigFile config = new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.WindowedResize.cfg"), true);
		internal static readonly ConfigEntry<bool> Use_FullScreenKey = config.Bind("WindowedResize", "Use_FullScreenKey", true, "Use FullScreen mode key change");
		internal static readonly ConfigEntry<string> Key_Mode = config.Bind("WindowedResize", "Key_Mode", "F11", "Window mode change button replacement");

		internal static readonly ConfigEntry<bool> Use_Feature = config.Bind("WindowedResize", "Use_Feature", true, "Use WindowedResize feature that allow resize and remember position & size");
		internal static readonly ConfigEntry<int> lastWindowSize_left = config.Bind("WindowedResize", "LastWindowSize_Left", 0, "Last left position of window on windowed mode");
		internal static readonly ConfigEntry<int> lastWindowSize_top = config.Bind("WindowedResize", "LastWindowSize_Top", 0, "Last top position of window on windowed mode");
		internal static readonly ConfigEntry<int> lastWindowSize_right = config.Bind("WindowedResize", "LastWindowSize_Right", 1280, "Last right position of window on windowed mode");
		internal static readonly ConfigEntry<int> lastWindowSize_bottom = config.Bind("WindowedResize", "LastWindowSize_Bottom", 720, "Last bottom position of window on windowed mode");

		internal static readonly ConfigEntry<int> lastWindowedMode = config.Bind("WindowedResize", "LastWindowed", 0, "Last window type flag. 0 is Windowed, 1 is Maximized, 2 is FullScreen");

		private static KeyCode FullScreenKey = KeyCode.F11;

		public void Awake() {
			var harmony = new Harmony("Symphony.WindowedResize");
			harmony.Patch(
				AccessTools.Method(typeof(GameManager), "Update"),
				transpiler: new HarmonyMethod(typeof(WindowedResize_Patch), nameof(WindowedResize_Patch.Patch_Update))
			);

			Key_Mode.SettingChanged += (s, e) => this.Update_FullScreenKey();
			this.Update_FullScreenKey();

			if (lastWindowSize_right.Value <= 30 || lastWindowSize_bottom.Value <= 40) {
				lastWindowSize_left.Value = 0;
				lastWindowSize_top.Value = 0;
				lastWindowSize_right.Value = 1280;
				lastWindowSize_bottom.Value = 720;
			}

			StartCoroutine(LazyStart());
		}

		private void Key_Mode_SettingChanged(object sender, EventArgs e) {
			throw new NotImplementedException();
		}

		private IEnumerator LazyStart() {
			yield return new WaitForEndOfFrame();

			var hWnd = Plugin.hWnd;
			if (lastWindowedMode.Value != (int)WindowType.FullScreen) {
				Screen.fullScreen = false;

				Patch_WindowedResize(true); // Apply window style
				ApplyLastWindowSize();      // Move & Resize window for windowed mode

				yield return new WaitForEndOfFrame();

				if (lastWindowedMode.Value == (int)WindowType.Maximized)
					Helper.MaximizeWindow(hWnd); // Maximize window
			}

			ready = true;
		}

		public void Update() {
			if (!ready) return;
			Patch_WindowedResize();
			Measure_WindowSize();
		}

		private void ApplyLastWindowSize() {
			if (!Use_Feature.Value) return;
			if (Screen.fullScreen) return;

			var hWnd = Plugin.hWnd;
			if (hWnd == IntPtr.Zero) {
				Plugin.Logger.LogError("[Symphony::WindowedResize] Failed to get Game Window Handle");
				return;
			}

			Plugin.Logger.LogInfo($"[Symphony::WindowedResize] Applying latest windowed position");

			var rc = new Helper.RECT(
				lastWindowSize_left.Value,
				lastWindowSize_top.Value,
				lastWindowSize_right.Value,
				lastWindowSize_bottom.Value
			);
			Helper.ResizeWindow(hWnd, rc);
			Plugin.Logger.LogDebug($"[Symphony::WindowedResize]  > {rc.left}, {rc.top}, {rc.right - rc.left}, {rc.bottom - rc.top}");
		}

		private void Patch_WindowedResize(bool init = false) {
			var winType = WindowType.Window;
			if (init)
				winType = (WindowType)lastWindowedMode.Value;

			if (!Use_Feature.Value) return;

			var hWnd = Plugin.hWnd;
			if (hWnd == IntPtr.Zero) {
				Plugin.Logger.LogError("[Symphony::WindowedResize] Failed to get Game Window Handle");
				return;
			}

			if (!init) {
				if (Screen.fullScreen)
					winType = WindowType.FullScreen;
				else if (Helper.IsWindowMaximized(hWnd))
					winType = WindowType.Maximized;

				if ((int)winType == lastWindowedMode.Value) return;
				lastWindowedMode.Value = (int)winType;
			}

			Plugin.Logger.LogDebug($"[Symphony::WindowedResize] Screen mode change detected, into {winType.ToString()}");

			if (winType == WindowType.FullScreen) {
				Plugin.Logger.LogDebug($"[Symphony::WindowedResize] Remove resizable window styles");
				Helper.ResizableWindow(hWnd, false);
			}
			else {
				Plugin.Logger.LogDebug($"[Symphony::WindowedResize] Add resizable window styles");
				Helper.ResizableWindow(hWnd, true);

				if (!init && winType == WindowType.Window) // Save position & size when windowed only
					ApplyLastWindowSize();
			}
		}


		private FrameLimit MeasureWindowSizeLimit = new(0.2f); 
		private void Measure_WindowSize() {
			if (!Use_Feature.Value) return;
			if (!MeasureWindowSizeLimit.Valid()) return;
			if (lastWindowedMode.Value != (int)WindowType.Window) return;

			var hWnd = Plugin.hWnd;
			if (hWnd == IntPtr.Zero) {
				Plugin.Logger.LogError("[Symphony::WindowedResize] Failed to get Game Window Handle");
				return;
			}

			if (Helper.GetWindowRect(hWnd, out var rc)) { // Save last window size 
				if (
					lastWindowSize_left.Value != rc.left ||
					lastWindowSize_top.Value != rc.top ||
					lastWindowSize_right.Value != rc.right ||
					lastWindowSize_bottom.Value != rc.bottom
				) {
					Plugin.Logger.LogInfo($"[Symphony::WindowedResize] Window position change detected, save it");

					lastWindowSize_left.Value = rc.left;
					lastWindowSize_top.Value = rc.top;
					lastWindowSize_right.Value = rc.right;
					lastWindowSize_bottom.Value = rc.bottom;
				}
			}
		}

		private void Update_FullScreenKey() {
			if (Helper.KeyCodeParse(Key_Mode.Value, out var kc)) {
				Plugin.Logger.LogInfo($"[Symphony::WindowedResize] > Key for Fullscreen toggle is '{Key_Mode.Value}', KeyCode is {kc}");
				FullScreenKey = kc;
			}
		}

		private static bool IsFullScreenKeyDowned() => Use_FullScreenKey.Value
			? Input.GetKeyDown(FullScreenKey)
			: Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter); // Game default value
	}
}
