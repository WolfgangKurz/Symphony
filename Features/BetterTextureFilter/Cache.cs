using AssetBundles;

using Symphony.Features.BetterTextureFilter.Formats;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;

namespace Symphony.Features.BetterTextureFilter {
	internal static class Cache {
		private const uint CACHE_MAGIC = 0x43465442; // BTFC
		private const int CACHE_VERSION = 1;
		private const int CACHE_HEADER_LENGTH = sizeof(uint) + sizeof(int) * 6 + sizeof(long);

		private static readonly object TASK_LOCK = new();
		private static readonly object FILE_LOCK = new();
		private static readonly ConcurrentQueue<TextureCacheTaskGuard> TASK_CLEANUP = new();
		private static readonly HashSet<TextureCacheTaskGuard> ACTIVE_TASKS = new();
		private static readonly Dictionary<string, long> FILE_GENERATIONS = new(StringComparer.OrdinalIgnoreCase);

		private static string cacheDirectory;
		private static DateTime initializedAtUtc;
		private static int initialized;
		private static int shuttingDown;
		private static int activeDetachedFileOperations;

		private sealed class TextureCacheTaskGuard {
			private const int STATE_PENDING = 0;
			private const int STATE_CALLER = 1;
			private const int STATE_CLEANUP = 2;
			private const int STATE_RELEASED = 3;

			private readonly Texture2D target;
			private readonly HideFlags originalHideFlags;
			private Task task;
			private int state;
			private int cleanupQueued;
			private int cleanupDeferred;

			public bool IsReleased => Volatile.Read(ref state) == STATE_RELEASED;
			public bool IsClaimedByCaller => Volatile.Read(ref state) == STATE_CALLER;
			public Task Task => Volatile.Read(ref task);
			public bool Owns(Texture2D texture) => ReferenceEquals(target, texture);

			public TextureCacheTaskGuard(Texture2D target) {
				if (!target) throw new ArgumentNullException(nameof(target));

				lock (TASK_LOCK) {
					if (Volatile.Read(ref shuttingDown) != 0)
						throw new OperationCanceledException("Texture cache tasks are shutting down.");

					this.target = target;
					originalHideFlags = target.hideFlags;
					target.hideFlags = originalHideFlags | HideFlags.DontUnloadUnusedAsset;
					ACTIVE_TASKS.Add(this);
				}
			}

			public bool TryClaimForCaller() => Interlocked.CompareExchange(ref state, STATE_CALLER, STATE_PENDING) == STATE_PENDING;
			public bool TryClaimForCleanup() => Interlocked.CompareExchange(ref state, STATE_CLEANUP, STATE_PENDING) == STATE_PENDING;
			public bool DeferCleanup() => Interlocked.Increment(ref cleanupDeferred) <= 2;

			public void Watch(Task task) {
				if (task == null) throw new ArgumentNullException(nameof(task));

				Volatile.Write(ref this.task, task);
				task.ContinueWith(
					completedTask => {
						// 참조해서 Unhandled 되지 않도록 조치
						if (completedTask.IsFaulted)
							_ = completedTask.Exception;
						QueueCleanup();
					},
					CancellationToken.None,
					TaskContinuationOptions.ExecuteSynchronously,
					TaskScheduler.Default
				);
			}

			private void QueueCleanup() {
				if (IsReleased || Interlocked.Exchange(ref cleanupQueued, 1) != 0) return;
				TASK_CLEANUP.Enqueue(this);
			}

			public void Release(bool destroy) {
				var currentState = Volatile.Read(ref state);
				if (currentState != STATE_CALLER && currentState != STATE_CLEANUP) return;
				if (Interlocked.CompareExchange(ref state, STATE_RELEASED, currentState) != currentState) return;

				lock (TASK_LOCK)
					ACTIVE_TASKS.Remove(this);

				ReleaseTarget(destroy);
			}

			public void ForceRelease(bool destroy) {
				while (true) {
					var currentState = Volatile.Read(ref state);
					if (currentState == STATE_RELEASED) return;
					if (Interlocked.CompareExchange(ref state, STATE_RELEASED, currentState) == currentState) break;
				}

				lock (TASK_LOCK)
					ACTIVE_TASKS.Remove(this);

				ReleaseTarget(destroy);
			}

