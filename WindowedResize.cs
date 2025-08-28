using BepInEx;
using BepInEx.Configuration;

using System;
using System.IO;
using System.Runtime.InteropServices;

using UnityEngine;

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

		private const int GWL_STYLE = -16;
		private const int WS_THICKFRAME = 0x00040000;

		private ConfigEntry<int> lastWindowSize_left;
		private ConfigEntry<int> lastWindowSize_top;
		private ConfigEntry<int> lastWindowSize_right;
		private ConfigEntry<int> lastWindowSize_bottom;

		private ConfigEntry<bool> lastWindowedMode;

		private bool ready = false;

		public void Awake() {
			var config = new ConfigFile(Path.Combine(Paths.ConfigPath, "Symphony.WindowedResize.cfg"), false);

			this.lastWindowSize_left = config.Bind("WindowedResize", "LastWindowSize_Left", 0, "Last left position of window on windowed mode");
			this.lastWindowSize_top = config.Bind("WindowedResize", "LastWindowSize_Top", 0, "Last top position of window on windowed mode");
			this.lastWindowSize_right = config.Bind("WindowedResize", "LastWindowSize_Right", 1280, "Last right position of window on windowed mode");
			this.lastWindowSize_bottom = config.Bind("WindowedResize", "LastWindowSize_Bottom", 720, "Last bottom position of window on windowed mode");

			this.lastWindowedMode = config.Bind("WindowedResize", "LastWindowed", false, "Last window type flag");

			if (this.lastWindowedMode.Value) {
				Screen.fullScreen = false;
				this.Patch_WindowedResize();
				this.ApplyLastWindowSize();
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
				Plugin.Logger.LogError("Failed to get Game Window Handle");
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
			Plugin.Logger.LogInfo($"[Symphony::WindowedResize]  > {rc.left}, {rc.top}, {rc.right - rc.left}, {rc.bottom - rc.top}");
		}

		private bool lastFullScreen = true;
		private void Patch_WindowedResize() {
			var fs = Screen.fullScreen;
			if (fs == this.lastFullScreen) return;

			this.lastFullScreen = fs;
			this.lastWindowedMode.Value = !fs;

			var hwnd = Plugin.hWnd;
			if (hwnd == IntPtr.Zero) {
				Plugin.Logger.LogError("Failed to get Game Window Handle");
				return;
			}

			Plugin.Logger.LogInfo($"[Symphony::WindowedResize] Screen mode change detected, into {(fs ? "Fullscreen" : "Windowed")}");

			int style = GetWindowLong(hwnd, GWL_STYLE);
			if (fs) {
				Plugin.Logger.LogInfo($"[Symphony::WindowedResize] Remove WS_THICKFRAME");
				SetWindowLong(hwnd, GWL_STYLE, style & ~WS_THICKFRAME);
			}
			else {
				Plugin.Logger.LogInfo($"[Symphony::WindowedResize] Add WS_THICKFRAME");
				SetWindowLong(hwnd, GWL_STYLE, style | WS_THICKFRAME);

				this.ApplyLastWindowSize();
			}
		}

		private float lastTime = 0f;
		private void Measure_WindowSize() {
			var cur = Time.realtimeSinceStartup;
			if (cur - this.lastTime < 0.2f) return;
			this.lastTime = cur;

			if (Screen.fullScreen) return;

			var hwnd = Plugin.hWnd;
			if (hwnd == IntPtr.Zero) {
				Plugin.Logger.LogError("Failed to get Game Window Handle");
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
