using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

using Unity.Collections;
using UnityEngine;

namespace Symphony.Features.BetterTextureFilter.Patchers {
	internal sealed class SimpleTexturePatcher : TexturePatcherBase {
		public override TexturePatchMode Mode => TexturePatchMode.Simple;

		protected override IEnumerator CreateTextureFromPixelsAsync(
			TextureCacheKey cacheKey,
			NativeArray<Color32> pixels,
			Texture2D target,
			byte pma,
			Bc3TextureData.EncodingLease encodingLease,
			Action<Texture2D> onComplete,
			Action onFault
		) {
			var handedOff = false;
			var backgroundOperationAcquired = false;
			var workerOwnsResources = false;
			Task<byte[]> encodingTask = null;

			try {
				if (target.format == TexturePayloadData.Rgba32Format) {
					try {
						target.SetPixelData(pixels, 0);
						target.Apply(updateMipmaps: true, makeNoLongerReadable: false);
						pixels.Dispose();
						pixels = default;
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
					yield break;
				}

				if (!Cache.TryEnterBackgroundOperation()) {
					onFault?.Invoke();
					yield break;
				}
				backgroundOperationAcquired = true;

				try {
					var workerPixels = pixels;
					var width = target.width;
					var height = target.height;
					var mipCount = target.mipmapCount;
					encodingTask = Task.Run(() => {
						try {
							return Bc3TextureData.EncodeGeneratedMipChain(
								workerPixels,
								width,
								height,
								mipCount,
								encodingLease
							);
						} finally {
							try {
								if (workerPixels.IsCreated)
									workerPixels.Dispose();
							} finally {
								try {
									encodingLease?.Dispose();
								} finally {
									Cache.ExitBackgroundOperation();
								}
							}
						}
					});
					pixels = default;
					workerOwnsResources = true;
					backgroundOperationAcquired = false;
				} catch (Exception exception) {
					Plugin.Logger.LogError(exception.ToString());
					onFault?.Invoke();
					yield break;
				}

				while (!encodingTask.IsCompleted)
					yield return null;

				byte[] encodedData;
				try {
					encodedData = encodingTask.GetAwaiter().GetResult();
					encodingTask = null;
				} catch (Exception exception) {
					Plugin.Logger.LogError(exception.ToString());
					onFault?.Invoke();
					yield break;
				}

				try {
					Bc3TextureData.Load(target, encodedData);
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
				if (encodingTask != null && !encodingTask.IsCompleted) {
					encodingTask.ContinueWith(
						completedTask => {
							if (completedTask.IsFaulted)
								_ = completedTask.Exception;
						},
						CancellationToken.None,
						TaskContinuationOptions.ExecuteSynchronously,
						TaskScheduler.Default
					);
				} else {
					if (encodingTask != null && encodingTask.IsFaulted)
						_ = encodingTask.Exception;
				}
				if (backgroundOperationAcquired)
					Cache.ExitBackgroundOperation();
				if (!workerOwnsResources) {
					try {
						if (pixels.IsCreated)
							pixels.Dispose();
					} finally {
						encodingLease?.Dispose();
					}
				}
				if (!handedOff && target && !Cache.OwnsTaskTarget(target))
					UnityEngine.Object.Destroy(target);
			}
		}

		protected override Texture2D CreateTextureFromPixelsSync(
			TextureCacheKey cacheKey,
			NativeArray<Color32> pixels,
			Texture2D target,
			byte pma,
			Bc3TextureData.EncodingLease encodingLease
		) {
			try {
				if (target.format == TexturePayloadData.Rgba32Format) {
					target.SetPixelData(pixels, 0);
					target.Apply(updateMipmaps: true, makeNoLongerReadable: false);
					Cache.WriteTextureSync(cacheKey, Mode, target);
					target.Apply(updateMipmaps: false, makeNoLongerReadable: true);
					return target;
				}

				var encodedData = Bc3TextureData.EncodeGeneratedMipChain(
					pixels,
					target.width,
					target.height,
					target.mipmapCount,
					encodingLease
				);
				Bc3TextureData.Load(target, encodedData);
				encodedData = null;

				Cache.WriteTextureSync(cacheKey, Mode, target);

				target.Apply(updateMipmaps: false, makeNoLongerReadable: true);
				return target;
			} finally {
				if (pixels.IsCreated)
					pixels.Dispose();
			}
		}
	}
}