			private void ReleaseTarget(bool destroy) {
				if (!target) return;

				try {
					target.hideFlags = originalHideFlags;
				} catch (Exception e) {
					Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to restore cache target flags: {e}");
				}

				if (!destroy) return;

				try {
					UnityEngine.Object.Destroy(target);
				} catch (Exception e) {
					Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to destroy abandoned cache target: {e}");
				}
			}
		}

		internal static bool OwnsTaskTarget(Texture2D target) {
			if (ReferenceEquals(target, null)) return false;

			lock (TASK_LOCK)
				return ACTIVE_TASKS.Any(guard => !guard.IsReleased && guard.Owns(target));
		}

		internal static void Initialize(MonoBehaviour runner) {
			if (!runner) throw new ArgumentNullException(nameof(runner));
			if (Interlocked.CompareExchange(ref initialized, 1, 0) != 0) return;

			Volatile.Write(ref shuttingDown, 0);
			initializedAtUtc = DateTime.UtcNow;
			cacheDirectory = Path.Combine(Plugin.GameDir, "BetterTextureFilter", "Caches");

			try {
				Directory.CreateDirectory(cacheDirectory);
			} catch (Exception e) {
				Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to create texture cache directory: {e}");
			}

			Compress.Initialize();
			runner.StartCoroutine(CleanupOrphanCache());
			runner.StartCoroutine(CleanupOutdated());
			runner.StartCoroutine(ProcessTaskCleanup());
		}

		internal static void Shutdown() {
			if (Interlocked.Exchange(ref shuttingDown, 1) != 0) return;
			Compress.Shutdown();

			TextureCacheTaskGuard[] active;
			lock (TASK_LOCK) {
				active = ACTIVE_TASKS.ToArray();
				while (activeDetachedFileOperations != 0)
					Monitor.Wait(TASK_LOCK);
			}

			foreach (var guard in active) {
				try {
					guard.Task?.GetAwaiter().GetResult();
				} catch (Exception e) {
					Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed while draining a cache task: {e}");
				} finally {
					guard.ForceRelease(destroy: true);
				}
			}

			while (TASK_CLEANUP.TryDequeue(out var guard))
				guard.ForceRelease(destroy: true);
		}

		internal static CacheReadResult ReadSync(TextureCacheKey key, TexturePatchMode mode, Texture2D target) {
			if (Volatile.Read(ref shuttingDown) != 0) return CacheReadResult.Abandoned;

			var cachePath = GetCachePath(key, mode);
			if (string.IsNullOrEmpty(cachePath) || !File.Exists(cachePath))
				return CacheReadResult.Miss;

			var cacheGeneration = 0L;
			try {
				var width = target.width;
				var height = target.height;
				var mipCount = target.mipmapCount;
				var format = target.format;
				var payloadLength = GetPayloadLength(format, width, height, mipCount);
				var destination = GetTextureBuffer(target, payloadLength, out var destinationLength);

				ReadCache(
					cachePath, mode,
					width, height, mipCount, format,
					payloadLength,
					destination, destinationLength,
					out cacheGeneration
				);

				Plugin.Logger.LogDebug($"[Symphony::Experimental] Texture cache hit: {cachePath}");
				return CacheReadResult.Hit;
			} catch (Exception e) {
				LogInvalidCache(cachePath, e);
				DeleteInvalidCache(cachePath, cacheGeneration);
				return CacheReadResult.Invalid;
			}
		}

