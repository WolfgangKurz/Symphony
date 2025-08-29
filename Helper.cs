using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

using UnityEngine;
using UnityEngine.XR;

namespace Symphony {
	internal class Helper {
		[Serializable]
		[StructLayout(LayoutKind.Sequential)]
		public struct RECT {
			public int left;
			public int top;
			public int right;
			public int bottom;

			public RECT() : this(0, 0, 0, 0) { }
			public RECT(int left, int top, int right, int bottom) {
				this.left = left;
				this.top = top;
				this.right = right;
				this.bottom = bottom;
			}

			public static bool operator ==(RECT r1, RECT r2) {
				return r1.left == r2.left && r1.top == r2.top && r1.right == r2.right && r1.bottom == r2.bottom;
			}
			public static bool operator !=(RECT r1, RECT r2) {
				return r1.left != r2.left || r1.top != r2.top || r1.right != r2.right || r1.bottom != r2.bottom;
			}

			public override bool Equals(object obj) => obj.GetType().IsInstanceOfType(typeof(RECT)) && this == (RECT)obj;
			public override int GetHashCode() => base.GetHashCode();
		}

		[Serializable]
		[StructLayout(LayoutKind.Sequential)]
		private struct POINT {
			public int X;
			public int Y;
		}

		private static Dictionary<string, string> KeyCodeAlias = new() {
			{ "1", "Alpha1" },
			{ "2", "Alpha2" },
			{ "3", "Alpha3" },
			{ "4", "Alpha4" },
			{ "5", "Alpha5" },
			{ "6", "Alpha6" },
			{ "7", "Alpha7" },
			{ "8", "Alpha8" },
			{ "9", "Alpha9" },
			{ "0", "Alpha0" },
		};
		public static bool KeyCodeParse(string name, out KeyCode keyCode) {
			if(KeyCodeAlias.ContainsKey(name)) 
				name = KeyCodeAlias[name];

			return Enum.TryParse<KeyCode>(name, out keyCode);
		}

		private class WindowHandleFinder {
			private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

			[DllImport("user32.dll")]
			[return: MarshalAs(UnmanagedType.Bool)]
			private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

			[DllImport("user32.dll", SetLastError = true)]
			private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

			[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
			private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

			private static IntPtr foundHandle = IntPtr.Zero;
			private static uint ourProcessId = 0;
			private const string UNITY_WND_CLASS = "UnityWndClass";

			public static IntPtr GetMainWindowHandle() {
				if (foundHandle != IntPtr.Zero) return foundHandle;

				ourProcessId = (uint)Process.GetCurrentProcess().Id;
				EnumWindows(EnumWindowsCallback, IntPtr.Zero);
				return foundHandle;
			}

			private static bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam) {
				GetWindowThreadProcessId(hWnd, out uint windowProcessId);
				if (windowProcessId != ourProcessId) return true;

				var classNameBuilder = new StringBuilder(256);
				GetClassName(hWnd, classNameBuilder, classNameBuilder.Capacity);

				if (classNameBuilder.ToString() == UNITY_WND_CLASS) {
					foundHandle = hWnd;
					return false;
				}
				return true;
			}
		}
		public static IntPtr GetMainWindowHandle() => WindowHandleFinder.GetMainWindowHandle();

		private class WindowMaximizedChecker {
			[DllImport("user32.dll", SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

			[Serializable]
			[StructLayout(LayoutKind.Sequential)]
			private struct WINDOWPLACEMENT {
				public int length;
				public int flags;
				public int showCmd;
				public POINT minPosition;
				public POINT maxPosition;
				public RECT normalPosition;
			}

			private const int SW_SHOWNORMAL = 1;
			private const int SW_SHOWMINIMIZED = 2;
			private const int SW_SHOWMAXIMIZED = 3;
			public static bool IsWindowMaximized(IntPtr hWnd) {
				if (hWnd == IntPtr.Zero) return false;

				WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
				placement.length = Marshal.SizeOf(placement);

				if (GetWindowPlacement(hWnd, ref placement))
					return placement.showCmd == SW_SHOWMAXIMIZED;
				return false;
			}
		}
		public static bool IsWindowMaximized(IntPtr hWnd) => WindowMaximizedChecker.IsWindowMaximized(hWnd);

		private static class WindowDisplayHelper {

			[DllImport("user32.dll")]
			private static extern IntPtr GetActiveWindow();

			[DllImport("user32.dll")]
			private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

			[DllImport("user32.dll")]
			private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

			[DllImport("user32.dll", EntryPoint = "GetWindowRect")]
			private static extern int GetWindowRect_API(IntPtr hWnd, out Helper.RECT rc);

			[DllImport("user32.dll")]
			private static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

			[DllImport("user32.dll")]
			private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

			private const int GWL_STYLE = -16;
			private const int WS_MAXIMIZEBOX = 0x00010000;
			private const int WS_THICKFRAME = 0x00040000;
			private const int SW_MAXIMIZE = 3;

			private const int _WS_RESIZABLE = WS_THICKFRAME | WS_MAXIMIZEBOX;

			public static void MaximizeWindow(IntPtr hWnd) => ShowWindow(hWnd, SW_MAXIMIZE);
			public static void ResizeWindow(IntPtr hWnd, RECT rc)
				=> SetWindowPos(hWnd, IntPtr.Zero, rc.left, rc.top, rc.right - rc.left, rc.bottom - rc.top, 0x14 /* SWP_NOZORDER | SWP_NOACTIVE */);
			public static void ResizableWindow(IntPtr hWnd, bool resizable) {
				int style = GetWindowLong(hWnd, GWL_STYLE);
				SetWindowLong(
					hWnd,
					GWL_STYLE,
					resizable
						? style | _WS_RESIZABLE
						: style & ~_WS_RESIZABLE
				);
			}
			public static bool GetWindowRect(IntPtr hWnd, out RECT rc) => GetWindowRect_API(hWnd, out rc) != 0;
		}
		public static void MaximizeWindow(IntPtr hWnd) => WindowDisplayHelper.MaximizeWindow(hWnd);
		public static void ResizeWindow(IntPtr hWnd, RECT rc) => WindowDisplayHelper.ResizeWindow(hWnd, rc);
		public static void ResizableWindow(IntPtr hWnd, bool resizable) => WindowDisplayHelper.ResizableWindow(hWnd, resizable);
		public static bool GetWindowRect(IntPtr hWnd, out RECT rc) => WindowDisplayHelper.GetWindowRect(hWnd, out rc);
	}
}
