#:project ../../src/Fetchle.Core/Fetchle.Core.csproj
#:property RestorePackagesWithLockFile=false

// usage: dotnet run -c Release tools/model/convert.cs -- <src-dir> <out-dir>
//
// fetches the pinned potion-retrieval-32M files into src-dir (skipping any whose sha256 already matches),
// then writes <out-dir>/potion-retrieval-32M.fmodel. does nothing when the output was already made from the
// same sources by the same format version. the layout is documented in src/Fetchle.Core/Embedding/ModelFormat.cs
using System.Security.Cryptography;
using System.Text.Json;
using Fetchle.Core.Embedding;

const int EXIT_USAGE = 2;
const int EXIT_FAILED = 1;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: convert.cs <src-dir> <out-dir>");
    return EXIT_USAGE;
}

try
{
    var source = ModelSource.Read(Path.Combine(AppContext.GetData("EntryPointFileDirectoryPath") as string ?? ".", "source.txt"));
    var srcDir = Path.GetFullPath(args[0]);
    var outDir = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(srcDir);
    Directory.CreateDirectory(outDir);

    foreach (var file in source.Files)
    {
        await Fetch.EnsureAsync(source, file, srcDir);
    }

    var output = Path.Combine(outDir, StaticModel.FILE_NAME);
    var stampPath = output + ".source";
    var stamp = string.Join('\n', [$"format {ModelWriter.FormatVersion}", $"revision {source.Revision}", .. source.Files.Select(f => $"{f.Name} {f.Sha256}")]) + '\n';
    if (File.Exists(output) && File.Exists(stampPath) && File.ReadAllText(stampPath) == stamp)
    {
        Console.WriteLine($"model up to date: {output} ({new FileInfo(output).Length:N0} bytes)");
        return 0;
    }

    var tokenizer = Tokenizer.Read(Path.Combine(srcDir, "tokenizer.json"));
    var weights = Safetensors.ReadEmbeddings(Path.Combine(srcDir, "model.safetensors"), out var dims);
    if (weights.Length != (long)tokenizer.Tokens.Length * dims)
    {
        throw new InvalidDataException($"{weights.Length / dims} embedding rows for {tokenizer.Tokens.Length} tokens");
    }

    var image = ModelWriter.Build(tokenizer.Tokens, weights, dims, tokenizer.UnkId, tokenizer.Prefix, tokenizer.MaxWordChars);
    ModelWriter.Write(output, image);
    File.WriteAllText(stampPath, stamp);
    Console.WriteLine($"wrote {output} ({image.Length:N0} bytes, {tokenizer.Tokens.Length} tokens x {dims} dims)");
    return 0;
}
catch (Exception e) when (e is IOException or HttpRequestException or InvalidDataException or JsonException or KeyNotFoundException or InvalidOperationException)
{
    Console.Error.WriteLine($"convert: {e.Message}");
    return EXIT_FAILED;
}

sealed record SourceFile(string Name, string Sha256);

sealed record ModelSource(string Repo, string Revision, SourceFile[] Files)
{
    public static ModelSource Read(string path)
    {
        string? repo = null;
        string? revision = null;
        var files = new List<SourceFile>();
        foreach (var line in File.ReadAllLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts[0].StartsWith('#'))
            {
                continue;
            }

            switch (parts)
            {
                case ["repo", var r]: repo = r; break;
                case ["revision", var v]: revision = v; break;
                case ["file", var name, var sha]: files.Add(new SourceFile(name, sha)); break;
                default: throw new InvalidDataException($"{path}: cannot read '{line}'");
            }
        }

        return new ModelSource(repo ?? throw new InvalidDataException("source.txt has no repo"),
            revision ?? throw new InvalidDataException("source.txt has no revision"), [.. files]);
    }

    public string UrlFor(string name) => $"https://huggingface.co/{Repo}/resolve/{Revision}/{name}";
}

static class Fetch
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public static async Task EnsureAsync(ModelSource source, SourceFile file, string dir)
    {
        var path = Path.Combine(dir, file.Name);
        if (File.Exists(path) && await Sha256Async(path) == file.Sha256)
        {
            Console.WriteLine($"cached {file.Name}");
            return;
        }

        var url = source.UrlFor(file.Name);
        Console.WriteLine($"downloading {url}");
        var temp = path + ".part";
        await using (var body = await Http.GetStreamAsync(url))
        await using (var target = File.Create(temp))
        {
            await body.CopyToAsync(target);
        }

        var actual = await Sha256Async(temp);
        if (actual != file.Sha256)
        {
            File.Delete(temp);
            throw new InvalidDataException($"{file.Name} sha256 is {actual}, expected {file.Sha256}");
        }

        File.Move(temp, path, overwrite: true);
    }

    static async Task<string> Sha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
    }
}

