using System.IO.Compression;

namespace MageQuitDA.Core;

/// <summary>
/// The files the mod installs. Layout:
///   game/...         copied into the game folder as-is (BepInEx, XUnity, plugin, configs)
///   fontpatch.json   Danish-letter font patch
///   strings.json     shipped translations
///   labels.json      shipped image-label translations
///   VERSION          mod version
/// Comes from a payload.zip embedded in the Manager, or a payload folder during development.
/// </summary>
public abstract class Payload : IDisposable
{
    public abstract IEnumerable<string> GameFiles();   // paths relative to game/, '/' separated
    public abstract byte[] Read(string path);           // path relative to payload root

    public string ReadText(string path) => System.Text.Encoding.UTF8.GetString(Read(path));
    public string Version => ReadText("VERSION").Trim();

    public virtual void Dispose() { }

    public static Payload FromZip(Stream zip) => new ZipPayload(new ZipArchive(zip, ZipArchiveMode.Read));
    public static Payload FromFolder(string dir) => new FolderPayload(dir);

    sealed class ZipPayload(ZipArchive zip) : Payload
    {
        public override IEnumerable<string> GameFiles() =>
            zip.Entries.Where(e => e.FullName.StartsWith("game/") && !e.FullName.EndsWith('/'))
                       .Select(e => e.FullName["game/".Length..]);

        public override byte[] Read(string path)
        {
            var entry = zip.GetEntry(path) ?? throw new FileNotFoundException("Missing in payload: " + path);
            using var s = entry.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }

        public override void Dispose() => zip.Dispose();
    }

    sealed class FolderPayload(string root) : Payload
    {
        public override IEnumerable<string> GameFiles()
        {
            var game = Path.Combine(root, "game");
            return Directory.EnumerateFiles(game, "*", SearchOption.AllDirectories)
                            .Select(f => Path.GetRelativePath(game, f).Replace('\\', '/'));
        }

        public override byte[] Read(string path) => File.ReadAllBytes(Path.Combine(root, path));
    }
}
