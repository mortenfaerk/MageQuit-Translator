using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MageQuitTranslator.Core;

namespace MageQuitTranslator.Manager;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        Raise(name);
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>The book's palette. Gilt is reserved for the primary action and sealed lines.</summary>
public static class Ink
{
    public static readonly IBrush Chalk = Brush("#EFE7FF");
    public static readonly IBrush ChalkDim = Brush("#B9ADD6");
    public static readonly IBrush Pencil = Brush("#A196C6");
    public static readonly IBrush BindingInk = Brush("#1B1230");
    public static readonly IBrush Gilt = Brush("#E8C25A");
    public static readonly IBrush Fire = Brush("#FF6B3D");
    public static readonly IBrush Water = Brush("#3FA7FF");
    public static readonly IBrush Ice = Brush("#9FE8FF");
    public static readonly IBrush Nature = Brush("#7ED957");
    public static readonly IBrush Electric = Brush("#F5E14A");
    public static readonly IBrush Earth = Brush("#D09A5B");
    public static readonly IBrush Arcane = Brush("#C78BFF");
    public static readonly IBrush Air = Brush("#D8E2FF");
    public static readonly IBrush Vellum = Brush("#2B1D48");
    public static readonly IBrush Gutter = Brush("#22173A");

    /// <summary>Ribbon colours for language bookmarks, in order.</summary>
    public static readonly IBrush[] Ribbons = [Brush("#A3322C"), Brush("#1F6E64"), Brush("#2E4AA6"), Brush("#8A4E14"),
                                               Brush("#5F3A9C"), Brush("#386A20"), Brush("#8E2F61"), Brush("#46546E")];
    public static readonly IBrush OriginalRibbon = Brush("#4A3D70");

    static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex)).ToImmutable();
}

/// <summary>A chapter of the book: strings grouped by where they appear in the game.</summary>
public sealed class Chapter(string key, string title, string blurb, IBrush ink) : ViewModelBase
{
    public string Key { get; } = key;
    public string Title { get; } = title;
    public string Blurb { get; } = blurb;
    public IBrush Ink { get; } = ink;

    int _count, _sealed;
    double _tabHeight = 40;
    bool _isCurrent;

    public int Count { get => _count; set { Set(ref _count, value); Raise(nameof(CountText)); Raise(nameof(SealedWidth)); Raise(nameof(Weight)); } }
    public int Sealed { get => _sealed; set { Set(ref _sealed, value); Raise(nameof(CountText)); Raise(nameof(SealedWidth)); } }
    public string CountText => $"{Sealed}/{Count}";
    /// <summary>Thumb-index tab height, proportional to the chapter's extent.</summary>
    public double TabHeight { get => _tabHeight; set => Set(ref _tabHeight, value); }
    /// <summary>Gilt fill along the tab edge: how much of the chapter is sealed.</summary>
    public double SealedWidth => Count == 0 ? 0 : 100.0 * Sealed / Count;
    public bool IsCurrent
    {
        get => _isCurrent;
        set { Set(ref _isCurrent, value); Raise(nameof(TabWidth)); }
    }

    /// <summary>The open chapter's tab sticks out further, as if the book were open there.</summary>
    public double TabWidth => IsCurrent ? 148 : 132;
    /// <summary>Share of the thumb index height (square root, so small chapters stay readable).</summary>
    public double Weight => Math.Sqrt(Math.Max(1, Count));

    /// <summary>Which chapter a string belongs to, from where it was found.</summary>
    public static string Classify(TranslationEntry e)
    {
        if (e.IsPattern) return "patterns";
        if (e.Confidence is "low" or "captured") return "uncertain";
        var src = string.Join(" ", e.Sources);
        if (e.En.StartsWith("TIP:") || e.En.StartsWith("OBJECTIVE:") || src.Contains("Tip") || src.Contains("TutorialManager"))
            return "tips";
        if (src.Contains(".description") || src.Contains("SpellName.") || src.Contains("Element.") || src.Contains("Combo"))
            return "spells";
        if (src.Contains("StageName.") || src.Contains("SpellSelectionMode.") || src.Contains("SelectionMenu.cs"))
            return "stages";
        if (Regex.IsMatch(src, @"^level\d+|sharedassets|resources")) return "menus";
        return "messages";
    }
}

/// <summary>One line of the facing-page spread: English on the verso, the translation on the recto.</summary>
public sealed class EntryViewModel(TranslationEntry entry, MainViewModel owner) : ViewModelBase
{
    public TranslationEntry Entry { get; } = entry;
    public string ChapterKey { get; } = Chapter.Classify(entry);

    public string En => Entry.En;
    public string Gloss => Entry.Sources.Count == 0 ? "" : Entry.Sources[0];

