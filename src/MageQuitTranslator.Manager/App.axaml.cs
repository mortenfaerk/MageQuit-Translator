using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace MageQuitTranslator.Manager;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();
            // Development aid for screenshots: MQT_DEV_PAGE=spread|art|new opens that page directly.
            switch (Environment.GetEnvironmentVariable("MQT_DEV_PAGE"))
            {
                case "spread": vm.Page = Page.Spread; break;
                case "art": vm.Page = Page.Spread; vm.CurrentChapter = vm.Chapters.LastOrDefault(); break;
                case "new": vm.BeginNewLanguage(); vm.NewCode = "de"; break;
            }
            desktop.MainWindow = new MainWindow { DataContext = vm };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