		internal static IEnumerator ReadAsync(TextureCacheKey key, TexturePatchMode mode, Texture2D target, Action<CacheReadResult> continuation) {
			if (continuation == null) throw new ArgumentNullException(nameof(continuation));

			if (Volatile.Read(ref shuttingDown) != 0) {
				continuation(CacheReadResult.Abandoned);
				yield break;
			}

			var cachePath = GetCachePath(key, mode);
			if (string.IsNullOrEmpty(cachePath) || !File.Exists(cachePath)) {
				continuation(CacheReadResult.Miss);
				yield break;
			}

			TextureCacheTaskGuard guard = null;
			Task task = null;
			Exception error = null;
			var claimed = false;
			var cacheGeneration = 0L;

			try {
				var width = target.width;
				var height = target.height;
				var mipCount = target.mipmapCount;
				var format = target.format;
				var payloadLength = GetPayloadLength(format, width, height, mipCount);

				guard = new TextureCacheTaskGuard(target);
				var destination = GetTextureBuffer(target, payloadLength, out var destinationLength);
				task = Task.Run(() => {
					try {
						ReadCache(
							cachePath, mode,
							width, height, mipCount, format,
							payloadLength,
							destination, destinationLength,
							out cacheGeneration
						);
					} finally {
						GC.KeepAlive(guard);
					}
				});
				guard.Watch(task);
			} catch (Exception e) {
				error = e;
			}

			if (task != null) {
				while (!task.IsCompleted)
					yield return null;

				claimed = guard.TryClaimForCaller();
				if (!claimed) {
					continuation(CacheReadResult.Abandoned);
					yield break;
				}

				try {
					task.GetAwaiter().GetResult();
				} catch (Exception e) {
					error = e;
				}
			} else if (guard != null) {
				claimed = guard.TryClaimForCaller();
				if (!claimed) {
					continuation(CacheReadResult.Abandoned);
					yield break;
				}
			}

			var result = error == null ? CacheReadResult.Hit : CacheReadResult.Invalid;
			if (error != null) {
				if (error is OperationCanceledException && Volatile.Read(ref shuttingDown) != 0)
					result = CacheReadResult.Abandoned;
				else {
					LogInvalidCache(cachePath, error);
					DeleteInvalidCache(cachePath, cacheGeneration);
				}
			} else {
				Plugin.Logger.LogDebug($"[Symphony::Experimental] Texture cache hit: {cachePath}");
			}

			try {
				continuation(result);
			} finally {
				if (claimed)
					guard.Release(destroy: false);
			}
		}

		internal static void WriteTextureSync(
			TextureCacheKey key,
			TexturePatchMode mode,
			Texture2D target
		) {
			if (Volatile.Read(ref shuttingDown) != 0) return;

			var cachePath = GetCachePath(key, mode);
			if (string.IsNullOrEmpty(cachePath) || File.Exists(cachePath)) return;

			var width = target.width;
			var height = target.height;
			var mipCount = target.mipmapCount;
			var format = target.format;
			var payloadLength = GetPayloadLength(format, width, height, mipCount);
			var payload = GetTextureBuffer(target, payloadLength, out var payloadLengthInt);

			TryWriteCache(cachePath, mode, width, height, mipCount, format, stream => WritePointer(stream, payload, payloadLengthInt));
		}

		internal static IEnumerator WriteTextureAsync(
			TextureCacheKey key,
			TexturePatchMode mode,
			Texture2D target,
			Action onWritten,
			Action<Exception> onFault
		) {
			if (onWritten == null) throw new ArgumentNullException(nameof(onWritten));

			if (Volatile.Read(ref shuttingDown) != 0) {
				onFault?.Invoke(new OperationCanceledException("Texture cache tasks are shutting down."));
				yield break;
			}

			var cachePath = GetCachePath(key, mode);
			if (string.IsNullOrEmpty(cachePath) || File.Exists(cachePath)) {
				onWritten();
				yield break;
			}

			TextureCacheTaskGuard guard = null;
			Task task = null;
			Exception error = null;
			var claimed = false;

			try {
				var width = target.width;
				var height = target.height;
				var mipCount = target.mipmapCount;
				var format = target.format;
				var payloadLength = GetPayloadLength(format, width, height, mipCount);

				guard = new TextureCacheTaskGuard(target);
				var payload = GetTextureBuffer(target, payloadLength, out var payloadLengthInt);
				task = Task.Run(() => {
					try {
						TryWriteCache(cachePath, mode, width, height, mipCount, format, stream => WritePointer(stream, payload, payloadLengthInt));
					} finally {
						GC.KeepAlive(guard);
					}
				});
				guard.Watch(task);
			} catch (Exception e) {
				error = e;
			}

			if (task != null) {
				while (!task.IsCompleted)
					yield return null;

				claimed = guard.TryClaimForCaller();
				if (!claimed) {
					onFault?.Invoke(new OperationCanceledException("Texture cache write was abandoned."));
					yield break;
				}

				try {
					task.GetAwaiter().GetResult();
				} catch (Exception e) {
					error = e;
				}
			} else if (guard != null) {
				claimed = guard.TryClaimForCaller();
				if (!claimed) {
					onFault?.Invoke(new OperationCanceledException("Texture cache write was abandoned."));
					yield break;
				}
			}

			try {
				if (error == null)
					onWritten();
				else
					onFault?.Invoke(error);
			} finally {
				if (claimed)
					guard.Release(destroy: false);
			}
		}

