using K4os.Compression.LZ4;

using SevenZip.Compression.LZMA;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Symphony.Utils {
	public static class AssetBundleCompatibilityPatcher {
		private const uint Windows64BuildTarget = 19;

		public static bool TryPatchToWindows(byte[] bundleData, out byte[] patchedData, out string error, Action<string> logInfo = null, Action<string> logWarning = null) {
			patchedData = null;
			error = null;
			PatchLog.Info = logInfo;
			PatchLog.Warning = logWarning;

			try {
				var bundle = UnityFsBundle.Read(bundleData);

				var patchedAssetFiles = 0;
				var patchedTextures = 0;
				var patchedPlatforms = 0;
				foreach (var directory in bundle.Directories) {
					if (!SerializedFileData.IsSerializedFile(bundle.GetEntryData(directory))) continue;

					var serializedFile = SerializedFileData.Read(bundle.GetEntryData(directory));
					var assetPatched = false;

					if (serializedFile.TargetPlatform != Windows64BuildTarget) {
						serializedFile.TargetPlatform = Windows64BuildTarget;
						patchedPlatforms++;
						assetPatched = true;
					}

					if (serializedFile.TryPatchTextures(bundle, out var texturePatchedCount, out var texturePatchError)) {
						patchedTextures += texturePatchedCount;
						if (texturePatchedCount > 0) assetPatched = true;
					}
					else if (!string.IsNullOrEmpty(texturePatchError)) {
						PatchLog.LogWarning($"[Symphony::AssetLoader::BundlePatch] Texture patch skipped for '{directory.Name}': {texturePatchError}");
					}

					if (!assetPatched) continue;

					bundle.ReplaceEntryData(directory, serializedFile.Write());
					patchedAssetFiles++;
				}

				if (patchedAssetFiles == 0) {
					error = "No serialized asset files were patched";
					return false;
				}

				patchedData = bundle.WriteUncompressed();
				PatchLog.LogInfo($"[Symphony::AssetLoader::BundlePatch] Patched {patchedAssetFiles} asset files, {patchedPlatforms} platform headers, {patchedTextures} textures");
				return true;
			} catch (Exception ex) {
				error = ex.ToString();
				return false;
			} finally {
				PatchLog.Info = null;
				PatchLog.Warning = null;
			}
		}
	}

	internal static class PatchLog {
		internal static Action<string> Info { get; set; }
		internal static Action<string> Warning { get; set; }

		internal static void LogInfo(string message) => Info?.Invoke(message);
		internal static void LogWarning(string message) => Warning?.Invoke(message);
	}

	internal sealed class UnityFsBundle {
		private const uint CompressionMask = 0x3F;
		private const uint HasDirectoryInfo = 0x40;
		private const uint BlockAndDirAtEnd = 0x80;
		private const uint OldWebPluginCompatibility = 0x100;
		private const uint BlockInfoNeedPaddingAtStart = 0x200;

		private readonly Dictionary<string, byte[]> _entryData;

		private UnityFsBundle(string signature, uint formatVersion, string playerVersion, string engineVersion, long totalFileSize, uint flags, byte[] hash, List<BundleBlockInfo> blocks, List<BundleDirectoryEntry> directories, Dictionary<string, byte[]> entryData) {
			Signature = signature;
			FormatVersion = formatVersion;
			PlayerVersion = playerVersion;
			EngineVersion = engineVersion;
			TotalFileSize = totalFileSize;
			Flags = flags;
			Hash = hash;
			Blocks = blocks;
			Directories = directories;
			_entryData = entryData;
		}

		public string Signature { get; }
		public uint FormatVersion { get; }
		public string PlayerVersion { get; }
		public string EngineVersion { get; }
		public long TotalFileSize { get; }
		public uint Flags { get; }
		public byte[] Hash { get; }
		public List<BundleBlockInfo> Blocks { get; }
		public List<BundleDirectoryEntry> Directories { get; }

		public byte[] GetEntryData(BundleDirectoryEntry directory) => _entryData[directory.Name];

		public void ReplaceEntryData(BundleDirectoryEntry directory, byte[] data) {
			_entryData[directory.Name] = data;
		}

		public bool TryGetResourceData(string resourcePath, ulong offset, uint size, out byte[] data) {
			data = null;
			if (string.IsNullOrEmpty(resourcePath) || size == 0) return false;

			var searchPath = resourcePath.StartsWith("archive:/", StringComparison.OrdinalIgnoreCase) ? resourcePath.Substring(9) : resourcePath;
			var searchName = Path.GetFileName(searchPath);
			var entry = Directories.FirstOrDefault(x => string.Equals(Path.GetFileName(x.Name), searchName, StringComparison.OrdinalIgnoreCase));
			if (entry == null) return false;

			var entryBytes = GetEntryData(entry);
			var start = checked((int)offset);
			var length = checked((int)size);
			if (start < 0 || length < 0 || start + length > entryBytes.Length) return false;

			data = new byte[length];
			Buffer.BlockCopy(entryBytes, start, data, 0, length);
			return true;
		}

		public byte[] WriteUncompressed() {
			var dataStream = new MemoryStream();
			var rebuiltDirectories = new List<BundleDirectoryEntry>(Directories.Count);
			foreach (var directory in Directories) {
				var data = _entryData[directory.Name];
				rebuiltDirectories.Add(new BundleDirectoryEntry(dataStream.Position, data.LongLength, directory.Flags, directory.Name));
				dataStream.Write(data, 0, data.Length);
			}

			var blockInfoWriter = new EndianWriter(true);
			blockInfoWriter.WriteBytes(Hash ?? new byte[16]);
			blockInfoWriter.WriteInt32(1);
			blockInfoWriter.WriteUInt32(checked((uint)dataStream.Length));
			blockInfoWriter.WriteUInt32(checked((uint)dataStream.Length));
			blockInfoWriter.WriteUInt16(0);
			blockInfoWriter.WriteInt32(rebuiltDirectories.Count);
			foreach (var directory in rebuiltDirectories) {
				blockInfoWriter.WriteInt64(directory.Offset);
				blockInfoWriter.WriteInt64(directory.Size);
				blockInfoWriter.WriteUInt32(directory.Flags);
				blockInfoWriter.WriteNullTerminated(directory.Name);
			}

			var headerWriter = new EndianWriter(true);
			headerWriter.WriteNullTerminated(Signature);
			headerWriter.WriteUInt32(FormatVersion);
			headerWriter.WriteNullTerminated(PlayerVersion);
			headerWriter.WriteNullTerminated(EngineVersion);
			headerWriter.WriteInt64(0);
			headerWriter.WriteUInt32((uint)blockInfoWriter.Length);
			headerWriter.WriteUInt32((uint)blockInfoWriter.Length);
			headerWriter.WriteUInt32((Flags & OldWebPluginCompatibility) | HasDirectoryInfo);
			if (NeedsBundleHeaderAlignment(FormatVersion, EngineVersion)) headerWriter.Align(16);

			var totalFileSize = headerWriter.Length + blockInfoWriter.Length + dataStream.Length;
			var output = new MemoryStream(checked((int)totalFileSize));
			var writer = new EndianWriter(output, true);
			writer.WriteBytes(headerWriter.ToArray());
			writer.WriteBytes(blockInfoWriter.ToArray());
			writer.WriteBytes(dataStream.ToArray());

			output.Position = GetFsHeaderOffset();
			writer.WriteInt64(totalFileSize);
			return output.ToArray();
		}

		public static UnityFsBundle Read(byte[] bundleBytes) {
			using var stream = new MemoryStream(bundleBytes, false);
			var reader = new EndianReader(stream, true);

			var signature = reader.ReadNullTerminated();
			if (!string.Equals(signature, "UnityFS", StringComparison.Ordinal))
				throw new NotSupportedException($"Unsupported bundle signature '{signature}'");

			var formatVersion = reader.ReadUInt32();
			var playerVersion = reader.ReadNullTerminated();
			var engineVersion = reader.ReadNullTerminated();
			var totalFileSize = reader.ReadInt64();
			var compressedBlocksInfoSize = reader.ReadUInt32();
			var uncompressedBlocksInfoSize = reader.ReadUInt32();
			var flags = reader.ReadUInt32();
			if (NeedsBundleHeaderAlignment(formatVersion, engineVersion)) reader.Align(16);

			var engine = EngineVersionToken.Parse(engineVersion);
			var usesNewFlags = engine.IsAtLeast(2020, 3, 34) || engine.IsAtLeast(2021, 3, 2) || engine.IsAtLeast(2022, 1, 1);
			if (!usesNewFlags && (flags & BlockInfoNeedPaddingAtStart) != 0)
				throw new NotSupportedException("Encrypted UnityFS bundles are not supported");

			var blocksInfoOffset = (flags & BlockAndDirAtEnd) != 0 ? totalFileSize - compressedBlocksInfoSize : stream.Position;
			stream.Position = blocksInfoOffset;
			var blocksInfoBytes = ReadCompressedChunk(reader, compressedBlocksInfoSize, uncompressedBlocksInfoSize, flags & CompressionMask);

			var blocksReader = new EndianReader(new MemoryStream(blocksInfoBytes, false), true);
			var hash = blocksReader.ReadBytes(16);
			var blockCount = blocksReader.ReadInt32();
			var blocks = new List<BundleBlockInfo>(blockCount);
			for (var i = 0; i < blockCount; i++)
				blocks.Add(new BundleBlockInfo(blocksReader.ReadUInt32(), blocksReader.ReadUInt32(), blocksReader.ReadUInt16()));

			var directoryCount = blocksReader.ReadInt32();
			var directories = new List<BundleDirectoryEntry>(directoryCount);
			for (var i = 0; i < directoryCount; i++)
				directories.Add(new BundleDirectoryEntry(blocksReader.ReadInt64(), blocksReader.ReadInt64(), blocksReader.ReadUInt32(), blocksReader.ReadNullTerminated()));

			stream.Position = GetFileDataOffset(signature, formatVersion, playerVersion, engineVersion, compressedBlocksInfoSize, flags, usesNewFlags);
			var decompressedData = new MemoryStream();
			foreach (var block in blocks) {
				var chunk = ReadCompressedChunk(reader, block.CompressedSize, block.DecompressedSize, (uint)(block.Flags & CompressionMask));
				decompressedData.Write(chunk, 0, chunk.Length);
			}

			var entryData = new Dictionary<string, byte[]>(StringComparer.Ordinal);
			var allData = decompressedData.ToArray();
			foreach (var directory in directories) {
				var offset = checked((int)directory.Offset);
				var size = checked((int)directory.Size);
				if (offset < 0 || size < 0 || offset + size > allData.Length)
					throw new InvalidDataException($"Bundle entry '{directory.Name}' is out of range");

				var entryBytes = new byte[size];
				Buffer.BlockCopy(allData, offset, entryBytes, 0, size);
				entryData[directory.Name] = entryBytes;
			}

			return new UnityFsBundle(signature, formatVersion, playerVersion, engineVersion, totalFileSize, flags, hash, blocks, directories, entryData);
		}

		private static bool NeedsBundleHeaderAlignment(uint formatVersion, string engineVersion) {
			if (formatVersion >= 7) return true;
			if (formatVersion != 6) return false;
			return EngineVersionToken.Parse(engineVersion).IsAtLeast(2019, 4, 15);
		}

		private long GetFsHeaderOffset() {
			var prefix = Encoding.UTF8.GetByteCount(Signature) + 1 +
				4 +
				Encoding.UTF8.GetByteCount(PlayerVersion) + 1 +
				Encoding.UTF8.GetByteCount(EngineVersion) + 1;
			return prefix;
		}

		private static long GetFileDataOffset(string signature, uint formatVersion, string playerVersion, string engineVersion, uint compressedBlocksInfoSize, uint flags, bool usesNewFlags) {
			long offset = Encoding.UTF8.GetByteCount(signature) + 1 +
				4 +
				Encoding.UTF8.GetByteCount(playerVersion) + 1 +
				Encoding.UTF8.GetByteCount(engineVersion) + 1 +
				8 + 4 + 4 + 4;
			if (NeedsBundleHeaderAlignment(formatVersion, engineVersion)) offset = Align(offset, 16);
			if ((flags & BlockAndDirAtEnd) == 0) offset += compressedBlocksInfoSize;
			if (usesNewFlags && (flags & BlockInfoNeedPaddingAtStart) != 0) offset = Align(offset, 16);
			return offset;
		}

		private static long Align(long value, int alignment) {
			var remainder = value % alignment;
			return remainder == 0 ? value : value + alignment - remainder;
		}

		private static byte[] ReadCompressedChunk(EndianReader reader, uint compressedSize, uint uncompressedSize, uint compressionType) {
			var compressedBytes = reader.ReadBytes(checked((int)compressedSize));
			switch (compressionType) {
				case 0:
					return compressedBytes;
				case 1:
					using (var input = new MemoryStream(compressedBytes, false))
					using (var output = new MemoryStream(checked((int)uncompressedSize))) {
						LzmaHelper.StreamDecompress(input, output, compressedSize, uncompressedSize);
						return output.ToArray();
					}
				case 2:
				case 3:
					var uncompressedBytes = new byte[checked((int)uncompressedSize)];
					var decoded = LZ4Codec.Decode(compressedBytes, 0, compressedBytes.Length, uncompressedBytes, 0, uncompressedBytes.Length);
					if (decoded != uncompressedBytes.Length)
						throw new InvalidDataException($"Failed to decode UnityFS LZ4 block ({decoded}/{uncompressedBytes.Length})");
					return uncompressedBytes;
				default:
					throw new NotSupportedException($"Unsupported UnityFS compression type {compressionType}");
			}
		}
	}

	internal sealed class BundleBlockInfo {
		public BundleBlockInfo(uint decompressedSize, uint compressedSize, ushort flags) {
			DecompressedSize = decompressedSize;
			CompressedSize = compressedSize;
			Flags = flags;
		}

		public uint DecompressedSize { get; }
		public uint CompressedSize { get; }
		public ushort Flags { get; }
	}

	internal static class LzmaHelper {
		public static void StreamDecompress(Stream compressedStream, Stream decompressedStream, long compressedSize, long decompressedSize) {
			var basePosition = compressedStream.Position;
			var decoder = new SevenZip.Compression.LZMA.Decoder();
			var properties = new byte[5];
			if (compressedStream.Read(properties, 0, 5) != 5)
				throw new InvalidDataException("input .lzma is too short");

			decoder.SetDecoderProperties(properties);
			decoder.Code(compressedStream, decompressedStream, compressedSize - 5, decompressedSize, null);
			compressedStream.Position = basePosition + compressedSize;
		}
	}

	internal sealed class BundleDirectoryEntry {
		public BundleDirectoryEntry(long offset, long size, uint flags, string name) {
			Offset = offset;
			Size = size;
			Flags = flags;
			Name = name;
		}

		public long Offset { get; }
		public long Size { get; }
		public uint Flags { get; }
		public string Name { get; }
	}

	internal readonly struct EngineVersionToken {
		private EngineVersionToken(int major, int minor, int patch) {
			Major = major;
			Minor = minor;
			Patch = patch;
		}

		public int Major { get; }
		public int Minor { get; }
		public int Patch { get; }

		public bool IsAtLeast(int major, int minor, int patch) {
			if (Major != major) return Major > major;
			if (Minor != minor) return Minor > minor;
			return Patch >= patch;
		}

		public static EngineVersionToken Parse(string version) {
			if (string.IsNullOrWhiteSpace(version)) return default;

			var split = version.Split('.');
			var major = split.Length > 0 && int.TryParse(split[0], out var parsedMajor) ? parsedMajor : 0;
			var minor = split.Length > 1 && int.TryParse(split[1], out var parsedMinor) ? parsedMinor : 0;
			var patch = 0;
			if (split.Length > 2) {
				var patchBuilder = new StringBuilder();
				foreach (var ch in split[2]) {
					if (!char.IsDigit(ch)) break;
					patchBuilder.Append(ch);
				}
				if (patchBuilder.Length > 0) int.TryParse(patchBuilder.ToString(), out patch);
			}

			return new EngineVersionToken(major, minor, patch);
		}
	}

	internal sealed class EndianReader : IDisposable {
		private readonly Stream _stream;
		private readonly bool _bigEndian;

		public EndianReader(Stream stream, bool bigEndian) {
			_stream = stream;
			_bigEndian = bigEndian;
		}

		public long Position {
			get => _stream.Position;
			set => _stream.Position = value;
		}

		public long Length => _stream.Length;

		public byte ReadByte() {
			var value = _stream.ReadByte();
			if (value < 0) throw new EndOfStreamException();
			return (byte)value;
		}

		public bool ReadBoolean() => ReadByte() != 0;

		public ushort ReadUInt16() => BitConverter.ToUInt16(ReadEndianBytes(2), 0);
		public uint ReadUInt32() => BitConverter.ToUInt32(ReadEndianBytes(4), 0);
		public int ReadInt32() => BitConverter.ToInt32(ReadEndianBytes(4), 0);
		public ulong ReadUInt64() => BitConverter.ToUInt64(ReadEndianBytes(8), 0);
		public long ReadInt64() => BitConverter.ToInt64(ReadEndianBytes(8), 0);
		public float ReadSingle() => BitConverter.ToSingle(ReadEndianBytes(4), 0);

		public byte[] ReadBytes(int count) {
			var buffer = new byte[count];
			var read = 0;
			while (read < count) {
				var current = _stream.Read(buffer, read, count - read);
				if (current == 0) throw new EndOfStreamException();
				read += current;
			}
			return buffer;
		}

		public string ReadNullTerminated() {
			var bytes = new List<byte>();
			while (true) {
				var value = ReadByte();
				if (value == 0) return Encoding.UTF8.GetString(bytes.ToArray());
				bytes.Add(value);
			}
		}

		public string ReadString() {
			var length = ReadInt32();
			if (length <= 0) {
				Align(4);
				return string.Empty;
			}

			var value = Encoding.UTF8.GetString(ReadBytes(length));
			Align(4);
			return value;
		}

		public void Align(int alignment) {
			while ((_stream.Position % alignment) != 0) _stream.Position++;
		}

		private byte[] ReadEndianBytes(int count) {
			var bytes = ReadBytes(count);
			if (_bigEndian == BitConverter.IsLittleEndian) Array.Reverse(bytes);
			return bytes;
		}

		public void Dispose() {
			_stream.Dispose();
		}
	}

	internal sealed class EndianWriter {
		private readonly Stream _stream;
		private readonly bool _bigEndian;

		public EndianWriter(bool bigEndian) : this(new MemoryStream(), bigEndian) {
		}

		public EndianWriter(Stream stream, bool bigEndian) {
			_stream = stream;
			_bigEndian = bigEndian;
		}

		public long Length => _stream.Length;
		public long Position {
			get => _stream.Position;
			set => _stream.Position = value;
		}

		public void WriteByte(byte value) => _stream.WriteByte(value);
		public void WriteBoolean(bool value) => WriteByte(value ? (byte)1 : (byte)0);
		public void WriteUInt16(ushort value) => WriteEndianBytes(BitConverter.GetBytes(value));
		public void WriteUInt32(uint value) => WriteEndianBytes(BitConverter.GetBytes(value));
		public void WriteInt32(int value) => WriteEndianBytes(BitConverter.GetBytes(value));
		public void WriteUInt64(ulong value) => WriteEndianBytes(BitConverter.GetBytes(value));
		public void WriteInt64(long value) => WriteEndianBytes(BitConverter.GetBytes(value));
		public void WriteSingle(float value) => WriteEndianBytes(BitConverter.GetBytes(value));
		public void WriteBytes(byte[] value) => _stream.Write(value, 0, value.Length);

		public void WriteNullTerminated(string value) {
			var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
			WriteBytes(bytes);
			WriteByte(0);
		}

		public void WriteString(string value) {
			var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
			WriteInt32(bytes.Length);
			WriteBytes(bytes);
			Align(4);
		}

		public void Align(int alignment) {
			while ((_stream.Position % alignment) != 0) _stream.WriteByte(0);
		}

		public byte[] ToArray() => _stream is MemoryStream memory ? memory.ToArray() : throw new InvalidOperationException("Writer stream is not a MemoryStream");

		private void WriteEndianBytes(byte[] bytes) {
			if (_bigEndian == BitConverter.IsLittleEndian) Array.Reverse(bytes);
			WriteBytes(bytes);
		}
	}

	internal sealed class SerializedFileData {
		private const int Texture2DTypeId = 28;

		private SerializedFileData(byte[] fileBytes, uint version, bool bigEndian, int headerSize, long dataOffset, string unityVersion, uint targetPlatform, bool typeTreeEnabled, byte[] typesRaw, List<SerializedType> types, List<SerializedObjectInfo> objects, byte[] metadataTailRaw) {
			FileBytes = fileBytes;
			Version = version;
			BigEndian = bigEndian;
			HeaderSize = headerSize;
			DataOffset = dataOffset;
			UnityVersion = unityVersion;
			TargetPlatform = targetPlatform;
			TypeTreeEnabled = typeTreeEnabled;
			TypesRaw = typesRaw;
			Types = types;
			Objects = objects;
			MetadataTailRaw = metadataTailRaw;
		}

		public byte[] FileBytes { get; }
		public uint Version { get; }
		public bool BigEndian { get; }
		public int HeaderSize { get; }
		public long DataOffset { get; }
		public string UnityVersion { get; }
		public uint TargetPlatform { get; set; }
		public bool TypeTreeEnabled { get; }
		public byte[] TypesRaw { get; }
		public List<SerializedType> Types { get; }
		public List<SerializedObjectInfo> Objects { get; }
		public byte[] MetadataTailRaw { get; }

		public bool TryPatchTextures(UnityFsBundle bundle, out int patchedCount, out string error) {
			patchedCount = 0;
			error = null;

			try {
				if (!TypeTreeEnabled) return true;

				foreach (var asset in Objects) {
					if (asset.TypeId != Texture2DTypeId) continue;

					var type = Types[asset.TypeIdOrIndex];
					if (type.Nodes.Count == 0) continue;

					var objectBytes = asset.GetObjectData(FileBytes, DataOffset);
					if (!TextureTypeTreePatcher.TryRead(type.Nodes, objectBytes, BigEndian, out var texture, out var readError)) {
						PatchLog.LogWarning($"[Symphony::AssetLoader::BundlePatch] Failed to read Texture2D {asset.PathId}: {readError}");
						continue;
					}

					if (!TextureTranscoder.NeedsTranscode(texture.TextureFormat)) continue;

					var sourceData = texture.ImageData;
					if ((sourceData == null || sourceData.Length == 0) && !bundle.TryGetResourceData(texture.StreamPath, texture.StreamOffset, texture.StreamSize, out sourceData)) {
						PatchLog.LogWarning($"[Symphony::AssetLoader::BundlePatch] Failed to locate external texture data for Texture2D {asset.PathId}");
						continue;
					}

					if (!TextureTranscoder.TryTranscode(new TextureTranscoder.TextureTranscodeRequest(texture.Width, texture.Height, texture.TextureFormat, texture.MipCount, sourceData), out var transcoded, out var transcodeError)) {
						PatchLog.LogWarning($"[Symphony::AssetLoader::BundlePatch] Failed to transcode Texture2D {asset.PathId}: {transcodeError}");
						continue;
					}

					var overrides = new TextureObjectOverrides(
						TextureTranscoder.Dxt5TextureFormat,
						transcoded.Data.Length,
						transcoded.MipCount,
						Array.Empty<byte>(),
						transcoded.Data,
						0,
						0,
						string.Empty
					);
					if (!TextureTypeTreePatcher.TryRewrite(type.Nodes, objectBytes, BigEndian, overrides, out var patchedBytes, out var writeError)) {
						PatchLog.LogWarning($"[Symphony::AssetLoader::BundlePatch] Failed to rewrite Texture2D {asset.PathId}: {writeError}");
						continue;
					}

					asset.PatchedData = patchedBytes;
					patchedCount++;
				}

				return true;
			} catch (Exception ex) {
				error = ex.ToString();
				return false;
			}
		}

		public byte[] Write() {
			var sortedObjects = Objects.OrderBy(x => x.PathId).ToList();
			var objectData = new MemoryStream();
			for (var i = 0; i < sortedObjects.Count; i++) {
				var asset = sortedObjects[i];
				var bytes = asset.PatchedData ?? asset.GetObjectData(FileBytes, DataOffset);
				asset.ByteOffset = objectData.Position;
				asset.ByteSize = (uint)bytes.Length;
				objectData.Write(bytes, 0, bytes.Length);
				if (i != sortedObjects.Count - 1) AlignStream(objectData, 8);
			}

			var metadataWriter = new EndianWriter(BigEndian);
			metadataWriter.WriteNullTerminated(UnityVersion);
			metadataWriter.WriteUInt32(TargetPlatform);
			if (Version >= 13) metadataWriter.WriteBoolean(TypeTreeEnabled);
			metadataWriter.WriteBytes(TypesRaw);
			metadataWriter.WriteInt32(sortedObjects.Count);
			metadataWriter.Align(4);
			foreach (var asset in sortedObjects) asset.Write(metadataWriter, Version);
			metadataWriter.WriteBytes(MetadataTailRaw);

			var metadataSize = (int)metadataWriter.Length;
			var dataOffset = GetDataOffset(HeaderSize, metadataSize);
			var output = new MemoryStream();
			WriteHeader(output, Version, metadataSize, dataOffset + objectData.Length, dataOffset, BigEndian);
			output.Write(metadataWriter.ToArray(), 0, metadataSize);
			while (output.Position < dataOffset) output.WriteByte(0);
			objectData.Position = 0;
			objectData.CopyTo(output);
			return output.ToArray();
		}

		public static bool IsSerializedFile(byte[] bytes) {
			if (bytes == null || bytes.Length < 0x30) return false;
			if (bytes[0] == (byte)'U' && bytes[1] == (byte)'n' && bytes[2] == (byte)'i' && bytes[3] == (byte)'t' && bytes[4] == (byte)'y')
				return false;

			var format = ReadUInt32BigEndian(bytes, 8);
			if (format > 99) return false;

			var versionOffset = format >= 22 ? 0x30 : 0x14;
			if (versionOffset >= bytes.Length) return false;
			var cursor = versionOffset;
			while (cursor < bytes.Length && bytes[cursor] != 0) {
				var ch = (char)bytes[cursor];
				if (!(char.IsLetterOrDigit(ch) || ch == '.' || ch == '\n' || ch == '-')) return false;
				cursor++;
				if (cursor - versionOffset > 0xFF) return false;
			}

			return cursor > versionOffset;
		}

		public static SerializedFileData Read(byte[] bytes) {
			var stream = new MemoryStream(bytes, false);
			var headerReader = new EndianReader(stream, true);
			var metadataSize = (int)headerReader.ReadUInt32();
			long fileSize = headerReader.ReadUInt32();
			var version = headerReader.ReadUInt32();
			long dataOffset = headerReader.ReadUInt32();
			var bigEndian = headerReader.ReadBoolean();
			headerReader.ReadBytes(3);
			var headerSize = 20;
			if (version >= 22) {
				metadataSize = (int)headerReader.ReadUInt32();
				fileSize = headerReader.ReadInt64();
				dataOffset = headerReader.ReadInt64();
				headerReader.ReadBytes(8);
				headerSize = 48;
			}

			stream.Position = headerSize;
			var metadataReader = new EndianReader(stream, bigEndian);
			var unityVersion = metadataReader.ReadNullTerminated();
			var targetPlatform = metadataReader.ReadUInt32();
			var typeTreeEnabled = version < 13 || metadataReader.ReadBoolean();

			var typesRawStart = (int)stream.Position;
			var typeCount = metadataReader.ReadInt32();
			var types = new List<SerializedType>(typeCount);
			for (var i = 0; i < typeCount; i++)
				types.Add(SerializedType.Read(metadataReader, version, typeTreeEnabled));

			var typesRawEnd = (int)stream.Position;
			var typesRaw = Slice(bytes, typesRawStart, typesRawEnd - typesRawStart);

			var assetCount = metadataReader.ReadInt32();
			metadataReader.Align(4);
			var objects = new List<SerializedObjectInfo>(assetCount);
			for (var i = 0; i < assetCount; i++)
				objects.Add(SerializedObjectInfo.Read(metadataReader, version, types));

			var metadataEnd = headerSize + metadataSize;
			var metadataTailRaw = Slice(bytes, (int)stream.Position, metadataEnd - (int)stream.Position);

			return new SerializedFileData(bytes, version, bigEndian, headerSize, dataOffset, unityVersion, targetPlatform, typeTreeEnabled, typesRaw, types, objects, metadataTailRaw);
		}

		private static int GetDataOffset(int headerSize, int metadataSize) {
			var offset = headerSize + metadataSize;
			if (offset < 0x1000) return 0x1000;
			var aligned = (offset + 15) & ~15;
			return aligned == offset ? aligned + 16 : aligned;
		}

		private static void WriteHeader(Stream stream, uint version, long metadataSize, long fileSize, long dataOffset, bool bigEndian) {
			var writer = new EndianWriter(stream, true);
			if (version >= 22) {
				writer.WriteUInt32(0);
				writer.WriteUInt32(0);
				writer.WriteUInt32(version);
				writer.WriteUInt32(0);
			} else {
				writer.WriteUInt32((uint)metadataSize);
				writer.WriteUInt32((uint)fileSize);
				writer.WriteUInt32(version);
				writer.WriteUInt32((uint)dataOffset);
			}

			writer.WriteBoolean(bigEndian);
			writer.WriteBytes(new byte[3]);
			if (version < 22) return;

			writer.WriteUInt32((uint)metadataSize);
			writer.WriteInt64(fileSize);
			writer.WriteInt64(dataOffset);
			writer.WriteBytes(new byte[8]);
		}

		private static byte[] Slice(byte[] source, int offset, int count) {
			if (count <= 0) return Array.Empty<byte>();
			var buffer = new byte[count];
			Buffer.BlockCopy(source, offset, buffer, 0, count);
			return buffer;
		}

		private static uint ReadUInt32BigEndian(byte[] source, int offset) {
			var buffer = new byte[4];
			Buffer.BlockCopy(source, offset, buffer, 0, 4);
			if (BitConverter.IsLittleEndian) Array.Reverse(buffer);
			return BitConverter.ToUInt32(buffer, 0);
		}

		private static void AlignStream(Stream stream, int alignment) {
			while ((stream.Position % alignment) != 0) stream.WriteByte(0);
		}
	}

	internal sealed class SerializedType {
		public SerializedType(int typeId, ushort scriptTypeIndex, List<TypeTreeNodeInfo> nodes) {
			TypeId = typeId;
			ScriptTypeIndex = scriptTypeIndex;
			Nodes = nodes;
		}

		public int TypeId { get; }
		public ushort ScriptTypeIndex { get; }
		public List<TypeTreeNodeInfo> Nodes { get; }

		public static SerializedType Read(EndianReader reader, uint version, bool typeTreeEnabled) {
			var typeId = reader.ReadInt32();
			if (version >= 16) reader.ReadBoolean();
			var scriptTypeIndex = version >= 17 ? reader.ReadUInt16() : ushort.MaxValue;
			var hasScriptHash = (version < 17 && typeId < 0) || (version >= 17 && typeId == 114);
			if (hasScriptHash) reader.ReadBytes(16);
			reader.ReadBytes(16);

			var nodes = new List<TypeTreeNodeInfo>();
			if (!typeTreeEnabled) return new SerializedType(typeId, scriptTypeIndex, nodes);

			var nodeCount = reader.ReadInt32();
			var stringBufferLength = reader.ReadInt32();
			var rawNodes = new List<(byte Level, uint TypeOffset, uint NameOffset, uint MetaFlags)>(nodeCount);
			for (var i = 0; i < nodeCount; i++) {
				reader.ReadUInt16();
				var level = reader.ReadByte();
				reader.ReadByte();
				var typeOffset = reader.ReadUInt32();
				var nameOffset = reader.ReadUInt32();
				reader.ReadInt32();
				reader.ReadUInt32();
				var metaFlags = reader.ReadUInt32();
				if (version >= 18) reader.ReadUInt64();
				rawNodes.Add((level, typeOffset, nameOffset, metaFlags));
			}

			var stringBuffer = reader.ReadBytes(stringBufferLength);
			if (version >= 21) {
				var dependencyCount = reader.ReadInt32();
				reader.ReadBytes(dependencyCount * 4);
			}

			foreach (var rawNode in rawNodes)
				nodes.Add(new TypeTreeNodeInfo(rawNode.Level, ResolveTypeTreeString(stringBuffer, rawNode.TypeOffset), ResolveTypeTreeString(stringBuffer, rawNode.NameOffset), rawNode.MetaFlags));

			return new SerializedType(typeId, scriptTypeIndex, nodes);
		}

		private static string ResolveTypeTreeString(byte[] stringBuffer, uint offset) {
			var source = stringBuffer;
			if ((offset & 0x80000000) != 0) {
				offset &= 0x7FFFFFFF;
				source = CommonStringTable;
			}

			var index = (int)offset;
			var end = index;
			while (end < source.Length && source[end] != 0) end++;
			return Encoding.UTF8.GetString(source, index, end - index);
		}

		private static readonly byte[] CommonStringTable = Encoding.UTF8.GetBytes(
			"AABB\0AnimationClip\0AnimationCurve\0AnimationState\0Array\0Base\0BitField\0bitset\0bool\0char\0" +
			"ColorRGBA\0Component\0data\0deque\0double\0dynamic_array\0FastPropertyName\0first\0float\0Font\0" +
			"GameObject\0Generic Mono\0GradientNEW\0GUID\0GUIStyle\0int\0list\0long long\0map\0Matrix4x4f\0MdFour\0" +
			"MonoBehaviour\0MonoScript\0m_ByteSize\0m_Curve\0m_EditorClassIdentifier\0m_EditorHideFlags\0m_Enabled\0" +
			"m_ExtensionPtr\0m_GameObject\0m_Index\0m_IsArray\0m_IsStatic\0m_MetaFlag\0m_Name\0m_ObjectHideFlags\0" +
			"m_PrefabInternal\0m_PrefabParentObject\0m_Script\0m_StaticEditorFlags\0m_Type\0m_Version\0Object\0" +
			"pair\0PPtr<Component>\0PPtr<GameObject>\0PPtr<Material>\0PPtr<MonoBehaviour>\0PPtr<MonoScript>\0" +
			"PPtr<Object>\0PPtr<Prefab>\0PPtr<Sprite>\0PPtr<TextAsset>\0PPtr<Texture>\0PPtr<Texture2D>\0PPtr<Transform>\0" +
			"Prefab\0Quaternionf\0Rectf\0RectInt\0RectOffset\0second\0set\0short\0size\0SInt16\0SInt32\0SInt64\0" +
			"SInt8\0staticvector\0string\0TextAsset\0TextMesh\0Texture\0Texture2D\0Transform\0TypelessData\0UInt16\0" +
			"UInt32\0UInt64\0UInt8\0unsigned int\0unsigned long long\0unsigned short\0vector\0Vector2f\0Vector3f\0" +
			"Vector4f\0m_ScriptingClassIdentifier\0Gradient\0Type*\0int2_storage\0int3_storage\0BoundsInt\0m_CorrespondingSourceObject\0" +
			"m_PrefabInstance\0m_PrefabAsset\0FileSize\0Hash128\0RenderingLayerMask\0");
	}

	internal sealed class TypeTreeNodeInfo {
		public TypeTreeNodeInfo(byte level, string typeName, string name, uint metaFlags) {
			Level = level;
			TypeName = typeName;
			Name = name;
			MetaFlags = metaFlags;
		}

		public byte Level { get; }
		public string TypeName { get; }
		public string Name { get; }
		public uint MetaFlags { get; }
	}

	internal sealed class SerializedObjectInfo {
		public long PathId { get; private set; }
		public long ByteOffset { get; set; }
		public uint ByteSize { get; set; }
		public int TypeIdOrIndex { get; private set; }
		public int TypeId { get; private set; }
		public ushort OldTypeId { get; private set; }
		public ushort ScriptTypeIndex { get; private set; }
		public byte Stripped { get; private set; }
		public byte[] PatchedData { get; set; }

		public byte[] GetObjectData(byte[] fileBytes, long dataOffset) {
			var start = checked((int)(ByteOffset + dataOffset));
			var length = checked((int)ByteSize);
			var bytes = new byte[length];
			Buffer.BlockCopy(fileBytes, start, bytes, 0, length);
			return bytes;
		}

		public static SerializedObjectInfo Read(EndianReader reader, uint version, List<SerializedType> types) {
			reader.Align(4);
			var info = new SerializedObjectInfo {
				PathId = version >= 14 ? reader.ReadInt64() : reader.ReadUInt32(),
				ByteOffset = version >= 22 ? reader.ReadInt64() : reader.ReadUInt32(),
				ByteSize = reader.ReadUInt32(),
				TypeIdOrIndex = reader.ReadInt32(),
			};
			if (version <= 15) info.OldTypeId = reader.ReadUInt16();
			if (version <= 16) info.ScriptTypeIndex = reader.ReadUInt16();
			if (version >= 15 && version <= 16) info.Stripped = reader.ReadByte();
			info.TypeId = version < 16 ? info.TypeIdOrIndex : types[info.TypeIdOrIndex].TypeId;
			return info;
		}

		public void Write(EndianWriter writer, uint version) {
			writer.Align(4);
			if (version >= 14) writer.WriteInt64(PathId);
			else writer.WriteUInt32((uint)PathId);
			if (version >= 22) writer.WriteInt64(ByteOffset);
			else writer.WriteUInt32((uint)ByteOffset);
			writer.WriteUInt32(ByteSize);
			writer.WriteInt32(TypeIdOrIndex);
			if (version <= 15) writer.WriteUInt16(OldTypeId);
			if (version <= 16) writer.WriteUInt16(ScriptTypeIndex);
			if (version >= 15 && version <= 16) writer.WriteByte(Stripped);
		}
	}

	internal readonly struct TextureObjectData {
		public TextureObjectData(int width, int height, int textureFormat, int mipCount, byte[] imageData, ulong streamOffset, uint streamSize, string streamPath) {
			Width = width;
			Height = height;
			TextureFormat = textureFormat;
			MipCount = mipCount;
			ImageData = imageData;
			StreamOffset = streamOffset;
			StreamSize = streamSize;
			StreamPath = streamPath;
		}

		public int Width { get; }
		public int Height { get; }
		public int TextureFormat { get; }
		public int MipCount { get; }
		public byte[] ImageData { get; }
		public ulong StreamOffset { get; }
		public uint StreamSize { get; }
		public string StreamPath { get; }
	}

	internal readonly struct TextureObjectOverrides {
		public TextureObjectOverrides(int textureFormat, int completeImageSize, int mipCount, byte[] platformBlob, byte[] imageData, ulong streamOffset, uint streamSize, string streamPath) {
			TextureFormat = textureFormat;
			CompleteImageSize = completeImageSize;
			MipCount = mipCount;
			PlatformBlob = platformBlob;
			ImageData = imageData;
			StreamOffset = streamOffset;
			StreamSize = streamSize;
			StreamPath = streamPath;
		}

		public int TextureFormat { get; }
		public int CompleteImageSize { get; }
		public int MipCount { get; }
		public byte[] PlatformBlob { get; }
		public byte[] ImageData { get; }
		public ulong StreamOffset { get; }
		public uint StreamSize { get; }
		public string StreamPath { get; }
	}

	internal static class TextureTypeTreePatcher {
		public static bool TryRead(List<TypeTreeNodeInfo> nodes, byte[] objectBytes, bool bigEndian, out TextureObjectData data, out string error) {
			data = default;
			error = null;

			try {
				var state = new TextureReadState();
				using var reader = new EndianReader(new MemoryStream(objectBytes, false), bigEndian);
				var index = 1;
				while (index < nodes.Count) index = ReadNode(nodes, index, nodes[index].Name, reader, state);
				data = new TextureObjectData(state.Width, state.Height, state.TextureFormat, state.MipCount, state.ImageData, state.StreamOffset, state.StreamSize, state.StreamPath);
				return true;
			} catch (Exception ex) {
				error = ex.ToString();
				return false;
			}
		}

		public static bool TryRewrite(List<TypeTreeNodeInfo> nodes, byte[] objectBytes, bool bigEndian, TextureObjectOverrides overrides, out byte[] patchedBytes, out string error) {
			patchedBytes = null;
			error = null;

			try {
				using var input = new EndianReader(new MemoryStream(objectBytes, false), bigEndian);
				var outputStream = new MemoryStream();
				var output = new EndianWriter(outputStream, bigEndian);
				var index = 1;
				while (index < nodes.Count) index = WriteNode(nodes, index, nodes[index].Name, input, output, overrides);
				patchedBytes = outputStream.ToArray();
				return true;
			} catch (Exception ex) {
				error = ex.ToString();
				return false;
			}
		}

		private static int ReadNode(List<TypeTreeNodeInfo> nodes, int index, string path, EndianReader reader, TextureReadState state) {
			var node = nodes[index];
			if (IsStringNode(node)) {
				AssignValue(state, path, reader.ReadString());
				AlignAfterNode(nodes, index, reader);
				return GetNextIndex(nodes, index);
			}
			if (IsByteArrayNode(nodes, index)) {
				AssignValue(state, path, ReadByteArray(reader));
				AlignAfterNode(nodes, index, reader);
				return GetNextIndex(nodes, index);
			}
			if (!HasChildren(nodes, index)) {
				AssignValue(state, path, ReadPrimitive(reader, node.TypeName));
				AlignAfterNode(nodes, index, reader);
				return index + 1;
			}

			var next = index + 1;
			while (next < nodes.Count && nodes[next].Level > node.Level) {
				var childPath = $"{path}.{nodes[next].Name}";
				next = ReadNode(nodes, next, childPath, reader, state);
			}
			AlignAfterNode(nodes, index, reader);
			return next;
		}

		private static int WriteNode(List<TypeTreeNodeInfo> nodes, int index, string path, EndianReader input, EndianWriter output, TextureObjectOverrides overrides) {
			var node = nodes[index];
			if (IsStringNode(node)) {
				var original = input.ReadString();
				output.WriteString(path == "m_StreamData.path" ? overrides.StreamPath : original);
				AlignAfterNode(nodes, index, input, output);
				return GetNextIndex(nodes, index);
			}
			if (IsByteArrayNode(nodes, index)) {
				var original = ReadByteArray(input);
				if (path == "m_PlatformBlob") WriteByteArray(output, overrides.PlatformBlob);
				else if (path == "image data") WriteByteArray(output, overrides.ImageData);
				else WriteByteArray(output, original);
				AlignAfterNode(nodes, index, input, output);
				return GetNextIndex(nodes, index);
			}
			if (!HasChildren(nodes, index)) {
				WritePrimitive(output, node.TypeName, OverridePrimitive(path, ReadPrimitive(input, node.TypeName), overrides));
				AlignAfterNode(nodes, index, input, output);
				return index + 1;
			}

			var next = index + 1;
			while (next < nodes.Count && nodes[next].Level > node.Level) {
				var childPath = $"{path}.{nodes[next].Name}";
				next = WriteNode(nodes, next, childPath, input, output, overrides);
			}
			AlignAfterNode(nodes, index, input, output);
			return next;
		}

		private static bool HasChildren(List<TypeTreeNodeInfo> nodes, int index) => index + 1 < nodes.Count && nodes[index + 1].Level > nodes[index].Level;
		private static int GetNextIndex(List<TypeTreeNodeInfo> nodes, int index) {
			var level = nodes[index].Level;
			var next = index + 1;
			while (next < nodes.Count && nodes[next].Level > level) next++;
			return next;
		}

		private static bool IsStringNode(TypeTreeNodeInfo node) => string.Equals(node.TypeName, "string", StringComparison.Ordinal);
		private static bool IsByteArrayNode(List<TypeTreeNodeInfo> nodes, int index) => (nodes[index].TypeName == "vector" || nodes[index].TypeName == "TypelessData") && HasChildren(nodes, index);

		private static byte[] ReadByteArray(EndianReader reader) {
			var length = reader.ReadInt32();
			return length <= 0 ? Array.Empty<byte>() : reader.ReadBytes(length);
		}

		private static void WriteByteArray(EndianWriter writer, byte[] bytes) {
			writer.WriteInt32(bytes?.Length ?? 0);
			if (bytes != null && bytes.Length > 0) writer.WriteBytes(bytes);
		}

		private static object ReadPrimitive(EndianReader reader, string typeName) {
			switch (typeName) {
				case "bool":
					return reader.ReadBoolean();
				case "SInt8":
					return unchecked((sbyte)reader.ReadByte());
				case "UInt8":
				case "char":
					return reader.ReadByte();
				case "short":
				case "SInt16":
					return unchecked((short)reader.ReadUInt16());
				case "UInt16":
					return reader.ReadUInt16();
				case "int":
				case "SInt32":
					return reader.ReadInt32();
				case "unsigned int":
					return reader.ReadUInt32();
				case "UInt32":
					return reader.ReadUInt32();
				case "SInt64":
				case "long long":
					return reader.ReadInt64();
				case "UInt64":
				case "FileSize":
					return reader.ReadUInt64();
				case "float":
					return reader.ReadSingle();
				default:
					throw new NotSupportedException($"Unsupported primitive type '{typeName}'");
			}
		}

		private static void WritePrimitive(EndianWriter writer, string typeName, object value) {
			switch (typeName) {
				case "bool":
					writer.WriteBoolean((bool)value);
					return;
				case "SInt8":
					writer.WriteByte(unchecked((byte)Convert.ToSByte(value)));
					return;
				case "UInt8":
				case "char":
					writer.WriteByte(Convert.ToByte(value));
					return;
				case "short":
				case "SInt16":
					writer.WriteUInt16(unchecked((ushort)Convert.ToInt16(value)));
					return;
				case "UInt16":
					writer.WriteUInt16(Convert.ToUInt16(value));
					return;
				case "int":
				case "SInt32":
					writer.WriteInt32(Convert.ToInt32(value));
					return;
				case "unsigned int":
					writer.WriteUInt32(Convert.ToUInt32(value));
					return;
				case "UInt32":
					writer.WriteUInt32(Convert.ToUInt32(value));
					return;
				case "SInt64":
				case "long long":
					writer.WriteInt64(Convert.ToInt64(value));
					return;
				case "UInt64":
				case "FileSize":
					writer.WriteUInt64(Convert.ToUInt64(value));
					return;
				case "float":
					writer.WriteSingle(Convert.ToSingle(value));
					return;
				default:
					throw new NotSupportedException($"Unsupported primitive type '{typeName}'");
			}
		}

		private static object OverridePrimitive(string path, object originalValue, TextureObjectOverrides overrides) {
			switch (path) {
				case "m_TextureFormat":
					return overrides.TextureFormat;
				case "m_CompleteImageSize":
					return (uint)overrides.CompleteImageSize;
				case "m_MipCount":
					return overrides.MipCount;
				case "m_StreamData.offset":
					return overrides.StreamOffset;
				case "m_StreamData.size":
					return overrides.StreamSize;
				default:
					return originalValue;
			}
		}

		private static void AssignValue(TextureReadState state, string path, object value) {
			switch (path) {
				case "m_Width":
					state.Width = Convert.ToInt32(value);
					return;
				case "m_Height":
					state.Height = Convert.ToInt32(value);
					return;
				case "m_TextureFormat":
					state.TextureFormat = Convert.ToInt32(value);
					return;
				case "m_MipCount":
					state.MipCount = Convert.ToInt32(value);
					return;
				case "image data":
					state.ImageData = (byte[])value;
					return;
				case "m_StreamData.offset":
					state.StreamOffset = Convert.ToUInt64(value);
					return;
				case "m_StreamData.size":
					state.StreamSize = Convert.ToUInt32(value);
					return;
				case "m_StreamData.path":
					state.StreamPath = value as string ?? string.Empty;
					return;
			}
		}

		private static void AlignAfterNode(List<TypeTreeNodeInfo> nodes, int index, EndianReader reader, EndianWriter writer = null) {
			var needsAlign = (nodes[index].MetaFlags & 0x4000) != 0;
			if (!needsAlign && index + 1 < nodes.Count && nodes[index + 1].Level == nodes[index].Level + 1 && string.Equals(nodes[index + 1].Name, "Array", StringComparison.Ordinal))
				needsAlign = (nodes[index + 1].MetaFlags & 0x4000) != 0;
			if (!needsAlign) return;

			reader.Align(4);
			writer?.Align(4);
		}

		private sealed class TextureReadState {
			public int Width { get; set; }
			public int Height { get; set; }
			public int TextureFormat { get; set; }
			public int MipCount { get; set; }
			public byte[] ImageData { get; set; }
			public ulong StreamOffset { get; set; }
			public uint StreamSize { get; set; }
			public string StreamPath { get; set; } = string.Empty;
		}
	}
}
