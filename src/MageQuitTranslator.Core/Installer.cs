using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MageQuitTranslator.Core;

/// <summary>Record of everything the mod put into the game folder, so uninstall can remove exactly that.</summary>
public sealed class InstallManifest
{
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("installedAt")] public DateTime InstalledAt { get; set; }
    /// <summary>True when BepInEx was installed by us (and may be removed entirely on uninstall).</summary>
    [JsonPropertyName("ownsBepInEx")] public bool OwnsBepInEx { get; set; }
    /// <summary>Files we created, relative to the game folder.</summary>
    [JsonPropertyName("files")] public List<string> Files { get; set; } = [];
    /// <summary>Pre-existing files we overwrote; originals are kept under the data folder's backup/.</summary>
    [JsonPropertyName("backups")] public List<string> Backups { get; set; } = [];
    /// <summary>Directories we created (removed on uninstall if empty).</summary>
    [JsonPropertyName("dirs")] public List<string> Dirs { get; set; } = [];
    [JsonPropertyName("fontsPatched")] public List<string> FontsPatched { get; set; } = [];
}

/// <summary>Installs, updates, switches language and uninstalls the translator in a MageQuit folder.</summary>
public sealed class Installer(string gameDir)
{
    public const string DataDir = "BepInEx/MageQuit-Translator";
    public const string ManifestPath = DataDir + "/manifest.json";
    public const string LanguagesDir = DataDir + "/languages";
    public const string CapturedPath = DataDir + "/captured.tsv";
    public const string FontPatchPath = DataDir + "/fontpatch.json";
    public const string FontsPath = DataDir + "/fonts.json";
    const string TranslationRoot = "BepInEx/Translation";
    const string BackupDir = DataDir + "/backup";
    const string DoorstopConfig = "doorstop_config.ini";
    const string XUnityConfig = "BepInEx/config/AutoTranslatorConfig.ini";
    const string PluginConfig = "BepInEx/config/magequit.translator.cfg";
    /// <summary>Loose files BepInEx puts in the game root; removed with BepInEx when we own it.</summary>
    static readonly string[] BepInExRootFiles = ["winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt"];

    public string GameDir { get; } = gameDir;
    public Action<string>? Log { get; set; }

    string Full(string rel) => Path.Combine(GameDir, rel.Replace('/', Path.DirectorySeparatorChar));
    static string LanguageDir(string code) => $"{LanguagesDir}/{code}";
    public static string TextOutput(string code) => $"{TranslationRoot}/{code}/Text/MageQuit.txt";
    public static string TextureDir(string code) => $"{TranslationRoot}/{code}/Texture";

