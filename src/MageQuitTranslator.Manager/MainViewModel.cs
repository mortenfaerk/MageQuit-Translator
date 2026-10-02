using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MageQuitTranslator.Core;

namespace MageQuitTranslator.Manager;

public enum Page { Home, Spread, NewLanguage }

public sealed class MainViewModel : ViewModelBase
{
    public const string RepoUrl = "https://github.com/mortenfaerk/MageQuit-Translator";

    string? _gameDir;
    Page _page = Page.Home;
    Ribbon? _current;
    Chapter? _chapter;
    EntryViewModel? _selected;
    string _search = "";
    FilterOption _filter;
    string _colophon = "";
    bool _busy, _dirty;
    LabelRenderer? _renderer;
    readonly DispatcherTimer _flipTimer = new() { Interval = TimeSpan.FromMilliseconds(55) };
    List<EntryViewModel> _all = [];

    public MainViewModel()
    {
        _filter = Filters[0];
        _flipTimer.Tick += (_, _) => FlipStep();
        GameDir = Settings.Load().GameDir is { } saved && GameLocator.IsGameDir(saved) ? saved : GameLocator.Find();
    }

    // ---- State --------------------------------------------------------------------------------

    public ObservableCollection<Ribbon> Ribbons { get; } = [];
    public ObservableCollection<Chapter> Chapters { get; } = [];
    public ObservableCollection<EntryViewModel> Entries { get; } = [];
    public ObservableCollection<LabelViewModel> Labels { get; } = [];
    public ObservableCollection<FlipWord> FlipWords { get; } = [];
    public ObservableCollection<LogLine> Log { get; } = [];
    public FontCoverage Coverage { get; private set; } = new();

    public List<FilterOption> Filters { get; } =
    [
        new("all", "Every line"),
        new("todo", "Not sealed"),
        new("empty", "Not translated"),
        new("warn", "With warnings"),
    ];

