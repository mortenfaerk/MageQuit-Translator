using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MageQuitTranslator.Core;

/// <summary>
/// Applies/reverts the Danish-letter font patch (see tools/fonts/make_font_patch.py).
/// A patched Font object is appended to the asset file and the object's table entry is
/// repointed to it; reverting restores the entry and truncates the file, which brings
/// the file back to its exact original bytes.
/// </summary>
public static class FontPatcher
{
    public enum State { Original, Patched, Unknown }

    public sealed class Patch
    {
        [JsonPropertyName("file")] public string File { get; set; } = "";
        [JsonPropertyName("font")] public string Font { get; set; } = "";
        [JsonPropertyName("pathId")] public long PathId { get; set; }
        [JsonPropertyName("entryOffset")] public long EntryOffset { get; set; }
        [JsonPropertyName("origFileSize")] public uint OrigFileSize { get; set; }
        [JsonPropertyName("dataOffset")] public uint DataOffset { get; set; }
        [JsonPropertyName("origByteStart")] public uint OrigByteStart { get; set; }
        [JsonPropertyName("origByteSize")] public uint OrigByteSize { get; set; }
        [JsonPropertyName("fontDataOffset")] public int FontDataOffset { get; set; }
        [JsonPropertyName("origObjectSha256")] public string OrigObjectSha256 { get; set; } = "";
        [JsonPropertyName("newObjectSha256")] public string NewObjectSha256 { get; set; } = "";
        [JsonPropertyName("delta")] public List<DeltaOp> Delta { get; set; } = [];
    }

    public sealed class DeltaOp
    {
        [JsonPropertyName("copy")] public long[]? Copy { get; set; }
        [JsonPropertyName("insert")] public string? Insert { get; set; }
    }

    sealed class PatchFile
    {
        [JsonPropertyName("patches")] public List<Patch> Patches { get; set; } = [];
    }

    public static List<Patch> Load(string json) =>
        JsonSerializer.Deserialize<PatchFile>(json)?.Patches ?? [];

    public static State GetState(string gameDir, Patch p)
    {
        var path = Path.Combine(gameDir, p.File);
        if (!System.IO.File.Exists(path))
            return State.Unknown;
        using var f = System.IO.File.OpenRead(path);
        var (pathId, start, size) = ReadEntry(f, p.EntryOffset);
        if (pathId != p.PathId)
            return State.Unknown;
        if (start == p.OrigByteStart && size == p.OrigByteSize && f.Length == p.OrigFileSize)
            return Sha(ReadAt(f, p.DataOffset + start, (int)size)) == p.OrigObjectSha256 ? State.Original : State.Unknown;
        if (start >= p.OrigFileSize - p.DataOffset &&
            Sha(ReadAt(f, p.DataOffset + start, (int)size)) == p.NewObjectSha256)
            return State.Patched;
        return State.Unknown;
    }

    /// <summary>Patches the file. Returns false (and changes nothing) if it is not in the expected original state.</summary>
    public static bool Apply(string gameDir, Patch p)
    {
        var state = GetState(gameDir, p);
        if (state == State.Patched)
            return true;
        if (state != State.Original)
            return false;

        var path = Path.Combine(gameDir, p.File);
        using var f = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var original = ReadAt(f, p.DataOffset + p.OrigByteStart, (int)p.OrigByteSize);
        var patched = ApplyDelta(original, p.Delta);
        if (Sha(patched) != p.NewObjectSha256)
            throw new InvalidDataException($"Font patch for {p.Font} produced unexpected data.");

        long newStart = p.OrigFileSize - p.DataOffset;
        newStart += (16 - newStart % 16) % 16;
        f.SetLength(p.DataOffset + newStart);
        f.Position = p.DataOffset + newStart;
        f.Write(patched);
        var newSize = (uint)f.Length;

        Span<byte> entry = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(entry, (uint)newStart);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[4..], (uint)patched.Length);
        f.Position = p.EntryOffset + 8;
        f.Write(entry);
        WriteHeaderFileSize(f, newSize);
        f.Flush(true);
        return true;
    }

    /// <summary>Restores the original file. Returns false if the file is in an unrecognised state.</summary>
    public static bool Revert(string gameDir, Patch p)
    {
        var state = GetState(gameDir, p);
        if (state == State.Original)
            return true;
        if (state != State.Patched)
            return false;

        var path = Path.Combine(gameDir, p.File);
        using var f = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Span<byte> entry = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(entry, p.OrigByteStart);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[4..], p.OrigByteSize);
        f.Position = p.EntryOffset + 8;
        f.Write(entry);
        WriteHeaderFileSize(f, p.OrigFileSize);
        f.SetLength(p.OrigFileSize);
        f.Flush(true);
        return true;
    }

    /// <summary>The TTF bytes of the patched font (used to render label textures in the same typeface).</summary>
    public static byte[]? ReadPatchedTtf(string gameDir, Patch p)
    {
        if (GetState(gameDir, p) != State.Patched)
            return null;
        using var f = System.IO.File.OpenRead(Path.Combine(gameDir, p.File));
        var (_, start, size) = ReadEntry(f, p.EntryOffset);
        var obj = ReadAt(f, p.DataOffset + start, (int)size);
        var len = BinaryPrimitives.ReadInt32LittleEndian(obj.AsSpan(p.FontDataOffset));
        return obj.AsSpan(p.FontDataOffset + 4, len).ToArray();
    }

    internal static byte[] ApplyDelta(byte[] original, List<DeltaOp> ops)
    {
        using var ms = new MemoryStream();
        foreach (var op in ops)
        {
            if (op.Copy is { Length: 2 } c)
                ms.Write(original, (int)c[0], (int)c[1]);
            else if (op.Insert != null)
                ms.Write(Convert.FromBase64String(op.Insert));
        }
        return ms.ToArray();
    }

    static (long pathId, uint start, uint size) ReadEntry(Stream f, long offset)
    {
        var b = ReadAt(f, offset, 16);
        return (BinaryPrimitives.ReadInt64LittleEndian(b), BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(8)),
                BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(12)));
    }

    static void WriteHeaderFileSize(Stream f, uint size)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(b, size);
        f.Position = 4;
        f.Write(b);
    }

    static byte[] ReadAt(Stream f, long offset, int count)
    {
        if (offset < 0 || offset + count > f.Length)
            return [];
        var buf = new byte[count];
        f.Position = offset;
        f.ReadExactly(buf);
        return buf;
    }

    static string Sha(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
}
