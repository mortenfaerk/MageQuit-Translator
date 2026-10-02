using System.Security.Cryptography;
using MageQuitDA.Core;
using Xunit;

namespace MageQuitDA.Tests;

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
                new() { En = "Back", Da = "Tilbage" },
                new() { En = "Untranslated", Da = "" },
                new() { En = "PARTY", Da = "Gruppe", UppercaseOnly = true },
                new() { En = @"^Level (\d+)$", Da = "Niveau $1", Kind = "regex" },
                new() { En = @"^(?<el>.+) WILL$", Da = "${el} VIL", Kind = "split" },
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
                new() { En = "A", Da = "mit A", Edited = true, Status = "reviewed" },
                new() { En = "B", Da = "gammelt B" },
                new() { En = "Captured", Confidence = "captured", Edited = true },
            ],
        };
        var shipped = new TranslationFile
        {
            Strings = [new() { En = "A", Da = "nyt A" }, new() { En = "B", Da = "nyt B" }, new() { En = "C", Da = "C" }],
        };
        mine.MergeShipped(shipped);
        Assert.Equal(["A:mit A", "B:nyt B", "C:C", "Captured:"], mine.Strings.Select(s => $"{s.En}:{s.Da}"));
    }

    [Fact]
    public void ImportCapturedSkipsKnownTranslatedAndNumericText()
    {
        var file = new TranslationFile
        {
            Strings = [new() { En = "Back", Da = "Tilbage" }, new() { En = @"^Level (\d+)$", Da = "Niveau $1", Kind = "regex" }],
        };
        var tsv = Path.GetTempFileName();
        File.WriteAllText(tsv, "text\tfont\tscene\tpath\tcount\n" +
                               "Back\tteen\tMain Menu\tCanvas/Back\t1\n" +
                               "Tilbage\tteen\tMain Menu\tCanvas/Back\t1\n" +
                               "Level 4\tteen\tMain Menu\tCanvas/Lvl\t1\n" +
                               "1920x1080\tteen\tMain Menu\tCanvas/Res\t1\n" +
                               "New text\\nline 2\tMageQuitHeaderThin\tLobby\tCanvas/X\t2\n");
        Assert.Equal(1, file.ImportCaptured(tsv));
        var added = file.Strings.Last();
        Assert.Equal("New text\nline 2", added.En);
        Assert.True(added.UppercaseOnly);
        Assert.Equal("captured", added.Confidence);
    }

    [Fact]
    public void ShippedStringsJsonIsValid()
    {
        var file = TranslationFile.Load(Path.Combine(RepoRoot(), "translation", "strings.json"));
        Assert.True(file.Strings.Count > 500);
        Assert.Equal(file.Strings.Count, file.Strings.Select(s => s.Kind + s.En).Distinct().Count());
        foreach (var s in file.Strings.Where(s => s.Kind is "regex" or "split"))
            _ = new System.Text.RegularExpressions.Regex(s.En);
    }

    internal static string RepoRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "VERSION")))
                return d.FullName;
        throw new DirectoryNotFoundException("repo root");
    }
}

public class LabelTests
{
    [Fact]
    public void FileNameUsesXUnityNameHash() =>
        Assert.Equal("Main Screen_Couch [7890810C83].png", new LabelEntry { Texture = "Main Screen_Couch" }.FileName);
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
            "payload", "game", "BepInEx", "MageQuit-DA", "fontpatch.json")));
        var temp = Directory.CreateTempSubdirectory("mqda").FullName;
        try
        {
            foreach (var p in patches)
            {
                var src = Path.Combine(GameDir, p.File);
                var dst = Path.Combine(temp, p.File);
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(src, dst);
                if (FontPatcher.GetState(temp, p) != FontPatcher.State.Original)
                    Assert.Skip("Game files are not in original state (mod installed?)");
                var before = SHA256.HashData(File.ReadAllBytes(dst));

                Assert.True(FontPatcher.Apply(temp, p));
                Assert.Equal(FontPatcher.State.Patched, FontPatcher.GetState(temp, p));
                var ttf = FontPatcher.ReadPatchedTtf(temp, p);
                Assert.NotNull(ttf);
                Assert.Equal(0x00010000, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(ttf));
                Assert.True(FontPatcher.Apply(temp, p)); // idempotent

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
