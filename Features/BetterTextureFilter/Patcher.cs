using Symphony.Features.BetterTextureFilter.Patchers;
using Symphony.Utils;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

namespace Symphony.Features.BetterTextureFilter {
	internal readonly struct TextureCacheKey {
		public readonly string bundleName;
		public readonly string assetName;
		public readonly Hash128 version;
		public readonly string versionText;
		public readonly string texName;

		public TextureCacheKey(string bundleName, string assetName, Hash128 version, string texName) {
			this.bundleName = bundleName;
			this.assetName = assetName;
			this.version = version;
			versionText = version.ToString();
			this.texName = texName;
		}

		public override string ToString() => $"{bundleName}:{assetName}:{texName}@{versionText}";
	}

	internal enum TexturePatchMode {
		Simple = 1,
		Lanczos = 2
	}

	internal enum CacheReadResult {
		Miss,
		Hit,
		Invalid,
		Abandoned
	}

	internal interface ITexturePatcher {
		TexturePatchMode Mode { get; }

		Texture2D CreateTextureSync(TextureCacheKey cacheKey, Texture2D texture, byte pma = 0);
		IEnumerator CreateTextureAsync(TextureCacheKey cacheKey, Texture2D texture, Action<Texture2D> onComplete, Action onFault = null, byte pma = 0);
	}

	internal static class Bc3TextureData {
		internal const TextureFormat Format = TextureFormat.DXT5;

		private const int BLOCK_SIZE = 4;
		private const int BYTES_PER_BLOCK = 16;
		private const int RGBA32_BYTES_PER_PIXEL = 4;
		private const int MIN_BLOCKS_PER_THREAD = 1024;
		private static readonly SemaphoreSlim ASYNC_ENCODING_GATE = new(1, 1);

		internal sealed class EncodingLease : IDisposable {
			private int releasesGate;

			internal int MaximumWorkerCount { get; }

			internal EncodingLease(int maximumWorkerCount, bool releasesGate) {
				MaximumWorkerCount = maximumWorkerCount;
				this.releasesGate = releasesGate ? 1 : 0;
			}

			public void Dispose() {
				if (Interlocked.Exchange(ref releasesGate, 0) != 0)
					ASYNC_ENCODING_GATE.Release();
			}
		}

		internal static bool TryAcquireAsyncEncoding(out EncodingLease lease) {
			if (!ASYNC_ENCODING_GATE.Wait(0)) {
				lease = null;
				return false;
			}

			lease = new EncodingLease(GetDefaultWorkerCount(), releasesGate: true);
			return true;
		}

		internal static EncodingLease AcquireSynchronousEncoding() {
			if (ASYNC_ENCODING_GATE.Wait(0))
				return new EncodingLease(GetDefaultWorkerCount(), releasesGate: true);

			// A synchronous Harmony path must not wait for an unrelated async texture.
			// One native worker can safely run beside the half-core async encoder.
			return new EncodingLease(maximumWorkerCount: 1, releasesGate: false);
		}

		internal static long GetPayloadLength(int width, int height, int mipCount) {
			ValidateDimensions(width, height, mipCount);

			long length = 0;
			for (var mipLevel = 0; mipLevel < mipCount; mipLevel++) {
				var mipWidth = Math.Max(1, width >> mipLevel);
				var mipHeight = Math.Max(1, height >> mipLevel);
				length = checked(length + GetMipLength(mipWidth, mipHeight));
			}

			return length;
		}

