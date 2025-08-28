using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

using UnityEngine;

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

		private static class WindowHandleFinder {
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

		private static class WindowMaximizedChecker {
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
	}
}
