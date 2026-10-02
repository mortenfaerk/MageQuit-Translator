using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace MageQuitTranslator.Manager;

public partial class MainWindow : Window
{
    bool _confirmedClose;

    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        Closing += OnClosing;
    }

    MainViewModel Vm => (MainViewModel)DataContext!;

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;
        switch (e.Key)
        {
            case Key.S:
                Vm.Save();
                e.Handled = true;
                break;
            case Key.Enter when Vm.IsSpread:
                Vm.SealSelectedAndAdvance();
                Lines.ScrollIntoView(Vm.Selected!);
                e.Handled = true;
                break;
            case Key.F when Vm.IsSpread:
                SearchBox.Focus();
                e.Handled = true;
                break;
        }
    }

    async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_confirmedClose || !Vm.Dirty)
            return;
        e.Cancel = true;
        var answer = await Dialog.Ask(this, "Unsaved changes", "You have changes that are not saved. Save them?",
                                      "Save", "Discard", "Cancel");
        if (answer == 2)
            return;
        if (answer == 0)
            Vm.Save();
        _confirmedClose = true;
        Close();
    }

    void OnHome(object? sender, RoutedEventArgs e) => Vm.Page = Page.Home;
    void OnTranslate(object? sender, RoutedEventArgs e) => Vm.Page = Page.Spread;
    void OnNewLanguage(object? sender, RoutedEventArgs e) => Vm.BeginNewLanguage();
    void OnCreateLanguage(object? sender, RoutedEventArgs e) => Vm.CreateLanguage();
    void OnSave(object? sender, RoutedEventArgs e) => Vm.Save();
    void OnImportCaptured(object? sender, RoutedEventArgs e) => Vm.ImportCaptured();
    void OnOpenRepo(object? sender, RoutedEventArgs e) => MainViewModel.Open(MainViewModel.RepoUrl);
    async void OnInstall(object? sender, RoutedEventArgs e) => await Vm.InstallAsync();
    async void OnPlay(object? sender, RoutedEventArgs e) => await Vm.PlayAsync();

    void OnRibbon(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is Ribbon r)
            Vm.Current = r;
    }

    void OnChapter(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is Chapter c)
        {
            Vm.Search = "";
            Vm.CurrentChapter = c;
        }
    }

    void OnSeal(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is EntryViewModel entry)
        {
            Vm.Selected = entry;
            entry.Seal();
        }
    }

    void OnSealLabel(object? sender, RoutedEventArgs e) => ((sender as Control)?.DataContext as LabelViewModel)?.Seal();

    void OnHistory(object? sender, RoutedEventArgs e)
    {
        if (sender is Control c)
            FlyoutBase.ShowAttachedFlyout(c);
    }

    async void OnUninstall(object? sender, RoutedEventArgs e)
    {
        var answer = await Dialog.Ask(this, "Uninstall",
            "Remove the translator and every language from this MageQuit folder? The game's files are restored to their originals." +
            "\n\nYour own translation work is deleted too. Export it first if you want to keep it.",
            "Uninstall", "Cancel");
        if (answer == 0)
            await Vm.UninstallAsync();
    }

    async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose the MageQuit folder (the one with MageQuit.exe)",
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            Vm.ChooseGameDir(path);
    }

    async void OnExport(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose where to put the language folder (for example translation/ in your fork)",
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            Vm.ExportForPullRequest(path);
    }

    async void OnCopyLaunchOption(object? sender, RoutedEventArgs e)
    {
        if (Clipboard != null)
            await Clipboard.SetTextAsync(Vm.LinuxLaunchOption);
    }
}