		internal static unsafe byte[] EncodeGeneratedMipChain(
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
				throw new ArgumentException("BC3 mip generation requires a complete mip chain.", nameof(mipCount));

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

		internal static unsafe byte[] Encode(
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

		internal static void Load(Texture2D target, byte[] data) {
			if (!target) throw new ArgumentNullException(nameof(target));
			if (target.format != Format)
				throw new InvalidOperationException("BC3 data can only be loaded into a DXT5 texture.");
			if (data == null)
				throw new ArgumentNullException(nameof(data));

			var expectedLength = GetPayloadLength(target.width, target.height, target.mipmapCount);
			if (data.LongLength != expectedLength)
				throw new ArgumentException("BC3 mip chain length does not match the target texture.", nameof(data));

			target.LoadRawTextureData(data);
		}

		private static unsafe void EncodeLevel(
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

		private static unsafe void EncodeNative(
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

			var error = TextureFilter.Bc3(
				source,
				new UIntPtr((uint)sourceLength),
				width,
				height,
				destination,
				new UIntPtr((uint)destinationLength),
				generateMipMaps: generateMipMaps ? (byte)1 : (byte)0,
				threadCount: workerCount
			);
			if (error != 0)
				throw new InvalidOperationException($"BC3 encoding failed with error {error}.");
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

	internal abstract class TexturePatcherBase : ITexturePatcher {
		internal const string TextureFilterName = " $BetterTextureFilter";

		public abstract TexturePatchMode Mode { get; }

		public IEnumerator CreateTextureAsync(
			TextureCacheKey cacheKey,
			Texture2D texture,
			Action<Texture2D> onComplete,
			Action onFault = null,
			byte pma = 0
		) {
			if (onComplete == null)
				throw new ArgumentNullException(nameof(onComplete));
			if (!texture) {
				onFault?.Invoke();
				yield break;
			}

			var target = CreateTargetTexture(texture);
			var targetHideFlags = target.hideFlags;
			target.hideFlags = targetHideFlags | HideFlags.DontUnloadUnusedAsset;
			var handedOff = false;
			var targetOwnershipTransferred = false;
			Bc3TextureData.EncodingLease encodingLease = null;
			try {
				var cacheReadResult = CacheReadResult.Miss;
				Exception cacheCompletionError = null;
				yield return Cache.ReadAsync(cacheKey, Mode, target, result => {
					cacheReadResult = result;
					if (result != CacheReadResult.Hit) return;

					try {
						target.Apply(updateMipmaps: false, makeNoLongerReadable: true);
						onComplete(target);
						handedOff = true;
					} catch (Exception exception) {
						cacheCompletionError = exception;
					}
				});
				if (cacheCompletionError != null) {
					Plugin.Logger.LogError(cacheCompletionError.ToString());
					onFault?.Invoke();
					yield break;
				}
				if (cacheReadResult == CacheReadResult.Hit)
					yield break;
				if (cacheReadResult == CacheReadResult.Abandoned) {
					onFault?.Invoke();
					yield break;
				}

				if (cacheReadResult == CacheReadResult.Invalid) {
					if (target) UnityEngine.Object.Destroy(target);
					target = CreateTargetTexture(texture);
					targetHideFlags = target.hideFlags;
					target.hideFlags = targetHideFlags | HideFlags.DontUnloadUnusedAsset;
				}

				while (!Bc3TextureData.TryAcquireAsyncEncoding(out encodingLease))
					yield return null;

				yield return GetGPUPixelsAsync(
					texture,
					pixels => {
						var transferredEncodingLease = encodingLease;
						var processor = CreateTextureFromPixelsAsync(
							cacheKey,
							pixels,
							target,
							pma,
							transferredEncodingLease,
							completedTexture => {
								onComplete(completedTexture);
								handedOff = true;
							},
							onFault
						);
						encodingLease = null;
						targetOwnershipTransferred = true;
						return processor;
					},
					error => {
						Plugin.Logger.LogError(error.ToString());
						onFault?.Invoke();
					}
				);
			} finally {
				encodingLease?.Dispose();

				// A pending cache read owns a raw pointer into target. If this coroutine is
				// abandoned, Cache's task guard destroys it after the worker has stopped.
				// Once pixel processing starts, ownership belongs to the concrete patcher.
				var cacheOwnsTarget = Cache.OwnsTaskTarget(target);
				if (target && !cacheOwnsTarget) {
					try {
						target.hideFlags = targetHideFlags;
					} catch (Exception exception) {
						Plugin.Logger.LogWarning(
							$"[Symphony::Experimental] Failed to restore texture flags: {exception}"
						);
					}
				}

				if (
					!handedOff && !targetOwnershipTransferred && target &&
					!cacheOwnsTarget
				)
					UnityEngine.Object.Destroy(target);
			}
		}

		public Texture2D CreateTextureSync(
			TextureCacheKey cacheKey,
			Texture2D texture,
			byte pma = 0
		) {
			if (!texture) throw new ArgumentNullException(nameof(texture));

			var target = CreateTargetTexture(texture);
			var targetHideFlags = target.hideFlags;
			target.hideFlags = targetHideFlags | HideFlags.DontUnloadUnusedAsset;
			var completed = false;
			try {
				var cacheReadResult = Cache.ReadSync(cacheKey, Mode, target);
				if (cacheReadResult == CacheReadResult.Hit) {
					target.Apply(updateMipmaps: false, makeNoLongerReadable: true);
					completed = true;
					return target;
				}
				if (cacheReadResult == CacheReadResult.Abandoned)
					throw new OperationCanceledException("Texture cache read was abandoned.");

				if (cacheReadResult == CacheReadResult.Invalid) {
					if (target) UnityEngine.Object.Destroy(target);
					target = CreateTargetTexture(texture);
					targetHideFlags = target.hideFlags;
					target.hideFlags = targetHideFlags | HideFlags.DontUnloadUnusedAsset;
				}

				var pixels = GetGPUPixelsSync(texture);
				using var encodingLease = Bc3TextureData.AcquireSynchronousEncoding();
				var result = CreateTextureFromPixelsSync(cacheKey, pixels, target, pma, encodingLease);
				completed = true;
				return result;
			} finally {
				if (target) {
					try {
						target.hideFlags = targetHideFlags;
					} catch (Exception exception) {
						Plugin.Logger.LogWarning(
							$"[Symphony::Experimental] Failed to restore texture flags: {exception}"
						);
					}
				}
				if (!completed && target)
					UnityEngine.Object.Destroy(target);
			}
		}

		// Concrete patchers own pixels and must dispose them on every completion path.
		protected abstract IEnumerator CreateTextureFromPixelsAsync(
			TextureCacheKey cacheKey,
			NativeArray<Color32> pixels,
			Texture2D target,
			byte pma,
			Bc3TextureData.EncodingLease encodingLease,
			Action<Texture2D> onComplete,
			Action onFault
		);

		protected abstract Texture2D CreateTextureFromPixelsSync(
			TextureCacheKey cacheKey,
			NativeArray<Color32> pixels,
			Texture2D target,
			byte pma,
			Bc3TextureData.EncodingLease encodingLease
		);

		protected static Texture2D CreateTargetTexture(Texture2D source) {
			if (!SystemInfo.SupportsTextureFormat(Bc3TextureData.Format))
				throw new NotSupportedException("BC3/DXT5 textures are not supported by this graphics device.");

			var result = new Texture2D(
				source.width,
				source.height,
				Bc3TextureData.Format,
				mipChain: true,
				linear: false
			) {
				filterMode = FilterMode.Trilinear,
				mipMapBias = 0f,
				wrapModeU = source.wrapModeU,
				wrapModeV = source.wrapModeV,
				wrapModeW = source.wrapModeW,
				anisoLevel = source.anisoLevel,
				name = (source.name ?? "") + TextureFilterName
			};

			return result;
		}

		private static IEnumerator GetGPUPixelsAsync(
			Texture2D texture,
			Func<NativeArray<Color32>, IEnumerator> onComplete,
			Action<Exception> onFault
		) {
			if (texture.isReadable) {
				var result = new NativeArray<Color32>(texture.GetPixels32(0), Allocator.Persistent);
				var driver = DrivePixelConsumer(result, onComplete);
				try {
					while (driver.MoveNext())
						yield return driver.Current;
				} finally {
					(driver as IDisposable)?.Dispose();
				}
				yield break;
			}

			var done = 0;
			var abandoned = 0;
			var pixelsDisposed = 0;
			var renderTextureReleased = 0;
			var ownershipTransferred = false;
			Exception error = null;
			var width = texture.width;
			var height = texture.height;

			var renderTexture = RenderTexture.GetTemporary(
				width,
				height,
				0,
				RenderTextureFormat.ARGB32,
				RenderTextureReadWrite.Default
			);
			var resultPixels = default(NativeArray<Color32>);
			void DisposePixels() {
				if (Interlocked.Exchange(ref pixelsDisposed, 1) == 0 && resultPixels.IsCreated)
					resultPixels.Dispose();
			}
			void ReleaseRenderTexture() {
				if (Interlocked.Exchange(ref renderTextureReleased, 1) == 0)
					RenderTexture.ReleaseTemporary(renderTexture);
			}

			try {
				try {
					renderTexture.filterMode = FilterMode.Point;
					Graphics.Blit(texture, renderTexture, Vector2.one, Vector2.zero, 0, 0);
					resultPixels = new NativeArray<Color32>(
						width * height,
						Allocator.Persistent,
						NativeArrayOptions.UninitializedMemory
					);
					AsyncGPUReadback.RequestIntoNativeArray(
						ref resultPixels,
						renderTexture,
						0,
						TextureFormat.RGBA32,
						request => {
							try {
								if (request.hasError) {
									error = new Exception("Failed to read texture.");
									DisposePixels();
								}
							} finally {
								Volatile.Write(ref done, 1);
								ReleaseRenderTexture();
								if (Volatile.Read(ref abandoned) != 0)
									DisposePixels();
							}
						}
					);
				} catch (Exception exception) {
					error = exception;
					DisposePixels();
					ReleaseRenderTexture();
					Volatile.Write(ref done, 1);
				}

				while (Volatile.Read(ref done) == 0) yield return null;
				if (error != null) {
					onFault?.Invoke(error);
					yield break;
				}

				ownershipTransferred = true;
				var driver = DrivePixelConsumer(resultPixels, onComplete);
				try {
					while (driver.MoveNext())
						yield return driver.Current;
				} finally {
					(driver as IDisposable)?.Dispose();
				}
			} finally {
				if (!ownershipTransferred) {
					Volatile.Write(ref abandoned, 1);
					if (Volatile.Read(ref done) != 0)
						DisposePixels();
				}
			}
		}

		private static IEnumerator DrivePixelConsumer(
			NativeArray<Color32> pixels,
			Func<NativeArray<Color32>, IEnumerator> consumerFactory
		) {
			IEnumerator consumer;
			try {
				consumer = consumerFactory(pixels);
			} catch {
				if (pixels.IsCreated) pixels.Dispose();
				throw;
			}
			if (consumer == null) {
				if (pixels.IsCreated) pixels.Dispose();
				throw new InvalidOperationException("Texture pixel consumer was not created.");
			}

			try {
				while (consumer.MoveNext())
					yield return consumer.Current;
			} finally {
				(consumer as IDisposable)?.Dispose();
			}
		}

		private static NativeArray<Color32> GetGPUPixelsSync(Texture2D texture) {
			if (texture.isReadable)
				return new NativeArray<Color32>(texture.GetPixels32(0), Allocator.Persistent);

			var width = texture.width;
			var height = texture.height;
			var renderTexture = RenderTexture.GetTemporary(
				width,
				height,
				0,
				RenderTextureFormat.ARGB32,
				RenderTextureReadWrite.Default
			);
			renderTexture.filterMode = FilterMode.Point;
			Graphics.Blit(texture, renderTexture, Vector2.one, Vector2.zero, 0, 0);

			var completed = false;
			var resultPixels = new NativeArray<Color32>(
				width * height,
				Allocator.Persistent,
				NativeArrayOptions.UninitializedMemory
			);
			try {
				var request = AsyncGPUReadback.RequestIntoNativeArray(
					ref resultPixels,
					renderTexture,
					0,
					TextureFormat.RGBA32,
					null
				);
				request.WaitForCompletion();

				if (request.hasError)
					throw new Exception($"Failed to read texture '{texture.name}'.");

				completed = true;
				return resultPixels;
			} catch {
				if (resultPixels.IsCreated) resultPixels.Dispose();
				throw;
			} finally {
				RenderTexture.ReleaseTemporary(renderTexture);

				if (!completed && resultPixels.IsCreated)
					resultPixels.Dispose();
			}
		}

		internal static Sprite CloneSpriteWithNewTexture(Sprite original, Texture2D newTexture) {
			void CopyAttribute<T>(Sprite source, Sprite target, VertexAttribute attribute) where T : struct {
				if (!source.HasVertexAttribute(attribute)) return;

				var sourceData = source.GetVertexAttribute<T>(attribute);
				var copy = new NativeArray<T>(
					sourceData.Length,
					Allocator.Temp,
					NativeArrayOptions.UninitializedMemory
				);

				try {
					for (var i = 0; i < sourceData.Length; i++)
						copy[i] = sourceData[i];

					target.SetVertexAttribute(attribute, copy);
				} finally {
					copy.Dispose();
				}
			}

			void CopyRawSpriteData(Sprite source, Sprite target) {
				target.SetVertexCount(source.GetVertexCount());

				CopyAttribute<Vector3>(source, target, VertexAttribute.Position);
				CopyAttribute<Vector3>(source, target, VertexAttribute.Normal);
				CopyAttribute<Vector4>(source, target, VertexAttribute.Tangent);
				CopyAttribute<Color32>(source, target, VertexAttribute.Color);

				CopyAttribute<Vector2>(source, target, VertexAttribute.TexCoord0);
				CopyAttribute<Vector2>(source, target, VertexAttribute.TexCoord1);
				CopyAttribute<Vector2>(source, target, VertexAttribute.TexCoord2);
				CopyAttribute<Vector2>(source, target, VertexAttribute.TexCoord3);
				CopyAttribute<Vector2>(source, target, VertexAttribute.TexCoord4);
				CopyAttribute<Vector2>(source, target, VertexAttribute.TexCoord5);
				CopyAttribute<Vector2>(source, target, VertexAttribute.TexCoord6);
				CopyAttribute<Vector2>(source, target, VertexAttribute.TexCoord7);

				CopyAttribute<BoneWeight>(source, target, VertexAttribute.BlendWeight);

				var sourceIndices = source.GetIndices();
				var indices = new NativeArray<ushort>(
					sourceIndices.Length,
					Allocator.Temp,
					NativeArrayOptions.UninitializedMemory
				);

				try {
					for (var i = 0; i < sourceIndices.Length; i++)
						indices[i] = sourceIndices[i];

					target.SetIndices(indices);
				} finally {
					indices.Dispose();
				}

				target.SetBones(source.GetBones());

				var sourceBindPoses = source.GetBindPoses();
				if (sourceBindPoses.Length == 0) return;

				var bindPoses = new NativeArray<Matrix4x4>(
					sourceBindPoses.Length,
					Allocator.Temp,
					NativeArrayOptions.UninitializedMemory
				);

				try {
					for (var i = 0; i < sourceBindPoses.Length; i++)
						bindPoses[i] = sourceBindPoses[i];

					target.SetBindPoses(bindPoses);
				} finally {
					bindPoses.Dispose();
				}
			}

			void CopyPhysicsShapes(Sprite source, Sprite target) {
				var shapeCount = source.GetPhysicsShapeCount();
				if (shapeCount == 0) return;

				var shapes = new List<Vector2[]>(shapeCount);
				for (var i = 0; i < shapeCount; i++) {
					var points = new List<Vector2>(source.GetPhysicsShapePointCount(i));
					source.GetPhysicsShape(i, points);

					var shape = points.ToArray();
					// GetPhysicsShape uses pivot-based local units, while
					// OverridePhysicsShape expects coordinates in the target rect.
					for (var j = 0; j < shape.Length; j++)
						shape[j] = shape[j] * target.pixelsPerUnit + target.pivot;

					shapes.Add(shape);
				}

				target.OverridePhysicsShape(shapes);
			}

			if (!original) throw new ArgumentNullException(nameof(original));
			if (!newTexture) throw new ArgumentNullException(nameof(newTexture));
			if (original.texture.width != newTexture.width || original.texture.height != newTexture.height)
				throw new ArgumentException(
					"Replacement texture dimensions must match the source texture.",
					nameof(newTexture)
				);

			var logicalRect = original.rect;
			var seedRect = original.packed
				? new Rect(0f, 0f, logicalRect.width, logicalRect.height)
				: logicalRect;
			if (seedRect.width > newTexture.width || seedRect.height > newTexture.height)
				throw new NotSupportedException("Packed Sprite logical rect does not fit in the replacement atlas.");

			var normalizedPivot = new Vector2(
				original.pivot.x / logicalRect.width,
				original.pivot.y / logicalRect.height
			);

			var secondaryTextures = new SecondarySpriteTexture[original.GetSecondaryTextureCount()];
			if (secondaryTextures.Length != 0)
				original.GetSecondaryTextures(secondaryTextures);

			var sprite = Sprite.Create(
				newTexture,
				seedRect,
				normalizedPivot,
				original.pixelsPerUnit,
				original.extrude,
				SpriteMeshType.FullRect,
				original.border,
				generateFallbackPhysicsShape: false,
				secondaryTextures
			);

			try {
				CopyRawSpriteData(original, sprite);
				CopyPhysicsShapes(original, sprite);

				sprite.name = original.name + TextureFilterName;
				sprite.hideFlags = original.hideFlags;
				return sprite;
			} catch {
				UnityEngine.Object.Destroy(sprite);
				throw;
			}
		}
	}

	internal static class TexturePatchService {
		private static readonly ITexturePatcher SimplePatcher = new SimpleTexturePatcher();
		private static readonly ITexturePatcher LanczosPatcher = new LanczosTexturePatcher();
		private static readonly ConditionalWeakTable<GameObject, UIPrefabPatchState> UIPrefabPatchStates = new();

		private enum UIPrefabPatchStatus {
			Idle,
			InFlight,
			Processed
		}

		private sealed class UIPrefabPatchState {
			public UIPrefabPatchStatus status;
		}

		private sealed class UISpriteTextureGroup {
			public readonly Texture2D source;
			public readonly string sourceName;
			public readonly List<UISpriteTextureUsage> usages = new();
			public readonly HashSet<(int atlasId, int materialId)> usageIds = new();

			public UISpriteTextureGroup(Texture2D source) {
				this.source = source;
				sourceName = source.name ?? "";
			}
		}

		private sealed class UISpriteTextureUsage {
			public readonly INGUIAtlas atlas;
			public readonly Material material;

			public UISpriteTextureUsage(INGUIAtlas atlas, Material material) {
				this.atlas = atlas;
				this.material = material;
			}
		}

		private sealed class TextureUsageGroup {
			public readonly Texture2D source;
			public readonly string sourceName;
			public string cacheDiscriminator;
			public readonly List<SpriteRenderer> spriteRenderers = new();
			public readonly List<MaterialUsage> materialUsages = new();

			public string cacheName => string.Concat(
				sourceName.Length.ToString("X8"), ":", sourceName, ":", cacheDiscriminator
			);

			public TextureUsageGroup(Texture2D source) {
				this.source = source;
				sourceName = source.name ?? "";
			}

			public void RegisterCacheDiscriminator(string value) {
				if (cacheDiscriminator == null || string.CompareOrdinal(value, cacheDiscriminator) < 0)
					cacheDiscriminator = value;
			}
		}

		private sealed class MaterialUsage {
			public readonly Renderer renderer;
			public readonly int materialIndex;
			public readonly Material material;

			public MaterialUsage(Renderer renderer, int materialIndex, Material material) {
				this.renderer = renderer;
				this.materialIndex = materialIndex;
				this.material = material;
			}
		}

		private static ITexturePatcher CurrentPatcher => Conf.Experimental.Use_BetterTextureFilter_Patcher.Value switch {
			"simple" => SimplePatcher,
			"lanczos" => LanczosPatcher,
			_ => throw new ArgumentOutOfRangeException("Use_BetterTextureFilter_Patcher")
		};

		internal static bool CanPatch(Texture2D texture) =>
			texture && SystemInfo.SupportsTextureFormat(Bc3TextureData.Format) &&
			texture.mipmapCount <= 1 &&
			!(texture.name ?? "").EndsWith(TexturePatcherBase.TextureFilterName, StringComparison.Ordinal);

		internal static TextureCacheKey CreateCacheKey(string bundleName,string assetName,string textureName) 
			=> new(bundleName,assetName,Caching.GetCachedVersions(bundleName).FirstOrDefault(),textureName ?? "");
		internal static TextureCacheKey CreateCacheKey(string bundleName,string assetName,Texture2D texture)
			=> CreateCacheKey(bundleName, assetName, texture ? texture.name : "");

		internal static Texture2D CreateTextureSync(
			TextureCacheKey cacheKey,
			Texture2D texture,
			byte pma = 0
		) => CurrentPatcher.CreateTextureSync(cacheKey, texture, pma);
		internal static Texture2D CreateTextureSync(
			string bundleName,
			string assetName,
			Texture2D texture,
			byte pma = 0
		) => CreateTextureSync(CreateCacheKey(bundleName, assetName, texture), texture, pma);
		internal static IEnumerator CreateTextureAsync(
			TextureCacheKey cacheKey,
			Texture2D texture,
			Action<Texture2D> onComplete,
			Action onFault = null,
			byte pma = 0
		) => CurrentPatcher.CreateTextureAsync(cacheKey, texture, onComplete, onFault, pma);
		internal static IEnumerator CreateTextureAsync(
			string bundleName,
			string assetName,
			Texture2D texture,
			Action<Texture2D> onComplete,
			Action onFault = null,
			byte pma = 0
		) => CreateTextureAsync(
			CreateCacheKey(bundleName, assetName, texture),
			texture,
			onComplete,
			onFault,
			pma
		);

		internal static Sprite CloneSpriteWithNewTexture(Sprite original, Texture2D newTexture) =>
			TexturePatcherBase.CloneSpriteWithNewTexture(original, newTexture);

		internal static Sprite CreateSpriteSync(
			string bundleName,
			string assetName,
			Sprite sprite
		) {
			if (!sprite) throw new ArgumentNullException(nameof(sprite));

			var replacement = CreateTextureSync(bundleName, assetName, sprite.texture);
			try {
				return CloneSpriteWithNewTexture(sprite, replacement);
			} catch {
				if (replacement) UnityEngine.Object.Destroy(replacement);
				throw;
			}
		}

		private static List<UISpriteTextureGroup> CollectUISpriteTextureGroups(GameObject prefab) {
			var groups = new List<UISpriteTextureGroup>();
			var groupsByTextureId = new Dictionary<int, UISpriteTextureGroup>();

			foreach (var uiSprite in prefab.GetComponentsInChildren<UISprite>(true)) {
				if (uiSprite == null) continue;

				var atlas = uiSprite.atlas;
				if (!(atlas is UnityEngine.Object atlasObject) || !atlasObject) continue;
				var material = atlas.spriteMaterial;
				if (!material || !(material.mainTexture is Texture2D texture) || !CanPatch(texture)) continue;

				var textureId = texture.GetInstanceID();
				if (!groupsByTextureId.TryGetValue(textureId, out var group)) {
					group = new UISpriteTextureGroup(texture);
					groupsByTextureId.Add(textureId, group);
					groups.Add(group);
				}

				if (group.usageIds.Add((atlasObject.GetInstanceID(), material.GetInstanceID())))
					group.usages.Add(new UISpriteTextureUsage(atlas, material));
			}

			return groups;
		}

		private static bool ApplyUISpriteTextureGroup(
			UISpriteTextureGroup group,
			Texture2D replacement,
			out bool allResolved
		) {
			var retained = false;
			allResolved = true;
			foreach (var usage in group.usages) {
				try {
					if (!(usage.atlas is UnityEngine.Object atlasObject) || !atlasObject) continue;

					var currentMaterial = usage.atlas.spriteMaterial;
					if (!currentMaterial) continue;
					if (currentMaterial != usage.material) {
						if (currentMaterial.mainTexture is Texture2D alternateTexture && CanPatch(alternateTexture))
							allResolved = false;
						continue;
					}

					var currentTexture = usage.material.mainTexture;
					if (currentTexture != group.source && currentTexture != replacement) {
						if (currentTexture is Texture2D texture && CanPatch(texture))
							allResolved = false;
						continue;
					}

					if (currentTexture == group.source)
						usage.material.mainTexture = replacement;
					retained = true;
					usage.atlas.MarkAsChanged();
				} catch (Exception exception) {
					try {
						if (
							usage.material && usage.material.mainTexture is Texture2D remainingTexture &&
							CanPatch(remainingTexture)
						)
							allResolved = false;
					} catch { }

					Plugin.Logger.LogWarning(
						$"[Symphony::Experimental] Failed to apply filtered UI atlas texture '{group.sourceName}': {exception}"
					);
				}
			}

			return retained;
		}

		private static IEnumerator PatchUIPrefabTextureGroupsAsync(
			string assetBundleName,
			string assetName,
			UIPrefabPatchState state,
			List<UISpriteTextureGroup> groups
		) {
			var processed = false;
			try {
				var allGroupsCompleted = true;
				var patcher = CurrentPatcher;
				foreach (var group in groups) {
					if (!group.source) {
						allGroupsCompleted = false;
						continue;
					}

					var completed = false;
					void Complete(Texture2D replacement) {
						var allResolved = false;
						var retained = replacement && ApplyUISpriteTextureGroup(group, replacement, out allResolved);
						if (!retained && replacement) 
							UnityEngine.Object.Destroy(replacement);

						completed = allResolved;
					}

					var cacheKey = CreateCacheKey(assetBundleName, assetName, group.source);
					yield return patcher.CreateTextureAsync(cacheKey, group.source, Complete);

					if (!completed) allGroupsCompleted = false;
				}

				processed = allGroupsCompleted;
			} finally {
				state.status = processed ? UIPrefabPatchStatus.Processed : UIPrefabPatchStatus.Idle;
			}
		}

		internal static void PatchUIPrefabTextures(MonoBehaviour coroutineRunner, string assetBundleName, string assetName, GameObject prefab) {
			if (!coroutineRunner || !prefab) return;

			var state = UIPrefabPatchStates.GetValue(prefab, _ => new UIPrefabPatchState());
			if (state.status != UIPrefabPatchStatus.Idle) return;
			state.status = UIPrefabPatchStatus.InFlight;

			try {
				var groups = CollectUISpriteTextureGroups(prefab);
				if (groups.Count == 0) {
					state.status = UIPrefabPatchStatus.Processed;
					return;
				}

				var coroutine = coroutineRunner.StartCoroutine(
					PatchUIPrefabTextureGroupsAsync(assetBundleName, assetName, state, groups)
				);
				if (coroutine == null)
					state.status = UIPrefabPatchStatus.Idle;
			} catch (Exception exception) {
				state.status = UIPrefabPatchStatus.Idle;
				Plugin.Logger.LogWarning(
					$"[Symphony::Experimental] Failed to start UI prefab texture patch for '{assetName}': {exception}"
				);
			}
		}

		private static TextureAssetOwner GetAssetOwner(GameObject owner) {
			if (!owner) return null;

			try {
				var assetOwner = owner.GetComponent<TextureAssetOwner>();
				if (!assetOwner) {
					assetOwner = owner.AddComponent<TextureAssetOwner>();
					assetOwner.hideFlags = HideFlags.HideInInspector;
				}

				return assetOwner;
			} catch (Exception exception) {
				Plugin.Logger.LogWarning(
					$"[Symphony::Experimental] Failed to attach texture asset owner to '{owner.name}': {exception}"
				);
				return null;
			}
		}

		private static List<TextureUsageGroup> CollectTextureUsageGroups(Renderer[] renderers, TextureAssetOwner assetOwner) {
			var groups = new List<TextureUsageGroup>();
			var groupsByTextureId = new Dictionary<int, TextureUsageGroup>();
			var spriteRendererIds = new HashSet<int>();
			var materialSlots = new HashSet<(int rendererId, int materialIndex)>();

			TextureUsageGroup GetGroup(Texture2D texture) {
				var textureId = texture.GetInstanceID();
				if (groupsByTextureId.TryGetValue(textureId, out var group)) return group;

				group = new TextureUsageGroup(texture);
				groupsByTextureId.Add(textureId, group);
				groups.Add(group);
				return group;
			}

			void CollectMaterialRenderer(Renderer materialRenderer, int rendererIndex) {
				var sharedMaterials = materialRenderer.sharedMaterials;
				List<int> patchableSlots = null;
				for (var materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++) {
					var material = sharedMaterials[materialIndex];
					if (material && material.mainTexture is Texture2D texture && CanPatch(texture))
						(patchableSlots ??= new List<int>()).Add(materialIndex);
				}
				if (patchableSlots == null) return;

				var materials = materialRenderer.materials;
				foreach (var material in materials)
					if (assetOwner) assetOwner.Track(material);

				foreach (var materialIndex in patchableSlots) {
					if (materialIndex >= materials.Length) continue;
					var material = materials[materialIndex];
					if (!material || !(material.mainTexture is Texture2D texture) || !CanPatch(texture)) continue;
					if (!materialSlots.Add((materialRenderer.GetInstanceID(), materialIndex))) continue;

					var group = GetGroup(texture);
					group.RegisterCacheDiscriminator($"M{rendererIndex:X8}:{materialIndex:X8}");
					group.materialUsages.Add(new MaterialUsage(materialRenderer, materialIndex, material));
				}
			}

			if (renderers == null) return groups;
			for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++) {
				var renderer = renderers[rendererIndex];
				if (!renderer) continue;

				switch (renderer) {
					case SpriteRenderer spriteRenderer:
						; {
							if (!spriteRendererIds.Add(spriteRenderer.GetInstanceID())) continue;

							var sprite = spriteRenderer.sprite;
							if (!sprite || !CanPatch(sprite.texture)) continue;

							var group = GetGroup(sprite.texture);
							group.RegisterCacheDiscriminator($"S{rendererIndex:X8}");
							group.spriteRenderers.Add(spriteRenderer);
							break;
						}

					case MeshRenderer meshRenderer:
						if (!SpinePatcher.IsSpineRenderer(meshRenderer))
							CollectMaterialRenderer(meshRenderer, rendererIndex);
						break;

					case SkinnedMeshRenderer skinnedRenderer:
						CollectMaterialRenderer(skinnedRenderer, rendererIndex);
						break;
				}
			}

			return groups;
		}

		private static int ApplyTextureUsageGroup(
			TextureUsageGroup group,
			Texture2D replacement,
			string ownerName,
			TextureAssetOwner assetOwner
		) {
			var appliedCount = 0;
			foreach (var spriteRenderer in group.spriteRenderers) {
				Sprite clonedSprite = null;
				try {
					if (!spriteRenderer) continue;

					var currentSprite = spriteRenderer.sprite;
					if (!currentSprite || currentSprite.texture != group.source) continue;

					clonedSprite = CloneSpriteWithNewTexture(currentSprite, replacement);
					spriteRenderer.sprite = clonedSprite;
					if (assetOwner) assetOwner.Track(clonedSprite);

					clonedSprite = null;
					appliedCount++;
				} catch (Exception exception) {
					if (clonedSprite) UnityEngine.Object.Destroy(clonedSprite);
					Plugin.Logger.LogWarning(
						$"[Symphony::Experimental] Failed to apply filtered texture '{group.sourceName}' to SpriteRenderer on '{ownerName}': {exception}"
					);
				}
			}

			foreach (var usage in group.materialUsages) {
				try {
					if (!usage.renderer || !usage.material) continue;

					var currentMaterials = usage.renderer.sharedMaterials;
					if (usage.materialIndex < 0 || usage.materialIndex >= currentMaterials.Length) continue;

					var currentMaterial = currentMaterials[usage.materialIndex];
					if (currentMaterial != usage.material || currentMaterial.mainTexture != group.source) continue;

					currentMaterial.mainTexture = replacement;
					appliedCount++;
				} catch (Exception exception) {
					Plugin.Logger.LogWarning(
						$"[Symphony::Experimental] Failed to apply filtered texture '{group.sourceName}' to material on '{ownerName}': {exception}"
					);
				}
			}

			return appliedCount;
		}

		internal static IEnumerator PatchRendererTexturesAsync(string assetBundleName, string assetName, GameObject owner, Renderer[] renderers) {
			if (!Conf.Experimental.Use_BetterTextureFilter.Value || !owner) yield break;

			var ownerName = owner.name;
			var assetOwner = GetAssetOwner(owner);
			if (!assetOwner) yield break;

			var groups = CollectTextureUsageGroups(renderers, assetOwner);
			if (groups.Count == 0) yield break;

			var patcher = CurrentPatcher;
			foreach (var group in groups) {
				if (!group.source) continue;

				Texture2D replacement = null;
				var handedOff = false;
				try {
					void Complete(Texture2D texture) {
						replacement = texture;
						if (
							!texture || !owner || !assetOwner ||
							ApplyTextureUsageGroup(group, texture, ownerName, assetOwner) == 0
						) {
							if (texture) UnityEngine.Object.Destroy(texture);
							replacement = null;
							return;
						}

						assetOwner.Track(texture);
						handedOff = true;
					}

					var cacheKey = CreateCacheKey(assetBundleName, assetName, group.cacheName);
					yield return patcher.CreateTextureAsync(cacheKey, group.source, Complete);
				} finally {
					if (replacement && !handedOff)
						UnityEngine.Object.Destroy(replacement);
				}
			}
		}

		internal static IEnumerator PatchActorAsync(string assetBundleName, string assetName, Actor actor) {
			if (!actor) yield break;

			var renderers = actor.GetComponentsInChildren<Renderer>(true);
			yield return PatchRendererTexturesAsync(assetBundleName, assetName, actor.gameObject, renderers);
		}
	}

	internal sealed class TextureAssetOwner : MonoBehaviour {
		private readonly HashSet<int> trackedInstanceIds = new();
		private readonly List<Sprite> sprites = new();
		private readonly List<Material> materials = new();
		private readonly List<Texture2D> textures = new();

		private bool TryTrack(UnityEngine.Object asset) =>
			asset && trackedInstanceIds.Add(asset.GetInstanceID());

		internal void Track(Sprite sprite) {
			if (TryTrack(sprite)) sprites.Add(sprite);
		}

		internal void Track(Material material) {
			if (TryTrack(material)) materials.Add(material);
		}

		internal void Track(Texture2D texture) {
			if (TryTrack(texture)) textures.Add(texture);
		}

		private void OnDestroy() {
			foreach (var sprite in sprites)
				if (sprite) UnityEngine.Object.Destroy(sprite);
			foreach (var material in materials)
				if (material) UnityEngine.Object.Destroy(material);
			foreach (var texture in textures)
				if (texture) UnityEngine.Object.Destroy(texture);

			sprites.Clear();
			materials.Clear();
			textures.Clear();
			trackedInstanceIds.Clear();
		}
	}
}
