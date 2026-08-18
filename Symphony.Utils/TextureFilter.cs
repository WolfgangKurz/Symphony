using System;
using System.Runtime.InteropServices;

namespace Symphony.Utils {
	public class TextureFilter {
		[DllImport("Symphony.Native", EntryPoint = "bc3", CallingConvention = CallingConvention.Cdecl)]
		public static extern unsafe int Bc3(
			void* source,
			UIntPtr sourceSize,
			int width,
			int height,
			void* output,
			UIntPtr outputSize,
			byte generateMipMaps,
			int threadCount
		);


		[DllImport("Symphony.Native", EntryPoint = "lanczos2", CallingConvention = CallingConvention.Cdecl)]
		public static extern unsafe int Lanczos2(
			void* source,
			UIntPtr sourceSize,
			int srcWidth,
			int srcHeight,
			void* output,
			UIntPtr outputSize,
			int outWidth,
			int outHeight,
			byte pma,
			float filterTightness
		);


		[DllImport("Symphony.Native", EntryPoint = "sharpen", CallingConvention = CallingConvention.Cdecl)]
		public static extern unsafe int Sharpen(
			void* buffer,
			int width,
			int height,
			float amount
		);
	}
}
