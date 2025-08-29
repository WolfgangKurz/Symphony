using BepInEx;
using BepInEx.Configuration;

using HarmonyLib;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.XR;

namespace Symphony {
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
					AccessTools.Method(typeof(WindowedResize), nameof(WindowedResize.IsFullScreenKeyDowned))
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

		private ConfigEntry<int> lastWindowSize_left;
		private ConfigEntry<int> lastWindowSize_top;
		private ConfigEntry<int> lastWindowSize_right;
		private ConfigEntry<int> lastWindowSize_bottom;

		private ConfigEntry<int> lastWindowedMode;

		private ConfigEntry<string> Key_Mode;

		private bool ready = false;

		private static ConfigFile config = new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.WindowedResize.cfg"), true);
		private static KeyCode FullScreenKey = KeyCode.F11;

		public void Awake() {
			var harmony = new Harmony("Symphony.WindowedResize");
			harmony.Patch(
				AccessTools.Method(typeof(GameManager), "Update"),
				transpiler: new HarmonyMethod(typeof(WindowedResize_Patch), nameof(WindowedResize_Patch.Patch_Update))
			);

			this.Key_Mode = config.Bind("WindowedResize", "Key_Mode", "F11", "Window mode change button replacement. Clear will not regsiter hotkey");

			this.lastWindowSize_left = config.Bind("WindowedResize", "LastWindowSize_Left", 0, "Last left position of window on windowed mode");
			this.lastWindowSize_top = config.Bind("WindowedResize", "LastWindowSize_Top", 0, "Last top position of window on windowed mode");
			this.lastWindowSize_right = config.Bind("WindowedResize", "LastWindowSize_Right", 1280, "Last right position of window on windowed mode");
			this.lastWindowSize_bottom = config.Bind("WindowedResize", "LastWindowSize_Bottom", 720, "Last bottom position of window on windowed mode");

			if (this.lastWindowSize_right.Value <= 30 || this.lastWindowSize_bottom.Value <= 40) {
				this.lastWindowSize_left.Value = 0;
				this.lastWindowSize_top.Value = 0;
				this.lastWindowSize_right.Value = 1280;
				this.lastWindowSize_bottom.Value = 720;
			}

			this.lastWindowedMode = config.Bind("WindowedResize", "LastWindowed", 0, "Last window type flag. 0 is Windowed, 1 is Maximized, 2 is FullScreen");

			StartCoroutine(this.LazyStart());
		}

		private IEnumerator LazyStart() {
			yield return new WaitForEndOfFrame();

			var hWnd = Plugin.hWnd;
			if (this.lastWindowedMode.Value != (int)WindowType.FullScreen) {
				Screen.fullScreen = false;

				this.Patch_WindowedResize(true); // Apply window style
				this.ApplyLastWindowSize();      // Move & Resize window for windowed mode

				yield return new WaitForEndOfFrame();

				if (this.lastWindowedMode.Value == (int)WindowType.Maximized)
					Helper.MaximizeWindow(hWnd); // Maximize window
			}

			this.ready = true;
		}

		public void Update() {
			if (!this.ready) return;
			this.Patch_WindowedResize();
			this.Measure_WindowSize();
			this.Update_FullScreenKey();
		}

		private void ApplyLastWindowSize() {
			if (Screen.fullScreen) return;

			var hWnd = Plugin.hWnd;
			if (hWnd == IntPtr.Zero) {
				Plugin.Logger.LogError("[Symphony::WindowedResize] Failed to get Game Window Handle");
				return;
			}

			Plugin.Logger.LogInfo($"[Symphony::WindowedResize] Applying latest windowed position");

			var rc = new Helper.RECT(
				this.lastWindowSize_left.Value,
				this.lastWindowSize_top.Value,
				this.lastWindowSize_right.Value,
				this.lastWindowSize_bottom.Value
			);
			Helper.ResizeWindow(hWnd, rc);
			Plugin.Logger.LogDebug($"[Symphony::WindowedResize]  > {rc.left}, {rc.top}, {rc.right - rc.left}, {rc.bottom - rc.top}");
		}

		private void Patch_WindowedResize(bool init = false) {
			var hWnd = Plugin.hWnd;
			if (hWnd == IntPtr.Zero) {
				Plugin.Logger.LogError("[Symphony::WindowedResize] Failed to get Game Window Handle");
				return;
			}

			var winType = WindowType.Window;
			if (init) {
				winType = (WindowType)this.lastWindowedMode.Value;
			}
			else {
				if (Screen.fullScreen)
					winType = WindowType.FullScreen;
				else if (Helper.IsWindowMaximized(hWnd))
					winType = WindowType.Maximized;

				if ((int)winType == this.lastWindowedMode.Value) return;
				this.lastWindowedMode.Value = (int)winType;
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
					this.ApplyLastWindowSize();
			}
		}

		private float lastTime_Measure_WindowSize = 0f;
		private void Measure_WindowSize() {
			var cur = Time.realtimeSinceStartup;
			if (cur - this.lastTime_Measure_WindowSize < 0.2f) return;
			this.lastTime_Measure_WindowSize = cur;

			if (this.lastWindowedMode.Value != (int)WindowType.Window) return;

			var hWnd = Plugin.hWnd;
			if (hWnd == IntPtr.Zero) {
				Plugin.Logger.LogError("[Symphony::WindowedResize] Failed to get Game Window Handle");
				return;
			}

			if (Helper.GetWindowRect(hWnd, out var rc)) { // Save last window size 
				if (
					this.lastWindowSize_left.Value != rc.left ||
					this.lastWindowSize_top.Value != rc.top ||
					this.lastWindowSize_right.Value != rc.right ||
					this.lastWindowSize_bottom.Value != rc.bottom
				) {
					Plugin.Logger.LogInfo($"[Symphony::WindowedResize] Window position change detected, save it");

					this.lastWindowSize_left.Value = rc.left;
					this.lastWindowSize_top.Value = rc.top;
					this.lastWindowSize_right.Value = rc.right;
					this.lastWindowSize_bottom.Value = rc.bottom;
				}
			}
		}

		private float lastTime_FullScreenKey = 0f;
		private void Update_FullScreenKey() {
			var cur = Time.realtimeSinceStartup;
			if (cur - this.lastTime_FullScreenKey < 5.0f) return;
			this.lastTime_FullScreenKey = cur;

			string prevKey = this.Key_Mode.Value;

			config.Reload();
			if (this.Key_Mode.Value != prevKey) {
				if (this.Key_Mode.Value != "") {
					if (Helper.KeyCodeParse(this.Key_Mode.Value, out var kc)) {
						Plugin.Logger.LogInfo($"[Symphony::WindowedResize] > Key for Fullscreen toggle is '{this.Key_Mode.Value}', KeyCode is {kc}");
						WindowedResize.FullScreenKey = kc;
					}
				}
				else
					Plugin.Logger.LogInfo($"[Symphony::WindowedResize] > Key for Fullscreen toggle is '{this.Key_Mode.Value}', KeyCode is not valid");
			}
		}
		private static bool IsFullScreenKeyDowned() => Input.GetKeyDown(WindowedResize.FullScreenKey);
	}
}
