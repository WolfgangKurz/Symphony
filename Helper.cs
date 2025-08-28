using System.Runtime.InteropServices;

namespace Symphony {
	internal class Helper {
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
	}
}
