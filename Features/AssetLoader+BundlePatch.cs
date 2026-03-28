using K4os.Compression.LZ4;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;

namespace Symphony.Features.AssetLoaderPatch {
	internal static class AssetLoader_BundlePatch {
		private const int CompressionTypeMask = 0x3F;
		private const uint BlocksInfoAtTheEnd = 0x80;
		private const uint BlockInfoNeedPaddingAtStart = 0x200;
		private static readonly Regex UnityVersionRegex = new Regex(@"(\d+)\.(\d+)\.(\d+)", RegexOptions.Compiled);

		private const int SerializedFileMetadataAtEndVersion = 9;
		private const int SerializedFileVersionStringVersion = 7;
		private const int SerializedFileTargetPlatformVersion = 8;
		private const int Windows64BuildTarget = 19;

		public static bool TryPatchToWindows(byte[] bundleData, out byte[] patchedData, out string error) {
			patchedData = null;
			error = null;

			try {
				var bundle = UnityFsBundle.Read(bundleData);
				if (!bundle.PatchSerializedFiles(Windows64BuildTarget)) {
					error = "No serialized files with a patchable target platform were found";
					return false;
				}

				patchedData = bundle.Write();
				return true;
			} catch (Exception ex) {
				error = ex.ToString();
				return false;
			}
		}

		private sealed class UnityFsBundle {
			private readonly string signature;
			private readonly uint formatVersion;
			private readonly string playerVersion;
			private readonly string engineVersion;
			private readonly uint headerFlags;
			private readonly byte[] blocksInfoHash;
			private readonly List<BlockInfo> blocks;
			private readonly List<DirectoryEntry> directoryEntries;
			private readonly byte[] dataStream;
			private readonly bool usesHeaderAlignment;
			private readonly bool usesNewArchiveFlags;

			private UnityFsBundle(
				string signature,
				uint formatVersion,
				string playerVersion,
				string engineVersion,
				uint headerFlags,
				byte[] blocksInfoHash,
				List<BlockInfo> blocks,
				List<DirectoryEntry> directoryEntries,
				byte[] dataStream,
				bool usesHeaderAlignment,
				bool usesNewArchiveFlags
			) {
				this.signature = signature;
				this.formatVersion = formatVersion;
				this.playerVersion = playerVersion;
				this.engineVersion = engineVersion;
				this.headerFlags = headerFlags;
				this.blocksInfoHash = blocksInfoHash;
				this.blocks = blocks;
				this.directoryEntries = directoryEntries;
				this.dataStream = dataStream;
				this.usesHeaderAlignment = usesHeaderAlignment;
				this.usesNewArchiveFlags = usesNewArchiveFlags;
			}

