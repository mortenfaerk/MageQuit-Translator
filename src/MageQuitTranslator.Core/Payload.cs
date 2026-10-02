using System.IO.Compression;
using System.Text;

namespace MageQuitTranslator.Core;

/// <summary>
/// The files the mod installs. Layout:
///   game/...                       copied into the game folder (BepInEx, XUnity, plugin, configs, font data)
///   languages/&lt;code&gt;/...    shipped language packs (language.json, strings.json, labels.json)
///   VERSION                        mod version
/// Comes from a payload.zip embedded in the app, or a staged payload folder during development.
/// </summary>
public abstract class Payload : IDisposable
{
    public abstract IEnumerable<string> GameFiles();   // paths relative to game/, '/' separated
    public abstract bool Exists(string path);
    public abstract byte[] Read(string path);           // path relative to payload root

    public string ReadText(string path) => Encoding.UTF8.GetString(Read(path));
    public string Version => ReadText("VERSION").Trim();

    public IEnumerable<string> LanguageCodes() =>
        AllFiles().Where(f => f.StartsWith("languages/") && f.EndsWith("/" + LanguagePack.InfoFile))
                  .Select(f => f.Split('/')[1]).Distinct().Order();

    public LanguagePack ReadLanguage(string code)
    {
        var dir = $"languages/{code}/";
        return LanguagePack.Parse(ReadText(dir + LanguagePack.InfoFile), ReadText(dir + LanguagePack.StringsFile),
                                  Exists(dir + LanguagePack.LabelsFile) ? ReadText(dir + LanguagePack.LabelsFile) : null);
    }

    protected abstract IEnumerable<string> AllFiles();

    public virtual void Dispose() { }

    public static Payload FromZip(Stream zip) => new ZipPayload(new ZipArchive(zip, ZipArchiveMode.Read));
    public static Payload FromFolder(string dir) => new FolderPayload(dir);

    sealed class ZipPayload(ZipArchive zip) : Payload
    {
        protected override IEnumerable<string> AllFiles() =>
            zip.Entries.Where(e => !e.FullName.EndsWith('/')).Select(e => e.FullName);

        public override IEnumerable<string> GameFiles() =>
            AllFiles().Where(f => f.StartsWith("game/")).Select(f => f["game/".Length..]);

        public override bool Exists(string path) => zip.GetEntry(path) != null;

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
        protected override IEnumerable<string> AllFiles() =>
            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'));

        public override IEnumerable<string> GameFiles() =>
            AllFiles().Where(f => f.StartsWith("game/")).Select(f => f["game/".Length..]);

        public override bool Exists(string path) => File.Exists(Path.Combine(root, path));
        public override byte[] Read(string path) => File.ReadAllBytes(Path.Combine(root, path));
    }
}
