using Symphony.Utils;

using System;
using System.Collections.Generic;
using System.Threading;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;

namespace Symphony.Features.BetterTextureFilter.Formats {
	internal enum BCTextureFormat {
		BC3,
		BC7,
	}

	internal sealed class BCTextureData {
		private const int BLOCK_SIZE = 4;
		private const int BYTES_PER_BLOCK = 16;
		private const int RGBA32_BYTES_PER_PIXEL = 4;
		private const int MIN_BLOCKS_PER_THREAD = 1024;

		private readonly SemaphoreSlim asyncEncodingGate = new(1, 1);

		internal BCTextureFormat EncodingFormat { get; }
		internal TextureFormat Format { get; }

		internal BCTextureData(BCTextureFormat encodingFormat) {
			EncodingFormat = encodingFormat;
			Format = encodingFormat switch {
				BCTextureFormat.BC3 => TextureFormat.DXT5,
				BCTextureFormat.BC7 => TextureFormat.BC7,
				_ => throw new ArgumentOutOfRangeException(nameof(encodingFormat))
			};
		}

		internal sealed class EncodingLease : IDisposable {
			private readonly SemaphoreSlim encodingGate;
			private int releasesGate;

			internal int MaximumWorkerCount { get; }

			internal EncodingLease(int maximumWorkerCount, SemaphoreSlim encodingGate, bool releasesGate) {
				MaximumWorkerCount = maximumWorkerCount;
				this.encodingGate = encodingGate;
				this.releasesGate = releasesGate ? 1 : 0;
			}

			public void Dispose() {
				if (Interlocked.Exchange(ref releasesGate, 0) != 0)
					encodingGate.Release();
			}
		}

		internal bool TryAcquireAsyncEncoding(out EncodingLease lease) {
			if (!asyncEncodingGate.Wait(0)) {
				lease = null;
				return false;
			}

			lease = new EncodingLease(
				GetDefaultWorkerCount(),
				asyncEncodingGate,
				releasesGate: true
			);
			return true;
		}

		internal EncodingLease AcquireSynchronousEncoding() {
			if (asyncEncodingGate.Wait(0))
				return new EncodingLease(
					GetDefaultWorkerCount(),
					asyncEncodingGate,
					releasesGate: true
				);

			// A synchronous Harmony path must not wait for an unrelated async texture.
			// One native worker can safely run beside the half-core async encoder.
			return new EncodingLease(
				maximumWorkerCount: 1,
				asyncEncodingGate,
				releasesGate: false
			);
		}

		internal long GetPayloadLength(int width, int height, int mipCount) {
			ValidateDimensions(width, height, mipCount);

			long length = 0;
			for (var mipLevel = 0; mipLevel < mipCount; mipLevel++) {
				var mipWidth = Math.Max(1, width >> mipLevel);
				var mipHeight = Math.Max(1, height >> mipLevel);
				length = checked(length + GetMipLength(mipWidth, mipHeight));
			}

			return length;
		}