			public static UnityFsBundle Read(byte[] bytes) {
				var reader = new BigEndianReader(bytes);

				var signature = reader.ReadNullTerminatedString();
				if (!string.Equals(signature, "UnityFS", StringComparison.Ordinal)) {
					throw new NotSupportedException($"Unsupported bundle signature '{signature}'");
				}

				var formatVersion = reader.ReadUInt32();
				var playerVersion = reader.ReadNullTerminatedString();
				var engineVersion = reader.ReadNullTerminatedString();

				reader.ReadInt64(); // total bundle size
				var compressedBlocksInfoSize = reader.ReadUInt32();
				var uncompressedBlocksInfoSize = reader.ReadUInt32();
				var headerFlags = reader.ReadUInt32();

				var parsedVersion = ParseUnityVersion(engineVersion, playerVersion);
				var usesNewArchiveFlags = UsesNewArchiveFlags(parsedVersion);
				var usesHeaderAlignment = formatVersion >= 7 || Is2019AlignmentBackport(parsedVersion);
				if (!usesNewArchiveFlags && (headerFlags & BlockInfoNeedPaddingAtStart) != 0)
					throw new NotSupportedException("Encrypted old-archive UnityFS bundles are not supported");

				if (usesHeaderAlignment) reader.Align(16);

				var afterHeaderOffset = reader.Position;
				var blocksInfoAtTheEnd = (headerFlags & BlocksInfoAtTheEnd) != 0;
				var blocksInfoOffset = blocksInfoAtTheEnd
					? bytes.Length - checked((int)compressedBlocksInfoSize)
					: afterHeaderOffset;

				var compressedBlocksInfo = new byte[checked((int)compressedBlocksInfoSize)];
				Buffer.BlockCopy(bytes, blocksInfoOffset, compressedBlocksInfo, 0, compressedBlocksInfo.Length);
				var blocksInfoBytes = Decompress(
					compressedBlocksInfo,
					checked((int)uncompressedBlocksInfoSize),
					checked((int)(headerFlags & CompressionTypeMask))
				);

				var blocksInfoReader = new BigEndianReader(blocksInfoBytes);
				var blocksInfoHash = blocksInfoReader.ReadBytes(16);
				var blockCount = blocksInfoReader.ReadInt32();
				var blocks = new List<BlockInfo>(blockCount);
				for (var i = 0; i < blockCount; i++) {
					blocks.Add(new BlockInfo {
						UncompressedSize = blocksInfoReader.ReadUInt32(),
						CompressedSize = blocksInfoReader.ReadUInt32(),
						Flags = blocksInfoReader.ReadUInt16(),
					});
				}

				var entryCount = blocksInfoReader.ReadInt32();
				var entries = new List<DirectoryEntry>(entryCount);
				for (var i = 0; i < entryCount; i++) {
					entries.Add(new DirectoryEntry {
						Offset = blocksInfoReader.ReadInt64(),
						Size = blocksInfoReader.ReadInt64(),
						Flags = blocksInfoReader.ReadUInt32(),
						Path = blocksInfoReader.ReadNullTerminatedString(),
					});
				}

				var dataOffset = blocksInfoAtTheEnd ? afterHeaderOffset : afterHeaderOffset + checked((int)compressedBlocksInfoSize);
				if (usesNewArchiveFlags && (headerFlags & BlockInfoNeedPaddingAtStart) != 0) dataOffset = Align(dataOffset, 16);

				var dataStream = new byte[blocks.Sum(x => checked((int)x.UncompressedSize))];
				var compressedOffset = dataOffset;
				var uncompressedOffset = 0;
				foreach (var block in blocks) {
					var compressedSize = checked((int)block.CompressedSize);
					var compressed = new byte[compressedSize];
					Buffer.BlockCopy(bytes, compressedOffset, compressed, 0, compressedSize);

					var uncompressed = Decompress(
						compressed,
						checked((int)block.UncompressedSize),
						block.Flags & CompressionTypeMask
					);

					Buffer.BlockCopy(uncompressed, 0, dataStream, uncompressedOffset, uncompressed.Length);
					compressedOffset += compressedSize;
					uncompressedOffset += uncompressed.Length;
				}

				return new UnityFsBundle(
					signature,
					formatVersion,
					playerVersion,
					engineVersion,
					headerFlags,
					blocksInfoHash,
					blocks,
					entries,
					dataStream,
					usesHeaderAlignment,
					usesNewArchiveFlags
				);
			}

			public bool PatchSerializedFiles(int buildTarget) {
				var patched = false;
				foreach (var entry in directoryEntries) {
					if (entry.Offset < 0 || entry.Size <= 0 || entry.Offset + entry.Size > dataStream.LongLength) {
						continue;
					}

					var fileBytes = new byte[checked((int)entry.Size)];
					Buffer.BlockCopy(dataStream, checked((int)entry.Offset), fileBytes, 0, fileBytes.Length);
					if (!TryPatchSerializedFile(fileBytes, buildTarget)) {
						continue;
					}

					Buffer.BlockCopy(fileBytes, 0, dataStream, checked((int)entry.Offset), fileBytes.Length);
					patched = true;
				}

				return patched;
			}