		private static IEnumerator ProcessTaskCleanup() {
			while (Volatile.Read(ref shuttingDown) == 0) {
				List<TextureCacheTaskGuard> deferred = null;

				while (TASK_CLEANUP.TryDequeue(out var guard)) {
					try {
						if (guard.IsReleased || guard.IsClaimedByCaller) continue;
						if (guard.DeferCleanup()) {
							(deferred ??= new List<TextureCacheTaskGuard>()).Add(guard);
							continue;
						}
						if (guard.TryClaimForCleanup())
							guard.Release(destroy: true);
					} catch (Exception e) {
						Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to clean up a cache task target: {e}");
					}
				}

				if (deferred != null)
					foreach (var guard in deferred)
						TASK_CLEANUP.Enqueue(guard);

				yield return null;
			}
		}

		internal static bool TryEnterBackgroundOperation() {
			lock (TASK_LOCK) {
				if (Volatile.Read(ref shuttingDown) != 0) return false;
				activeDetachedFileOperations++;
				return true;
			}
		}

		internal static void ExitBackgroundOperation() {
			lock (TASK_LOCK) {
				activeDetachedFileOperations--;
				if (activeDetachedFileOperations == 0)
					Monitor.PulseAll(TASK_LOCK);
			}
		}

		private static IEnumerator CleanupOrphanCache() {
			(string bundleName, string assetName, string textureName)[] ORPHAN_CACHE_KEYS = [
				("mainui", "UIPcWarehouse", "AndroidPC"),
				("mainui", "UIPcWarehouse", "masterAtlas"),
				("mainui", "UIPcWarehouse", "masterAtlas_V2"),
				("mainui", "UIInvenSquad", "AndroidPC"),
				("mainui", "UIInvenSquad", "masterAtlas_V2"),
			];

		var directory = cacheDirectory;
			if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
				yield break;

			var orphanKeys = ORPHAN_CACHE_KEYS
				.Select(key => (
					bundleName: key.bundleName.ToLowerInvariant(),
					bundleId: ComputeId(key.bundleName.ToLowerInvariant()),
					key.assetName,
					key.textureName
				))
				.ToArray();

			string[] cachePaths;
			try {
				cachePaths = Directory.GetFiles(directory, "*.cache", SearchOption.AllDirectories);
			} catch (Exception e) {
				Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to enumerate orphan texture caches: {e}");
				yield break;
			}

			var removedCount = 0;
			for (var i = 0; i < cachePaths.Length; i++) {
				if (Volatile.Read(ref shuttingDown) != 0) yield break;

				var cachePath = cachePaths[i];
				try {
					var parts = Path.GetFileNameWithoutExtension(cachePath).Split('.');
					if (
						parts.Length != 4 ||
						parts[1].Length != 32 || !parts[1].All(Uri.IsHexDigit) ||
						!TryParseModeName(parts[2], out var mode)
					)
						continue;

					var modeName = GetModeName(mode);
					var isOrphan = orphanKeys.Any(key =>
						string.Equals(parts[0], key.bundleId, StringComparison.OrdinalIgnoreCase) &&
						string.Equals(
							parts[3],
							ComputeTextureId(
								key.bundleName,
								key.assetName,
								parts[1],
								key.textureName,
								modeName
							),
							StringComparison.OrdinalIgnoreCase
						)
					);
					if (!isOrphan) continue;

					var removed = false;
					lock (FILE_LOCK) {
						if (File.Exists(cachePath)) {
							File.Delete(cachePath);
							AdvanceCacheFileGenerationUnsafe(cachePath);
							removed = true;
						}
					}

					if (removed)
						removedCount++;
				} catch (Exception e) {
					Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to remove orphan texture cache '{cachePath}': {e}");
				}

				if ((i + 1) % 32 == 0)
					yield return null;
			}

			if (removedCount > 0)
				Plugin.Logger.LogInfo($"[Symphony::Experimental] Removed {removedCount} orphan texture cache file(s).");
		}

