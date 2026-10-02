using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageQuitTranslator
{
    /// <summary>
    /// Records the original (pre-translation) text of UI components, with the scene,
    /// GameObject path and font they were shown in. Stored as TSV so the app can import
    /// strings that static extraction missed (e.g. text built at runtime).
    /// UGUI Text and TextMeshPro are reached through reflection so the plugin builds
    /// against plain Unity reference assemblies.
    /// </summary>
    internal static class TextCapture
    {
        sealed class Entry
        {
            public string Text, Font, Scene, Path;
            public int Count;
        }

        sealed class TextType
        {
            public Type Type;
            public PropertyInfo TextProperty, FontProperty;
        }

        static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();
        static readonly List<TextType> Types = new List<TextType>();
        static string _file;
        static bool _dirty;
        static float _lastFlush;

        /// <summary>Patches the text setters of UGUI Text and TextMeshPro. Returns how many were hooked.</summary>
        public static int Hook(string harmonyId)
        {
            var harmony = new Harmony(harmonyId);
            var prefix = new HarmonyMethod(typeof(TextCapture).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic))
            {
                // Run before XUnity replaces the text, so the English original is recorded.
                priority = Priority.First,
            };
            foreach (var name in new[] { "UnityEngine.UI.Text, UnityEngine.UI", "TMPro.TMP_Text, Unity.TextMeshPro" })
            {
                var type = Type.GetType(name);
                var text = type?.GetProperty("text");
                if (text?.GetSetMethod() == null)
                    continue;
                Types.Add(new TextType { Type = type, TextProperty = text, FontProperty = type.GetProperty("font") });
                harmony.Patch(text.GetSetMethod(), prefix: prefix);
            }
            return Types.Count;
        }

        static void Prefix(Component __instance, string value) => Record(__instance, value);

        public static void ScanScene()
        {
            foreach (var t in Types)
                foreach (var obj in Resources.FindObjectsOfTypeAll(t.Type))
                    if (obj is Component c && c.gameObject.scene.IsValid())
                        Record(c, t.TextProperty.GetValue(c, null) as string);
        }

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

        static void Record(Component c, string text)
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
                Font = FontName(c),
                Scene = SceneManager.GetActiveScene().name,
                Path = c != null ? GetPath(c.transform) : "",
                Count = 1,
            };
            _dirty = true;
        }

        static string FontName(Component c)
        {
            if (c == null)
                return "";
            foreach (var t in Types)
                if (t.Type.IsInstanceOfType(c) && t.FontProperty?.GetValue(c, null) is UnityEngine.Object font && font != null)
                    return font.name;
            return "";
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
    }
}
