using K4os.Compression.LZ4;

using System;

namespace Symphony.Utils {
	public enum LZ4CompressionLevel {
		Fast = 0,
		HC4 = 4
	}

	public static class LZ4 {
		public static int MaximumOutputSize(int sourceLength) => LZ4Codec.MaximumOutputSize(sourceLength);

		public static int Encode(ReadOnlySpan<byte> source, Span<byte> destination, LZ4CompressionLevel level)
			=> LZ4Codec.Encode(source, destination, GetLevel(level));

		public static int Decode(ReadOnlySpan<byte> source, Span<byte> destination)
			=> LZ4Codec.Decode(source, destination);

		private static LZ4Level GetLevel(LZ4CompressionLevel level) => level switch {
			LZ4CompressionLevel.Fast => LZ4Level.L00_FAST,
			LZ4CompressionLevel.HC4 => LZ4Level.L04_HC,
			_ => throw new ArgumentOutOfRangeException(nameof(level))
		};
	}
}
