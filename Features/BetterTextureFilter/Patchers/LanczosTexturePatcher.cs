using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Symphony.Features.BetterTextureFilter.Formats;
using Symphony.Utils;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Symphony.Features.BetterTextureFilter.Patchers {
	internal sealed class LanczosTexturePatcher : TexturePatcherBase {
		private const int MAX_TASK_COUNT = 5;
		private static int taskCount;

		public override TexturePatchMode Mode => TexturePatchMode.Lanczos;

		protected override IEnumerator CreateTextureFromPixelsAsync(
			TextureCacheKey cacheKey,
			NativeArray<Color32> pixels,
			Texture2D target,
			BCTextureData.EncodingLease encodingLease,
			Action<Texture2D> onComplete,
			Action onFault
		) {
			var taskSlotAcquired = false;
			var backgroundOperationAcquired = false;
			var workerOwnsResources = false;
			var handedOff = false;
			var mips = new Dictionary<int, NativeArray<Color32>>();
			Task<byte[]> task = null;
			try {
				var endOfFrame = new WaitForEndOfFrame();
				var mipCount = target.mipmapCount;
				var textureWidth = target.width;
				var textureHeight = target.height;
				var textureFormat = target.format;

				while (Volatile.Read(ref taskCount) >= MAX_TASK_COUNT)
					yield return endOfFrame;
				if (!Cache.TryEnterBackgroundOperation()) {
					onFault?.Invoke();
					yield break;
				}
				backgroundOperationAcquired = true;

				Interlocked.Increment(ref taskCount);
				taskSlotAcquired = true;

				try {
					var workerPixels = pixels;
					task = Task.Run(() => {
						try {
							CreateMipChain(workerPixels, mips, textureWidth, textureHeight, mipCount);
							return TexturePayloadData.Encode(
								textureFormat,
								workerPixels,
								mips,
								textureWidth,
								textureHeight,
								mipCount,
								encodingLease
							);
						} finally {
							try {
								try {
									DisposeMipChain(mips);
								} finally {
									DisposePixels(workerPixels);
								}
							} finally {
								try {
									encodingLease?.Dispose();
								} finally {
									Interlocked.Decrement(ref taskCount);
									Cache.ExitBackgroundOperation();
								}
							}
						}
					});
					pixels = default;
					workerOwnsResources = true;
					taskSlotAcquired = false;
					backgroundOperationAcquired = false;
				} catch {
					Interlocked.Decrement(ref taskCount);
					taskSlotAcquired = false;
					if (backgroundOperationAcquired) {
						Cache.ExitBackgroundOperation();
						backgroundOperationAcquired = false;
					}
					throw;
				}

				while (!task.IsCompleted)
					yield return endOfFrame;

				byte[] encodedData;
				try {
					encodedData = task.GetAwaiter().GetResult();
					task = null;
				} catch (Exception exception) {
					Plugin.Logger.LogError(exception.ToString());
					onFault?.Invoke();
					yield break;
				}

				try {
					TexturePayloadData.Load(target, encodedData);
					encodedData = null;
				} catch (Exception exception) {
					Plugin.Logger.LogError(exception.ToString());
					onFault?.Invoke();
					yield break;
				}

				yield return Cache.WriteTextureAsync(
					cacheKey,
					Mode,
					target,
					() => {
						try {
							target.Apply(updateMipmaps: false, makeNoLongerReadable: true);
							onComplete(target);
							handedOff = true;
						} catch (Exception exception) {
							Plugin.Logger.LogError(exception.ToString());
							onFault?.Invoke();
						}
					},
					exception => {
						Plugin.Logger.LogError(exception.ToString());
						onFault?.Invoke();
					}
				);
			} finally {
				if (task != null && !task.IsCompleted) {
					task.ContinueWith(
						completedTask => {
							if (completedTask.IsFaulted)
								_ = completedTask.Exception;
						},
						CancellationToken.None,
						TaskContinuationOptions.ExecuteSynchronously,
						TaskScheduler.Default
					);
				}
				if (!workerOwnsResources) {
					try {
						try {
							DisposeMipChain(mips);
						} finally {
							DisposePixels(pixels);
						}
					} finally {
						encodingLease?.Dispose();
					}
				}
				if (taskSlotAcquired)
					Interlocked.Decrement(ref taskCount);
				if (backgroundOperationAcquired)
					Cache.ExitBackgroundOperation();
				if (!handedOff && target && !Cache.OwnsTaskTarget(target))
					UnityEngine.Object.Destroy(target);
			}
		}

		protected override Texture2D CreateTextureFromPixelsSync(
			TextureCacheKey cacheKey,
			NativeArray<Color32> pixels,
			Texture2D target,
			BCTextureData.EncodingLease encodingLease
		) {
			var taskSlotAcquired = false;
			try {
				var mipCount = target.mipmapCount;
				var textureWidth = target.width;
				var textureHeight = target.height;
				var textureFormat = target.format;

				while (Volatile.Read(ref taskCount) >= MAX_TASK_COUNT)
					Thread.Sleep(1);

				Interlocked.Increment(ref taskCount);
				taskSlotAcquired = true;

				byte[] encodedData;
				var mips = new Dictionary<int, NativeArray<Color32>>();
				try {
					CreateMipChain(pixels, mips, textureWidth, textureHeight, mipCount);
					encodedData = TexturePayloadData.Encode(
						textureFormat,
						pixels,
						mips,
						textureWidth,
						textureHeight,
						mipCount,
						encodingLease
					);
				} finally {
					DisposeMipChain(mips);
				}

				TexturePayloadData.Load(target, encodedData);
				encodedData = null;
				Cache.WriteTextureSync(cacheKey, Mode, target);
				target.Apply(updateMipmaps: false, makeNoLongerReadable: true);
				return target;
			} finally {
				if (taskSlotAcquired)
					Interlocked.Decrement(ref taskCount);
				if (pixels.IsCreated)
					pixels.Dispose();
			}
		}

		private static void CreateMipChain(
			NativeArray<Color32> pixels,
			Dictionary<int, NativeArray<Color32>> mips,
			int textureWidth,
			int textureHeight,
			int mipCount
		) {
			for (var mipLevel = 1; mipLevel < mipCount; mipLevel++) {
				var mipWidth = Math.Max(1, textureWidth >> mipLevel);
				var mipHeight = Math.Max(1, textureHeight >> mipLevel);
				var mip = new NativeArray<Color32>(
					mipWidth * mipHeight,
					Allocator.Persistent,
					NativeArrayOptions.UninitializedMemory
				);

				try {
					unsafe {
						var error = TextureFilter.Lanczos2(
							NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(pixels),
							new UIntPtr((ulong)pixels.Length * (ulong)UnsafeUtility.SizeOf<Color32>()),
							textureWidth,
							textureHeight,
							NativeArrayUnsafeUtility.GetUnsafePtr(mip),
							new UIntPtr((ulong)mip.Length * (ulong)UnsafeUtility.SizeOf<Color32>()),
							mipWidth,
							mipHeight,
							pma: 0,
							filterTightness: 1.35f
						);
						if (error != 0) {
							Plugin.Logger.LogWarning("[Symphony::Experimental] Failed to Lanczos2");
							throw new InvalidOperationException("Lanczos2 failed");
						}

						error = TextureFilter.Sharpen(
							NativeArrayUnsafeUtility.GetUnsafePtr(mip),
							mipWidth,
							mipHeight,
							amount: 0.1f
						);
						if (error != 0) {
							Plugin.Logger.LogWarning("[Symphony::Experimental] Failed to Sharpen");
							throw new InvalidOperationException("Sharpen failed");
						}
					}

					mips.Add(mipLevel, mip);
				} catch {
					if (mip.IsCreated)
						mip.Dispose();
					throw;
				}
			}
		}

		private static void DisposeMipChain(Dictionary<int, NativeArray<Color32>> mips) {
			foreach (var mip in mips.Values) {
				try {
					if (mip.IsCreated) mip.Dispose();
				} catch (Exception exception) {
					Plugin.Logger.LogWarning(
						$"[Symphony::Experimental] Failed to release a Lanczos mip buffer: {exception}"
					);
				}
			}
			mips.Clear();
		}

		private static void DisposePixels(NativeArray<Color32> pixels) {
			try {
				if (pixels.IsCreated) pixels.Dispose();
			} catch (Exception exception) {
				Plugin.Logger.LogWarning(
					$"[Symphony::Experimental] Failed to release Lanczos source pixels: {exception}"
				);
			}
		}
	}
}