    public string Text
    {
        get => Entry.Text;
        set
        {
            if (Entry.Text == value)
                return;
            Entry.Text = value;
            Entry.Edited = true;
            // Writing makes a line a draft; only sealing makes it reviewed.
            Entry.Status = string.IsNullOrWhiteSpace(value) ? "new" : "machine";
            RaiseAll();
            owner.OnEntryChanged(this);
        }
    }

    public string Note
    {
        get => Entry.Note;
        set
        {
            if (Entry.Note == value)
                return;
            Entry.Note = value;
            Entry.Edited = true;
            Raise();
            owner.OnEntryChanged(this);
        }
    }

    public bool IsSealed => Entry.Status == "reviewed" && Entry.Text.Length > 0;
    public bool IsDraft => !IsSealed && Entry.Text.Length > 0;
    public bool IsUnsealed => Entry.Text.Length == 0;
    public string SealTip => IsSealed ? "Sealed (reviewed)" : IsDraft ? "Draft, not yet sealed" : "Not translated";

    /// <summary>Length budget: the translation's length against the English, as a hairline up to 2× wide.</summary>
    public double BudgetWidth => Entry.En.Length == 0 ? 0 : Math.Min(2.0, (double)Entry.Text.Length / Entry.En.Length) * 60;
    public bool IsOverBudget => Entry.En.Length > 4 && Entry.Text.Length > Entry.En.Length * 1.4;
    public IBrush BudgetInk => IsOverBudget ? Ink.Fire : Ink.Pencil;

    public string Missing => owner.Coverage.MissingChars(Entry.Text, Entry.Fonts, Entry.UppercaseOnly);

    public List<string> Warnings
    {
        get
        {
            var w = new List<string>();
            if (Missing.Length > 0)
                w.Add($"The game font {string.Join(", ", Entry.Fonts)} cannot draw {string.Join(" ", Missing.ToCharArray())}. They will show as boxes in game.");
            if (Entry.UppercaseOnly)
                w.Add("This font only has capitals, so the line is shown in CAPITALS in game.");
            if (IsOverBudget)
                w.Add($"{Entry.Text.Length * 100 / Math.Max(1, Entry.En.Length) - 100}% longer than the English. It may be clipped in game.");
            if (Entry.Text.Length > 0 && CountTokens(Entry.En) != CountTokens(Entry.Text))
                w.Add("Placeholders like {0}, $1 or <color> tags do not match the English.");
            if (Entry.Text.Length > 0 && !Entry.IsPattern &&
                (Entry.En.StartsWith(' ') != Entry.Text.StartsWith(' ') || Entry.En.EndsWith(' ') != Entry.Text.EndsWith(' ')))
                w.Add("Leading or trailing spaces differ from the English. The game glues this line to other text.");
            return w;
        }
    }

    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>The translation re-printed with letters the game font lacks marked in fire ink.</summary>
    public List<ProofSegment> Proof
    {
        get
        {
            var missing = Missing;
            var text = Entry.UppercaseOnly ? Entry.Text.ToUpperInvariant() : Entry.Text;
            var segs = new List<ProofSegment>();
            foreach (var c in text)
            {
                bool bad = missing.Contains(c);
                if (segs.Count > 0 && segs[^1].IsMissing == bad)
                    segs[^1] = segs[^1] with { Text = segs[^1].Text + c };
                else
                    segs.Add(new ProofSegment(c.ToString(), bad));
            }
            return segs;
        }
    }

    public string Context => string.Join("\n", Entry.Sources);
    public string FontsText => Entry.Fonts.Count == 0 ? "unknown font" : string.Join(", ", Entry.Fonts);
    public string KindText => Entry.Kind switch
    {
        "regex" => "Pattern: text the game builds while it runs. Keep $1, $2… where the numbers or names go.",
        "split" => "Split pattern: ${name} parts are translated separately and inserted.",
        _ => "",
    };
    public bool IsPattern => Entry.IsPattern;

    public bool Matches(string query) =>
        Entry.En.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        Entry.Text.Contains(query, StringComparison.OrdinalIgnoreCase);

    public void Seal()
    {
        if (Entry.Text.Length == 0)
            return;
        Entry.Status = IsSealed ? "machine" : "reviewed";
        Entry.Edited = true;
        RaiseAll();
        owner.OnEntryChanged(this);
    }

    void RaiseAll()
    {
        foreach (var n in new[] { nameof(Text), nameof(IsSealed), nameof(IsDraft), nameof(IsUnsealed), nameof(SealTip),
                     nameof(BudgetWidth), nameof(IsOverBudget), nameof(BudgetInk), nameof(Missing), nameof(Warnings),
                     nameof(HasWarnings), nameof(Proof) })
            Raise(n);
    }

