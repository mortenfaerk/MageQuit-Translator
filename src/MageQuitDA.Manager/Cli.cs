using System.Reflection;
using System.Runtime.InteropServices;
using MageQuitDA.Core;

namespace MageQuitDA.Manager;

/// <summary>
/// Command line mode for scripted use:
///   MageQuit-DA --install [gameDir] | --uninstall [gameDir] | --enable | --disable | --status
/// Exit code 0 on success.
/// </summary>
static partial class Cli
{
    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        var cmd = args[0].ToLowerInvariant();
        if (cmd is not ("--install" or "--uninstall" or "--enable" or "--disable" or "--status" or "--help"))
            return false;
        if (OperatingSystem.IsWindows())
            AttachConsole(-1);

        if (cmd == "--help")
        {
            Console.WriteLine("MageQuit-DA --install|--uninstall|--enable|--disable|--status [spilmappe]");
            return true;
        }
        var dir = args.Length > 1 ? args[1] : GameLocator.Find();
        if (!GameLocator.IsGameDir(dir))
        {
            Console.Error.WriteLine("MageQuit blev ikke fundet. Angiv spilmappen som argument.");
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
                case "--enable":
                case "--disable":
                    installer.SetEnabled(cmd == "--enable");
                    break;
            }
            Console.WriteLine($"{dir}: {installer.State}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FEJL: " + ex.Message);
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
        throw new InvalidOperationException("Installationsfilerne mangler i programmet.");
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);
}
