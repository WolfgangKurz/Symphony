using BepInEx.Logging;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using UnityEngine;

namespace Symphony {
	internal static partial class Helper {
		#region STRUCTURES
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
		#endregion

		public static bool KeyCodeParse(string name, out KeyCode keyCode) => Enum.TryParse<KeyCode>(name, out keyCode);
		public static bool IsReservedKey(KeyCode k) => new KeyCode[] {
			KeyCode.Escape, KeyCode.F1
		}.Contains(k);

		public static LogLevel ToLogLevel(this LogType t) {
			switch(t) {
				case LogType.Error:
				case LogType.Assert: return LogLevel.Error;
				case LogType.Warning: return LogLevel.Warning;
				case LogType.Log: return LogLevel.Message;
				case LogType.Exception: return LogLevel.Error;
			}
			return LogLevel.None;
		}

		public static bool IsLesserVersion(string v1, string v2) {
			try {
				var p1 = v1.Split(".").Select(int.Parse).ToArray();
				var p2 = v2.Split(".").Select(int.Parse).ToArray();

				var len = Math.Min(p1.Length, p2.Length);
				for(var i=0; i<len; i++) {
					if (p1[i] < p2[i])
						return true;
					else if (p1[i] > p2[i])
						return false;
				}

				if (p1.Length < p2.Length) return true;
			} catch { }
			return false;
		}
		public static bool IsLesserOrEqualVersion(string v1, string v2) => v1 == v2 || IsLesserVersion(v1, v2);
		public static Assembly RegisterAssemblyFromResource(Assembly owner, string resourceName) {
			using var stream = owner.GetManifestResourceStream(resourceName);
			if (stream == null)
				throw new FileNotFoundException($"Embedded dependency resource '{resourceName}' not found");

			var loadedName = Path.GetFileNameWithoutExtension(resourceName.Replace('/', Path.DirectorySeparatorChar));
			var alreadyLoaded = AppDomain.CurrentDomain.GetAssemblies()
				.FirstOrDefault(x => string.Equals(x.GetName().Name, loadedName, StringComparison.Ordinal));
			if (alreadyLoaded != null) return alreadyLoaded;

			using var memory = new MemoryStream();
			stream.CopyTo(memory);
			return Assembly.Load(memory.ToArray());
		}

		#region Windows
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

			private const long PlacementCacheTick = TimeSpan.TicksPerMillisecond * 100;
			private static long lastPlacementTick = 0L;
			private static WINDOWPLACEMENT placement = new WINDOWPLACEMENT() { length = Marshal.SizeOf<WINDOWPLACEMENT>() };

			public static bool IsWindowMaximized(IntPtr hWnd) {
				if (hWnd == IntPtr.Zero) return false;

				var curTick = DateTime.Now.Ticks;
				if (curTick - lastPlacementTick > PlacementCacheTick) {
					lastPlacementTick = curTick;
					if (!GetWindowPlacement(hWnd, ref placement)) return false;
				}

				return placement.showCmd == SW_SHOWMAXIMIZED;
			}
		}
		public static bool IsWindowMaximized(IntPtr hWnd) => WindowMaximizedChecker.IsWindowMaximized(hWnd);

		private static class WindowDisplayHelper {
			private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

			[DllImport("user32.dll")]
			private static extern IntPtr GetActiveWindow();

			[DllImport("user32.dll")]
			private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

			[DllImport("user32.dll")]
			private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