    public string AppVersion => "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?");

    public string? GameDir
    {
        get => _gameDir;
        set { if (Set(ref _gameDir, value)) Refresh(); }
    }

    public Page Page
    {
        get => _page;
        set
        {
            if (!Set(ref _page, value))
                return;
            Raise(nameof(IsHome));
            Raise(nameof(IsSpread));
            Raise(nameof(IsNewLanguage));
            Raise(nameof(BookMargin));
            if (value == Page.Home)
                StartFlip();
        }
    }

    public bool IsHome => Page == Page.Home;
    /// <summary>The book keeps a margin to the window edge unless the thumb index sits there.</summary>
    public Avalonia.Thickness BookMargin => IsSpread && IsInstalled ? new(0, 0, 0, 12) : new(0, 0, 28, 12);
    public bool IsSpread => Page == Page.Spread;
    public bool IsNewLanguage => Page == Page.NewLanguage;

    public bool HasGame => GameLocator.IsGameDir(GameDir);
    public bool IsInstalled => HasGame && Installer!.IsInstalled;
    public bool NeedsInstall => HasGame && !IsInstalled;
    public bool CanTranslate => IsInstalled && Current?.Pack != null;
    public bool ShowLinuxHint => GameLocator.RunningOnLinux && IsInstalled;
    public string LinuxLaunchOption => "WINEDLLOVERRIDES=\"winhttp=n,b\" %command%";

    public string InstallLine
    {
        get
        {
            if (!HasGame) return "MageQuit was not found";
            var m = Installer!.ReadManifest();
            if (m == null) return "Not installed in this MageQuit";
            var active = Installer.ActiveLanguage;
            var lang = Ribbons.FirstOrDefault(r => r.Code == active)?.NativeName;
            return $"Installed {m.Version} · game is in {lang ?? "English"}";
        }
    }

    public bool Busy { get => _busy; private set { Set(ref _busy, value); Raise(nameof(NotBusy)); } }
    public bool NotBusy => !Busy;

    public bool Dirty
    {
        get => _dirty;
        private set { Set(ref _dirty, value); Raise(nameof(Title)); }
    }

    public string Title => "MageQuit Translator" + (Dirty ? " — unsaved" : "");
    public string Colophon { get => _colophon; private set => Set(ref _colophon, value); }

    public Ribbon? Current
    {
        get => _current;
        set
        {
            if (value == null || _current == value)
                return;
            if (Dirty)
                Save();
            if (_current != null) _current.IsCurrent = false;
            _current = value;
            _current.IsCurrent = true;
            Raise();
            Raise(nameof(PlayLabel));
            Raise(nameof(CanTranslate));
            Raise(nameof(CurrentProgress));
            Raise(nameof(PlayHint));
            LoadBook();
            if (Page == Page.NewLanguage)
                Page = Page.Home;
            StartFlip();
        }
    }

    public string PlayLabel => Current?.Pack == null ? "Play in English" : $"Play in {Current.NativeName}";
    public string PlayHint => Current?.Pack == null
        ? "Turns the translation off. The game runs with its original English text."
        : $"Sets the game to {Current.Name} and starts MageQuit through Steam.";
    public string CurrentProgress => Current?.ProgressText ?? "";

    public Chapter? CurrentChapter
    {
        get => _chapter;
        set
        {
            if (value == null || _chapter == value)
                return;
            if (_chapter != null) _chapter.IsCurrent = false;
            _chapter = value;
            _chapter.IsCurrent = true;
            Raise();
            Raise(nameof(IsLabelChapter));
            Raise(nameof(IsTextChapter));
            ApplyFilter();
        }
    }

    public bool IsLabelChapter => CurrentChapter?.Key == "art";
    public bool IsTextChapter => !IsLabelChapter;

    public EntryViewModel? Selected { get => _selected; set => Set(ref _selected, value); }

    public string Search { get => _search; set { if (Set(ref _search, value)) ApplyFilter(); } }
    public FilterOption Filter { get => _filter; set { if (Set(ref _filter, value)) ApplyFilter(); } }

    Installer? Installer => HasGame ? new Installer(GameDir!) { Log = AddLog } : null;

    // ---- Loading ------------------------------------------------------------------------------

    public void AddLog(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Log.Insert(0, new LogLine(text));
            Colophon = text;
        });
    }

    public void Refresh()
    {
        foreach (var n in new[] { nameof(HasGame), nameof(IsInstalled), nameof(NeedsInstall), nameof(CanTranslate),
                     nameof(ShowLinuxHint), nameof(BookMargin) })
            Raise(n);
        var previous = Current?.Code;
        Ribbons.Clear();
        _current = null;
        _renderer?.Dispose();
        _renderer = null;
        Coverage = new FontCoverage();

        if (IsInstalled)
        {
            var installer = Installer!;
            Coverage = installer.LoadFontCoverage();
            if (installer.ReadLabelFont() is { } ttf)
                _renderer = new LabelRenderer(ttf);
            var active = installer.ActiveLanguage;
            var packs = installer.LoadLanguages();
            for (int i = 0; i < packs.Count; i++)
                Ribbons.Add(new Ribbon(packs[i], Ink.Ribbons[i % Ink.Ribbons.Length]) { IsActive = packs[i].Code == active });
            Ribbons.Add(new Ribbon(null, Ink.OriginalRibbon) { IsActive = active == null });
            Current = Ribbons.FirstOrDefault(r => r.Code == (previous ?? active ?? "")) ?? Ribbons[0];
        }
        else
        {
            FlipWords.Clear();
            foreach (var w in new[] { "Couch", "Online", "Tutorial", "Wardrobe", "Settings", "Quit" })
                FlipWords.Add(new FlipWord(w));
        }
        Raise(nameof(InstallLine));
        Raise(nameof(Current));
        if (!HasGame)
            Colophon = "Point the app at your MageQuit folder to begin.";
    }

    void LoadBook()
    {
        Entries.Clear();
        Labels.Clear();
        Chapters.Clear();
        _all = [];
        Selected = null;
        var pack = Current?.Pack;
        if (pack != null)
        {
            _all = pack.Strings.Strings.Select(e => new EntryViewModel(e, this)).ToList();
            foreach (var l in pack.Labels.Labels)
                Labels.Add(new LabelViewModel(l, this));
        }
        var defs = new (string key, string title, string blurb, Avalonia.Media.IBrush ink)[]
        {
            ("menus", "Menus", "Text in the game's screens: menus, lobby, settings, round results.", Ink.Water),
            ("spells", "Spells", "Spell, element and combo names, and every spell description.", Ink.Fire),
            ("stages", "Stages & modes", "Stage names and descriptions, draft and game-mode rules.", Ink.Electric),
            ("tips", "Tips", "Loading-screen tips and tutorial hints.", Ink.Nature),
            ("messages", "Messages", "Pop-ups, lobby messages and errors written in the game's code.", Ink.Ice),
            ("patterns", "Patterns", "Sentences the game builds while running, like \"Round 3 of 9\".", Ink.Earth),
            ("uncertain", "Uncertain", "Strings that may never appear on screen, plus ones captured in game.", Ink.Air),
            ("art", "Menu art", "Menu words drawn as pictures. Rendered in the game's own font.", Ink.Arcane),
        };
        foreach (var d in defs)
            Chapters.Add(new Chapter(d.key, d.title, d.blurb, d.ink));
        UpdateChapterCounts();
        _chapter = null;
        CurrentChapter = Chapters[0];
    }

    void UpdateChapterCounts()
    {
        foreach (var c in Chapters)
        {
            if (c.Key == "art")
            {
                c.Count = Labels.Count;
                c.Sealed = Labels.Count(l => l.IsSealed);
            }
            else
            {
                c.Count = _all.Count(e => e.ChapterKey == c.Key);
                c.Sealed = _all.Count(e => e.ChapterKey == c.Key && e.IsSealed);
            }
        }

        Current?.RefreshProgress();
        Raise(nameof(CurrentProgress));
        Raise(nameof(SpreadProgress));
    }

    public string SpreadProgress
    {
        get
        {
            if (_all.Count == 0) return "";
            int sealedCount = _all.Count(e => e.IsSealed), drafts = _all.Count(e => e.IsDraft);
            return $"{sealedCount} of {_all.Count} lines sealed · {drafts} drafts · {_all.Count - sealedCount - drafts} not translated";
        }
    }

    void ApplyFilter()
    {
        if (CurrentChapter == null)
            return;
        var q = Search.Trim();
        IEnumerable<EntryViewModel> items = q.Length > 0 ? _all.Where(e => e.Matches(q)) : _all.Where(e => e.ChapterKey == CurrentChapter.Key);
        items = Filter.Key switch
        {
            "todo" => items.Where(e => !e.IsSealed),
            "empty" => items.Where(e => e.IsUnsealed),
            "warn" => items.Where(e => e.HasWarnings),
            _ => items,
        };
        Entries.Clear();
        foreach (var e in items)
            Entries.Add(e);
        Selected = Entries.FirstOrDefault();
    }

    // ---- Editing ------------------------------------------------------------------------------

    public void OnEntryChanged(EntryViewModel e)
    {
        Dirty = true;
        UpdateChapterCounts();
    }

    public void OnLabelChanged()
    {
        Dirty = true;
        UpdateChapterCounts();
    }

    public Bitmap? RenderLabel(LabelEntry label)
    {
        if (_renderer == null)
            return null;
        using var ms = new MemoryStream(_renderer.RenderPng(label));
        return new Bitmap(ms);
    }

    public void SealSelectedAndAdvance()
    {
        if (Selected == null)
            return;
        Selected.Seal();
        var i = Entries.IndexOf(Selected);
        if (i >= 0 && i + 1 < Entries.Count)
            Selected = Entries[i + 1];
    }

    public void Save()
    {
        var pack = Current?.Pack;
        if (pack == null || !IsInstalled)
            return;
        try
        {
            Installer!.SaveLanguage(pack);
            Dirty = false;
            AddLog($"Saved {pack.Info.NativeName}. Press Alt+R in the game to see text changes; menu art needs a game restart.");
        }
        catch (Exception ex)
        {
            AddLog("Could not save: " + ex.Message);
        }
    }

    // ---- Frontispiece flip --------------------------------------------------------------------

    void StartFlip()
    {
        FlipWords.Clear();
        var labels = Current?.Pack?.Labels.Labels.Where(l => l.Texture.StartsWith("Main")).Take(6).ToList() ?? [];
        if (labels.Count == 0)
            labels = [.. new[] { "Couch", "Online", "Tutorial", "Wardrobe", "Settings", "Quit" }.Select(w => new LabelEntry { En = w, Text = w })];
        foreach (var l in labels)
        {
            var target = Current?.Pack == null || string.IsNullOrEmpty(l.Text) ? l.En : l.Text;
            var w = new FlipWord(l.En) { Target = target };
            w.Image = RenderWord(l.En);
            FlipWords.Add(w);
        }
        _flipStep = 0;
        _flipTimer.Start();
    }

    int _flipStep;

    /// <summary>Cascades each word one letter at a time from English into the target language.</summary>
    void FlipStep()
    {
        _flipStep++;
        bool done = true;
        for (int i = 0; i < FlipWords.Count; i++)
        {
            var w = FlipWords[i];
            int k = _flipStep - i * 3;   // stagger the words like flaps down a board
            if (k <= 0) { done = false; continue; }
            int len = Math.Max(w.Display.Length, w.Target.Length);
            if (w.Display == w.Target) continue;
            done = false;
            var chars = new char[Math.Min(len, Math.Max(k, w.Target.Length))];
            for (int c = 0; c < chars.Length; c++)
                chars[c] = c < k ? (c < w.Target.Length ? w.Target[c] : ' ') : (c < w.Display.Length ? w.Display[c] : ' ');
            var next = new string(chars).TrimEnd();
            if (k >= len) next = w.Target;
            if (next != w.Display)
            {
                w.Display = next;
                w.Image = RenderWord(next);
            }
        }
        if (done)
            _flipTimer.Stop();
    }

    Bitmap? RenderWord(string text)
    {
        if (_renderer == null || string.IsNullOrWhiteSpace(text))
            return null;
        using var ms = new MemoryStream(_renderer.RenderWord(text, 50, 0xFFEFE7FF));
        return new Bitmap(ms);
    }

    // ---- Actions ------------------------------------------------------------------------------

    public void ChooseGameDir(string dir)
    {
        GameDir = dir;
        if (HasGame && !string.Equals(GameLocator.Find(), dir, StringComparison.OrdinalIgnoreCase))
            Settings.Save(new Settings { GameDir = dir });
        else if (!HasGame)
            AddLog("That folder does not contain MageQuit.exe.");
    }

    public async Task InstallAsync()
    {
        if (Dirty) Save();
        await RunAsync(() =>
        {
            using var payload = Cli.OpenPayload();
            Installer!.Install(payload);
        });
    }

    public async Task UninstallAsync() => await RunAsync(() => Installer!.Uninstall());

    public async Task PlayAsync()
    {
        if (Dirty) Save();
        var code = Current?.Pack?.Code;
        await RunAsync(() => Installer!.ActiveLanguage = code);
        foreach (var r in Ribbons) r.IsActive = r.Code == (code ?? "");
        Raise(nameof(InstallLine));
        LaunchGame();
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
            AddLog(ex.Message);
        }
        finally
        {
            Busy = false;
            Refresh();
        }
    }

    public void LaunchGame()
    {
        var appIdFile = Path.Combine(GameDir!, "steam_appid.txt");
        var appId = File.Exists(appIdFile) ? File.ReadAllText(appIdFile).Trim() : "572220";
        Open($"steam://rungameid/{appId}");
    }

    public static void Open(string target) =>
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });

    public bool CaptureEnabled
    {
        get => IsInstalled && Installer!.CaptureEnabled;
        set
        {
            if (!IsInstalled) return;
            Installer!.CaptureEnabled = value;
            AddLog(value ? "Capture is on. Play a few rounds, then choose Import captured lines."
                         : "Capture is off.");
            Raise();
        }
    }

    public void ImportCaptured()
    {
        var pack = Current?.Pack;
        if (pack == null) return;
        var path = Path.Combine(GameDir!, Installer.CapturedPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            AddLog("Nothing captured yet. Turn on capture and play a little first.");
            return;
        }
        var others = Ribbons.Where(r => r.Pack != null && r.Pack != pack)
                            .SelectMany(r => r.Pack!.Strings.Strings.Select(s => s.Text)).Where(t => t.Length > 0);
        int added = pack.Strings.ImportCaptured(path, others);
        AddLog(added == 0 ? "No new lines found in the capture." : $"Found {added} new lines. They are in the Uncertain chapter.");
        if (added > 0)
        {
            LoadBook();
            CurrentChapter = Chapters.First(c => c.Key == "uncertain");
            Dirty = true;
        }
    }

    public void ExportForPullRequest(string parentFolder)
    {
        var pack = Current?.Pack;
        if (pack == null) return;
        if (Dirty) Save();
        var dir = Path.Combine(parentFolder, pack.Code);
        pack.ExportForPullRequest(dir);
        AddLog($"Exported to {dir}. Copy the folder into translation/ in your fork of the project and open a pull request.");
        Open(dir);
    }

    // ---- New language -------------------------------------------------------------------------

    string _newCode = "", _newName = "", _newNative = "", _newError = "";
    Ribbon? _newTemplate;

    public string NewCode
    {
        get => _newCode;
        set
        {
            if (!Set(ref _newCode, value.Trim())) return;
            if (LanguageInfo.IsValidCode(_newCode))
            {
                var guess = LanguageInfo.Create(_newCode);
                if (string.IsNullOrEmpty(_newName) || _autoNames) { NewName = guess.Name; NewNative = guess.NativeName; _autoNames = true; }
            }
            NewError = "";
        }
    }

    bool _autoNames = true;
    public string NewName { get => _newName; set => Set(ref _newName, value); }
    public string NewNative { get => _newNative; set => Set(ref _newNative, value); }
    public string NewError { get => _newError; set => Set(ref _newError, value); }
    public IEnumerable<Ribbon> Templates => Ribbons.Where(r => r.Pack != null);
    public Ribbon? NewTemplate { get => _newTemplate; set => Set(ref _newTemplate, value); }

    public void BeginNewLanguage()
    {
        NewCode = "";
        NewName = "";
        NewNative = "";
        NewError = "";
        _autoNames = true;
        Raise(nameof(Templates));
        NewTemplate = Templates.FirstOrDefault();
        Page = Page.NewLanguage;
    }

    public void CreateLanguage()
    {
        try
        {
            if (!LanguageInfo.IsValidCode(NewCode))
                throw new ArgumentException("Use a language code like de, sv, es or pt-BR.");
            if (NewTemplate?.Pack == null)
                throw new ArgumentException("Pick a language to copy the list of lines from.");
            var info = LanguageInfo.Create(NewCode, NewName, NewNative);
            Installer!.CreateLanguage(info, NewTemplate.Pack);
            Refresh();
            Current = Ribbons.First(r => r.Code == info.Code);
            Page = Page.Spread;
        }
        catch (Exception ex)
        {
            NewError = ex.Message;
        }
    }
}

/// <summary>Remembers a hand-picked game folder between runs (auto-detected folders are not stored).</summary>
public sealed class Settings
{
    public string? GameDir { get; set; }

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MageQuit-Translator", "settings.json");

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