			public byte[] Write() {
				foreach (var block in blocks) {
					block.CompressedSize = block.UncompressedSize;
					block.Flags = 0;
				}

				var blocksInfoWriter = new BigEndianWriter();
				blocksInfoWriter.Write(blocksInfoHash);
				blocksInfoWriter.WriteInt32(blocks.Count);
				foreach (var block in blocks) {
					blocksInfoWriter.WriteUInt32(block.UncompressedSize);
					blocksInfoWriter.WriteUInt32(block.CompressedSize);
					blocksInfoWriter.WriteUInt16(block.Flags);
				}

				blocksInfoWriter.WriteInt32(directoryEntries.Count);
				foreach (var entry in directoryEntries) {
					blocksInfoWriter.WriteInt64(entry.Offset);
					blocksInfoWriter.WriteInt64(entry.Size);
					blocksInfoWriter.WriteUInt32(entry.Flags);
					blocksInfoWriter.WriteNullTerminatedString(entry.Path);
				}

				var uncompressedBlocksInfo = blocksInfoWriter.ToArray();
				var compressedBlocksInfo = uncompressedBlocksInfo;
				var outputHeaderFlags = headerFlags & ~((uint)CompressionTypeMask);
				if (!usesNewArchiveFlags) outputHeaderFlags &= ~BlockInfoNeedPaddingAtStart;

				var writer = new BigEndianWriter();
				writer.WriteNullTerminatedString(signature);
				writer.WriteUInt32(formatVersion);
				writer.WriteNullTerminatedString(playerVersion);
				writer.WriteNullTerminatedString(engineVersion);

				var bundleSizePosition = writer.Position;
				writer.WriteInt64(0);
				writer.WriteUInt32(checked((uint)compressedBlocksInfo.Length));
				writer.WriteUInt32(checked((uint)uncompressedBlocksInfo.Length));
				writer.WriteUInt32(outputHeaderFlags);

				if (usesHeaderAlignment) {
					writer.Align(16);
				}

				var blocksInfoAtTheEnd = (outputHeaderFlags & BlocksInfoAtTheEnd) != 0;
				if (!blocksInfoAtTheEnd) {
					writer.Write(compressedBlocksInfo);
				}

				if (usesNewArchiveFlags && (outputHeaderFlags & BlockInfoNeedPaddingAtStart) != 0) writer.Align(16);

				writer.Write(dataStream);

				if (blocksInfoAtTheEnd) {
					writer.Write(compressedBlocksInfo);
				}

				writer.WriteInt64At(bundleSizePosition, writer.Position);
				return writer.ToArray();
			}
		}

		private sealed class BlockInfo {
			public uint UncompressedSize { get; set; }
			public uint CompressedSize { get; set; }
			public ushort Flags { get; set; }
		}

		private sealed class DirectoryEntry {
			public long Offset { get; set; }
			public long Size { get; set; }
			public uint Flags { get; set; }
			public string Path { get; set; }
		}

		private static bool TryPatchSerializedFile(byte[] fileBytes, int buildTarget) {
			if (fileBytes.Length < 20) return false;

			var version = BinaryPrimitives.ReadUInt32BigEndian(fileBytes.AsSpan(8, 4));
			if (version < SerializedFileTargetPlatformVersion) return false;

			var metadataSize = 0u;
			var fileSize = 0L;
			var metadataOffset = 0;
			var littleEndian = false;

			if (version >= 22) {
				if (fileBytes.Length < 48) return false;

				littleEndian = fileBytes[16] == 0;
				metadataSize = BinaryPrimitives.ReadUInt32BigEndian(fileBytes.AsSpan(20, 4));
				fileSize = BinaryPrimitives.ReadInt64BigEndian(fileBytes.AsSpan(24, 8));
				metadataOffset = 48;
			} else if (version >= SerializedFileMetadataAtEndVersion) {
				littleEndian = fileBytes[16] == 0;
				metadataSize = BinaryPrimitives.ReadUInt32BigEndian(fileBytes.AsSpan(0, 4));
				fileSize = BinaryPrimitives.ReadUInt32BigEndian(fileBytes.AsSpan(4, 4));
				metadataOffset = 20;
			} else {
				metadataSize = BinaryPrimitives.ReadUInt32BigEndian(fileBytes.AsSpan(0, 4));
				fileSize = BinaryPrimitives.ReadUInt32BigEndian(fileBytes.AsSpan(4, 4));
				if (fileSize <= metadataSize || fileSize > fileBytes.Length) return false;

				metadataOffset = checked((int)(fileSize - metadataSize));
				littleEndian = fileBytes[metadataOffset] == 0;
				metadataOffset += 1;
				metadataSize -= 1;
			}

			if (metadataSize <= 0 || fileSize <= 0 || fileSize > fileBytes.Length) return false;

			if (metadataOffset < 0 || metadataOffset >= fileBytes.Length || metadataOffset + metadataSize > fileBytes.Length) return false;

			var cursor = metadataOffset;
			if (version >= SerializedFileVersionStringVersion) {
				var zeroIndex = Array.IndexOf(fileBytes, (byte)0, cursor);
				if (zeroIndex < 0) return false;

				cursor = zeroIndex + 1;
			}

			if (cursor + 4 > fileBytes.Length) return false;

			var currentTarget = ReadInt32(fileBytes, cursor, littleEndian);
			if (currentTarget == buildTarget) return false;

			WriteInt32(fileBytes, cursor, buildTarget, littleEndian);
			return true;
		}