			[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", CharSet = CharSet.Unicode, SetLastError = true)]
			private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

			[DllImport("user32.dll", EntryPoint = "SetWindowLongW", CharSet = CharSet.Unicode, SetLastError = true)]
			private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

			[DllImport("user32.dll", EntryPoint = "CallWindowProcW", CharSet = CharSet.Unicode)]
			private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

			[DllImport("user32.dll", EntryPoint = "DefWindowProcW", CharSet = CharSet.Unicode)]
			private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

			[DllImport("user32.dll", EntryPoint = "GetWindowRect")]
			private static extern int GetWindowRect_API(IntPtr hWnd, out Helper.RECT rc);

			[DllImport("user32.dll")]
			private static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

			[DllImport("user32.dll")]
			private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

			private const int GWL_STYLE = -16;
			private const int GWLP_WNDPROC = -4;
			private const int WS_MAXIMIZEBOX = 0x00010000;
			private const int WS_THICKFRAME = 0x00040000;
			private const int SW_MAXIMIZE = 3;
			private const uint WM_SETCURSOR = 0x0020;

			private const int HTLEFT = 10;
			private const int HTRIGHT = 11;
			private const int HTTOP = 12;
			private const int HTTOPLEFT = 13;
			private const int HTTOPRIGHT = 14;
			private const int HTBOTTOM = 15;
			private const int HTBOTTOMLEFT = 16;
			private const int HTBOTTOMRIGHT = 17;

			private const int _WS_RESIZABLE = WS_THICKFRAME | WS_MAXIMIZEBOX;
			private static IntPtr originalWndProc = IntPtr.Zero;

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

			private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong) {
				if (IntPtr.Size == 8)
					return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
				return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
			}

			private static int LOWORD(IntPtr value) => unchecked((ushort)value.ToInt64());

			private static CursorType? GetResizeCursorType(int hitTest) {
				switch (hitTest) {
					case HTLEFT:
					case HTRIGHT:
						return CursorType.SizeWE;
					case HTTOP:
					case HTBOTTOM:
						return CursorType.SizeNS;
					case HTTOPLEFT:
					case HTBOTTOMRIGHT:
						return CursorType.SizeNWSE;
					case HTTOPRIGHT:
					case HTBOTTOMLEFT:
						return CursorType.SIZENESW;
				}
				return null;
			}

			private static IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) {
				if (msg == WM_SETCURSOR &&
					// Screen.fullScreenMode == FullScreenMode.Windowed &&
					!IsWindowMaximized(hWnd)
				) {
					var hitTest = LOWORD(lParam);
					var cursorType = GetResizeCursorType(hitTest);
					if (cursorType.HasValue) {
						Helper.ChangeCursor(cursorType.Value);
						return new IntPtr(1);
					}
				}

				if (originalWndProc != IntPtr.Zero)
					return CallWindowProc(originalWndProc, hWnd, msg, wParam, lParam);
				return DefWindowProc(hWnd, msg, wParam, lParam);
			}