    static int CountTokens(string s) => Regex.Matches(s, @"\{\d+\}|<[^>]+>|\$\d|\$\{\w+\}").Count;
}

public sealed record ProofSegment(string Text, bool IsMissing)
{
    public IBrush Ink => IsMissing ? Manager.Ink.Fire : Manager.Ink.Chalk;
    public TextDecorationCollection? Decorations => IsMissing ? TextDecorations.Underline : null;
}

/// <summary>A menu label that is an image in the game, with its rendered preview.</summary>
public sealed class LabelViewModel(LabelEntry label, MainViewModel owner) : ViewModelBase
{
    public LabelEntry Label { get; } = label;
    Bitmap? _preview = owner.RenderLabel(label);
    public Bitmap? EnglishPreview { get; } = owner.RenderLabel(new LabelEntry
    {
        Texture = label.Texture, En = label.En, Text = label.En, Width = label.Width, Height = label.Height, Align = label.Align,
    });

    public string En => Label.En;
    public string Size => $"{Label.Width} × {Label.Height}";
    public Bitmap? Preview => _preview;
    public bool IsSealed => Label.Status == "reviewed" && Label.Text.Length > 0;
    public bool IsDraft => !IsSealed && Label.Text.Length > 0;
    public bool IsUnsealed => Label.Text.Length == 0;

    public string Text
    {
        get => Label.Text;
        set
        {
            if (Label.Text == value)
                return;
            Label.Text = value;
            Label.Edited = true;
            Label.Status = string.IsNullOrWhiteSpace(value) ? "new" : "machine";
            Raise();
            Refresh();
            owner.OnLabelChanged();
        }
    }

    public string Missing => owner.Coverage.MissingChars(Label.Text, ["MageQuit-Body"]);
    public bool HasMissing => Missing.Length > 0;

    public void Seal()
    {
        if (Label.Text.Length == 0)
            return;
        Label.Status = IsSealed ? "machine" : "reviewed";
        Label.Edited = true;
        Raise(nameof(IsSealed));
        Raise(nameof(IsDraft));
        owner.OnLabelChanged();
    }

    public void Refresh()
    {
        var old = _preview;
        _preview = owner.RenderLabel(Label);
        foreach (var n in new[] { nameof(Preview), nameof(IsSealed), nameof(IsDraft), nameof(IsUnsealed), nameof(Missing), nameof(HasMissing) })
            Raise(n);
        old?.Dispose();
    }
}

/// <summary>A language bookmark down the book's edge. A null pack is "English (original)": translation off.</summary>
public sealed class Ribbon(LanguagePack? pack, IBrush colour) : ViewModelBase
{
    public LanguagePack? Pack { get; } = pack;
    public IBrush Colour { get; } = colour;
    public string Code => Pack?.Code ?? "";
    public string NativeName => Pack?.Info.NativeName ?? "English";
    public string Name => Pack == null ? "Original game text" : Pack.Info.Name;
    public bool IsOriginal => Pack == null;

    bool _isCurrent, _isActive;
    public bool IsCurrent { get => _isCurrent; set { Set(ref _isCurrent, value); Raise(nameof(Width)); } }
    /// <summary>The language the game is currently set to.</summary>
    public bool IsActive { get => _isActive; set => Set(ref _isActive, value); }
    public double Width => IsCurrent ? 224 : 196;

    public string ProgressText
    {
        get
        {
            if (Pack == null)
                return "Mod off";
            var (total, reviewed, draft) = Pack.Progress();
            if (total == 0) return "";
            int written = (reviewed + draft) * 100 / total, checkedPct = reviewed * 100 / total;
            return checkedPct >= 95 ? $"{written}% translated · checked"
                 : checkedPct >= 10 ? $"{written}% translated · {checkedPct}% checked"
                 : $"{written}% translated · draft";
        }
    }

    public void RefreshProgress() => Raise(nameof(ProgressText));
}

/// <summary>A word of the game's main menu on the frontispiece, flipping letter by letter into the chosen language.</summary>
public sealed class FlipWord(string from) : ViewModelBase
{
    string _display = from;
    Bitmap? _image;
    public string Display { get => _display; set => Set(ref _display, value); }
    public Bitmap? Image { get => _image; set { var old = _image; Set(ref _image, value); Raise(nameof(HasImage)); old?.Dispose(); } }
    public bool HasImage => _image != null;
    public string Target { get; set; } = from;
}

public sealed class LogLine(string text)
{
    public string Text { get; } = $"{DateTime.Now:HH:mm}  {text}";
}

public sealed class FilterOption(string key, string title)
{
    public string Key { get; } = key;
    public string Title { get; } = title;
    public override string ToString() => Title;
}
