using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MageQuitDA.Core;

/// <summary>Record of everything the mod put into the game folder, so uninstall can remove exactly that.</summary>
public sealed class InstallManifest
{
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("installedAt")] public DateTime InstalledAt { get; set; }
    /// <summary>True when BepInEx was installed by us (and may be removed entirely on uninstall).</summary>
    [JsonPropertyName("ownsBepInEx")] public bool OwnsBepInEx { get; set; }
    /// <summary>Files we created, relative to the game folder.</summary>
    [JsonPropertyName("files")] public List<string> Files { get; set; } = [];
    /// <summary>Pre-existing files we overwrote; originals are kept under BepInEx/MageQuit-DA/backup.</summary>
    [JsonPropertyName("backups")] public List<string> Backups { get; set; } = [];
    /// <summary>Directories we created (removed on uninstall if empty).</summary>
    [JsonPropertyName("dirs")] public List<string> Dirs { get; set; } = [];
    [JsonPropertyName("fontsPatched")] public List<string> FontsPatched { get; set; } = [];
}

public enum InstallState { NotInstalled, Enabled, Disabled }

/// <summary>Installs, updates, toggles and uninstalls the Danish translation in a MageQuit folder.</summary>
public sealed class Installer(string gameDir)
{
    public const string DataDir = "BepInEx/MageQuit-DA";
    public const string ManifestPath = DataDir + "/manifest.json";
    public const string StringsPath = DataDir + "/strings.json";
    public const string LabelsPath = DataDir + "/labels.json";
    public const string CapturedPath = DataDir + "/captured.tsv";
    public const string FontPatchPath = DataDir + "/fontpatch.json";
    public const string TextOutput = "BepInEx/Translation/da/Text/MageQuit.txt";
    public const string TextureDir = "BepInEx/Translation/da/Texture";
    const string BackupDir = DataDir + "/backup";
    const string DoorstopConfig = "doorstop_config.ini";
    const string XUnityConfig = "BepInEx/config/AutoTranslatorConfig.ini";
    const string PluginConfig = "BepInEx/config/dk.magequit.da.cfg";
    /// <summary>Loose files BepInEx puts in the game root; removed with BepInEx when we own it.</summary>
    static readonly string[] BepInExRootFiles = ["winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt"];

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string GameDir { get; } = gameDir;
    public Action<string>? Log { get; set; }

    string Full(string rel) => Path.Combine(GameDir, rel.Replace('/', Path.DirectorySeparatorChar));