sealed record TokenizerData(string[] Tokens, int UnkId, string Prefix, int MaxWordChars);

static class Tokenizer
{
    public static TokenizerData Read(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        RequireBertPipeline(root);

        var model = root.GetProperty("model");
        if (model.GetProperty("type").GetString() != "WordPiece")
        {
            throw new InvalidDataException("tokenizer model is not WordPiece");
        }

        var vocab = model.GetProperty("vocab");
        var tokens = new string[vocab.GetPropertyCount()];
        foreach (var entry in vocab.EnumerateObject())
        {
            var id = entry.Value.GetInt32();
            if ((uint)id >= (uint)tokens.Length || tokens[id] is not null)
            {
                throw new InvalidDataException($"vocab ids are not dense, saw {id} for '{entry.Name}'");
            }

            tokens[id] = entry.Name;
        }

        var unk = model.GetProperty("unk_token").GetString()!;
        var unkId = vocab.GetProperty(unk).GetInt32();
        return new TokenizerData(tokens, unkId, model.GetProperty("continuing_subword_prefix").GetString()!,
            model.GetProperty("max_input_chars_per_word").GetInt32());
    }

    // the runtime hardcodes this pipeline, so a tokenizer.json that differs must stop the build rather than quietly drift
    static void RequireBertPipeline(JsonElement root)
    {
        var normalizer = root.GetProperty("normalizer");
        var strip = normalizer.GetProperty("strip_accents");
        var ok = normalizer.GetProperty("type").GetString() == "BertNormalizer"
            && normalizer.GetProperty("clean_text").GetBoolean()
            && normalizer.GetProperty("handle_chinese_chars").GetBoolean()
            && normalizer.GetProperty("lowercase").GetBoolean()
            && (strip.ValueKind == JsonValueKind.Null || strip.GetBoolean())
            && root.GetProperty("pre_tokenizer").GetProperty("type").GetString() == "BertPreTokenizer";
        if (!ok)
        {
            throw new InvalidDataException("tokenizer.json is not the BertNormalizer + BertPreTokenizer pipeline the runtime implements");
        }
    }
}

static class Safetensors
{
    const string TENSOR = "embeddings";

    // 8-byte little-endian header length, a json header, then raw tensor bytes
    public static float[] ReadEmbeddings(string path, out int dims)
    {
        using var handle = File.OpenHandle(path);
        Span<byte> length = stackalloc byte[8];
        RandomAccess.Read(handle, length, 0);
        var headerLength = (long)BitConverter.ToUInt64(length);
        if (headerLength is <= 0 or > 100_000_000)
        {
            throw new InvalidDataException("bad safetensors header length");
        }

        var headerBytes = new byte[headerLength];
        RandomAccess.Read(handle, headerBytes, 8);
        using var header = JsonDocument.Parse(headerBytes);
        var tensor = header.RootElement.GetProperty(TENSOR);
        if (tensor.GetProperty("dtype").GetString() != "F32")
        {
            throw new InvalidDataException("embeddings are not F32");
        }

        var shape = tensor.GetProperty("shape");
        if (shape.GetArrayLength() != 2)
        {
            throw new InvalidDataException("embeddings are not a matrix");
        }

        var rows = shape[0].GetInt32();
        dims = shape[1].GetInt32();
        var offsets = tensor.GetProperty("data_offsets");
        var begin = offsets[0].GetInt64();
        var end = offsets[1].GetInt64();
        if (end - begin != (long)rows * dims * sizeof(float))
        {
            throw new InvalidDataException("embeddings byte range does not match their shape");
        }

        var weights = new float[(long)rows * dims];
        var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(weights.AsSpan());
        var read = 0;
        while (read < bytes.Length)
        {
            var got = RandomAccess.Read(handle, bytes[read..], 8 + headerLength + begin + read);
            if (got == 0)
            {
                throw new InvalidDataException("safetensors file is truncated");
            }

            read += got;
        }

        return weights;
    }
}