		private static IEnumerator CleanupOutdated() {
			var directory = cacheDirectory;
			if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
				yield break;

			while (
				Volatile.Read(ref shuttingDown) == 0 &&
				AssetBundleManager.AssetBundleManifestObject == null
			) yield return null;

			if (Volatile.Read(ref shuttingDown) != 0) yield break;

			Dictionary<string, string> currentBundleVersions;
			TexturePatchMode currentPatchMode;
			TextureFormatMode currentFormatMode;
			try {
				currentPatchMode = TexturePatchService.CurrentPatchMode;
				currentFormatMode = TexturePatchService.CurrentFormatMode;
				var manifest = AssetBundleManager.AssetBundleManifestObject;
				currentBundleVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				foreach (var name in manifest.GetAllAssetBundles()) {
					var bundleId = ComputeId(name.ToLowerInvariant());
					var version = manifest.GetAssetBundleHash(name).ToString();
					if (
						currentBundleVersions.TryGetValue(bundleId, out var knownVersion) &&
						!string.Equals(knownVersion, version, StringComparison.OrdinalIgnoreCase)
					) throw new InvalidDataException($"Conflicting versions for asset bundle '{name}'.");

					currentBundleVersions[bundleId] = version;
				}
			} catch (Exception e) {
				Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to read current cache settings or AssetBundle versions: {e}");
				yield break;
			}

			string[] cachePaths;
			string[] temporaryPaths;
			try {
				cachePaths = Directory.GetFiles(directory, "*.cache", SearchOption.AllDirectories);
				temporaryPaths = Directory.GetFiles(directory, "*.tmp", SearchOption.AllDirectories);
			} catch (Exception e) {
				Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to enumerate texture caches: {e}");
				yield break;
			}

			var removedCount = 0;
			for (var i = 0; i < cachePaths.Length; i++) {
				var cachePath = cachePaths[i];
				try {
					var removed = false;
					lock (FILE_LOCK) {
						if (!IsCurrentCache(
							cachePath,
							currentBundleVersions,
							currentPatchMode,
							currentFormatMode
						)) {
							File.Delete(cachePath);
							AdvanceCacheFileGenerationUnsafe(cachePath);
							removed = true;
						}
					}
					if (removed)
						removedCount++;
				} catch (Exception e) {
					Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to remove outdated texture cache '{cachePath}': {e}");
				}

				if ((i + 1) % 32 == 0)
					yield return null;
			}

			for (var i = 0; i < temporaryPaths.Length; i++) {
				try {
					if (Compress.IsTemporaryFileActive(temporaryPaths[i]))
						continue;
					if (File.GetLastWriteTimeUtc(temporaryPaths[i]) >= initializedAtUtc)
						continue;

					File.Delete(temporaryPaths[i]);
					removedCount++;
				} catch (Exception e) {
					Plugin.Logger.LogDebug($"[Symphony::Experimental] Deferred temporary cache cleanup for '{temporaryPaths[i]}': {e.Message}");
				}

				if ((i + 1) % 32 == 0)
					yield return null;
			}

			try {
				var directories = Directory
					.GetDirectories(directory, "*", SearchOption.AllDirectories)
					.OrderByDescending(path => path.Length);

				foreach (var subdirectory in directories) {
					if (!Directory.EnumerateFileSystemEntries(subdirectory).Any())
						Directory.Delete(subdirectory);
				}
			} catch (Exception e) {
				Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to remove empty texture cache directories: {e}");
			}

			if (removedCount > 0)
				Plugin.Logger.LogInfo($"[Symphony::Experimental] Removed {removedCount} outdated texture cache file(s).");
		}