			public static bool InstallCursorIgnore(IntPtr hWnd) {
				if (hWnd == IntPtr.Zero) return false;
				if (originalWndProc != IntPtr.Zero) return true;

				var wndProc = Marshal.GetFunctionPointerForDelegate(CustomWndProc);
				var prevWndProc = SetWindowLongPtr(hWnd, GWLP_WNDPROC, wndProc);
				if (prevWndProc == IntPtr.Zero)
					return false;

				originalWndProc = prevWndProc;
				return true;
			}
		}
		public static void MaximizeWindow(IntPtr hWnd) => WindowDisplayHelper.MaximizeWindow(hWnd);
		public static void ResizeWindow(IntPtr hWnd, RECT rc) => WindowDisplayHelper.ResizeWindow(hWnd, rc);
		public static void ResizableWindow(IntPtr hWnd, bool resizable) => WindowDisplayHelper.ResizableWindow(hWnd, resizable);
		public static bool GetWindowRect(IntPtr hWnd, out RECT rc) => WindowDisplayHelper.GetWindowRect(hWnd, out rc);
		public static bool InstallCursorIgnore(IntPtr hWnd) => WindowDisplayHelper.InstallCursorIgnore(hWnd);

		[DllImport("user32.dll", EntryPoint = "SetWindowTextW", CharSet = CharSet.Unicode)]
		private static extern bool SetWindowText(IntPtr hWnd, string lpString);
		public static void SetWindowTitle(IntPtr hWnd, string title) => SetWindowText(hWnd, title);

		public enum CursorType : ushort {
			Arrow = 32512,
			IBeam = 32513,
			Wait = 32514,
			Cross = 32515,
			UpArrow = 32516,

			SizeNWSE = 32642,
			SIZENESW = 32643,
			SizeWE = 32644,
			SizeNS = 32645,
			SizeAll = 32646,

			No = 32648,
			Hand = 32649,
			AppStarting = 32650,
			Help = 32651,
			Pin = 32671,
			Person = 32672,

			_Pen = 32631,
			_ArrowNS = 32652,
			_ArrowWE = 32653,
			_ArrowAll = 32654,
			_ArrowN = 32655,
			_ArrowS = 32656,
			_ArrowW = 32657,
			_ArrowE = 32658,
			_ArrowNW = 32659,
			_ArrowNE = 32660,
			_ArrowSW = 32661,
			_ArrowSE = 32662,
			_CD = 32663,
		}

		private static class CursorHelper {
			[DllImport("user32.dll")]
			private static extern IntPtr LoadImage(IntPtr hInst, IntPtr name, uint type, int cx, int cy, uint fuLoad);
			[DllImport("user32.dll")]
			private static extern IntPtr SetCursor(IntPtr hCursor);

			private const uint IMAGE_CURSOR = 2;
			private const uint LR_SHARED = 0x8000;

			private static Dictionary<CursorType, IntPtr> CursorCache = new();

			public static void ChangeCursor(CursorType type) {
				IntPtr cursor;
				if (!CursorCache.TryGetValue(type, out cursor))
					cursor = LoadImage(IntPtr.Zero, new IntPtr((ushort)type), IMAGE_CURSOR, 0, 0, LR_SHARED);

				if (cursor != IntPtr.Zero)
					SetCursor(cursor);
			}
		}
		public static void ChangeCursor(CursorType type) => CursorHelper.ChangeCursor(type);
		#endregion

		#region Rect
		public static Rect Shrink(this Rect rc, float left, float top, float right, float bottom) => Rect.MinMaxRect(
			rc.xMin + left,
			rc.yMin + top,
			rc.xMax - right,
			rc.yMax - bottom
		);
		public static Rect Shrink(this Rect rc, float horizontal, float vertical) => rc.Shrink(horizontal, vertical, horizontal, vertical);
		public static Rect Shrink(this Rect rc, float amount) => rc.Shrink(amount, amount, amount, amount);

		public static Rect Expand(this Rect rc, float left, float top, float right, float bottom) => rc.Shrink(-left,-top,-right,-bottom);
		public static Rect Expand(this Rect rc, float horizontal, float vertical) => rc.Expand(horizontal, vertical, horizontal, vertical);
		public static Rect Expand(this Rect rc, float amount) => rc.Expand(amount, amount, amount, amount);

		public static Rect Resize(this Rect rc, float width, float height) => Rect.MinMaxRect(rc.xMin, rc.yMin, rc.xMin + width, rc.yMin + height);
		public static Rect Width(this Rect rc, float width) => Rect.MinMaxRect(rc.xMin, rc.yMin, rc.xMin + width, rc.yMax);
		public static Rect Height(this Rect rc, float height) => Rect.MinMaxRect(rc.xMin, rc.yMin, rc.xMax, rc.yMin + height);

		public static Rect Move(this Rect rc, float x, float y) => Rect.MinMaxRect(rc.xMin + x, rc.yMin + y, rc.xMax + x, rc.yMax + y);
		public static Rect ZeroOffset(this Rect rc) => rc.Move(-rc.xMin, -rc.yMin);
		public static Rect LockInScreen(this Rect rc) {
			var ret = rc;

			if (ret.xMax > Screen.width) {
				var d = ret.xMax - Screen.width;
				ret.xMin -= d;
				ret.width -= d;
			}
			if (ret.yMax > Screen.height) {
				var d = ret.yMax - Screen.height;
				ret.yMin -= d;
				ret.height -= d;
			}
			if (ret.xMin < 0) {
				var d = -ret.xMin;
				ret.xMin = 0;
				ret.width += d;
			}
			if (ret.yMin < 0) {
				var d = -ret.yMin;
				ret.yMin = 0;
				ret.height += d;
			}
			return ret;
		}
		public static Rect Clip(this Rect rc, Rect to) => Rect.MinMaxRect(
			Mathf.Clamp(rc.xMin, to.xMin, to.xMax),
			Mathf.Clamp(rc.yMin, to.yMin, to.yMax),
			Mathf.Clamp(rc.xMax, to.xMin, to.xMax),
			Mathf.Clamp(rc.yMax, to.yMin, to.xMax)
		);
		#endregion

		#region Reflection
		public static T GetValue<T>(this FieldInfo fi, object obj) => (T)fi.GetValue(obj);

		private static T XGetFieldValue<T>(Type type, object obj, string name) {
			var f = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (f == null) {
				if (type.BaseType == null) return default;
				return XGetFieldValue<T>(type.BaseType, obj, name);
			}
			return f.GetValue<T>(obj);
		}
		public static T XGetFieldValue<T>(this object obj, string name) => XGetFieldValue<T>(obj.GetType(), obj, name);

		private static void XSetFieldValue<T>(Type type, object obj, string name, T value) {
			var f = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (f == null) {
				if (type.BaseType == null) return;
				XSetFieldValue<T>(type.BaseType, obj, name, value);
			}
			f.SetValue(obj, value);
		}
		public static void XSetFieldValue<T>(this object obj, string name, T value) => XSetFieldValue<T>(obj.GetType(), obj, name, value);

		private static void XSetPropertyValue<T>(Type type, object obj, string name, T value) {
			var f = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (f == null) {
				if (type.BaseType == null) return;
				XSetPropertyValue<T>(type.BaseType, obj, name, value);
			}
			f.SetValue(obj, value);
		}
		public static void XSetPropertyValue<T>(this object obj, string name, T value) => XSetPropertyValue<T>(obj.GetType(), obj, name, value);

		public static Action XGetMethodVoid(Type type, object obj, string name) {
			var mi = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (mi == null) {
				if (type.BaseType == null) return null;
				return XGetMethodVoid(type.BaseType, obj, name);
			}
			return () => mi.Invoke(obj, []);
		}
		public static Action XGetMethodVoid(this object obj, string name) => XGetMethodVoid(obj.GetType(), obj, name);
		#endregion

		#region Linq
		public static bool Any<TSource>(this TSource[] source, Func<TSource, int, bool> predicate) {
			for (int i = 0; i < source.Length; i++)
				if (predicate(source[i], i))
					return true;
			return false;
		}
		#endregion

		#region LastOrigin
		public static bool IsPartsActive(this ActorSpinePartsView aspv) {
			var ssp = aspv.XGetFieldValue<ActorSpineSkinController>("spineSkinController");
			return ssp.XGetFieldValue<bool>("isActiveParts");
		}
		public static bool IsPartsActive(this ActorPartsView apv) {
			var _isSwapParts = apv.XGetFieldValue<bool>("_isSwapParts");

			if (!_isSwapParts) {
				var _listParts = apv.XGetFieldValue<List<GameObject>>("_listParts");
				return _listParts.Any(x => x.activeSelf);
			}

			var _listSwapActiveObject = apv.XGetFieldValue<List<GameObject>>("_listSwapActiveObject");
			return _listSwapActiveObject.Any(x => x.activeSelf);
		}
		#endregion
	}

	public class EnumX {
		public static T[] GetValues<T>() where T : Enum {

			return (T[])Enum.GetValues(typeof(T));
		}
	}
}
