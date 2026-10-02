using System.Reflection;
using System.Runtime.InteropServices;
using MageQuitTranslator.Core;

namespace MageQuitTranslator.Manager;

/// <summary>
/// Command line mode for scripted use:
///   MageQuit-Translator --install [gameDir] | --uninstall [gameDir] | --language &lt;code|off&gt; [gameDir] | --status [gameDir]
/// Exit code 0 on success.
/// </summary>
static partial class Cli
{
    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        var cmd = args[0].ToLowerInvariant();
        if (cmd is not ("--install" or "--uninstall" or "--language" or "--status" or "--help"))
            return false;
        if (OperatingSystem.IsWindows())
            AttachConsole(-1);

        if (cmd == "--help")
        {
            Console.WriteLine("MageQuit-Translator --install | --uninstall | --language <code|off> | --status  [game folder]");
            return true;
        }
        var rest = args.Skip(1).ToList();
        string? language = null;
        if (cmd == "--language")
        {
            if (rest.Count == 0)
            {
                Console.Error.WriteLine("--language needs a language code, or 'off' for the original English.");
                exitCode = 2;
                return true;
            }
            language = rest[0];
            rest.RemoveAt(0);
        }
        var dir = rest.Count > 0 ? rest[0] : GameLocator.Find();
        if (!GameLocator.IsGameDir(dir))
        {
            Console.Error.WriteLine("MageQuit was not found. Pass the game folder as an argument.");
            exitCode = 2;
            return true;
        }
        var installer = new Installer(dir!) { Log = Console.WriteLine };
        try
        {
            switch (cmd)
            {
                case "--install":
                    using (var payload = OpenPayload())
                        installer.Install(payload);
                    break;
                case "--uninstall":
                    installer.Uninstall();
                    break;
                case "--language":
                    installer.ActiveLanguage = language == "off" ? null : language;
                    break;
            }
            Console.WriteLine(installer.IsInstalled
                ? $"{dir}: installed, game language: {installer.ActiveLanguage ?? "off (English)"}, " +
                  $"languages: {string.Join(", ", installer.LoadLanguages().Select(l => l.Code))}"
                : $"{dir}: not installed");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            exitCode = 1;
        }
        return true;
    }

    /// <summary>The embedded payload.zip, or dist/payload from build/package.ps1 during development.</summary>
    internal static Payload OpenPayload()
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip");
        if (stream != null)
            return Payload.FromZip(stream);
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var staged = Path.Combine(d.FullName, "dist", "payload");
            if (Directory.Exists(staged))
                return Payload.FromFolder(staged);
        }
        throw new InvalidOperationException("The installation files are missing from this build.");
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);
}
