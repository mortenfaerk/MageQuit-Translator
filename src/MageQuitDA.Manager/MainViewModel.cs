using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Media.Imaging;
using MageQuitDA.Core;

namespace MageQuitDA.Manager;

public sealed class MainViewModel : ViewModelBase
{
    string? _gameDir;
    string _status = "";
    string _search = "";
    FilterOption _filter = Collections.Filters[1];
    EntryViewModel? _selected;
    bool _dirty;
    bool _busy;
    List<EntryViewModel> _all = [];
    TranslationFile? _strings;
    LabelFile? _labels;
    LabelRenderer? _renderer;

    public MainViewModel()
    {
        GameDir = Settings.Load().GameDir is { } saved && GameLocator.IsGameDir(saved) ? saved : GameLocator.Find();
    }

    public ObservableCollection<EntryViewModel> Entries { get; } = [];
    public ObservableCollection<LabelViewModel> Labels { get; } = [];
    public ObservableCollection<LogLine> Log { get; } = [];
    public ObservableCollection<FilterOption> Filters => Collections.Filters;

    public string AppVersion => "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?");

    public string? GameDir
    {
        get => _gameDir;
        set
        {
            if (!Set(ref _gameDir, value))
                return;
            Refresh();
        }
    }

    /// <summary>A folder picked by hand is remembered; auto-detected folders are not (nothing is left behind).</summary>
    public void ChooseGameDir(string dir)
    {
        GameDir = dir;
        if (HasGame && !string.Equals(GameLocator.Find(), dir, StringComparison.OrdinalIgnoreCase))
            Settings.Save(new Settings { GameDir = dir });
        else if (!HasGame)
            AddLog("Mappen indeholder ikke MageQuit.exe.");
    }

    public string Status { get => _status; private set => Set(ref _status, value); }
    public bool HasGame => GameLocator.IsGameDir(GameDir);
    public bool IsInstalled => HasGame && Installer!.State != InstallState.NotInstalled;
    public bool IsEnabled => HasGame && Installer!.State == InstallState.Enabled;
    public bool CanInstall => HasGame && !Busy;
    public bool CanEdit => IsInstalled && !Busy;
    public string InstallButtonText => IsInstalled ? "Opdatér / reparér" : "Installér dansk";
    public string ToggleButtonText => IsEnabled ? "Slå dansk fra" : "Slå dansk til";
    public bool ShowLinuxHint => GameLocator.RunningOnLinux;
    public string LinuxLaunchOption => "WINEDLLOVERRIDES=\"winhttp=n,b\" %command%";

    public bool CaptureEnabled
    {
        get => IsInstalled && Installer!.CaptureEnabled;
        set
        {
            if (!IsInstalled)
                return;
            Installer!.CaptureEnabled = value;
            AddLog(value
                ? "Opfangning slået til. Spil spillet, og importér derefter de opfangede tekster."
                : "Opfangning slået fra.");
            Raise();
        }
    }

    public bool Busy
    {
        get => _busy;
        private set
        {
            Set(ref _busy, value);
            Raise(nameof(CanInstall));
            Raise(nameof(CanEdit));
        }
    }

    public bool Dirty
    {
        get => _dirty;
        private set
        {
            Set(ref _dirty, value);
            Raise(nameof(Title));
        }
    }

    public string Title => "MageQuit på dansk" + (Dirty ? " •" : "");

    public string Search
    {
        get => _search;
        set { if (Set(ref _search, value)) ApplyFilter(); }
    }

    public FilterOption Filter
    {
        get => _filter;
        set { if (Set(ref _filter, value)) ApplyFilter(); }
    }

    public EntryViewModel? Selected { get => _selected; set => Set(ref _selected, value); }

    public string Progress
    {
        get
        {
            if (_all.Count == 0)
                return "";
            int reviewed = _all.Count(e => e.Status == "reviewed");
            int machine = _all.Count(e => e.Status == "machine");
            int missing = _all.Count - reviewed - machine;
            return $"{_all.Count} tekster · {reviewed} godkendt · {machine} udkast · {missing} mangler";
        }
    }

    Installer? Installer => HasGame ? new Installer(GameDir!) { Log = AddLog } : null;

    public void AddLog(string text) => Log.Insert(0, new LogLine(text));

    public void Refresh()
    {
        foreach (var name in new[] { nameof(HasGame), nameof(IsInstalled), nameof(IsEnabled), nameof(CanInstall),
                     nameof(CanEdit), nameof(InstallButtonText), nameof(ToggleButtonText), nameof(CaptureEnabled) })
            Raise(name);

        if (!HasGame)
        {
            Status = "MageQuit blev ikke fundet. Vælg spillets mappe.";
            Unload();
            return;
        }
        var manifest = Installer!.ReadManifest();
        Status = manifest == null
            ? "Ikke installeret."
            : $"Installeret (version {manifest.Version}) · {(IsEnabled ? "dansk er slået til" : "dansk er slået fra")}" +
              (manifest.FontsPatched.Count == 2 ? "" : " · ADVARSEL: skrifttyper ikke opdateret");
        if (manifest != null)
            LoadTranslations();
        else
            Unload();
    }

    void Unload()
    {
        _strings = null;
        _labels = null;
        _all = [];
        Entries.Clear();
        Labels.Clear();
        Raise(nameof(Progress));
    }

    void LoadTranslations()
    {
        var dir = GameDir!;
        _strings = TranslationFile.Load(Path.Combine(dir, Installer.StringsPath));
        _labels = LabelFile.Load(Path.Combine(dir, Installer.LabelsPath));
        _renderer?.Dispose();
        _renderer = null;
        var patch = FontPatcher.Load(File.ReadAllText(Path.Combine(dir, Installer.FontPatchPath)))
                               .FirstOrDefault(p => p.Font == "MageQuit-Body");
        if (patch != null && FontPatcher.ReadPatchedTtf(dir, patch) is { } ttf)
            _renderer = new LabelRenderer(ttf);

        _all = _strings.Strings.Select(e => new EntryViewModel(e, MarkDirty)).ToList();
        Labels.Clear();
        foreach (var l in _labels.Labels)
            Labels.Add(new LabelViewModel(l, RenderLabel, MarkDirty));
        Dirty = false;
        ApplyFilter();
    }

    Bitmap? RenderLabel(LabelEntry label)
    {
        if (_renderer == null)
            return null;
        using var ms = new MemoryStream(_renderer.RenderPng(label));
        return new Bitmap(ms);
    }

    void MarkDirty()
    {
        Dirty = true;
        Raise(nameof(Progress));
    }

    void ApplyFilter()
    {
        var q = Search.Trim();
        IEnumerable<EntryViewModel> items = _all;
        if (q.Length > 0)
            items = items.Where(e => e.En.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                     e.Da.Contains(q, StringComparison.OrdinalIgnoreCase));
        items = Filter.Key switch
        {
            "todo" => items.Where(e => e.Status != "reviewed"),
            "new" => items.Where(e => e.Status == "new"),
            "machine" => items.Where(e => e.Status == "machine"),
            "reviewed" => items.Where(e => e.Status == "reviewed"),
            "warn" => items.Where(e => e.Warning.Length > 0),
            "captured" => items.Where(e => e.Entry.Confidence == "captured"),
            _ => items,
        };
        Entries.Clear();
        foreach (var e in items)
            Entries.Add(e);
        if (Selected == null || !Entries.Contains(Selected))
            Selected = Entries.FirstOrDefault();
        Raise(nameof(Progress));
    }

    public async Task InstallAsync()
    {
        if (Dirty)
            Save();
        await RunAsync(() =>
        {
            using var payload = Cli.OpenPayload();
            Installer!.Install(payload);
        });
    }

    public async Task UninstallAsync() => await RunAsync(() => Installer!.Uninstall());

    public async Task ToggleAsync()
    {
        bool enable = !IsEnabled;
        await RunAsync(() => Installer!.SetEnabled(enable));
        AddLog(enable ? "Dansk er slået til." : "Dansk er slået fra (spillet kører på engelsk).");
    }

    async Task RunAsync(Action action)
    {
        Busy = true;
        try
        {
            await Task.Run(action);
        }
        catch (Exception ex)
        {
            AddLog("FEJL: " + ex.Message);
        }
        finally
        {
            Busy = false;
            Refresh();
        }
    }

    public void Save()
    {
        if (_strings == null || _labels == null)
            return;
        try
        {
            Installer!.Apply(_strings, _labels);
            Dirty = false;
            AddLog("Gemt. Tryk Alt+R i spillet for at se ændringerne (knap-billeder kræver genstart af spillet).");
        }
        catch (Exception ex)
        {
            AddLog("FEJL ved gem: " + ex.Message);
        }
    }

    public void ImportCaptured()
    {
        if (_strings == null)
            return;
        var path = Path.Combine(GameDir!, Installer.CapturedPath);
        if (!File.Exists(path))
        {
            AddLog("Der er ingen opfangede tekster endnu. Slå opfangning til og spil lidt først.");
            return;
        }
        int added = _strings.ImportCaptured(path);
        AddLog($"{added} nye tekster importeret fra spillet.");
        if (added > 0)
        {
            _all = _strings.Strings.Select(e => new EntryViewModel(e, MarkDirty)).ToList();
            Filter = Filters.First(f => f.Key == "captured");
            ApplyFilter();
            MarkDirty();
        }
    }

    public void ExportTo(string path)
    {
        _strings?.Save(path);
        _labels?.Save(Path.ChangeExtension(path, null) + ".labels.json");
        AddLog($"Eksporteret til {path}");
    }

    /// <summary>Imports a strings.json someone shared: their non-empty translations replace ours.</summary>
    public void ImportFrom(string path)
    {
        if (_strings == null)
            return;
        var other = TranslationFile.Load(path);
        var mine = _strings.Strings.ToDictionary(e => e.Kind + "\0" + e.En);
        int changed = 0, added = 0;
        foreach (var o in other.Strings.Where(o => !string.IsNullOrWhiteSpace(o.Da)))
        {
            if (mine.TryGetValue(o.Kind + "\0" + o.En, out var m))
            {
                if (m.Da == o.Da && m.Status == o.Status)
                    continue;
                m.Da = o.Da;
                m.Status = o.Status;
                m.Edited = true;
                changed++;
            }
            else
            {
                o.Edited = true;
                _strings.Strings.Add(o);
                added++;
            }
        }
        _all = _strings.Strings.Select(e => new EntryViewModel(e, MarkDirty)).ToList();
        ApplyFilter();
        MarkDirty();
        AddLog($"Importeret: {changed} ændret, {added} nye.");
    }

    public void LaunchGame()
    {
        var appIdFile = Path.Combine(GameDir!, "steam_appid.txt");
        var appId = File.Exists(appIdFile) ? File.ReadAllText(appIdFile).Trim() : "572220";
        Process.Start(new ProcessStartInfo($"steam://rungameid/{appId}") { UseShellExecute = true });
    }

    public void OpenFolder(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
}

/// <summary>Remembers the chosen game folder between runs.</summary>
public sealed class Settings
{
    public string? GameDir { get; set; }

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MageQuit-DA", "settings.json");

    public static Settings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? System.Text.Json.JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new()
                : new();
        }
        catch (Exception)
        {
            return new();
        }
    }

    public static void Save(Settings s)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, System.Text.Json.JsonSerializer.Serialize(s));
        }
        catch (IOException)
        {
        }
    }
}
