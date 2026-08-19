using AssetRipper.TextureDecoder.Astc;
using AssetRipper.TextureDecoder.Atc;
using AssetRipper.TextureDecoder.Etc;
using AssetRipper.TextureDecoder.Pvrtc;

using System;

namespace Symphony.Utils {
	internal static class TextureTranscoder {
		internal const int BC7TextureFormat = 25;

		internal readonly struct TextureTranscodeRequest {
			public TextureTranscodeRequest(int width, int height, int textureFormat, int mipCount, byte[] data) {
				Width = width;
				Height = height;
				TextureFormat = textureFormat;
				MipCount = mipCount;
				Data = data;
			}

			public int Width { get; }
			public int Height { get; }
			public int TextureFormat { get; }
			public int MipCount { get; }
			public byte[] Data { get; }
		}

		internal readonly struct TextureTranscodeResult {
			public TextureTranscodeResult(byte[] data, int mipCount) {
				Data = data;
				MipCount = mipCount;
			}

			public byte[] Data { get; }
			public int MipCount { get; }
		}

		internal static bool NeedsTranscode(int textureFormat) {
			switch ((UnityTextureFormat)textureFormat) {
				case UnityTextureFormat.ATC_RGB4:
				case UnityTextureFormat.ATC_RGBA8:
				case UnityTextureFormat.PVRTC_RGB2:
				case UnityTextureFormat.PVRTC_RGBA2:
				case UnityTextureFormat.PVRTC_RGB4:
				case UnityTextureFormat.PVRTC_RGBA4:
				case UnityTextureFormat.ETC2_RGB4:
				case UnityTextureFormat.ETC2_RGBA1:
				case UnityTextureFormat.ETC2_RGBA8:
				case UnityTextureFormat.ASTC_RGB_4x4:
				case UnityTextureFormat.ASTC_RGB_5x5:
				case UnityTextureFormat.ASTC_RGB_6x6:
				case UnityTextureFormat.ASTC_RGB_8x8:
				case UnityTextureFormat.ASTC_RGB_10x10:
				case UnityTextureFormat.ASTC_RGB_12x12:
				case UnityTextureFormat.ASTC_RGBA_4x4:
				case UnityTextureFormat.ASTC_RGBA_5x5:
				case UnityTextureFormat.ASTC_RGBA_6x6:
				case UnityTextureFormat.ASTC_RGBA_8x8:
				case UnityTextureFormat.ASTC_RGBA_10x10:
				case UnityTextureFormat.ASTC_RGBA_12x12:
				case UnityTextureFormat.ASTC_HDR_4x4:
				case UnityTextureFormat.ASTC_HDR_5x5:
				case UnityTextureFormat.ASTC_HDR_6x6:
				case UnityTextureFormat.ASTC_HDR_8x8:
				case UnityTextureFormat.ASTC_HDR_10x10:
				case UnityTextureFormat.ASTC_HDR_12x12:
					return true;
				default:
					return false;
			}
		}

		internal static bool TryTranscode(TextureTranscodeRequest request, out TextureTranscodeResult result, out string error) {
			result = default;
			error = null;

			try {
				if (request.Width <= 0 || request.Height <= 0) {
					error = "Texture dimensions are invalid";
					return false;
				}

				if (request.Data == null || request.Data.Length == 0) {
					error = "Texture data is empty";
					return false;
				}

				var rgba = DecodeToRgba(request);
				if (rgba == null || rgba.Length == 0) {
					error = $"Texture format {request.TextureFormat} is not supported";
					return false;
				}

				var encoded = EncodeTexture(
					request.Width,
					request.Height,
					rgba,
					request.MipCount > 1,
					Math.Max(1, Environment.ProcessorCount / 2),
					out var mipCount
				);
				result = new TextureTranscodeResult(encoded, mipCount);
				return true;
			} catch (Exception ex) {
				error = ex.ToString();
				return false;
			}
		}

