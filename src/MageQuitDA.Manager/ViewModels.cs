using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using MageQuitDA.Core;

namespace MageQuitDA.Manager;

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

/// <summary>Editable row in the text grid.</summary>
public sealed class EntryViewModel(TranslationEntry entry, Action onChanged) : ViewModelBase
{
    public TranslationEntry Entry { get; } = entry;

    public string En => Entry.En;
    public string EnDisplay => Entry.En.Replace("\n", " ⏎ ");

    public string Da
    {
        get => Entry.Da;
        set
        {
            if (Entry.Da == value)
                return;
            Entry.Da = value;
            Entry.Edited = true;
            // A human edit counts as reviewed; clearing the text makes it untranslated again.
            Entry.Status = string.IsNullOrWhiteSpace(value) ? "new" : "reviewed";
            Raise();
            Raise(nameof(Status));
            Raise(nameof(StatusText));
            Raise(nameof(Warning));
            onChanged();
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
            onChanged();
        }
    }

    public string Status => Entry.Status;

    public string StatusText => Entry.Status switch
    {
        "reviewed" => "✔ Godkendt",
        "machine" => "✎ Udkast",
        _ => "• Mangler",
    };

    public string Kind => Entry.Kind == "regex" ? "Mønster" : "Tekst";

    public string Confidence => Entry.Confidence switch
    {
        "high" => "Set i UI",
        "enum" => "Navn",
        "captured" => "Opfanget",
        _ => "Måske",
    };

    public string Sources => string.Join("\n", Entry.Sources);
    public string Fonts => string.Join(", ", Entry.Fonts);

    /// <summary>Problems a translator should know about, shown in the details panel.</summary>
    public string Warning
    {
        get
        {
            var w = new List<string>();
            if (Entry.UppercaseOnly)
                w.Add("Skrifttypen har kun store bogstaver; teksten vises med VERSALER.");
            if (Entry.Da.Length > 0 && Entry.En.Length > 0 && Entry.Da.Length > Entry.En.Length * 1.4 && Entry.En.Length > 4)
                w.Add($"Teksten er {Entry.Da.Length * 100 / Entry.En.Length - 100}% længere end originalen og kan blive klippet.");
            if (CountFormat(Entry.En) != CountFormat(Entry.Da) && Entry.Da.Length > 0)
                w.Add("Antallet af {0}-pladsholdere eller <tags> passer ikke med originalen.");
            if (Entry.En.StartsWith(' ') != Entry.Da.StartsWith(' ') || Entry.En.EndsWith(' ') != Entry.Da.EndsWith(' '))
                if (Entry.Da.Length > 0)
                    w.Add("Mellemrum i start/slut afviger fra originalen (vigtigt for sammensatte tekster).");
            return string.Join("\n", w);
        }
    }

    static int CountFormat(string s) =>
        System.Text.RegularExpressions.Regex.Matches(s, @"\{\d+\}|<[^>]+>|\$\d").Count;

    public void Approve()
    {
        if (string.IsNullOrWhiteSpace(Entry.Da))
            return;
        Entry.Status = "reviewed";
        Entry.Edited = true;
        Raise(nameof(Status));
        Raise(nameof(StatusText));
        onChanged();
    }
}

/// <summary>Editable image label with a live preview.</summary>
public sealed class LabelViewModel(LabelEntry label, Func<LabelEntry, Bitmap?> render, Action onChanged) : ViewModelBase
{
    public LabelEntry Label { get; } = label;
    Bitmap? _preview = render(label);

    public string En => Label.En;
    public string Texture => Label.Texture;
    public string Size => $"{Label.Width}×{Label.Height}";
    public Bitmap? Preview => _preview;

    public string Da
    {
        get => Label.Da;
        set
        {
            if (Label.Da == value)
                return;
            Label.Da = value;
            Label.Edited = true;
            Label.Status = "reviewed";
            Raise();
            Refresh();
            onChanged();
        }
    }

    public void Refresh()
    {
        var old = _preview;
        _preview = render(Label);
        Raise(nameof(Preview));
        old?.Dispose();
    }
}

public sealed class LogLine(string text)
{
    public string Text { get; } = $"{DateTime.Now:HH:mm:ss}  {text}";
}

public sealed class FilterOption(string key, string title)
{
    public string Key { get; } = key;
    public string Title { get; } = title;
    public override string ToString() => Title;
}

public sealed class Collections
{
    public static ObservableCollection<FilterOption> Filters { get; } =
    [
        new("all", "Alle"),
        new("todo", "Mangler eller udkast"),
        new("new", "Mangler oversættelse"),
        new("machine", "Udkast (ikke godkendt)"),
        new("reviewed", "Godkendt"),
        new("warn", "Med advarsler"),
        new("captured", "Opfanget i spillet"),
    ];
}
