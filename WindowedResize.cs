using BepInEx;
using BepInEx.Configuration;

using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.XR;

namespace Symphony {
	internal class WindowedResize : MonoBehaviour {
		[DllImport("user32.dll")]
		private static extern IntPtr GetActiveWindow();

		[DllImport("user32.dll")]
		private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll")]
		private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

		[DllImport("user32.dll")]
		private static extern int GetWindowRect(IntPtr hWnd, out Helper.RECT rc);

		[DllImport("user32.dll")]
		private static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

		[DllImport("user32.dll")]
		private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

		private enum WindowType : int {
			Window = 0,
			Maximized = 1,
			FullScreen = 2,
		}

		private const int GWL_STYLE = -16;
		private const int WS_MAXIMIZEBOX = 0x00010000;
		private const int WS_THICKFRAME = 0x00040000;
		private const int SW_SHOWMINIMIZED = 2;
		private const int SW_MAXIMIZE = 3;
		private const int SW_RESTORE = 9;

		private ConfigEntry<int> lastWindowSize_left;
		private ConfigEntry<int> lastWindowSize_top;
		private ConfigEntry<int> lastWindowSize_right;
		private ConfigEntry<int> lastWindowSize_bottom;

		private ConfigEntry<int> lastWindowedMode;

		private bool ready = false;

		public void Awake() {
			var config = new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.WindowedResize.cfg"), true);

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
					ShowWindow(hWnd, SW_MAXIMIZE); // Maximize window
			}

			this.ready = true;
		}

		public void Update() {
			if (!this.ready) return;
			this.Patch_WindowedResize();
			this.Measure_WindowSize();
		}

		private void ApplyLastWindowSize() {
			if (Screen.fullScreen) return;

			var hwnd = Plugin.hWnd;
			if (hwnd == IntPtr.Zero) {
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
			SetWindowPos(hwnd, IntPtr.Zero, rc.left, rc.top, rc.right - rc.left, rc.bottom - rc.top, 0x14); // SWP_NOZORDER | SWP_NOACTIVE
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

			int style = GetWindowLong(hWnd, GWL_STYLE);
			if (winType == WindowType.FullScreen) {
				Plugin.Logger.LogDebug($"[Symphony::WindowedResize] Remove WS_THICKFRAME, WS_MAXIMIZEBOX");
				SetWindowLong(hWnd, GWL_STYLE, style & ~WS_THICKFRAME & ~WS_MAXIMIZEBOX);
			}
			else {
				Plugin.Logger.LogDebug($"[Symphony::WindowedResize] Add WS_THICKFRAME, WS_MAXIMIZEBOX");
				SetWindowLong(hWnd, GWL_STYLE, style | WS_THICKFRAME | WS_MAXIMIZEBOX);

				if (!init && winType == WindowType.Window) // Save position & size when windowed only
					this.ApplyLastWindowSize();
			}
		}

		private float lastTime = 0f;
		private void Measure_WindowSize() {
			var cur = Time.realtimeSinceStartup;
			if (cur - this.lastTime < 0.2f) return;
			this.lastTime = cur;

			if (this.lastWindowedMode.Value != (int)WindowType.Window) return;

			var hwnd = Plugin.hWnd;
			if (hwnd == IntPtr.Zero) {
				Plugin.Logger.LogError("[Symphony::WindowedResize] Failed to get Game Window Handle");
				return;
			}

			if (GetWindowRect(hwnd, out var rc) != 0) { // Save last window size 
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
	}
}