		private static byte[] DecodeToRgba(TextureTranscodeRequest request) {
			byte[] output;
			switch ((UnityTextureFormat)request.TextureFormat) {
				case UnityTextureFormat.ATC_RGB4:
					AtcDecoder.DecompressAtcRgb4(request.Data, request.Width, request.Height, out output);
					break;
				case UnityTextureFormat.ATC_RGBA8:
					AtcDecoder.DecompressAtcRgba8(request.Data, request.Width, request.Height, out output);
					break;
				case UnityTextureFormat.ETC2_RGB4:
					EtcDecoder.DecompressETC2(request.Data, request.Width, request.Height, out output);
					break;
				case UnityTextureFormat.ETC2_RGBA1:
					EtcDecoder.DecompressETC2A1(request.Data, request.Width, request.Height, out output);
					break;
				case UnityTextureFormat.ETC2_RGBA8:
					EtcDecoder.DecompressETC2A8(request.Data, request.Width, request.Height, out output);
					break;
				case UnityTextureFormat.ASTC_RGB_4x4:
				case UnityTextureFormat.ASTC_RGBA_4x4:
				case UnityTextureFormat.ASTC_HDR_4x4:
					AstcDecoder.DecodeASTC(request.Data, request.Width, request.Height, 4, 4, out output);
					break;
				case UnityTextureFormat.ASTC_RGB_5x5:
				case UnityTextureFormat.ASTC_RGBA_5x5:
				case UnityTextureFormat.ASTC_HDR_5x5:
					AstcDecoder.DecodeASTC(request.Data, request.Width, request.Height, 5, 5, out output);
					break;
				case UnityTextureFormat.ASTC_RGB_6x6:
				case UnityTextureFormat.ASTC_RGBA_6x6:
				case UnityTextureFormat.ASTC_HDR_6x6:
					AstcDecoder.DecodeASTC(request.Data, request.Width, request.Height, 6, 6, out output);
					break;
				case UnityTextureFormat.ASTC_RGB_8x8:
				case UnityTextureFormat.ASTC_RGBA_8x8:
				case UnityTextureFormat.ASTC_HDR_8x8:
					AstcDecoder.DecodeASTC(request.Data, request.Width, request.Height, 8, 8, out output);
					break;
				case UnityTextureFormat.ASTC_RGB_10x10:
				case UnityTextureFormat.ASTC_RGBA_10x10:
				case UnityTextureFormat.ASTC_HDR_10x10:
					AstcDecoder.DecodeASTC(request.Data, request.Width, request.Height, 10, 10, out output);
					break;
				case UnityTextureFormat.ASTC_RGB_12x12:
				case UnityTextureFormat.ASTC_RGBA_12x12:
				case UnityTextureFormat.ASTC_HDR_12x12:
					AstcDecoder.DecodeASTC(request.Data, request.Width, request.Height, 12, 12, out output);
					break;
				case UnityTextureFormat.PVRTC_RGB2:
				case UnityTextureFormat.PVRTC_RGBA2:
					PvrtcDecoder.DecompressPVRTC(request.Data, request.Width, request.Height, true, out output);
					break;
				case UnityTextureFormat.PVRTC_RGB4:
				case UnityTextureFormat.PVRTC_RGBA4:
					PvrtcDecoder.DecompressPVRTC(request.Data, request.Width, request.Height, false, out output);
					break;
				default:
					return null;
			}

			SwapRedBlue(output);
			return output;
		}

		private static unsafe byte[] EncodeTexture(
			int width,
			int height,
			byte[] rgbaBytes,
			bool generateMipMaps,
			int threadCount,
			out int mipCount
		) {
			var outputLength = GetBc7DataLength(width, height, generateMipMaps, out mipCount);
			var encoded = new byte[outputLength];

			fixed (byte* source = rgbaBytes)
			fixed (byte* output = encoded) {
				var error = TextureFilter.BC7(
					source,
					new UIntPtr((ulong)rgbaBytes.Length),
					width,
					height,
					output,
					new UIntPtr((ulong)encoded.Length),
					generateMipMaps ? (byte)1 : (byte)0,
					threadCount
				);
				if (error != 0)
					throw new InvalidOperationException($"Native BC7 encoding failed with error code {error}");
			}

			return encoded;
		}

		private static int GetBc7DataLength(int width, int height, bool generateMipMaps, out int mipCount) {
			var totalLength = 0L;
			var mipWidth = width;
			var mipHeight = height;
			mipCount = 0;

			while (true) {
				var blockWidth = ((long)mipWidth + 3) / 4;
				var blockHeight = ((long)mipHeight + 3) / 4;
				totalLength = checked(totalLength + blockWidth * blockHeight * 16);
				mipCount++;

				if (!generateMipMaps || mipWidth == 1 && mipHeight == 1)
					break;

				mipWidth = Math.Max(1, mipWidth / 2);
				mipHeight = Math.Max(1, mipHeight / 2);
			}

			return checked((int)totalLength);
		}

		private static void SwapRedBlue(byte[] data) {
			for (var i = 0; i + 3 < data.Length; i += 4) {
				var temp = data[i];
				data[i] = data[i + 2];
				data[i + 2] = temp;
			}
		}

		private enum UnityTextureFormat {
			BC7 = 25,
			PVRTC_RGB2 = 30,
			PVRTC_RGBA2 = 31,
			PVRTC_RGB4 = 32,
			PVRTC_RGBA4 = 33,
			ATC_RGB4 = 35,
			ATC_RGBA8 = 36,
			ETC2_RGB4 = 45,
			ETC2_RGBA1 = 46,
			ETC2_RGBA8 = 47,
			ASTC_RGB_4x4 = 48,
			ASTC_RGB_5x5 = 49,
			ASTC_RGB_6x6 = 50,
			ASTC_RGB_8x8 = 51,
			ASTC_RGB_10x10 = 52,
			ASTC_RGB_12x12 = 53,
			ASTC_RGBA_4x4 = 54,
			ASTC_RGBA_5x5 = 55,
			ASTC_RGBA_6x6 = 56,
			ASTC_RGBA_8x8 = 57,
			ASTC_RGBA_10x10 = 58,
			ASTC_RGBA_12x12 = 59,
			ASTC_HDR_4x4 = 66,
			ASTC_HDR_5x5 = 67,
			ASTC_HDR_6x6 = 68,
			ASTC_HDR_8x8 = 69,
			ASTC_HDR_10x10 = 70,
			ASTC_HDR_12x12 = 71,
		}
	}
}