		private static bool IsCurrentCache(
			string cachePath,
			Dictionary<string, string> currentBundleVersions,
			TexturePatchMode currentPatchMode,
			TextureFormatMode currentFormatMode
		) {
			var stem = Path.GetFileNameWithoutExtension(cachePath);
			var parts = stem.Split('.');
			if (
				parts.Length != 4 ||
				parts[0].Length != 32 || !parts[0].All(Uri.IsHexDigit) ||
				parts[1].Length != 32 || !parts[1].All(Uri.IsHexDigit) ||
				parts[3].Length != 32 || !parts[3].All(Uri.IsHexDigit) ||
				!TryParseModeName(parts[2], out var mode) ||
				mode != currentPatchMode
			) return false;

			if (
				!currentBundleVersions.TryGetValue(parts[0], out var currentVersion) ||
				!string.Equals(parts[1], currentVersion, StringComparison.OrdinalIgnoreCase)
			) return false;

			try {
				using var stream = new FileStream(
					cachePath,
					FileMode.Open,
					FileAccess.Read,
					FileShare.ReadWrite | FileShare.Delete
				);
				if (stream.Length < CACHE_HEADER_LENGTH) return false;

				using var reader = new BinaryReader(stream);
				if (
					reader.ReadUInt32() != CACHE_MAGIC ||
					reader.ReadInt32() != CACHE_VERSION ||
					reader.ReadInt32() != (int)currentPatchMode
				) return false;

				var width = reader.ReadInt32();
				var height = reader.ReadInt32();
				var mipCount = reader.ReadInt32();
				var format = (TextureFormat)reader.ReadInt32();
				var payloadLength = reader.ReadInt64();
				if (width <= 0 || height <= 0 || mipCount <= 0 || payloadLength <= 0)
					return false;

				var expectedFormat = TexturePayloadData.ResolveFormat(
					currentFormatMode,
					TexturePatchService.CanEncode(width, height)
				);
				return format == expectedFormat;
			} catch (Exception e) {
				// 사용중일 수 있음, 다음 시작 때 알아서 정리되도록 무시
				Plugin.Logger.LogDebug($"[Symphony::Experimental] Deferred cache validation for '{cachePath}': {e.Message}");
				return true;
			}
		}

		private static string GetCachePath(TextureCacheKey key, TexturePatchMode mode) {
			var directory = cacheDirectory;
			if (string.IsNullOrEmpty(directory) || !key.version.isValid) return null;

			var bundleName = (key.bundleName ?? "").ToLowerInvariant();
			var version = key.versionText;
			var modeName = GetModeName(mode);
			var bundleId = ComputeId(bundleName);
			var textureId = ComputeTextureId(
				bundleName,
				key.assetName,
				version,
				key.texName,
				modeName
			);

			return Path.Combine(directory, $"{bundleId}.{version}.{modeName}.{textureId}.cache");
		}

		private static string ComputeTextureId(
			string bundleName,
			string assetName,
			string version,
			string textureName,
			string modeName
		) => ComputeId(string.Concat(
				EncodeKeyPart(bundleName),
				EncodeKeyPart(assetName),
				EncodeKeyPart(version),
				EncodeKeyPart(textureName),
				EncodeKeyPart(modeName)
			));

		private static string EncodeKeyPart(string value) => $"{value.Length}:{value ?? ""}";

