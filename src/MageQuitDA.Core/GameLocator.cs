using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MageQuitDA.Core;

/// <summary>Finds the MageQuit install folder through Steam's library list.</summary>
public static partial class GameLocator
{
    public const string ExeName = "MageQuit.exe";
    const string FolderName = "MageQuit";

    public static bool IsGameDir(string? dir) =>
        !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, ExeName)) &&
        Directory.Exists(Path.Combine(dir, "MageQuit_Data"));

    public static string? Find()
    {
        foreach (var steam in SteamRoots())
        {
            foreach (var library in Libraries(steam))
            {
                var dir = Path.Combine(library, "steamapps", "common", FolderName);
                if (IsGameDir(dir))
                    return dir;
            }
        }
        return null;
    }

    static IEnumerable<string> SteamRoots()
    {
        var roots = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            roots.Add(RegistryValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath"));
            roots.Add(RegistryValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"));
            roots.Add(@"C:\Program Files (x86)\Steam");
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            roots.Add(Path.Combine(home, ".steam", "steam"));
            roots.Add(Path.Combine(home, ".local", "share", "Steam"));
            roots.Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"));
        }
        return roots.Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r))
                    .Select(r => Path.GetFullPath(r.Replace('/', Path.DirectorySeparatorChar)))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    static IEnumerable<string> Libraries(string steamRoot)
    {
        yield return steamRoot;
        var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf))
            yield break;
        foreach (Match m in LibraryPathRegex().Matches(File.ReadAllText(vdf)))
            yield return m.Groups[1].Value.Replace(@"\\", @"\");
    }

    static string RegistryValue(string key, string name)
    {
        if (!OperatingSystem.IsWindows())
            return "";
        return Microsoft.Win32.Registry.GetValue(key, name, null) as string ?? "";
    }

    /// <summary>True when the game process is running (files are locked and patching would fail).</summary>
    public static bool IsGameRunning() =>
        System.Diagnostics.Process.GetProcessesByName("MageQuit").Length > 0;

    public static bool RunningOnLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
    private static partial Regex LibraryPathRegex();
}
