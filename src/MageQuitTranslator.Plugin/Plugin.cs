using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageQuitTranslator
{
    /// <summary>
    /// Companion plugin for MageQuit Translator. Translation itself is done by
    /// XUnity.AutoTranslator; this plugin adds what translators need: capturing every
    /// UI string the game shows (with context) and a screenshot key.
    /// </summary>
    [BepInPlugin(Guid, "MageQuit Translator", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "magequit.translator";
        public const string Version = "0.2.0";

        internal static string DataDir;

        ConfigEntry<bool> _capture;
        ConfigEntry<KeyCode> _screenshotKey;
        ConfigEntry<string> _autoScreenshots;
        float[] _autoShotTimes = new float[0];
        int _nextAutoShot;

        void Awake()
        {
            DataDir = Path.Combine(Paths.BepInExRootPath, "MageQuit-Translator");
            Directory.CreateDirectory(DataDir);

            _capture = Config.Bind("Capture", "Enabled", false,
                "Record every UI text the game displays to MageQuit-Translator/captured.tsv, so missing strings can be imported in the MageQuit Translator app.");
            _screenshotKey = Config.Bind("Screenshots", "Key", KeyCode.F11,
                "Key that saves a screenshot to MageQuit-Translator/screenshots.");
            _autoScreenshots = Config.Bind("Screenshots", "AutoAtSeconds", "",
                "Developer option: comma separated times (seconds since start) at which to take screenshots automatically.");
            _autoShotTimes = ParseTimes(_autoScreenshots.Value);

            if (_capture.Value)
            {
                TextCapture.Load(Path.Combine(DataDir, "captured.tsv"));
                int hooks = TextCapture.Hook(Guid);
                SceneManager.sceneLoaded += (scene, _) => TextCapture.ScanScene();
                Logger.LogInfo($"Text capture enabled ({hooks} text components hooked)");
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(_screenshotKey.Value))
                TakeScreenshot("manual");
            if (_nextAutoShot < _autoShotTimes.Length && Time.realtimeSinceStartup >= _autoShotTimes[_nextAutoShot])
                TakeScreenshot("auto" + _nextAutoShot++);
            if (_capture.Value)
                TextCapture.FlushIfDue();
        }

        void OnApplicationQuit()
        {
            if (_capture.Value)
                TextCapture.Flush();
        }

        void TakeScreenshot(string tag)
        {
            var dir = Path.Combine(DataDir, "screenshots");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"{System.DateTime.Now:yyyyMMdd-HHmmss}-{tag}.png");
            ScreenCapture.CaptureScreenshot(file);
            Logger.LogInfo("Screenshot: " + file);
        }

        static float[] ParseTimes(string value)
        {
            var list = new System.Collections.Generic.List<float>();
            foreach (var part in value.Split(','))
                if (float.TryParse(part.Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var t))
                    list.Add(t);
            list.Sort();
            return list.ToArray();
        }
    }
}
