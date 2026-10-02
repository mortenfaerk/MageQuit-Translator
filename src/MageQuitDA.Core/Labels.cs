using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;

namespace MageQuitDA.Core;

/// <summary>A menu label that the game draws as an image instead of text (e.g. "Couch", "PLAY").</summary>
public sealed class LabelEntry
{
    /// <summary>Unity texture name; XUnity matches replacement PNGs by this name.</summary>
    [JsonPropertyName("texture")] public string Texture { get; set; } = "";
    [JsonPropertyName("en")] public string En { get; set; } = "";
    [JsonPropertyName("da")] public string Da { get; set; } = "";
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
    /// <summary>"left" or "center": where narrower Danish text sits inside the original image bounds.</summary>
    [JsonPropertyName("align")] public string Align { get; set; } = "center";
    [JsonPropertyName("status")] public string Status { get; set; } = "machine";
    [JsonPropertyName("edited")] public bool Edited { get; set; }

    /// <summary>File name XUnity looks for: "name [SHA1(name)[:10]].png" (see TextureTranslationCache).</summary>
    [JsonIgnore]
    public string FileName =>
        $"{Texture} [{Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(Texture)))[..10]}].png";
}

public sealed class LabelFile
{
    [JsonPropertyName("labels")] public List<LabelEntry> Labels { get; set; } = [];

    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static LabelFile Load(string path) =>
        JsonSerializer.Deserialize<LabelFile>(File.ReadAllText(path, Encoding.UTF8), Options) ?? new();

    public static LabelFile Parse(string json) => JsonSerializer.Deserialize<LabelFile>(json, Options) ?? new();

    public void Save(string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(this, Options).Replace("\r\n", "\n"), new UTF8Encoding(false));

    public void MergeShipped(LabelFile shipped)
    {
        var mine = Labels.ToDictionary(l => l.Texture);
        Labels = shipped.Labels.Select(s => mine.TryGetValue(s.Texture, out var m) && m.Edited ? m : s).ToList();
    }
}

/// <summary>
/// Renders Danish label images in the game's own label typeface (MageQuit-Body, with the
/// patched Danish letters). The English label is laid out first to recover the scale,
/// horizontal stretch and baseline of the original artwork; the Danish text reuses them,
/// shrinking only if it would not fit the original image size.
/// </summary>
public sealed class LabelRenderer : IDisposable
{
    readonly SKTypeface _typeface;

    public LabelRenderer(byte[] ttf)
    {
        _typeface = SKTypeface.FromData(SKData.CreateCopy(ttf))
                    ?? throw new InvalidDataException("Could not load label font.");
    }

    public byte[] RenderPng(LabelEntry label)
    {
        const float refSize = 100f;
        using var font = new SKFont(_typeface, refSize);
        var en = Measure(font, label.En);
        var da = Measure(font, string.IsNullOrWhiteSpace(label.Da) ? label.En : label.Da);

        // Scale and stretch that make the English text fill the original image exactly.
        float scale = label.Height / en.Height;
        float stretch = label.Width / (en.Width * scale);

        // Danish ink must stay inside the English vertical extent (e.g. the ring on Å).
        float top = Math.Min(en.Top, da.Top), bottom = Math.Max(en.Bottom, da.Bottom);
        float fit = en.Height / (bottom - top);
        float s = scale * fit;
        float sx = stretch;
        float width = da.Width * s * sx;
        if (width > label.Width)
        {
            sx *= label.Width / width;
            width = label.Width;
        }

        var info = new SKImageInfo(label.Width, label.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        float x0 = label.Align == "left" ? 0 : (label.Width - width) / 2f;
        canvas.Translate(x0, 0);
        canvas.Scale(s * sx, s);
        // Baseline so the combined ink extent starts at the top edge.
        canvas.Translate(-da.Left, -top);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawText(string.IsNullOrWhiteSpace(label.Da) ? label.En : label.Da, 0, 0, SKTextAlign.Left, font, paint);
        canvas.Flush();
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    static SKRect Measure(SKFont font, string text)
    {
        font.MeasureText(text, out var bounds);
        return bounds;
    }

    public void Dispose() => _typeface.Dispose();
}
