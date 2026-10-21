namespace Fetchle.Core.Ranking.Semantic;

// on-disk layout of vectors.fvec, the segment vectors beside a snapshot. see docs/diagrams/vector-store.excalidraw
//
//   header (128 bytes)
//     0  u64 magic "fetchvec"
//     8  u32 version
//    12  u32 endian marker 0x01020304, written native-endian
//    16  u64 file length
//    24  u64 xxhash3 of bytes [32, file length)
//    32  u64 checksum of the snapshot these vectors belong to
//    40  u64 checksum of the model that made them
//    48  u32 segments, u32 entries
//    56  u32 code bytes per segment, u32 section count
//    64  3 x (u64 offset, u64 length)
//   sections, each 8-byte aligned, zero padded
//     codes   segments x 64 bytes, SignCodes
//     empty   one bit per segment: the name had nothing to embed (a hash, a version), its vector is zero
//     norms   per entry, f32 length of its path's weighted centroid, so a query needs no per-entry vector math
//
// a vectors file whose snapshot checksum isn't the open snapshot's is stale and ignored: lexical still answers
static class VectorFormat
{
    public const ulong MAGIC = 0x6365_7668_6374_6566; // "fetchvec" read little-endian
    public const uint VERSION = 1;
    public const uint ENDIAN_MARKER = 0x0102_0304;
    public const string FILE_NAME = "vectors.fvec";

    public const int MAGIC_OFFSET = 0;
    public const int VERSION_OFFSET = 8;
    public const int ENDIAN_OFFSET = 12;
    public const int LENGTH_OFFSET = 16;
    public const int CHECKSUM_OFFSET = 24;
    public const int CHECKSUMMED_FROM = 32;
    public const int SNAPSHOT_CHECKSUM_OFFSET = 32;
    public const int MODEL_CHECKSUM_OFFSET = 40;
    public const int SEGMENT_COUNT_OFFSET = 48;
    public const int ENTRY_COUNT_OFFSET = 52;
    public const int CODE_BYTES_OFFSET = 56;
    public const int SECTION_COUNT_OFFSET = 60;
    public const int SECTION_TABLE_OFFSET = 64;
    public const int SECTION_ENTRY_SIZE = 16;

    public const int CODES = 0;
    public const int EMPTY = 1;
    public const int NORMS = 2;
    public const int SECTION_COUNT = 3;

    public const int HEADER_SIZE = 128;
    public const int SECTION_ALIGNMENT = 8;

    public static long Align(long offset) => (offset + SECTION_ALIGNMENT - 1) & ~(long)(SECTION_ALIGNMENT - 1);

    public static int EmptyWords(int segments) => (segments + 63) / 64;
}
