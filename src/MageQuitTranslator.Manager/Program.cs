using Avalonia;

namespace MageQuitTranslator.Manager;

static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && Cli.TryRun(args, out var exitCode))
        {
            Environment.Exit(exitCode);
            return;
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
