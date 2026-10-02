using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MageQuitTranslator.Core;
using Xunit;

namespace MageQuitTranslator.Tests;

public class TranslationTests
{
    [Theory]
    [InlineData("a=b", @"a\=b")]
    [InlineData("line1\nline2", @"line1\nline2")]
    [InlineData(@"back\slash", @"back\\slash")]
    [InlineData("see magequit.com//x", @"see magequit.com\u002F/x")]
    [InlineData("100%3D", @"100\u00253D")]
    public void EscapesForXUnity(string input, string expected) =>
        Assert.Equal(expected, TranslationFile.Escape(input));

    [Fact]
    public void WritesTextRegexAndSplitterLines()
    {
        var file = new TranslationFile
        {
            Strings =
            [
                new() { En = "Back", Text = "Tilbage" },
                new() { En = "Untranslated", Text = "" },
                new() { En = "PARTY", Text = "Gruppe", UppercaseOnly = true },
                new() { En = @"^Level (\d+)$", Text = "Niveau $1", Kind = "regex" },
                new() { En = @"^(?<el>.+) WILL$", Text = "${el} VIL", Kind = "split" },
            ],
        };
        var lines = file.ToXUnity().Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
        Assert.Equal(["Back=Tilbage", "PARTY=GRUPPE", @"r:""^Level (\\d+)$""=Niveau $1", @"sr:""^(?<el>.+) WILL$""=${el} VIL"], lines);
    }

    [Fact]
    public void MergeKeepsUserEditsAndAddsNewStrings()
    {
        var mine = new TranslationFile
        {
            Strings =
            [
                new() { En = "A", Text = "mit A", Edited = true, Status = "reviewed" },
                new() { En = "B", Text = "gammelt B" },
                new() { En = "Captured", Confidence = "captured", Edited = true },
            ],
        };
        var shipped = new TranslationFile
        {
            Strings = [new() { En = "A", Text = "nyt A" }, new() { En = "B", Text = "nyt B" }, new() { En = "C", Text = "C" }],
        };
        mine.MergeShipped(shipped);
        Assert.Equal(["A:mit A", "B:nyt B", "C:C", "Captured:"], mine.Strings.Select(s => $"{s.En}:{s.Text}"));
    }

    [Fact]
    public void TemplateKeepsEveryStringButNoTranslations()
    {
        var da = new TranslationFile
        {
            Language = "da",
            Strings = [new() { En = "Back", Text = "Tilbage", Status = "reviewed", Sources = ["level1: Back"] },
                       new() { En = @"^Round (\d+)$", Text = "Runde $1", Kind = "regex" }],
        };
        var de = da.ToTemplate("de");
        Assert.Equal("de", de.Language);
        Assert.All(de.Strings, s => Assert.Equal("", s.Text));
        Assert.All(de.Strings, s => Assert.Equal("new", s.Status));
        Assert.Equal(["level1: Back"], de.Strings[0].Sources);
        Assert.Contains("Runde $1", de.Strings[1].Note);
    }

    [Fact]
    public void ImportCapturedSkipsKnownTranslatedNoiseAndPatterns()
    {
        var file = new TranslationFile
        {
            Strings = [new() { En = "Back", Text = "Tilbage" }, new() { En = @"^Level (\d+)$", Text = "Niveau $1", Kind = "regex" }],
        };
        var tsv = Path.GetTempFileName();
        File.WriteAllText(tsv, "text\tfont\tscene\tpath\tcount\n" +
                               "Back\tteen\tMain Menu\tCanvas/Back\t1\n" +
                               "Tilbage\tteen\tMain Menu\tCanvas/Back\t1\n" +
                               "Level 4\tteen\tMain Menu\tCanvas/Lvl\t1\n" +
                               "1920x1080\tteen\tMain Menu\tCanvas/Res\t1\n" +
                               "Q\tteen\tMain Menu\tCanvas/Letter\t1\n" +
                               "Zurück\tteen\tMain Menu\tCanvas/Back\t1\n" +
                               "New text\\nline 2\tMageQuitHeaderThin\tLobby\tCanvas/X\t2\n");
        Assert.Equal(1, file.ImportCaptured(tsv, otherTranslations: ["Zurück"]));
        var added = file.Strings.Last();
        Assert.Equal("New text\nline 2", added.En);
        Assert.True(added.UppercaseOnly);
        Assert.Equal("captured", added.Confidence);
    }

    internal static string RepoRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "VERSION")))
                return d.FullName;
        throw new DirectoryNotFoundException("repo root");
    }
}

