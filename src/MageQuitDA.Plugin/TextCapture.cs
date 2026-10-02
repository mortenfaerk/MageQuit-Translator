using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MageQuitDA
{
    /// <summary>
    /// Records the original (pre-translation) text of UI components, with the scene,
    /// GameObject path and font they were shown in. Stored as TSV so the Manager can
    /// import strings that static extraction missed (e.g. text built at runtime).
    /// </summary>
    internal static class TextCapture
    {
        internal class Entry
        {
            public string Text, Font, Scene, Path;
            public int Count;
        }

        static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();
        static string _file;
        static bool _dirty;
        static float _lastFlush;

        public static void Load(string file)
        {
            _file = file;
            if (!File.Exists(file))
                return;
            foreach (var line in File.ReadAllLines(file, Encoding.UTF8))
            {
                var f = line.Split('\t');
                if (f.Length < 5 || f[0] == "text")
                    continue;
                int.TryParse(f[4], out var count);
                var e = new Entry { Text = Unescape(f[0]), Font = f[1], Scene = f[2], Path = Unescape(f[3]), Count = count };
                Entries[e.Text] = e;
            }
        }

        public static void Record(Component c, string text, string font)
        {
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0 || !HasLetter(text))
                return;
            if (Entries.TryGetValue(text, out var e))
            {
                e.Count++;
                return;
            }
            Entries[text] = new Entry
            {
                Text = text,
                Font = font ?? "",
                Scene = SceneManager.GetActiveScene().name,
                Path = c != null ? GetPath(c.transform) : "",
                Count = 1,
            };
            _dirty = true;
        }

        public static void ScanScene(string scene)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Text>())
                if (t.gameObject.scene.IsValid())
                    Record(t, t.text, t.font != null ? t.font.name : null);
            foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
                if (t.gameObject.scene.IsValid())
                    Record(t, t.text, t.font != null ? t.font.name : null);
        }

        public static void FlushIfDue()
        {
            if (_dirty && Time.realtimeSinceStartup - _lastFlush > 10f)
                Flush();
        }

        public static void Flush()
        {
            _lastFlush = Time.realtimeSinceStartup;
            if (!_dirty || _file == null)
                return;
            var sb = new StringBuilder("text\tfont\tscene\tpath\tcount\n");
            foreach (var e in Entries.Values)
                sb.Append(Escape(e.Text)).Append('\t').Append(e.Font).Append('\t').Append(e.Scene).Append('\t')
                  .Append(Escape(e.Path)).Append('\t').Append(e.Count).Append('\n');
            File.WriteAllText(_file, sb.ToString(), new UTF8Encoding(false));
            _dirty = false;
        }

        static bool HasLetter(string s)
        {
            foreach (var ch in s)
                if (char.IsLetter(ch))
                    return true;
            return false;
        }

        static string GetPath(Transform t)
        {
            var parts = new List<string>();
            for (; t != null && parts.Count < 8; t = t.parent)
                parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        static string Escape(string s) =>
            s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");

        static string Unescape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    var n = s[++i];
                    sb.Append(n == 't' ? '\t' : n == 'n' ? '\n' : n == 'r' ? '\r' : n);
                }
                else
                    sb.Append(s[i]);
            }
            return sb.ToString();
        }

        /// <summary>Prefixes run before XUnity's hooks replace the text, so they see the English original.</summary>
        internal static class Hooks
        {
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            [HarmonyPatch(typeof(Text), nameof(Text.text), MethodType.Setter)]
            static void UguiText(Text __instance, string value) =>
                Record(__instance, value, __instance.font != null ? __instance.font.name : null);

            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            [HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.text), MethodType.Setter)]
            static void TmpText(TMP_Text __instance, string value) =>
                Record(__instance, value, __instance.font != null ? __instance.font.name : null);
        }
    }
}