    public InstallManifest? ReadManifest() =>
        File.Exists(Full(ManifestPath)) ? JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(Full(ManifestPath))) : null;

    public bool IsInstalled => File.Exists(Full(ManifestPath));

    // ---- Languages -------------------------------------------------------------------------

    /// <summary>All languages in the game folder (shipped ones and ones the user created).</summary>
    public List<LanguagePack> LoadLanguages()
    {
        var dir = Full(LanguagesDir);
        if (!Directory.Exists(dir))
            return [];
        return Directory.EnumerateDirectories(dir)
                        .Where(d => File.Exists(Path.Combine(d, LanguagePack.InfoFile)))
                        .Select(LanguagePack.Load)
                        .OrderBy(p => p.Info.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// The language the game is translated into, or null when the translation is turned off
    /// (the game then runs in English).
    /// </summary>
    public string? ActiveLanguage
    {
        get
        {
            var m = ReadManifest();
            if (m == null)
                return null;
            if (m.OwnsBepInEx && string.Equals(GetIniValue(DoorstopConfig, "enabled"), "false", StringComparison.OrdinalIgnoreCase))
                return null;
            var lang = GetIniValue(XUnityConfig, "Language");
            return lang is null or "en" ? null : lang;
        }
        set
        {
            var m = ReadManifest() ?? throw new InvalidOperationException("MageQuit Translator is not installed.");
            if (value != null)
            {
                if (!Directory.Exists(Full(LanguageDir(value))))
                    throw new ArgumentException($"Language '{value}' is not installed.");
                SetIniValue(XUnityConfig, "Language", value);
            }
            else if (!m.OwnsBepInEx)
            {
                SetIniValue(XUnityConfig, "Language", "en");
            }
            if (m.OwnsBepInEx)
                SetIniValue(DoorstopConfig, "enabled", value != null ? "true" : "false");
        }
    }

    /// <summary>Saves a language's working copy and regenerates what the game reads (text file and label images).</summary>
    public void SaveLanguage(LanguagePack pack)
    {
        var manifest = ReadManifest() ?? throw new InvalidOperationException("MageQuit Translator is not installed.");
        pack.Save(Full(LanguageDir(pack.Code)));
        Generate(pack, manifest);
        SaveManifest(manifest);
    }

    /// <summary>Starts a new language from every English string of <paramref name="template"/>.</summary>
    public LanguagePack CreateLanguage(LanguageInfo info, LanguagePack template)
    {
        if (Directory.Exists(Full(LanguageDir(info.Code))))
            throw new InvalidOperationException($"A language with the code '{info.Code}' already exists.");
        var pack = LanguagePack.CreateFrom(template, info);
        SaveLanguage(pack);
        Log?.Invoke($"Created {info} with {pack.Strings.Strings.Count} strings to translate.");
        return pack;
    }

    // ---- Install / uninstall ----------------------------------------------------------------

    public void Install(Payload payload, string? activeLanguage = null)
    {
        EnsureGameClosed();
        var old = ReadManifest();
        var previousLanguage = old != null ? ActiveLanguage : null;
        bool wasOff = old != null && previousLanguage == null;
        var manifest = new InstallManifest
        {
            Version = payload.Version,
            InstalledAt = DateTime.UtcNow,
            OwnsBepInEx = old?.OwnsBepInEx ?? !File.Exists(Full("BepInEx/core/BepInEx.dll")),
            Backups = old?.Backups ?? [],
            Dirs = old?.Dirs ?? [],
        };
        var previouslyOurs = new HashSet<string>(old?.Files ?? [], StringComparer.OrdinalIgnoreCase);

        foreach (var rel in payload.GameFiles())
        {
            // An existing BepInEx belongs to the user; never replace its core files.
            if (!manifest.OwnsBepInEx && (rel.StartsWith("BepInEx/core/") || BepInExRootFiles.Contains(rel)))
                continue;
            // Keep the user's settings for our own plugin and XUnity on update.
            if (rel is PluginConfig or XUnityConfig && previouslyOurs.Contains(rel) && File.Exists(Full(rel)))
            {
                Track(manifest, rel);
                continue;
            }
            WriteFile(manifest, previouslyOurs, rel, payload.Read("game/" + rel));
        }

        foreach (var patch in FontPatcher.Load(File.ReadAllText(Full(FontPatchPath))))
        {
            if (FontPatcher.Apply(GameDir, patch))
            {
                manifest.FontsPatched.Add(patch.Font);
                Log?.Invoke($"Font updated with accented letters: {patch.Font}");
            }
            else
                Log?.Invoke($"WARNING: {patch.Font} could not be updated (unknown game version); accented letters may be missing.");
        }

        // Language working copies: shipped updates are merged in, the user's edits and own languages are kept.
        EnsureDir(manifest, LanguagesDir);
        var shippedCodes = payload.LanguageCodes().ToList();
        foreach (var code in shippedCodes)
        {
            var shipped = payload.ReadLanguage(code);
            var dir = Full(LanguageDir(code));
            LanguagePack pack;
            if (Directory.Exists(dir) && File.Exists(Path.Combine(dir, LanguagePack.InfoFile)))
            {
                pack = LanguagePack.Load(dir);
                pack.MergeShipped(shipped);
            }
            else
                pack = shipped;
            pack.Save(dir);
        }
        foreach (var pack in LoadLanguages())
            Generate(pack, manifest);

        // Remove files an older version installed that this version no longer has.
        foreach (var stale in previouslyOurs.Except(manifest.Files, StringComparer.OrdinalIgnoreCase))
            if (!stale.StartsWith(TranslationRoot + "/"))
                DeleteFile(stale);

        SaveManifest(manifest);
        ActiveLanguage = wasOff ? null : activeLanguage ?? previousLanguage ?? DefaultLanguage(shippedCodes);
        Log?.Invoke($"Installed MageQuit Translator {manifest.Version} with {shippedCodes.Count} language(s).");
    }

    /// <summary>The user's OS language if we ship it, otherwise the first shipped language.</summary>
    static string? DefaultLanguage(List<string> codes)
    {
        var culture = CultureInfo.CurrentUICulture;
        return codes.FirstOrDefault(c => c == culture.Name)
               ?? codes.FirstOrDefault(c => c == culture.TwoLetterISOLanguageName)
               ?? codes.FirstOrDefault();
    }

    public void Uninstall()
    {
        EnsureGameClosed();
        var m = ReadManifest() ?? throw new InvalidOperationException("MageQuit Translator is not installed.");

        foreach (var patch in FontPatcher.Load(File.ReadAllText(Full(FontPatchPath))))
            if (!FontPatcher.Revert(GameDir, patch))
                Log?.Invoke($"WARNING: {patch.Font} could not be restored. Use 'Verify integrity of game files' in Steam.");

        // Restore originals of files we overwrote, before deleting the data folder (which holds them).
        foreach (var rel in m.Backups)
        {
            var backup = Full(BackupDir + "/" + rel);
            if (File.Exists(backup))
                File.Copy(backup, Full(rel), overwrite: true);
        }
        var codes = LoadLanguages().Select(p => p.Code).ToList();
        var restored = new HashSet<string>(m.Backups, StringComparer.OrdinalIgnoreCase);
        foreach (var rel in m.Files.Where(f => !restored.Contains(f)))
            DeleteFile(rel);

        if (m.OwnsBepInEx && !HasForeignPlugins(m))
        {
            // Everything under BepInEx (logs, caches, generated configs) came from us.
            DeleteDir("BepInEx");
            foreach (var rel in BepInExRootFiles)
                DeleteFile(rel);
        }
        else
        {
            DeleteDir(DataDir);
            foreach (var code in codes)
                DeleteDir($"{TranslationRoot}/{code}");
            if (m.OwnsBepInEx)
                Log?.Invoke("BepInEx was kept because other mods are installed.");
        }
        foreach (var dir in m.Dirs.OrderByDescending(d => d.Length))
            if (Directory.Exists(Full(dir)) && !Directory.EnumerateFileSystemEntries(Full(dir)).Any())
                Directory.Delete(Full(dir));
        Log?.Invoke("MageQuit Translator was uninstalled.");
    }

    /// <summary>Plugin's capture mode, which records every UI string for import into the editor.</summary>
    public bool CaptureEnabled
    {
        get => string.Equals(GetIniValue(PluginConfig, "Enabled"), "true", StringComparison.OrdinalIgnoreCase);
        set => SetIniValue(PluginConfig, "Enabled", value ? "true" : "false");
    }

    public FontCoverage LoadFontCoverage() => FontCoverage.Load(Full(FontsPath));

    /// <summary>The TTF of the patched label font, for rendering label images; null if the patch is not applied.</summary>
    public byte[]? ReadLabelFont()
    {
        if (!File.Exists(Full(FontPatchPath)))
            return null;
        return FontPatcher.Load(File.ReadAllText(Full(FontPatchPath)))
                          .Where(p => p.Font == "MageQuit-Body")
                          .Select(p => FontPatcher.ReadPatchedTtf(GameDir, p)).FirstOrDefault();
    }

    // ---- Internals --------------------------------------------------------------------------

    void Generate(LanguagePack pack, InstallManifest manifest)
    {
        var text = TextOutput(pack.Code);
        EnsureDir(manifest, Path.GetDirectoryName(text)!.Replace('\\', '/'));
        File.WriteAllText(Full(text), pack.Strings.ToXUnity(), new UTF8Encoding(false));
        Track(manifest, text);

        var textureDir = TextureDir(pack.Code);
        EnsureDir(manifest, textureDir);
        var ttf = ReadLabelFont();
        if (ttf == null)
        {
            Log?.Invoke("WARNING: Menu images were not generated because the label font is not patched.");
            return;
        }
        using var renderer = new LabelRenderer(ttf);
        foreach (var label in pack.Labels.Labels)
        {
            var rel = textureDir + "/" + label.FileName;
            if (string.IsNullOrWhiteSpace(label.Text))
            {
                DeleteFile(rel);
                continue;
            }
            File.WriteAllBytes(Full(rel), renderer.RenderPng(label));
            Track(manifest, rel);
        }
    }

    /// <summary>True if the user added other BepInEx plugins/patchers after we installed BepInEx.</summary>
    bool HasForeignPlugins(InstallManifest m)
    {
        var ours = new HashSet<string>(m.Files, StringComparer.OrdinalIgnoreCase);
        foreach (var dir in new[] { "BepInEx/plugins", "BepInEx/patchers" })
        {
            if (!Directory.Exists(Full(dir)))
                continue;
            foreach (var file in Directory.EnumerateFiles(Full(dir), "*", SearchOption.AllDirectories))
                if (!ours.Contains(Path.GetRelativePath(GameDir, file).Replace('\\', '/')))
                    return true;
        }
        return false;
    }

    void WriteFile(InstallManifest m, HashSet<string> previouslyOurs, string rel, byte[] data)
    {
        var full = Full(rel);
        if (File.Exists(full) && !previouslyOurs.Contains(rel) && !m.Backups.Contains(rel))
        {
            var backup = Full(BackupDir + "/" + rel);
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            File.Copy(full, backup, overwrite: true);
            m.Backups.Add(rel);
        }
        EnsureDir(m, Path.GetDirectoryName(rel.Replace('/', Path.DirectorySeparatorChar))?.Replace('\\', '/') ?? "");
        File.WriteAllBytes(full, data);
        Track(m, rel);
    }

    static void Track(InstallManifest m, string rel)
    {
        if (!m.Files.Contains(rel, StringComparer.OrdinalIgnoreCase))
            m.Files.Add(rel);
    }

    void EnsureDir(InstallManifest m, string rel)
    {
        if (string.IsNullOrEmpty(rel))
            return;
        var parts = rel.Split('/');
        for (int i = 1; i <= parts.Length; i++)
        {
            var sub = string.Join('/', parts.Take(i));
            if (!Directory.Exists(Full(sub)))
            {
                Directory.CreateDirectory(Full(sub));
                if (!m.Dirs.Contains(sub))
                    m.Dirs.Add(sub);
            }
        }
    }

    void SaveManifest(InstallManifest m)
    {
        Directory.CreateDirectory(Full(DataDir));
        File.WriteAllText(Full(ManifestPath), JsonSerializer.Serialize(m, new JsonSerializerOptions { WriteIndented = true }));
    }

    void DeleteFile(string rel)
    {
        if (File.Exists(Full(rel)))
            File.Delete(Full(rel));
    }

    void DeleteDir(string rel)
    {
        if (Directory.Exists(Full(rel)))
            Directory.Delete(Full(rel), recursive: true);
    }

    static void EnsureGameClosed()
    {
        if (GameLocator.IsGameRunning())
            throw new InvalidOperationException("Close MageQuit first.");
    }

    string? GetIniValue(string rel, string key)
    {
        if (!File.Exists(Full(rel)))
            return null;
        foreach (var line in File.ReadLines(Full(rel)))
        {
            var t = line.Trim();
            if (t.StartsWith(key, StringComparison.Ordinal) && t[key.Length..].TrimStart().StartsWith('='))
                return t[(t.IndexOf('=') + 1)..].Split(';')[0].Trim();
        }
        return null;
    }

    void SetIniValue(string rel, string key, string value)
    {
        var lines = File.ReadAllLines(Full(rel)).ToList();
        for (int i = 0; i < lines.Count; i++)
        {
            var t = lines[i].Trim();
            if (t.StartsWith(key, StringComparison.Ordinal) && t[key.Length..].TrimStart().StartsWith('='))
            {
                lines[i] = $"{key} = {value}";
                File.WriteAllLines(Full(rel), lines);
                return;
            }
        }
        throw new InvalidDataException($"{key} not found in {rel}");
    }
}