/// <summary>Checks every language folder a pull request may add or change.</summary>
public class LanguageFolderTests
{
    public static TheoryData<string> Languages()
    {
        var data = new TheoryData<string>();
        foreach (var dir in Directory.EnumerateDirectories(Path.Combine(TranslationTests.RepoRoot(), "translation")))
            data.Add(Path.GetFileName(dir));
        return data;
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void LanguageFolderIsValid(string code)
    {
        var dir = Path.Combine(TranslationTests.RepoRoot(), "translation", code);
        Assert.True(File.Exists(Path.Combine(dir, LanguagePack.InfoFile)), $"{code}: language.json is missing");
        var pack = LanguagePack.Load(dir);

        Assert.True(LanguageInfo.IsValidCode(code), $"folder name '{code}' is not a language code like 'de' or 'pt-BR'");
        Assert.Equal(code, pack.Info.Code);
        Assert.Equal(code, pack.Strings.Language);
        Assert.False(string.IsNullOrWhiteSpace(pack.Info.Name), $"{code}: name is empty");
        Assert.False(string.IsNullOrWhiteSpace(pack.Info.NativeName), $"{code}: nativeName is empty");

        var keys = pack.Strings.Strings.Select(s => s.Kind + "\0" + s.En).ToList();
        var duplicate = keys.GroupBy(k => k).FirstOrDefault(g => g.Count() > 1)?.Key;
        Assert.True(duplicate == null, $"{code}: duplicate string '{duplicate?.Split('\0')[1]}'");
        foreach (var s in pack.Strings.Strings)
        {
            Assert.Contains(s.Status, new[] { "new", "machine", "reviewed" });
            Assert.Contains(s.Kind, new[] { "text", "regex", "split" });
            Assert.False(s.Edited, $"{code}: '{s.En}' still has the local 'edited' flag; export with the app");
            if (s.IsPattern)
                _ = new Regex(s.En);
        }
        foreach (var l in pack.Labels.Labels)
            Assert.True(l.Width > 0 && l.Height > 0, $"{code}: label {l.Texture} has no size");
    }
}

public class LanguageInfoTests
{
    [Theory]
    [InlineData("de", true)]
    [InlineData("pt-BR", true)]
    [InlineData("zh-Hans", true)]
    [InlineData("DE", false)]
    [InlineData("german", false)]
    public void ValidatesCodes(string code, bool valid) => Assert.Equal(valid, LanguageInfo.IsValidCode(code));

    [Fact]
    public void FillsNamesFromCulture()
    {
        var info = LanguageInfo.Create("de");
        Assert.Equal("German", info.Name);
        Assert.Equal("Deutsch", info.NativeName);
    }
}

public class LabelTests
{
    [Fact]
    public void FileNameUsesXUnityNameHash() =>
        Assert.Equal("Main Screen_Couch [7890810C83].png", new LabelEntry { Texture = "Main Screen_Couch" }.FileName);
}

public class FontCoverageTests
{
    [Fact]
    public void ReportsLettersAFontLacks()
    {
        var coverage = FontCoverage.Parse("""{"fonts":{"MageQuit-Body":{"chars":"abcdeÆØÅæøå ","uppercaseOnly":false}}}""");
        Assert.Equal("ß", coverage.MissingChars("abß", ["MageQuit-Body"]));
        Assert.Equal("", coverage.MissingChars("æøå", ["MageQuit-Body"]));
        Assert.Equal("", coverage.MissingChars("ß", ["unknown font"]));
    }
}

/// <summary>Runs against a copy of the real game files; skipped when the game is not installed.</summary>
public class FontPatchTests
{
    static readonly string GameDir = Environment.GetEnvironmentVariable("MAGEQUIT_DIR")
                                     ?? @"C:\Program Files (x86)\Steam\steamapps\common\MageQuit";

    [Fact]
    public void ApplyAndRevertRoundTripsExactly()
    {
        Assert.SkipUnless(GameLocator.IsGameDir(GameDir), "MageQuit not installed");
        var patches = FontPatcher.Load(File.ReadAllText(Path.Combine(TranslationTests.RepoRoot(),
            "payload", "game", "BepInEx", "MageQuit-Translator", "fontpatch.json")));
        var temp = Directory.CreateTempSubdirectory("mqt").FullName;
        try
        {
            foreach (var p in patches)
            {
                var dst = Path.Combine(temp, p.File);
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(Path.Combine(GameDir, p.File), dst);
                if (FontPatcher.GetState(temp, p) != FontPatcher.State.Original)
                    Assert.Skip("Game files are not in original state (mod installed?)");
                var before = SHA256.HashData(File.ReadAllBytes(dst));

                Assert.True(FontPatcher.Apply(temp, p));
                Assert.Equal(FontPatcher.State.Patched, FontPatcher.GetState(temp, p));
                var ttf = FontPatcher.ReadPatchedTtf(temp, p);
                Assert.NotNull(ttf);
                Assert.Equal(0x00010000, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(ttf));
                Assert.True(FontPatcher.Apply(temp, p));

                Assert.True(FontPatcher.Revert(temp, p));
                Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(dst)));
            }
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }
}