		private static byte[] Decompress(byte[] data, int uncompressedSize, int compression) {
			return compression switch {
				0 => data,
				1 => DecompressLzma(data, uncompressedSize),
				2 => DecompressLz4(data, uncompressedSize),
				3 => DecompressLz4(data, uncompressedSize),
				_ => throw new NotSupportedException($"Unsupported compression type '{compression}'"),
			};
		}

		private static byte[] DecompressLz4(byte[] data, int uncompressedSize) {
			var buffer = new byte[uncompressedSize];
			var decoded = LZ4Codec.Decode(data, 0, data.Length, buffer, 0, buffer.Length);
			if (decoded != uncompressedSize) throw new InvalidDataException($"LZ4 decode size mismatch: expected {uncompressedSize}, actual {decoded}");

			return buffer;
		}

		private static byte[] DecompressLzma(byte[] data, int uncompressedSize) {
			using var input = new MemoryStream(data, false);
			var properties = new byte[5];
			if (input.Read(properties, 0, properties.Length) != properties.Length) throw new InvalidDataException("Invalid LZMA header");

			// UnityPy treats bundle LZMA payloads as raw LZMA streams after the 5-byte property header.
			// The decompressed size comes from bundle metadata, so there is no 8-byte size prefix here.
			using var output = new MemoryStream(new byte[uncompressedSize], 0, uncompressedSize, true, true);
			var decoder = new SevenZip.Compression.LZMA.Decoder();
			decoder.SetDecoderProperties(properties);
			decoder.Code(input, output, input.Length - input.Position, uncompressedSize, null);
			return output.ToArray();
		}

		private static int Align(int value, int alignment) {
			var remainder = value % alignment;
			return remainder == 0 ? value : value + alignment - remainder;
		}

		private static UnityVersionInfo ParseUnityVersion(string engineVersion, string playerVersion) {
			if (TryParseUnityVersion(engineVersion, out var parsedEngineVersion)) return parsedEngineVersion;
			if (TryParseUnityVersion(playerVersion, out var parsedPlayerVersion)) return parsedPlayerVersion;
			return default;
		}

		private static bool TryParseUnityVersion(string versionText, out UnityVersionInfo version) {
			version = default;
			if (string.IsNullOrWhiteSpace(versionText)) return false;

			var match = UnityVersionRegex.Match(versionText);
			if (!match.Success) return false;

			version = new UnityVersionInfo(
				int.Parse(match.Groups[1].Value),
				int.Parse(match.Groups[2].Value),
				int.Parse(match.Groups[3].Value)
			);
			return true;
		}

		private static bool UsesNewArchiveFlags(UnityVersionInfo version) {
			if (version.Major == 0) return true;
			if (version.Major < 2020) return false;
			if (version.Major == 2020) return IsAtLeast(version, 2020, 3, 34);
			if (version.Major == 2021) return IsAtLeast(version, 2021, 3, 2);
			if (version.Major == 2022) return IsAtLeast(version, 2022, 1, 1);
			return true;
		}

