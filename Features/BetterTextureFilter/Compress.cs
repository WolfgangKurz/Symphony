using Symphony.Utils;

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Symphony.Features.BetterTextureFilter {
	internal enum CacheRecompressionStatus {
		CandidateCreated,
		BelowMinimumSize,
		AlreadyEvaluated,
		NotWorthwhile
	}

	internal readonly struct CacheRecompressionResult {
		internal CacheRecompressionStatus Status { get; }
		internal long OriginalLength { get; }
		internal long CandidateLength { get; }
		internal long BytesSaved => Math.Max(0, OriginalLength - CandidateLength);
		internal bool ShouldReplace => Status == CacheRecompressionStatus.CandidateCreated;

		internal CacheRecompressionResult(
			CacheRecompressionStatus status,
			long originalLength,
			long candidateLength
		) {
			Status = status;
			OriginalLength = originalLength;
			CandidateLength = candidateLength;
		}
	}

	internal static class Compress {
		private const int RECOMPRESSION_IDLE_MILLISECONDS = 10 * 1000;
		private const int MAX_RECOMPRESSION_ATTEMPTS = 3;
		private const uint COMPRESS_MAGIC = 0x34435A4C; // LZC4
		private const int COMPRESS_VERSION = 1;
		private const int FLAG_HIGH_COMPRESSION_EVALUATED = 1;
		private const int KNOWN_FLAGS = FLAG_HIGH_COMPRESSION_EVALUATED;
		private const int FLAGS_OFFSET = sizeof(uint) + sizeof(int) * 2;
		private const int HEADER_LENGTH = sizeof(uint) + sizeof(int) * 5 + sizeof(long);
		private const int BLOCK_HEADER_LENGTH = sizeof(int) * 3;
		private const int BLOCK_KIND_RAW = 0;
		private const int BLOCK_KIND_LZ4 = 1;
		private const int COPY_BUFFER_SIZE = 64 * 1024;
		private const long MINIMUM_SAVINGS = 256L * 1024;

		internal const int BLOCK_SIZE = 1024 * 1024;
		internal const long HIGH_COMPRESSION_MINIMUM_LENGTH = 4L * 1024 * 1024;

		private static readonly object WORKER_LOCK = new();
		private static readonly Queue<RecompressionWork> RECOMPRESSION_QUEUE = new();
		private static readonly Dictionary<string, long> QUEUED_GENERATIONS = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, long> EVALUATED_GENERATIONS = new(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> ACTIVE_TEMPORARY_PATHS = new(StringComparer.OrdinalIgnoreCase);
		private static readonly AutoResetEvent WORKER_SIGNAL = new(initialState: false);

		private static Thread workerThread;
		private static CancellationTokenSource activeCancellation;
		private static long lastActivityTimestamp;
		private static int activeCacheActivities;
		private static int initialized;
		private static int stopping;

		private sealed class RecompressionWork {
			internal readonly string CachePath;
			internal readonly long PayloadOffset;
			internal readonly long PayloadLength;
			internal readonly long Generation;
			internal int Attempts;

			internal RecompressionWork(
				string cachePath,
				long payloadOffset,
				long payloadLength,
				long generation
			) {
				CachePath = cachePath;
				PayloadOffset = payloadOffset;
				PayloadLength = payloadLength;
				Generation = generation;
			}
		}

		private readonly struct CompressionHeader {
			internal LZ4CompressionLevel Level { get; }
			internal int Flags { get; }
			internal long UncompressedLength { get; }
			internal int BlockCount { get; }

			internal bool IsHighCompressionEvaluated =>
				(Flags & FLAG_HIGH_COMPRESSION_EVALUATED) != 0;

			internal CompressionHeader(
				LZ4CompressionLevel level,
				int flags,
				long uncompressedLength,
				int blockCount
			) {
				Level = level;
				Flags = flags;
				UncompressedLength = uncompressedLength;
				BlockCount = blockCount;
			}
		}

		internal static void Initialize() {
			if (Interlocked.CompareExchange(ref initialized, 1, 0) != 0) return;

			lastActivityTimestamp = Stopwatch.GetTimestamp();
			workerThread = new Thread(ProcessRecompressionQueue) {
				IsBackground = true,
				Name = "Symphony Texture Cache Recompression",
				Priority = ThreadPriority.BelowNormal
			};
			workerThread.Start();
		}

		internal static void Shutdown() {
			if (Interlocked.Exchange(ref stopping, 1) != 0) return;

			lock (WORKER_LOCK) {
				RECOMPRESSION_QUEUE.Clear();
				QUEUED_GENERATIONS.Clear();
				activeCancellation?.Cancel();
			}
			WORKER_SIGNAL.Set();

			var thread = workerThread;
			if (thread != null && thread != Thread.CurrentThread)
				thread.Join();
		}

		internal static void BeginCacheActivity() {
			lock (WORKER_LOCK) {
				activeCacheActivities++;
				lastActivityTimestamp = Stopwatch.GetTimestamp();
				activeCancellation?.Cancel();
			}
			WORKER_SIGNAL.Set();
		}

		internal static void EndCacheActivity() {
			lock (WORKER_LOCK) {
				if (activeCacheActivities <= 0)
					throw new InvalidOperationException("Texture cache activity counter is unbalanced.");

				activeCacheActivities--;
				lastActivityTimestamp = Stopwatch.GetTimestamp();
			}
			WORKER_SIGNAL.Set();
		}

		internal static void QueueHighCompression(
			string cachePath,
			long payloadOffset,
			long payloadLength,
			long generation
		) {
			if (
				Volatile.Read(ref stopping) != 0 ||
				string.IsNullOrEmpty(cachePath) ||
				!IsHighCompressionCandidate(payloadLength)
			) return;

			lock (WORKER_LOCK) {
				if (Volatile.Read(ref stopping) != 0) return;
				if (
					EVALUATED_GENERATIONS.TryGetValue(cachePath, out var evaluatedGeneration) &&
					evaluatedGeneration >= generation
				) return;
				if (
					QUEUED_GENERATIONS.TryGetValue(cachePath, out var queuedGeneration) &&
					queuedGeneration >= generation
				) return;

				QUEUED_GENERATIONS[cachePath] = generation;
				RECOMPRESSION_QUEUE.Enqueue(
					new RecompressionWork(cachePath, payloadOffset, payloadLength, generation)
				);
			}
			WORKER_SIGNAL.Set();
		}

		internal static bool IsTemporaryFileActive(string path) {
			if (string.IsNullOrEmpty(path)) return false;
			lock (WORKER_LOCK)
				return ACTIVE_TEMPORARY_PATHS.Contains(path);
		}

		internal static void RegisterTemporaryFile(string path) {
			if (string.IsNullOrEmpty(path)) throw new ArgumentException("Temporary path is required.", nameof(path));
			lock (WORKER_LOCK)
				ACTIVE_TEMPORARY_PATHS.Add(path);
		}

		internal static void UnregisterTemporaryFile(string path) {
			if (string.IsNullOrEmpty(path)) return;
			lock (WORKER_LOCK)
				ACTIVE_TEMPORARY_PATHS.Remove(path);
		}

		private static void ProcessRecompressionQueue() {
			while (Volatile.Read(ref stopping) == 0) {
				var work = TryTakeWork(out var waitMilliseconds, out var cancellation);
				if (work == null) {
					WORKER_SIGNAL.WaitOne(waitMilliseconds);
					continue;
				}

				ProcessRecompressionWork(work, cancellation);
			}
		}

		private static RecompressionWork TryTakeWork(
			out int waitMilliseconds,
			out CancellationTokenSource cancellation
		) {
			lock (WORKER_LOCK) {
				waitMilliseconds = Timeout.Infinite;
				cancellation = null;
				if (Volatile.Read(ref stopping) != 0 || RECOMPRESSION_QUEUE.Count == 0)
					return null;
				if (activeCacheActivities != 0)
					return null;

				var elapsedMilliseconds = GetElapsedMilliseconds(lastActivityTimestamp);
				if (elapsedMilliseconds < RECOMPRESSION_IDLE_MILLISECONDS) {
					waitMilliseconds = Math.Max(
						1,
						RECOMPRESSION_IDLE_MILLISECONDS - elapsedMilliseconds
					);
					return null;
				}

				while (RECOMPRESSION_QUEUE.Count != 0) {
					var work = RECOMPRESSION_QUEUE.Dequeue();
					if (
						QUEUED_GENERATIONS.TryGetValue(work.CachePath, out var generation) &&
						generation == work.Generation
					) {
						cancellation = new CancellationTokenSource();
						activeCancellation = cancellation;
						return work;
					}
				}

				return null;
			}
		}

		private static int GetElapsedMilliseconds(long startedAt) {
			var elapsedTicks = Math.Max(0, Stopwatch.GetTimestamp() - startedAt);
			var milliseconds = elapsedTicks * 1000L / Stopwatch.Frequency;
			return milliseconds > int.MaxValue ? int.MaxValue : (int)milliseconds;
		}

		private static void ProcessRecompressionWork(
			RecompressionWork work,
			CancellationTokenSource cancellation
		) {
			var enteredBackgroundOperation = false;
			var retry = false;
			var evaluated = false;
			var evaluatedGeneration = work.Generation;
			var temporaryPath = work.CachePath + "." + Guid.NewGuid().ToString("N") + ".hc.tmp";
			try {
				if (!Cache.TryEnterBackgroundOperation()) return;
				enteredBackgroundOperation = true;

				RegisterTemporaryFile(temporaryPath);

				var result = TryCreateHighCompressionFile(
					work.CachePath,
					temporaryPath,
					work.PayloadOffset,
					work.PayloadLength,
					cancellation.Token
				);
				cancellation.Token.ThrowIfCancellationRequested();

				if (result.ShouldReplace) {
					evaluated = Cache.TryReplaceCacheFile(
						work.CachePath,
						temporaryPath,
						work.Generation,
						result.OriginalLength,
						out evaluatedGeneration
					);
				} else if (result.Status == CacheRecompressionStatus.NotWorthwhile) {
					evaluated = Cache.TryMarkHighCompressionEvaluated(
						work.CachePath,
						work.PayloadOffset,
						work.PayloadLength,
						work.Generation,
						result.OriginalLength,
						out evaluatedGeneration
					);
				} else {
					evaluated = true;
				}
			} catch (OperationCanceledException) {
				retry = Volatile.Read(ref stopping) == 0;
			} catch (Exception exception) {
				work.Attempts++;
				retry = work.Attempts < MAX_RECOMPRESSION_ATTEMPTS;
				Plugin.Logger.LogDebug(
					$"[Symphony::Experimental] Deferred texture cache recompression for '{work.CachePath}': {exception.Message}"
				);
			} finally {
				TryDelete(temporaryPath);
				lock (WORKER_LOCK) {
					ACTIVE_TEMPORARY_PATHS.Remove(temporaryPath);
					if (ReferenceEquals(activeCancellation, cancellation))
						activeCancellation = null;

					if (retry && Volatile.Read(ref stopping) == 0) {
						lastActivityTimestamp = Stopwatch.GetTimestamp();
						RECOMPRESSION_QUEUE.Enqueue(work);
					} else {
						if (
							QUEUED_GENERATIONS.TryGetValue(work.CachePath, out var generation) &&
							generation == work.Generation
						) QUEUED_GENERATIONS.Remove(work.CachePath);
						if (evaluated)
							EVALUATED_GENERATIONS[work.CachePath] = evaluatedGeneration;
					}
				}
				cancellation.Dispose();
				if (enteredBackgroundOperation)
					Cache.ExitBackgroundOperation();
				WORKER_SIGNAL.Set();
			}
		}

		internal static void WriteFast(
			Stream destination,
			long uncompressedLength,
			Action<Stream> writePayload
		) {
			if (destination == null) throw new ArgumentNullException(nameof(destination));
			if (!destination.CanWrite) throw new ArgumentException("Destination stream is not writable.", nameof(destination));
			if (writePayload == null) throw new ArgumentNullException(nameof(writePayload));

			WriteContainer(
				destination,
				uncompressedLength,
				LZ4CompressionLevel.Fast,
				flags: 0,
				writePayload
			);
		}

		internal static unsafe bool Read(
			Stream source,
			long storedLength,
			long expectedLength,
			IntPtr destinationPointer,
			int destinationLength
		) {
			if (destinationPointer == IntPtr.Zero && destinationLength != 0)
				throw new ArgumentNullException(nameof(destinationPointer));
			if (expectedLength != destinationLength)
				throw new InvalidDataException("Compressed cache destination length does not match the expected payload length.");

			return Read(
				source,
				storedLength,
				expectedLength,
				new Span<byte>((void*)destinationPointer, destinationLength)
			);
		}

		internal static bool Read(
			Stream source,
			long storedLength,
			long expectedLength,
			Span<byte> destination
		) {
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (!source.CanRead) throw new ArgumentException("Source stream is not readable.", nameof(source));
			if (expectedLength != destination.Length)
				throw new InvalidDataException("Compressed cache destination length does not match the expected payload length.");

			var reader = new BlockReader(source, storedLength, expectedLength);
			var encodedBuffer = ArrayPool<byte>.Shared.Rent(LZ4.MaximumOutputSize(BLOCK_SIZE));
			try {
				var offset = 0;
				for (var i = 0; i < reader.Header.BlockCount; i++) {
					var length = reader.ReadBlock(destination.Slice(offset), encodedBuffer);
					offset = checked(offset + length);
				}

				if (offset != destination.Length)
					throw new InvalidDataException("Compressed cache produced an unexpected payload length.");

				reader.Complete();
				return reader.Header.IsHighCompressionEvaluated;
			} finally {
				ArrayPool<byte>.Shared.Return(encodedBuffer);
			}
		}

		internal static CacheRecompressionResult TryCreateHighCompressionFile(
			string sourcePath,
			string temporaryPath,
			long payloadOffset,
			long expectedPayloadLength,
			CancellationToken cancellationToken = default
		) {
			if (string.IsNullOrEmpty(sourcePath)) throw new ArgumentException("Source path is required.", nameof(sourcePath));
			if (string.IsNullOrEmpty(temporaryPath)) throw new ArgumentException("Temporary path is required.", nameof(temporaryPath));
			if (payloadOffset < 0) throw new ArgumentOutOfRangeException(nameof(payloadOffset));
			if (expectedPayloadLength < 0) throw new ArgumentOutOfRangeException(nameof(expectedPayloadLength));
			if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(temporaryPath), StringComparison.OrdinalIgnoreCase))
				throw new ArgumentException("Temporary path must differ from the source path.", nameof(temporaryPath));

			if (!IsHighCompressionCandidate(expectedPayloadLength)) {
				var sourceLength = GetFileLength(sourcePath);
				return new CacheRecompressionResult(
					CacheRecompressionStatus.BelowMinimumSize,
					sourceLength,
					sourceLength
				);
			}

			var temporaryFileCreated = false;
			var keepTemporaryFile = false;
			try {
				using var source = new FileStream(
					sourcePath,
					FileMode.Open,
					FileAccess.Read,
					FileShare.Read | FileShare.Delete,
					BLOCK_SIZE,
					FileOptions.SequentialScan
				);
				var originalLength = source.Length;
				var storedLength = GetStoredLength(originalLength, payloadOffset);

				source.Position = payloadOffset;
				var inspection = new BlockReader(source, storedLength, expectedPayloadLength);
				if (inspection.Header.IsHighCompressionEvaluated)
					return new CacheRecompressionResult(
						CacheRecompressionStatus.AlreadyEvaluated,
						originalLength,
						originalLength
					);

				if (inspection.Header.Level != LZ4CompressionLevel.Fast)
					throw new InvalidDataException("Only a Fast LZ4 cache can be recompressed.");

				cancellationToken.ThrowIfCancellationRequested();
				var temporaryDirectory = Path.GetDirectoryName(temporaryPath);
				if (!string.IsNullOrEmpty(temporaryDirectory))
					Directory.CreateDirectory(temporaryDirectory);

				source.Position = 0;
				long candidateLength;
				using (var destination = new FileStream(
					temporaryPath,
					FileMode.CreateNew,
					FileAccess.Write,
					FileShare.None,
					BLOCK_SIZE,
					FileOptions.SequentialScan
				)) {
					temporaryFileCreated = true;
					CopyExactly(source, destination, payloadOffset, cancellationToken);
					var reader = new BlockReader(source, storedLength, expectedPayloadLength);

					WriteHeader(
						destination,
						LZ4CompressionLevel.HC4,
						FLAG_HIGH_COMPRESSION_EVALUATED,
						expectedPayloadLength
					);

					var rawBuffer = ArrayPool<byte>.Shared.Rent(BLOCK_SIZE);
					var encodedBuffer = ArrayPool<byte>.Shared.Rent(LZ4.MaximumOutputSize(BLOCK_SIZE));
					try {
						for (var i = 0; i < reader.Header.BlockCount; i++) {
							cancellationToken.ThrowIfCancellationRequested();
							var rawLength = reader.ReadBlock(rawBuffer, encodedBuffer);
							WriteBlock(
								destination,
								rawBuffer.AsSpan(0, rawLength),
								encodedBuffer,
								LZ4CompressionLevel.HC4
							);
							cancellationToken.ThrowIfCancellationRequested();
						}

						reader.Complete();
					} finally {
						ArrayPool<byte>.Shared.Return(encodedBuffer);
						ArrayPool<byte>.Shared.Return(rawBuffer);
					}

					destination.Flush(flushToDisk: true);
					candidateLength = destination.Length;
				}

				if (!IsMeaningfulSavings(originalLength, candidateLength))
					return new CacheRecompressionResult(
						CacheRecompressionStatus.NotWorthwhile,
						originalLength,
						candidateLength
					);

				keepTemporaryFile = true;
				return new CacheRecompressionResult(
					CacheRecompressionStatus.CandidateCreated,
					originalLength,
					candidateLength
				);
			} finally {
				if (temporaryFileCreated && !keepTemporaryFile)
					TryDelete(temporaryPath);
			}
		}

		internal static bool TryMarkHighCompressionEvaluated(
			string cachePath,
			long payloadOffset,
			long expectedPayloadLength
		) {
			if (string.IsNullOrEmpty(cachePath)) throw new ArgumentException("Cache path is required.", nameof(cachePath));
			if (payloadOffset < 0) throw new ArgumentOutOfRangeException(nameof(payloadOffset));
			if (expectedPayloadLength < 0) throw new ArgumentOutOfRangeException(nameof(expectedPayloadLength));

			using var stream = new FileStream(
				cachePath,
				FileMode.Open,
				FileAccess.ReadWrite,
				FileShare.Read | FileShare.Delete
			);
			var storedLength = GetStoredLength(stream.Length, payloadOffset);
			stream.Position = payloadOffset;
			var reader = new BlockReader(stream, storedLength, expectedPayloadLength);

			if (reader.Header.IsHighCompressionEvaluated)
				return false;
			if (reader.Header.Level != LZ4CompressionLevel.Fast)
				throw new InvalidDataException("Only a Fast LZ4 cache can be marked as evaluated.");

			stream.Position = checked(payloadOffset + FLAGS_OFFSET);
			WriteInt32(stream, reader.Header.Flags | FLAG_HIGH_COMPRESSION_EVALUATED);
			stream.Flush(flushToDisk: true);
			return true;
		}

		internal static bool IsHighCompressionCandidate(long uncompressedLength) =>
			uncompressedLength >= HIGH_COMPRESSION_MINIMUM_LENGTH;

		internal static bool IsMeaningfulSavings(long originalLength, long candidateLength) {
			if (originalLength <= 0) throw new ArgumentOutOfRangeException(nameof(originalLength));
			if (candidateLength < 0) throw new ArgumentOutOfRangeException(nameof(candidateLength));
			if (candidateLength >= originalLength) return false;

			var percentageSavings = originalLength / 20;
			if (originalLength % 20 != 0)
				percentageSavings++;

			return originalLength - candidateLength >= Math.Max(MINIMUM_SAVINGS, percentageSavings);
		}

		private static void WriteContainer(
			Stream destination,
			long uncompressedLength,
			LZ4CompressionLevel level,
			int flags,
			Action<Stream> writePayload
		) {
			ValidateUncompressedLength(uncompressedLength);
			WriteHeader(destination, level, flags, uncompressedLength);

			using var encoder = new BlockCompressionStream(destination, uncompressedLength, level);
			writePayload(encoder);
			encoder.Complete();
		}

		private static void WriteHeader(
			Stream destination,
			LZ4CompressionLevel level,
			int flags,
			long uncompressedLength
		) {
			ValidateLevelAndFlags(level, flags);
			var blockCount = GetBlockCount(uncompressedLength);

			WriteUInt32(destination, COMPRESS_MAGIC);
			WriteInt32(destination, COMPRESS_VERSION);
			WriteInt32(destination, (int)level);
			WriteInt32(destination, flags);
			WriteInt32(destination, BLOCK_SIZE);
			WriteInt64(destination, uncompressedLength);
			WriteInt32(destination, blockCount);
		}

		private static void WriteBlock(
			Stream destination,
			ReadOnlySpan<byte> source,
			byte[] encodedBuffer,
			LZ4CompressionLevel level
		) {
			if (source.Length <= 0 || source.Length > BLOCK_SIZE)
				throw new ArgumentOutOfRangeException(nameof(source));

			var maximumEncodedLength = LZ4.MaximumOutputSize(source.Length);
			if (encodedBuffer == null || encodedBuffer.Length < maximumEncodedLength)
				throw new ArgumentException("Encoded block buffer is too small.", nameof(encodedBuffer));

			var encodedLength = LZ4.Encode(
				source,
				encodedBuffer.AsSpan(0, maximumEncodedLength),
				level
			);
			if (encodedLength <= 0 || encodedLength >= source.Length) {
				WriteInt32(destination, BLOCK_KIND_RAW);
				WriteInt32(destination, source.Length);
				WriteInt32(destination, source.Length);
				destination.Write(source);
				return;
			}

			WriteInt32(destination, BLOCK_KIND_LZ4);
			WriteInt32(destination, source.Length);
			WriteInt32(destination, encodedLength);
			destination.Write(encodedBuffer.AsSpan(0, encodedLength));
		}

		private static int GetBlockCount(long uncompressedLength) {
			ValidateUncompressedLength(uncompressedLength);
			var count = uncompressedLength / BLOCK_SIZE;
			if (uncompressedLength % BLOCK_SIZE != 0)
				count++;
			if (count > int.MaxValue)
				throw new ArgumentOutOfRangeException(nameof(uncompressedLength));

			return (int)count;
		}

		private static void ValidateUncompressedLength(long uncompressedLength) {
			if (uncompressedLength < 0)
				throw new ArgumentOutOfRangeException(nameof(uncompressedLength));
		}

		private static void ValidateLevelAndFlags(LZ4CompressionLevel level, int flags) {
			if (level != LZ4CompressionLevel.Fast && level != LZ4CompressionLevel.HC4)
				throw new InvalidDataException("Unsupported texture cache compression level.");
			if ((flags & ~KNOWN_FLAGS) != 0)
				throw new InvalidDataException("Unsupported texture cache compression flags.");
			if (level == LZ4CompressionLevel.HC4 && (flags & FLAG_HIGH_COMPRESSION_EVALUATED) == 0)
				throw new InvalidDataException("A high-compression cache must be marked as evaluated.");
		}

		private static long GetStoredLength(long fileLength, long payloadOffset) {
			if (payloadOffset > fileLength || fileLength - payloadOffset < HEADER_LENGTH)
				throw new InvalidDataException("Compressed cache payload is incomplete.");

			return fileLength - payloadOffset;
		}

		private static long GetFileLength(string path) {
			var info = new FileInfo(path);
			return info.Length;
		}

		private static void CopyExactly(
			Stream source,
			Stream destination,
			long length,
			CancellationToken cancellationToken
		) {
			var buffer = ArrayPool<byte>.Shared.Rent(COPY_BUFFER_SIZE);
			try {
				while (length > 0) {
					cancellationToken.ThrowIfCancellationRequested();
					var requested = (int)Math.Min(buffer.Length, length);
					var read = source.Read(buffer, 0, requested);
					if (read == 0) throw new EndOfStreamException();
					destination.Write(buffer, 0, read);
					length -= read;
				}
			} finally {
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}

		private static void TryDelete(string path) {
			try {
				if (File.Exists(path)) File.Delete(path);
			} catch {
				// Cache cleanup retries stale temporary files on the next launch.
			}
		}

		private static void WriteUInt32(Stream destination, uint value) {
			Span<byte> buffer = stackalloc byte[sizeof(uint)];
			BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
			destination.Write(buffer);
		}

		private static void WriteInt32(Stream destination, int value) {
			Span<byte> buffer = stackalloc byte[sizeof(int)];
			BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
			destination.Write(buffer);
		}

		private static void WriteInt64(Stream destination, long value) {
			Span<byte> buffer = stackalloc byte[sizeof(long)];
			BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
			destination.Write(buffer);
		}

		private sealed class BlockReader {
			private readonly Stream source;
			private long storedRemaining;
			private long uncompressedRemaining;
			private int blockIndex;

			internal CompressionHeader Header { get; }

			internal BlockReader(Stream source, long storedLength, long expectedLength) {
				if (source == null) throw new ArgumentNullException(nameof(source));
				if (!source.CanRead) throw new ArgumentException("Source stream is not readable.", nameof(source));
				if (storedLength < HEADER_LENGTH)
					throw new InvalidDataException("Compressed cache payload is incomplete.");
				ValidateUncompressedLength(expectedLength);

				this.source = source;
				storedRemaining = storedLength;
				Header = ReadHeader(expectedLength);
				uncompressedRemaining = expectedLength;
			}

			internal int ReadBlock(Span<byte> destination, byte[] encodedBuffer) {
				if (blockIndex >= Header.BlockCount)
					throw new InvalidDataException("Compressed cache contains too many blocks.");

				var expectedRawLength = (int)Math.Min(BLOCK_SIZE, uncompressedRemaining);
				var kind = ReadInt32();
				var rawLength = ReadInt32();
				var storedLength = ReadInt32();

				if (rawLength != expectedRawLength || rawLength <= 0 || rawLength > BLOCK_SIZE)
					throw new InvalidDataException("Compressed cache block length does not match.");
				if (destination.Length < rawLength)
					throw new InvalidDataException("Compressed cache block exceeds the destination buffer.");

				if (kind == BLOCK_KIND_RAW) {
					if (storedLength != rawLength)
						throw new InvalidDataException("Raw cache block length does not match.");

					ReadExactly(destination.Slice(0, rawLength));
				} else if (kind == BLOCK_KIND_LZ4) {
					var maximumEncodedLength = LZ4.MaximumOutputSize(rawLength);
					if (storedLength <= 0 || storedLength >= rawLength || storedLength > maximumEncodedLength)
						throw new InvalidDataException("Compressed cache block has an invalid stored length.");
					if (encodedBuffer == null || encodedBuffer.Length < storedLength)
						throw new InvalidDataException("Compressed cache block exceeds the decode buffer.");

					ReadExactly(encodedBuffer.AsSpan(0, storedLength));
					var decodedLength = LZ4.Decode(
						encodedBuffer.AsSpan(0, storedLength),
						destination.Slice(0, rawLength)
					);
					if (decodedLength != rawLength)
						throw new InvalidDataException("Compressed cache block decoded to an unexpected length.");
				} else {
					throw new InvalidDataException("Compressed cache block has an unsupported encoding.");
				}

				blockIndex++;
				uncompressedRemaining -= rawLength;
				return rawLength;
			}

			internal void Complete() {
				if (
					blockIndex != Header.BlockCount ||
					uncompressedRemaining != 0 ||
					storedRemaining != 0
				) throw new InvalidDataException("Compressed cache payload has an unexpected length.");
			}

			private CompressionHeader ReadHeader(long expectedLength) {
				if (ReadUInt32() != COMPRESS_MAGIC)
					throw new InvalidDataException("Invalid compressed cache magic.");
				if (ReadInt32() != COMPRESS_VERSION)
					throw new InvalidDataException("Unsupported compressed cache version.");

				var level = (LZ4CompressionLevel)ReadInt32();
				var flags = ReadInt32();
				ValidateLevelAndFlags(level, flags);
				if (ReadInt32() != BLOCK_SIZE)
					throw new InvalidDataException("Compressed cache block size does not match.");

				var uncompressedLength = ReadInt64();
				if (uncompressedLength != expectedLength)
					throw new InvalidDataException("Compressed cache payload length does not match.");

				var blockCount = ReadInt32();
				if (blockCount != GetBlockCount(uncompressedLength))
					throw new InvalidDataException("Compressed cache block count does not match.");

				var minimumStoredLength = checked((long)blockCount * BLOCK_HEADER_LENGTH);
				if (storedRemaining < minimumStoredLength)
					throw new InvalidDataException("Compressed cache block table is incomplete.");

				return new CompressionHeader(level, flags, uncompressedLength, blockCount);
			}

			private uint ReadUInt32() {
				Span<byte> buffer = stackalloc byte[sizeof(uint)];
				ReadExactly(buffer);
				return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
			}

			private int ReadInt32() {
				Span<byte> buffer = stackalloc byte[sizeof(int)];
				ReadExactly(buffer);
				return BinaryPrimitives.ReadInt32LittleEndian(buffer);
			}

			private long ReadInt64() {
				Span<byte> buffer = stackalloc byte[sizeof(long)];
				ReadExactly(buffer);
				return BinaryPrimitives.ReadInt64LittleEndian(buffer);
			}

			private void ReadExactly(Span<byte> destination) {
				if (destination.Length > storedRemaining)
					throw new EndOfStreamException();

				var read = 0;
				while (read < destination.Length) {
					var count = source.Read(destination.Slice(read));
					if (count == 0) throw new EndOfStreamException();
					read += count;
				}

				storedRemaining -= destination.Length;
			}
		}

		private sealed class BlockCompressionStream : Stream {
			private readonly Stream destination;
			private readonly long expectedLength;
			private readonly int expectedBlockCount;
			private readonly LZ4CompressionLevel level;
			private byte[] rawBuffer;
			private byte[] encodedBuffer;
			private long written;
			private int buffered;
			private int blockCount;
			private bool completed;

			internal BlockCompressionStream(Stream destination, long expectedLength, LZ4CompressionLevel level) {
				this.destination = destination;
				this.expectedLength = expectedLength;
				this.level = level;
				expectedBlockCount = GetBlockCount(expectedLength);
				rawBuffer = ArrayPool<byte>.Shared.Rent(BLOCK_SIZE);
				encodedBuffer = ArrayPool<byte>.Shared.Rent(LZ4.MaximumOutputSize(BLOCK_SIZE));
			}

			public override bool CanRead => false;
			public override bool CanSeek => false;
			public override bool CanWrite => rawBuffer != null;
			public override long Length => throw new NotSupportedException();
			public override long Position {
				get => written;
				set => throw new NotSupportedException();
			}

			public override void Flush() {
				ThrowIfDisposed();
				destination.Flush();
			}

			public override int Read(byte[] buffer, int offset, int count) =>
				throw new NotSupportedException();

			public override long Seek(long offset, SeekOrigin origin) =>
				throw new NotSupportedException();

			public override void SetLength(long value) =>
				throw new NotSupportedException();

			public override void Write(byte[] buffer, int offset, int count) {
				if (buffer == null) throw new ArgumentNullException(nameof(buffer));
				if ((uint)offset > buffer.Length || (uint)count > buffer.Length - offset)
					throw new ArgumentOutOfRangeException();

				Write(buffer.AsSpan(offset, count));
			}

			public override void Write(ReadOnlySpan<byte> buffer) {
				ThrowIfDisposed();
				if (completed) throw new InvalidOperationException("Compressed cache payload is already complete.");
				if (buffer.Length > expectedLength - written)
					throw new InvalidDataException("Compressed cache writer received too much payload data.");

				while (!buffer.IsEmpty) {
					var count = Math.Min(BLOCK_SIZE - buffered, buffer.Length);
					buffer.Slice(0, count).CopyTo(rawBuffer.AsSpan(buffered));
					buffer = buffer.Slice(count);
					buffered += count;
					written += count;

					if (buffered == BLOCK_SIZE)
						FlushBlock();
				}
			}

			internal void Complete() {
				ThrowIfDisposed();
				if (completed) return;
				if (written != expectedLength)
					throw new InvalidDataException("Compressed cache writer received an unexpected payload length.");

				if (buffered != 0)
					FlushBlock();
				if (blockCount != expectedBlockCount)
					throw new InvalidDataException("Compressed cache writer produced an unexpected block count.");

				completed = true;
			}

			protected override void Dispose(bool disposing) {
				var raw = Interlocked.Exchange(ref rawBuffer, null);
				var encoded = Interlocked.Exchange(ref encodedBuffer, null);
				if (encoded != null) ArrayPool<byte>.Shared.Return(encoded);
				if (raw != null) ArrayPool<byte>.Shared.Return(raw);
				base.Dispose(disposing);
			}

			private void FlushBlock() {
				if (buffered <= 0) return;
				WriteBlock(
					destination,
					rawBuffer.AsSpan(0, buffered),
					encodedBuffer,
					level
				);
				buffered = 0;
				blockCount++;
			}

			private void ThrowIfDisposed() {
				if (rawBuffer == null) throw new ObjectDisposedException(nameof(BlockCompressionStream));
			}
		}
	}
}