		internal unsafe byte[] EncodeGeneratedMipChain(
			NativeArray<Color32> mip0,
			int width,
			int height,
			int mipCount,
			EncodingLease encodingLease
		) {
			if (encodingLease == null) throw new ArgumentNullException(nameof(encodingLease));
			ValidateDimensions(width, height, mipCount);
			if (!mip0.IsCreated || mip0.Length != checked(width * height))
				throw new ArgumentException("Base RGBA mip length does not match its dimensions.", nameof(mip0));
			if (mipCount != GetFullMipCount(width, height))
				throw new ArgumentException($"{EncodingFormat} mip generation requires a complete mip chain.", nameof(mipCount));

			var output = new byte[checked((int)GetPayloadLength(width, height, mipCount))];
			fixed (byte* destination = output) {
				EncodeNative(
					(byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(mip0),
					checked(mip0.Length * RGBA32_BYTES_PER_PIXEL),
					width,
					height,
					destination,
					output.Length,
					generateMipMaps: true,
					blockCount: output.Length / BYTES_PER_BLOCK,
					encodingLease
				);
			}

			return output;
		}

		internal unsafe byte[] Encode(
			NativeArray<Color32> mip0,
			Dictionary<int, NativeArray<Color32>> mips,
			int width,
			int height,
			int mipCount,
			EncodingLease encodingLease
		) {
			if (encodingLease == null) throw new ArgumentNullException(nameof(encodingLease));
			if (!mip0.IsCreated)
				throw new ArgumentException("Base mip is not allocated.", nameof(mip0));
			if (mips == null)
				throw new ArgumentNullException(nameof(mips));

			var output = new byte[checked((int)GetPayloadLength(width, height, mipCount))];
			fixed (byte* destination = output) {
				var destinationOffset = 0;
				for (var mipLevel = 0; mipLevel < mipCount; mipLevel++) {
					var mipWidth = Math.Max(1, width >> mipLevel);
					var mipHeight = Math.Max(1, height >> mipLevel);
					var mip = mipLevel == 0
						? mip0
						: mips.TryGetValue(mipLevel, out var value)
							? value
							: throw new InvalidOperationException($"Missing RGBA mip level {mipLevel}.");
					var expectedPixelCount = checked(mipWidth * mipHeight);
					if (!mip.IsCreated || mip.Length != expectedPixelCount)
						throw new InvalidOperationException($"RGBA mip level {mipLevel} has an invalid length.");

					var sourceLength = checked(expectedPixelCount * RGBA32_BYTES_PER_PIXEL);
					var destinationLength = checked((int)GetMipLength(mipWidth, mipHeight));
					EncodeLevel(
						(byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(mip),
						sourceLength,
						mipWidth,
						mipHeight,
						destination + destinationOffset,
						destinationLength,
						encodingLease
					);
					destinationOffset = checked(destinationOffset + destinationLength);
				}
			}

			return output;
		}

		internal void Load(Texture2D target, byte[] data) {
			if (!target) throw new ArgumentNullException(nameof(target));
			if (target.format != Format)
				throw new InvalidOperationException($"{EncodingFormat} data can only be loaded into a {Format} texture.");
			if (data == null)
				throw new ArgumentNullException(nameof(data));

			var expectedLength = GetPayloadLength(target.width, target.height, target.mipmapCount);
			if (data.LongLength != expectedLength)
				throw new ArgumentException($"{EncodingFormat} mip chain length does not match the target texture.", nameof(data));

			target.LoadRawTextureData(data);
		}

		private unsafe void EncodeLevel(
			byte* source,
			int sourceLength,
			int width,
			int height,
			byte* destination,
			int destinationLength,
			EncodingLease encodingLease
		) {
			var blockCount = checked(
				((width + BLOCK_SIZE - 1) / BLOCK_SIZE) *
				((height + BLOCK_SIZE - 1) / BLOCK_SIZE)
			);
			EncodeNative(
				source,
				sourceLength,
				width,
				height,
				destination,
				destinationLength,
				generateMipMaps: false,
				blockCount,
				encodingLease
			);
		}

		private unsafe void EncodeNative(
			byte* source,
			int sourceLength,
			int width,
			int height,
			byte* destination,
			int destinationLength,
			bool generateMipMaps,
			int blockCount,
			EncodingLease encodingLease
		) {
			var workerCount = Math.Min(
				encodingLease.MaximumWorkerCount,
				Math.Max(1, (blockCount + MIN_BLOCKS_PER_THREAD - 1) / MIN_BLOCKS_PER_THREAD)
			);

			var sourceSize = new UIntPtr((uint)sourceLength);
			var outputSize = new UIntPtr((uint)destinationLength);
			var generateMipMapsValue = generateMipMaps ? (byte)1 : (byte)0;
			var error = EncodingFormat switch {
				BCTextureFormat.BC3 => TextureFilter.BC3(
					source,
					sourceSize,
					width,
					height,
					destination,
					outputSize,
					generateMipMapsValue,
					workerCount
				),
				BCTextureFormat.BC7 => TextureFilter.BC7(
					source,
					sourceSize,
					width,
					height,
					destination,
					outputSize,
					generateMipMapsValue,
					workerCount
				),
				_ => throw new ArgumentOutOfRangeException(nameof(EncodingFormat))
			};
			if (error != 0)
				throw new InvalidOperationException($"{EncodingFormat} encoding failed with error {error}.");
		}

		private static long GetMipLength(int width, int height) {
			var blocksX = ((long)width + BLOCK_SIZE - 1) / BLOCK_SIZE;
			var blocksY = ((long)height + BLOCK_SIZE - 1) / BLOCK_SIZE;
			return checked(blocksX * blocksY * BYTES_PER_BLOCK);
		}

		private static int GetFullMipCount(int width, int height) {
			var mipCount = 1;
			while (width > 1 || height > 1) {
				width = Math.Max(1, width / 2);
				height = Math.Max(1, height / 2);
				mipCount++;
			}

			return mipCount;
		}

		private static void ValidateDimensions(int width, int height, int mipCount) {
			if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
			if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
			if (mipCount <= 0) throw new ArgumentOutOfRangeException(nameof(mipCount));
		}

		private static int GetDefaultWorkerCount() => Math.Max(1, Environment.ProcessorCount / 2);
	}
}
