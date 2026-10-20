namespace Fetchle.Core.Index;

// on-disk layout of snapshot.fidx, see docs/diagrams/path-store.excalidraw
//
//   header (192 bytes)
//     0  u64 magic "fetchidx"
//     8  u32 version
//    12  u32 endian marker 0x01020304, written native-endian
//    16  u64 file length
//    24  u64 xxhash3 of bytes [32, file length)
//    32  i64 built at, unix ms
//    40  u32 entries, u32 segments, u32 roots, u32 non-ascii segments
//    56  u32 section count, u32 reserved
//    64  8 x (u64 offset, u64 length)
//   sections, each 8-byte aligned, zero padded
//
// bytes [0, 32) are each checked exactly, everything after by the checksum
static class SnapshotFormat
{
    public const ulong MAGIC = 0x7864_6968_6374_6566; // "fetchidx" read little-endian
    public const uint VERSION = 1;
    public const uint ENDIAN_MARKER = 0x0102_0304;

    public const int MAGIC_OFFSET = 0;
    public const int VERSION_OFFSET = 8;
    public const int ENDIAN_OFFSET = 12;
    public const int LENGTH_OFFSET = 16;
    public const int CHECKSUM_OFFSET = 24;
    public const int CHECKSUMMED_FROM = 32;
    public const int BUILT_AT_OFFSET = 32;
    public const int ENTRY_COUNT_OFFSET = 40;
    public const int SEGMENT_COUNT_OFFSET = 44;
    public const int ROOT_COUNT_OFFSET = 48;
    public const int NON_ASCII_COUNT_OFFSET = 52;
    public const int SECTION_COUNT_OFFSET = 56;
    public const int SECTION_TABLE_OFFSET = 64;
    public const int SECTION_ENTRY_SIZE = 16;

    public const int SECTION_ALIGNMENT = 8;

    // entries: parent id, segment id (root index for roots), flags. roots come first, then breadth-first,
    // so a parent's id is always lower than its children's and the parent column never decreases
    public const int PARENTS = 0;
    public const int NAMES = 1;
    public const int FLAGS = 2;
    // unique name segments: u32 start offsets (count + 1) into a utf-8 blob, each segment NUL-terminated
    public const int SEGMENT_OFFSETS = 3;
    public const int SEGMENT_BLOB = 4;
    // ascending ids of segments with non-ascii bytes, the only ones that need decoding to match
    public const int NON_ASCII = 5;
    // per root: u32 entry id, u32 path offset, u32 path length, u32 entries, i64 walk time ticks
    public const int ROOTS = 6;
    public const int ROOT_PATHS = 7;
    public const int SECTION_COUNT = 8;

    public const int HEADER_SIZE = SECTION_TABLE_OFFSET + SECTION_COUNT * SECTION_ENTRY_SIZE;

    public const int ROOT_RECORD_SIZE = 24;

    public const uint NO_PARENT = uint.MaxValue;

    public const byte FLAG_DIRECTORY = 1;
    public const byte FLAG_ROOT = 2;
    public const byte KNOWN_FLAGS = FLAG_DIRECTORY | FLAG_ROOT;

    public static long Align(long offset) => (offset + SECTION_ALIGNMENT - 1) & ~(long)(SECTION_ALIGNMENT - 1);
}
