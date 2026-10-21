namespace Fetchle.Core.Embedding;

// on-disk layout of potion-retrieval-32M.fmodel, a Model2Vec static model flattened so it can be memory-mapped
// and used in place: no parsing, no dictionary, no copy. tools/model/convert.cs makes it from the Hugging Face
// safetensors + tokenizer.json, ModelWriter writes it, ModelChecks validates it.
//
//   header (160 bytes)
//     0  u64 magic "fetchmdl"
//     8  u32 version
//    12  u32 endian marker 0x01020304, written native-endian
//    16  u64 file length
//    24  u64 xxhash3 of bytes [32, file length)
//    32  u32 dims, u32 vocab count, u32 unk id, u32 max chars per word
//    48  f32 scale: a stored int8 value times this is the original f32 weight
//    52  u32 length of the continuing-subword prefix, then 16 bytes of it (utf-8, zero padded)
//    72  u64 reserved
//    80  5 x (u64 offset, u64 length)
//   sections, each 8-byte aligned, zero padded
//
// bytes [0, 32) are each checked exactly, everything after by the checksum
static class ModelFormat
{
    public const ulong MAGIC = 0x6c64_6d68_6374_6566; // "fetchmdl" read little-endian
    public const uint VERSION = 1;
    public const uint ENDIAN_MARKER = 0x0102_0304;

    public const int MAGIC_OFFSET = 0;
    public const int VERSION_OFFSET = 8;
    public const int ENDIAN_OFFSET = 12;
    public const int LENGTH_OFFSET = 16;
    public const int CHECKSUM_OFFSET = 24;
    public const int CHECKSUMMED_FROM = 32;
    public const int DIMS_OFFSET = 32;
    public const int VOCAB_OFFSET = 36;
    public const int UNK_OFFSET = 40;
    public const int MAX_WORD_CHARS_OFFSET = 44;
    public const int SCALE_OFFSET = 48;
    public const int PREFIX_LENGTH_OFFSET = 52;
    public const int PREFIX_OFFSET = 56;
    public const int MAX_PREFIX_BYTES = 16;
    public const int SECTION_TABLE_OFFSET = 80;
    public const int SECTION_ENTRY_SIZE = 16;
    public const int SECTION_ALIGNMENT = 8;

    // u32 start offsets (vocab + 1) into the blob, in token id order
    public const int TOKEN_OFFSETS = 0;
    // every token's utf-8 bytes back to back, no separators
    public const int TOKEN_BLOB = 1;
    // u32 token ids ordered by their utf-8 bytes, so a lookup is a binary search
    public const int LOOKUP = 2;
    // i8 [vocab x dims], row = token id, one global scale
    public const int EMBEDDINGS = 3;
    // f32 [dims], mean of every row after L2 normalization
    public const int MEAN = 4;
    public const int SECTION_COUNT = 5;

    public const int HEADER_SIZE = SECTION_TABLE_OFFSET + SECTION_COUNT * SECTION_ENTRY_SIZE;

    public const int MAX_DIMS = 4096;
    public const int INT8_MAX = 127;

    public static long Align(long offset) => (offset + SECTION_ALIGNMENT - 1) & ~(long)(SECTION_ALIGNMENT - 1);
}
