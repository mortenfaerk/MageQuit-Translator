using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace MageQuitDA.Manager;

public partial class MainWindow : Window
{
    bool _confirmedClose;

    public MainWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
                Vm.Save();
        };
        Closing += OnClosing;
    }

    MainViewModel Vm => (MainViewModel)DataContext!;

    async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_confirmedClose || !Vm.Dirty)
            return;
        e.Cancel = true;
        var answer = await Dialog.Ask(this, "Ikke gemte ændringer",
            "Du har ændringer, der ikke er gemt. Vil du gemme dem?", "Gem", "Kassér", "Annullér");
        if (answer == 2)
            return;
        if (answer == 0)
            Vm.Save();
        _confirmedClose = true;
        Close();
    }

    async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Vælg MageQuit-mappen (den med MageQuit.exe)",
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            Vm.ChooseGameDir(path);
    }

    async void OnInstall(object? sender, RoutedEventArgs e) => await Vm.InstallAsync();

    async void OnUninstall(object? sender, RoutedEventArgs e)
    {
        var edited = Vm.Dirty ? "\n\nDu har ændringer, der ikke er gemt." : "";
        var answer = await Dialog.Ask(this, "Afinstallér",
            "Fjern den danske oversættelse og alle mod-filer fra spilmappen? Spillets filer gendannes til originalen." +
            "\n\nVil du beholde dine egne rettelser, så brug Eksportér først." + edited,
            "Afinstallér", "Annullér");
        if (answer == 0)
            await Vm.UninstallAsync();
    }

    async void OnToggle(object? sender, RoutedEventArgs e) => await Vm.ToggleAsync();

    void OnLaunch(object? sender, RoutedEventArgs e) => Vm.LaunchGame();
    void OnSave(object? sender, RoutedEventArgs e) => Vm.Save();
    void OnImportCaptured(object? sender, RoutedEventArgs e) => Vm.ImportCaptured();
    void OnApprove(object? sender, RoutedEventArgs e) => Vm.Selected?.Approve();

    async void OnCopyLaunchOption(object? sender, RoutedEventArgs e)
    {
        if (Clipboard != null)
            await Clipboard.SetTextAsync(Vm.LinuxLaunchOption);
    }

    static readonly FilePickerFileType JsonType = new("Oversættelse (JSON)") { Patterns = ["*.json"] };

    async void OnExport(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Eksportér oversættelse",
            SuggestedFileName = "strings.json",
            FileTypeChoices = [JsonType],
        });
        if (file?.TryGetLocalPath() is { } path)
            Vm.ExportTo(path);
    }

    async void OnImportFile(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importér oversættelse",
            FileTypeFilter = [JsonType],
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
            Vm.ImportFrom(path);
    }
}