    public InstallManifest? ReadManifest() =>
        File.Exists(Full(ManifestPath))
            ? JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(Full(ManifestPath)))
            : null;

    public InstallState State
    {
        get
        {
            var m = ReadManifest();
            if (m == null)
                return InstallState.NotInstalled;
            return IsEnabled(m) ? InstallState.Enabled : InstallState.Disabled;
        }
    }

    public void Install(Payload payload)
    {
        EnsureGameClosed();
        var old = ReadManifest();
        var manifest = new InstallManifest
        {
            Version = payload.Version,
            InstalledAt = DateTime.UtcNow,
            OwnsBepInEx = old?.OwnsBepInEx ?? !File.Exists(Full("BepInEx/core/BepInEx.dll")),
            Backups = old?.Backups ?? [],
            Dirs = old?.Dirs ?? [],
        };
        var previouslyOurs = new HashSet<string>(old?.Files ?? [], StringComparer.OrdinalIgnoreCase);
        bool wasDisabled = old != null && !IsEnabled(old);

        foreach (var rel in payload.GameFiles())
        {
            // An existing BepInEx belongs to the user; never replace its core files.
            if (!manifest.OwnsBepInEx && (rel.StartsWith("BepInEx/core/") || BepInExRootFiles.Contains(rel)))
                continue;
            // Don't clobber the user's settings for our own plugin on update.
            if (rel == PluginConfig && previouslyOurs.Contains(rel) && File.Exists(Full(rel)))
            {
                manifest.Files.Add(rel);
                continue;
            }
            WriteFile(manifest, previouslyOurs, rel, payload.Read("game/" + rel));
        }

        // Font patch: adds Æ Ø Å æ ø å to the two game fonts that lack them.
        foreach (var patch in FontPatcher.Load(payload.ReadText("game/" + FontPatchPath)))
        {
            if (FontPatcher.Apply(GameDir, patch))
            {
                manifest.FontsPatched.Add(patch.Font);
                Log?.Invoke($"Skrifttype opdateret: {patch.Font}");
            }
            else
                Log?.Invoke($"ADVARSEL: {patch.Font} kunne ikke opdateres (ukendt spilversion). Æ/Ø/Å kan mangle.");
        }

        // Working copies of the translations: keep the user's edits across updates.
        var shippedStrings = TranslationFile.Parse(payload.ReadText("strings.json"));
        var strings = File.Exists(Full(StringsPath)) ? TranslationFile.Load(Full(StringsPath)) : null;
        if (strings == null)
            strings = shippedStrings;
        else
            strings.MergeShipped(shippedStrings);
        var shippedLabels = LabelFile.Parse(payload.ReadText("labels.json"));
        var labels = File.Exists(Full(LabelsPath)) ? LabelFile.Load(Full(LabelsPath)) : null;
        if (labels == null)
            labels = shippedLabels;
        else
            labels.MergeShipped(shippedLabels);
        EnsureDir(manifest, DataDir);
        strings.Save(Full(StringsPath));
        labels.Save(Full(LabelsPath));
        Track(manifest, StringsPath);
        Track(manifest, LabelsPath);
        Apply(strings, labels, manifest);

        // Remove files an older version installed that this version no longer has.
        foreach (var stale in previouslyOurs.Except(manifest.Files, StringComparer.OrdinalIgnoreCase))
            DeleteFile(stale);

        SaveManifest(manifest);
        if (wasDisabled)
            SetEnabled(false);
        Log?.Invoke($"Installeret version {manifest.Version}.");
    }

    /// <summary>Regenerates the XUnity text file and label images from the working copies.</summary>
    public void Apply(TranslationFile strings, LabelFile labels, InstallManifest? manifest = null)
    {
        bool save = manifest == null;
        manifest ??= ReadManifest() ?? throw new InvalidOperationException("MageQuit-DA er ikke installeret.");
        strings.Save(Full(StringsPath));
        labels.Save(Full(LabelsPath));

        EnsureDir(manifest, Path.GetDirectoryName(TextOutput)!.Replace('\\', '/'));
        File.WriteAllText(Full(TextOutput), strings.ToXUnity(), new UTF8Encoding(false));
        Track(manifest, TextOutput);

        EnsureDir(manifest, TextureDir);
        var ttf = FontPatcher.Load(ReadFontPatchJson())
                             .Where(p => p.Font == "MageQuit-Body")
                             .Select(p => FontPatcher.ReadPatchedTtf(GameDir, p)).FirstOrDefault();
        if (ttf == null)
        {
            Log?.Invoke("ADVARSEL: Knap-billeder blev ikke lavet, fordi skrifttypen ikke er opdateret.");
        }
        else
        {
            using var renderer = new LabelRenderer(ttf);
            foreach (var label in labels.Labels)
            {
                var rel = TextureDir + "/" + label.FileName;
                if (string.IsNullOrWhiteSpace(label.Da))
                {
                    DeleteFile(rel);
                    continue;
                }
                File.WriteAllBytes(Full(rel), renderer.RenderPng(label));
                Track(manifest, rel);
            }
        }
        if (save)
            SaveManifest(manifest);
    }

    string ReadFontPatchJson()
    {
        // The patch descriptor is installed with the data so Apply()/Uninstall() work without the payload.
        return File.ReadAllText(Full(FontPatchPath));
    }

    public void SetEnabled(bool enabled)
    {
        var m = ReadManifest() ?? throw new InvalidOperationException("MageQuit-DA er ikke installeret.");
        if (m.OwnsBepInEx)
            SetIniValue(DoorstopConfig, "enabled", enabled ? "true" : "false");
        else
            SetIniValue(XUnityConfig, "Language", enabled ? "da" : "en");
    }

    bool IsEnabled(InstallManifest m) =>
        m.OwnsBepInEx
            ? !string.Equals(GetIniValue(DoorstopConfig, "enabled"), "false", StringComparison.OrdinalIgnoreCase)
            : GetIniValue(XUnityConfig, "Language") == "da";

    /// <summary>Plugin's capture mode, which records every UI string for import into the editor.</summary>
    public bool CaptureEnabled
    {
        get => string.Equals(GetIniValue(PluginConfig, "Enabled"), "true", StringComparison.OrdinalIgnoreCase);
        set => SetIniValue(PluginConfig, "Enabled", value ? "true" : "false");
    }

    public void Uninstall()
    {
        EnsureGameClosed();
        var m = ReadManifest() ?? throw new InvalidOperationException("MageQuit-DA er ikke installeret.");

        foreach (var patch in FontPatcher.Load(ReadFontPatchJson()))
            if (!FontPatcher.Revert(GameDir, patch))
                Log?.Invoke($"ADVARSEL: {patch.Font} kunne ikke gendannes. Brug 'Kontrollér filernes integritet' i Steam.");

        // Restore originals of files we overwrote, before deleting our data folder (which holds them).
        foreach (var rel in m.Backups)
        {
            var backup = Full(BackupDir + "/" + rel);
            if (File.Exists(backup))
                File.Copy(backup, Full(rel), overwrite: true);
        }
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
            // XUnity's own output for our language.
            DeleteDir("BepInEx/Translation/da");
            if (m.OwnsBepInEx)
                Log?.Invoke("BepInEx blev beholdt, fordi der er andre mods installeret.");
        }
        foreach (var dir in m.Dirs.OrderByDescending(d => d.Length))
            if (Directory.Exists(Full(dir)) && !Directory.EnumerateFileSystemEntries(Full(dir)).Any())
                Directory.Delete(Full(dir));
        Log?.Invoke("MageQuit-DA er afinstalleret.");
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
        File.WriteAllText(Full(ManifestPath), JsonSerializer.Serialize(m, JsonOptions));
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

    void EnsureGameClosed()
    {
        if (GameLocator.IsGameRunning())
            throw new InvalidOperationException("Luk MageQuit først.");
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
