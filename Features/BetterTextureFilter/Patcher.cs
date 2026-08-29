using Symphony.Features.BetterTextureFilter.Patchers;
using Symphony.Features.BetterTextureFilter.Formats;

using System;
using System.Collections;
using System.Collections.Generic;
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
		Simple,
		Lanczos,
	}
	internal enum TextureFormatMode {
		RGBA32,
		BC3,
		DXT5 = BC3,
		BC7,
	}

	internal enum CacheReadResult {
		Miss,
		Hit,
		Invalid,
		Abandoned
	}

	internal interface ITexturePatcher {
		TexturePatchMode Mode { get; }

		Texture2D CreateTextureSync(TextureCacheKey cacheKey, Texture2D texture, TextureFormatMode format);
		IEnumerator CreateTextureAsync(TextureCacheKey cacheKey, Texture2D texture, TextureFormatMode format, Action<Texture2D> onComplete, Action onFault = null);
	}

	internal static class TexturePayloadData {
		internal const TextureFormat Rgba32Format = TextureFormat.RGBA32;
		private static readonly BCTextureData BC3 = new(BCTextureFormat.BC3);
		private static readonly BCTextureData BC7 = new(BCTextureFormat.BC7);

		internal static TextureFormat ResolveFormat(TextureFormatMode format, bool canEncode) {
			if (!canEncode || format == TextureFormatMode.RGBA32)
				return Rgba32Format;

			return format switch {
				TextureFormatMode.BC3 => BC3.Format,
				TextureFormatMode.BC7 => BC7.Format,
				_ => throw new ArgumentOutOfRangeException(nameof(format))
			};
		}

		internal static bool IsSupportedFormat(TextureFormat format) =>
			format == Rgba32Format || GetBCTextureData(format) != null;

		internal static long GetPayloadLength(
			TextureFormat format,
			int width,
			int height,
			int mipCount
		) {
			var data = GetBCTextureData(format);
			if (data != null)
				return data.GetPayloadLength(width, height, mipCount);
			if (format == Rgba32Format)
				return GetRgba32PayloadLength(width, height, mipCount);

			throw new NotSupportedException($"Unsupported texture payload format: {format}");
		}

		internal static bool TryAcquireAsyncEncoding(
			TextureFormat format,
			out BCTextureData.EncodingLease encodingLease
		) {
			var data = GetBCTextureData(format);
			if (data != null)
				return data.TryAcquireAsyncEncoding(out encodingLease);
			if (format != Rgba32Format)
				throw new NotSupportedException($"Unsupported texture payload format: {format}");

			encodingLease = null;
			return true;
		}

		internal static BCTextureData.EncodingLease AcquireSynchronousEncoding(TextureFormat format) {
			var data = GetBCTextureData(format);
			if (data != null)
				return data.AcquireSynchronousEncoding();
			if (format == Rgba32Format)
				return null;

			throw new NotSupportedException($"Unsupported texture payload format: {format}");
		}

		internal static byte[] EncodeGeneratedMipChain(
			TextureFormat format,
			NativeArray<Color32> mip0,
			int width,
			int height,
			int mipCount,
			BCTextureData.EncodingLease encodingLease
		) {
			var data = GetBCTextureData(format) ??
				throw new NotSupportedException($"Unsupported generated mip format: {format}");
			return data.EncodeGeneratedMipChain(mip0, width, height, mipCount, encodingLease);
		}

		internal static byte[] Encode(
			TextureFormat format,
			NativeArray<Color32> mip0,
			Dictionary<int, NativeArray<Color32>> mips,
			int width,
			int height,
			int mipCount,
			BCTextureData.EncodingLease encodingLease
		) {
			var data = GetBCTextureData(format);
			if (data != null)
				return data.Encode(mip0, mips, width, height, mipCount, encodingLease);
			if (format == Rgba32Format)
				return EncodeRgba32(mip0, mips, width, height, mipCount);

			throw new NotSupportedException($"Unsupported texture payload format: {format}");
		}

		internal static void Load(Texture2D target, byte[] data) {
			if (!target) throw new ArgumentNullException(nameof(target));

			var bcTextureData = GetBCTextureData(target.format);
			if (bcTextureData != null) {
				bcTextureData.Load(target, data);
				return;
			}
			if (target.format != Rgba32Format)
				throw new InvalidOperationException("Texture data can only be loaded into a BC3, BC7 or RGBA32 texture.");
			if (data == null)
				throw new ArgumentNullException(nameof(data));

			var expectedLength = GetPayloadLength(
				target.format,
				target.width,
				target.height,
				target.mipmapCount
			);
			if (data.LongLength != expectedLength)
				throw new ArgumentException("RGBA32 mip chain length does not match the target texture.", nameof(data));

			target.LoadRawTextureData(data);
		}

		private static BCTextureData GetBCTextureData(TextureFormat format) => format switch {
			TextureFormat.DXT5 => BC3,
			TextureFormat.BC7 => BC7,
			_ => null
		};

		private static long GetRgba32PayloadLength(int width, int height, int mipCount) {
			if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
			if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
			if (mipCount <= 0) throw new ArgumentOutOfRangeException(nameof(mipCount));

			long length = 0;
			for (var mipLevel = 0; mipLevel < mipCount; mipLevel++) {
				var mipWidth = Math.Max(1, width >> mipLevel);
				var mipHeight = Math.Max(1, height >> mipLevel);
				length = checked(length + (long)mipWidth * mipHeight * UnsafeUtility.SizeOf<Color32>());
			}

			return length;
		}

		private static unsafe byte[] EncodeRgba32(
			NativeArray<Color32> mip0,
			Dictionary<int, NativeArray<Color32>> mips,
			int width,
			int height,
			int mipCount
		) {
			var payloadLength = GetRgba32PayloadLength(width, height, mipCount);
			var output = new byte[checked((int)payloadLength)];

			fixed (byte* destination = output) {
				var destinationOffset = 0;
				for (var mipLevel = 0; mipLevel < mipCount; mipLevel++) {
					var mipWidth = Math.Max(1, width >> mipLevel);
					var mipHeight = Math.Max(1, height >> mipLevel);
					var mip = mipLevel == 0
						? mip0
						: mips.TryGetValue(mipLevel, out var value)
							? value
							: throw new ArgumentException($"Missing mip level {mipLevel}.", nameof(mips));
					var pixelCount = checked(mipWidth * mipHeight);
					if (!mip.IsCreated || mip.Length != pixelCount)
						throw new ArgumentException($"RGBA32 mip level {mipLevel} has an invalid length.", nameof(mips));

					var mipLength = checked(pixelCount * UnsafeUtility.SizeOf<Color32>());
					UnsafeUtility.MemCpy(
						destination + destinationOffset,
						NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(mip),
						mipLength
					);
					destinationOffset = checked(destinationOffset + mipLength);
				}
			}

			return output;
		}
	}

	internal abstract class TexturePatcherBase : ITexturePatcher {
		internal const string TextureFilterName = " $BetterTextureFilter";

		public abstract TexturePatchMode Mode { get; }

		public IEnumerator CreateTextureAsync(
			TextureCacheKey cacheKey,
			Texture2D texture,
			TextureFormatMode format,
			Action<Texture2D> onComplete,
			Action onFault = null
		) {
			if (onComplete == null)
				throw new ArgumentNullException(nameof(onComplete));
			if (!texture) {
				onFault?.Invoke();
				yield break;
			}

			var target = CreateTargetTexture(texture, format);
			var targetHideFlags = target.hideFlags;
			target.hideFlags = targetHideFlags | HideFlags.DontUnloadUnusedAsset;
			var handedOff = false;
			var targetOwnershipTransferred = false;
			BCTextureData.EncodingLease encodingLease = null;
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
					target = CreateTargetTexture(texture, format);
					targetHideFlags = target.hideFlags;
					target.hideFlags = targetHideFlags | HideFlags.DontUnloadUnusedAsset;
				}

				while (!TexturePayloadData.TryAcquireAsyncEncoding(target.format, out encodingLease))
					yield return null;

				yield return GetGPUPixelsAsync(
					texture,
					pixels => {
						var transferredEncodingLease = encodingLease;
						var processor = CreateTextureFromPixelsAsync(
							cacheKey,
							pixels,
							target,
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
			TextureFormatMode format
		) {
			if (!texture) throw new ArgumentNullException(nameof(texture));

			var target = CreateTargetTexture(texture, format);
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
					target = CreateTargetTexture(texture, format);
					targetHideFlags = target.hideFlags;
					target.hideFlags = targetHideFlags | HideFlags.DontUnloadUnusedAsset;
				}

				var pixels = GetGPUPixelsSync(texture);
				using var encodingLease = TexturePayloadData.AcquireSynchronousEncoding(target.format);
				var result = CreateTextureFromPixelsSync(
					cacheKey,
					pixels,
					target,
					encodingLease
				);
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
			BCTextureData.EncodingLease encodingLease,
			Action<Texture2D> onComplete,
			Action onFault
		);

		protected abstract Texture2D CreateTextureFromPixelsSync(
			TextureCacheKey cacheKey,
			NativeArray<Color32> pixels,
			Texture2D target,
			BCTextureData.EncodingLease encodingLease
		);

		protected static Texture2D CreateTargetTexture(Texture2D source, TextureFormatMode fmt) {
			var format = TexturePayloadData.ResolveFormat(fmt, TexturePatchService.CanEncode(source));

			if (!SystemInfo.SupportsTextureFormat(format))
				throw new NotSupportedException($"{format} textures are not supported by this graphics device.");

			var result = new Texture2D(source.width, source.height, format, mipChain: true, linear: false) {
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
		private static TextureFormatMode CurrentTextureFormat => Conf.Experimental.Use_BetterTextureFilter_Format.Value switch {
			"RGBA32" => TextureFormatMode.RGBA32,
			"BC3" => TextureFormatMode.BC3,
			"BC7" => TextureFormatMode.BC7,
			_ => throw new ArgumentOutOfRangeException("Use_BetterTextureFilter_Format")
		};
		internal static TexturePatchMode CurrentPatchMode => CurrentPatcher.Mode;
		internal static TextureFormatMode CurrentFormatMode => CurrentTextureFormat;

		internal static bool CanEncode(int width, int height) =>
			width > 0 && height > 0 && width % 4 == 0 && height % 4 == 0;
		internal static bool CanEncode(Texture2D texture) =>
			texture && CanEncode(texture.width, texture.height);

		internal static bool CanPatch(Texture2D texture) =>
			texture && SystemInfo.SupportsTextureFormat(
				TexturePayloadData.ResolveFormat(CurrentTextureFormat, CanEncode(texture))
			) &&
			texture.mipmapCount <= 1 &&
			!(texture.name ?? "").EndsWith(TexturePatcherBase.TextureFilterName, StringComparison.Ordinal);

		internal static TextureCacheKey CreateCacheKey(string bundleName, string assetName, string textureName) {
			bundleName = bundleName?.ToLowerInvariant() ?? "";
			var manifest = AssetBundles.AssetBundleManager.AssetBundleManifestObject;
			var version = default(Hash128);
			try {
				if (manifest != null)
					version = manifest.GetAssetBundleHash(bundleName);
			} catch (Exception exception) {
				Plugin.Logger.LogWarning(
					$"[Symphony::Experimental] Failed to resolve current asset bundle version for '{bundleName}': {exception}"
				);
			}
			return new TextureCacheKey(bundleName, assetName, version, textureName ?? "");
		}
		internal static TextureCacheKey CreateCacheKey(string bundleName,string assetName,Texture2D texture)
			=> CreateCacheKey(bundleName, assetName, texture ? texture.name : "");

		internal static Texture2D CreateTextureSync(TextureCacheKey cacheKey, Texture2D texture)
			=> CurrentPatcher.CreateTextureSync(cacheKey, texture, CurrentTextureFormat);
		internal static Texture2D CreateTextureSync(string bundleName, string assetName, Texture2D texture)
			=> CreateTextureSync(CreateCacheKey(bundleName, assetName, texture), texture);

		internal static IEnumerator CreateTextureAsync(
			TextureCacheKey cacheKey,
			Texture2D texture,
			Action<Texture2D> onComplete,
			Action onFault = null
		) => CurrentPatcher.CreateTextureAsync(cacheKey, texture, CurrentTextureFormat, onComplete, onFault);
		internal static IEnumerator CreateTextureAsync(
			string bundleName,
			string assetName,
			Texture2D texture,
			Action<Texture2D> onComplete,
			Action onFault = null
		) => CreateTextureAsync(CreateCacheKey(bundleName, assetName, texture), texture, onComplete, onFault);

		internal static Sprite CloneSpriteWithNewTexture(Sprite original, Texture2D newTexture)
			=> TexturePatcherBase.CloneSpriteWithNewTexture(original, newTexture);

		internal static Sprite CreateSpriteSync(string bundleName, string assetName, Sprite sprite) {
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
					yield return patcher.CreateTextureAsync(cacheKey, group.source, CurrentTextureFormat, Complete);

					if (!completed) allGroupsCompleted = false;
				}

				processed = allGroupsCompleted;
			} finally {
				state.status = processed ? UIPrefabPatchStatus.Processed : UIPrefabPatchStatus.Idle;
			}
		}

		internal static void PatchUIPrefabTextures(
			MonoBehaviour coroutineRunner,
			string assetBundleName,
			string assetName,
			GameObject prefab
		) {
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
						if (!meshRenderer.GetComponent<TextMesh>() && !SpinePatcher.IsSpineRenderer(meshRenderer))
							CollectMaterialRenderer(meshRenderer, rendererIndex);
						break;

					case SkinnedMeshRenderer skinnedRenderer:
						CollectMaterialRenderer(skinnedRenderer, rendererIndex);
						break;

					case ParticleSystemRenderer particleRenderer:
						CollectMaterialRenderer(particleRenderer, rendererIndex);
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

		internal static IEnumerator PatchRendererTexturesAsync(
			string assetBundleName,
			string assetName,
			GameObject owner,
			Renderer[] renderers
		) {
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
					yield return patcher.CreateTextureAsync(cacheKey, group.source, CurrentTextureFormat, Complete);
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