		private static string ComputeId(string value) {
			using var md5 = MD5.Create();
			var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""));
			var result = new StringBuilder(hash.Length * 2);
			foreach (var b in hash)
				result.Append(b.ToString("x2"));
			return result.ToString();
		}

		private static string GetModeName(TexturePatchMode mode) => mode switch {
			TexturePatchMode.Simple => "simple",
			TexturePatchMode.Lanczos => "lanczos",
			_ => throw new ArgumentOutOfRangeException(nameof(mode))
		};

		private static bool TryParseModeName(string value, out TexturePatchMode mode) {
			if (string.Equals(value, "simple", StringComparison.OrdinalIgnoreCase)) {
				mode = TexturePatchMode.Simple;
				return true;
			}
			if (string.Equals(value, "lanczos", StringComparison.OrdinalIgnoreCase)) {
				mode = TexturePatchMode.Lanczos;
				return true;
			}

			mode = default;
			return false;
		}

		private static long GetPayloadLength(
			TextureFormat format,
			int width,
			int height,
			int mipCount
		) {
			return TexturePayloadData.GetPayloadLength(format, width, height, mipCount);
		}

		private static unsafe IntPtr GetTextureBuffer(Texture2D target, long expectedPayloadLength, out int bufferLength) {
			if (!TexturePayloadData.IsSupportedFormat(target.format))
				throw new InvalidDataException("Texture cache target is not BC3/DXT5 or BC7 or RGBA32.");

			var buffer = target.GetRawTextureData<byte>();

			bufferLength = buffer.Length;
			if (bufferLength != expectedPayloadLength)
				throw new InvalidDataException("Texture raw data length does not match the expected cache payload length.");

			return (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(buffer);
		}

		private static unsafe void ReadCache(
			string cachePath,
			TexturePatchMode expectedMode,
			int expectedWidth,
			int expectedHeight,
			int expectedMipCount,
			TextureFormat expectedFormat,
			long expectedPayloadLength,
			IntPtr destinationPointer,
			int destinationLength,
			out long cacheGeneration
		) {
			cacheGeneration = 0;
			Compress.BeginCacheActivity();
			try {
				using var stream = OpenCacheReadStream(cachePath, out cacheGeneration);
				using var reader = new BinaryReader(stream);

				if (reader.ReadUInt32() != CACHE_MAGIC)
					throw new InvalidDataException("Invalid texture cache magic.");
				if (reader.ReadInt32() != CACHE_VERSION)
					throw new InvalidDataException("Unsupported texture cache version.");
				if (reader.ReadInt32() != (int)expectedMode)
					throw new InvalidDataException("Texture cache mode does not match.");
				if (reader.ReadInt32() != expectedWidth || reader.ReadInt32() != expectedHeight)
					throw new InvalidDataException("Texture cache dimensions do not match.");
				if (reader.ReadInt32() != expectedMipCount)
					throw new InvalidDataException("Texture cache mip count does not match.");
				if (reader.ReadInt32() != (int)expectedFormat)
					throw new InvalidDataException("Texture cache format does not match.");

				var payloadLength = reader.ReadInt64();
				if (payloadLength != expectedPayloadLength || payloadLength != destinationLength)
					throw new InvalidDataException("Texture cache payload length does not match.");

				var payloadOffset = stream.Position;
				var storedLength = stream.Length - payloadOffset;
				var highCompressionEvaluated = Compress.Read(
					stream,
					storedLength,
					payloadLength,
					destinationPointer,
					destinationLength
				);
				if (!highCompressionEvaluated)
					Compress.QueueHighCompression(
						cachePath,
						payloadOffset,
						payloadLength,
						cacheGeneration
					);
			} finally {
				Compress.EndCacheActivity();
			}
		}

		private static void TryWriteCache(
			string cachePath,
			TexturePatchMode mode,
			int width,
			int height,
			int mipCount,
			TextureFormat format,
			Action<Stream> writePayload
		) {
			if (string.IsNullOrEmpty(cachePath) || File.Exists(cachePath)) return;

			var temporaryPath = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
			Compress.RegisterTemporaryFile(temporaryPath);
			Compress.BeginCacheActivity();
			try {
				Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
				var payloadLength = GetPayloadLength(format, width, height, mipCount);
				long payloadOffset;
				using (var stream = new FileStream(
					temporaryPath,
					FileMode.CreateNew,
					FileAccess.Write,
					FileShare.None,
					1024 * 1024,
					FileOptions.SequentialScan
				))
				using (var writer = new BinaryWriter(stream)) {
					writer.Write(CACHE_MAGIC);
					writer.Write(CACHE_VERSION);
					writer.Write((int)mode);
					writer.Write(width);
					writer.Write(height);
					writer.Write(mipCount);
					writer.Write((int)format);
					writer.Write(payloadLength);
					writer.Flush();

					payloadOffset = stream.Position;
					Compress.WriteFast(stream, payloadLength, writePayload);

					stream.Flush(flushToDisk: true);
				}

				try {
					var generation = PublishCacheFile(temporaryPath, cachePath);
					Plugin.Logger.LogDebug($"[Symphony::Experimental] Texture cache saved: {cachePath}");
					Compress.QueueHighCompression(
						cachePath,
						payloadOffset,
						payloadLength,
						generation
					);
				} catch (IOException) when (File.Exists(cachePath)) {
					// 이미 캐시됨 (아마도 다른 요청에서 겹침)
				}
			} catch (Exception e) {
				Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to write texture cache '{cachePath}': {e}");
			} finally {
				try {
					if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
				} catch (Exception e) {
					Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to remove temporary texture cache '{temporaryPath}': {e}");
				}
				Compress.UnregisterTemporaryFile(temporaryPath);
				Compress.EndCacheActivity();
			}
		}

		private static unsafe void WritePointer(Stream stream, IntPtr pointer, int length) =>
			stream.Write(new ReadOnlySpan<byte>((void*)pointer, length));

		private static void LogInvalidCache(string cachePath, Exception error) =>
			Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to load texture cache '{cachePath}', rebuilding it: {error}");

		private static void DeleteInvalidCache(string cachePath, long expectedGeneration) {
			try {
				lock (FILE_LOCK) {
					if (GetCacheFileGenerationUnsafe(cachePath) != expectedGeneration) return;
					File.Delete(cachePath);
					AdvanceCacheFileGenerationUnsafe(cachePath);
				}
			} catch (Exception e) {
				Plugin.Logger.LogWarning($"[Symphony::Experimental] Failed to remove invalid texture cache '{cachePath}': {e}");
			}
		}

		private static FileStream OpenCacheReadStream(
			string cachePath,
			out long cacheGeneration
		) {
			lock (FILE_LOCK) {
				cacheGeneration = GetCacheFileGenerationUnsafe(cachePath);
				return new FileStream(
					cachePath,
					FileMode.Open,
					FileAccess.Read,
					FileShare.Read | FileShare.Delete,
					1024 * 1024,
					FileOptions.SequentialScan
				);
			}
		}

		private static long GetCacheFileGenerationUnsafe(string cachePath) =>
			FILE_GENERATIONS.TryGetValue(cachePath, out var generation) ? generation : 0;

		private static long AdvanceCacheFileGenerationUnsafe(string cachePath) {
			var generation = checked(GetCacheFileGenerationUnsafe(cachePath) + 1);
			FILE_GENERATIONS[cachePath] = generation;
			return generation;
		}

		private static long PublishCacheFile(string temporaryPath, string cachePath) {
			lock (FILE_LOCK) {
				File.Move(temporaryPath, cachePath);
				return AdvanceCacheFileGenerationUnsafe(cachePath);
			}
		}

		internal static bool TryReplaceCacheFile(
			string cachePath,
			string temporaryPath,
			long expectedGeneration,
			long expectedLength,
			out long replacedGeneration
		) {
			lock (FILE_LOCK) {
				replacedGeneration = GetCacheFileGenerationUnsafe(cachePath);
				if (replacedGeneration != expectedGeneration)
					return false;

				var cacheInfo = new FileInfo(cachePath);
				if (!cacheInfo.Exists || cacheInfo.Length != expectedLength)
					return false;

				File.Replace(temporaryPath, cachePath, destinationBackupFileName: null);
				replacedGeneration = AdvanceCacheFileGenerationUnsafe(cachePath);
				return true;
			}
		}

		internal static bool TryMarkHighCompressionEvaluated(
			string cachePath,
			long payloadOffset,
			long payloadLength,
			long expectedGeneration,
			long expectedLength,
			out long markedGeneration
		) {
			lock (FILE_LOCK) {
				markedGeneration = GetCacheFileGenerationUnsafe(cachePath);
				if (markedGeneration != expectedGeneration)
					return false;

				var cacheInfo = new FileInfo(cachePath);
				if (!cacheInfo.Exists || cacheInfo.Length != expectedLength)
					return false;

				if (Compress.TryMarkHighCompressionEvaluated(cachePath, payloadOffset, payloadLength))
					markedGeneration = AdvanceCacheFileGenerationUnsafe(cachePath);
				return true;
			}
		}
	}
}
