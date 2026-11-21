using System;
using System.Runtime.InteropServices;

namespace Symphony.Utils {
	public class TextureFilter {
		[DllImport("Symphony.Native", EntryPoint = "bc7", CallingConvention = CallingConvention.Cdecl)]
		public static extern unsafe int BC7(
			void* source,
			UIntPtr sourceSize,
			int width,
			int height,
			void* output,
			UIntPtr outputSize,
			byte generateMipMaps,
			int threadCount
		);
	}
}