		private static bool Is2019AlignmentBackport(UnityVersionInfo version) {
			return version.Major == 2019 && IsAtLeast(version, 2019, 4, 15);
		}

		private static bool IsAtLeast(UnityVersionInfo version, int major, int minor, int patch) {
			if (version.Major != major) return version.Major > major;
			if (version.Minor != minor) return version.Minor > minor;
			return version.Patch >= patch;
		}

		private static int ReadInt32(byte[] buffer, int offset, bool littleEndian) {
			return littleEndian
				? BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset, 4))
				: BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(offset, 4));
		}

		private static uint ReadUInt32(byte[] buffer, int offset, bool littleEndian) {
			return littleEndian
				? BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset, 4))
				: BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(offset, 4));
		}

		private static void WriteInt32(byte[] buffer, int offset, int value, bool littleEndian) {
			if (littleEndian) {
				BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset, 4), value);
				return;
			}

			BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(offset, 4), value);
		}

		private sealed class BigEndianReader {
			private readonly byte[] buffer;

			public BigEndianReader(byte[] buffer) {
				this.buffer = buffer;
			}

			public int Position { get; private set; }

			public void Align(int alignment) {
				Position = AssetLoader_BundlePatch.Align(Position, alignment);
			}

			public byte[] ReadBytes(int count) {
				var bytes = new byte[count];
				Buffer.BlockCopy(buffer, Position, bytes, 0, count);
				Position += count;
				return bytes;
			}

			public short ReadInt16() {
				var value = BinaryPrimitives.ReadInt16BigEndian(buffer.AsSpan(Position, 2));
				Position += 2;
				return value;
			}

			public int ReadInt32() {
				var value = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(Position, 4));
				Position += 4;
				return value;
			}

			public long ReadInt64() {
				var value = BinaryPrimitives.ReadInt64BigEndian(buffer.AsSpan(Position, 8));
				Position += 8;
				return value;
			}

			public ushort ReadUInt16() {
				var value = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(Position, 2));
				Position += 2;
				return value;
			}

			public uint ReadUInt32() {
				var value = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(Position, 4));
				Position += 4;
				return value;
			}

			public string ReadNullTerminatedString() {
				var end = Array.IndexOf(buffer, (byte)0, Position);
				if (end < 0) throw new InvalidDataException("Null-terminated string not found");

				var value = Encoding.UTF8.GetString(buffer, Position, end - Position);
				Position = end + 1;
				return value;
			}
		}

		private readonly struct UnityVersionInfo {
			public UnityVersionInfo(int major, int minor, int patch) {
				Major = major;
				Minor = minor;
				Patch = patch;
			}

			public int Major { get; }
			public int Minor { get; }
			public int Patch { get; }
		}

		private sealed class BigEndianWriter {
			private readonly MemoryStream stream = new MemoryStream();

			public long Position => stream.Position;

			public void Align(int alignment) {
				while (stream.Position % alignment != 0) {
					stream.WriteByte(0);
				}
			}

			public void Write(byte[] bytes) {
				stream.Write(bytes, 0, bytes.Length);
			}

			public void WriteInt32(int value) {
				var bytes = new byte[4];
				BinaryPrimitives.WriteInt32BigEndian(bytes, value);
				Write(bytes);
			}

			public void WriteInt64(long value) {
				var bytes = new byte[8];
				BinaryPrimitives.WriteInt64BigEndian(bytes, value);
				Write(bytes);
			}

			public void WriteInt64At(long position, long value) {
				var current = stream.Position;
				stream.Position = position;
				WriteInt64(value);
				stream.Position = current;
			}

			public void WriteUInt16(ushort value) {
				var bytes = new byte[2];
				BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
				Write(bytes);
			}

			public void WriteUInt32(uint value) {
				var bytes = new byte[4];
				BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
				Write(bytes);
			}

			public void WriteNullTerminatedString(string value) {
				var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
				Write(bytes);
				stream.WriteByte(0);
			}

			public byte[] ToArray() {
				return stream.ToArray();
			}
		}
	}
}
