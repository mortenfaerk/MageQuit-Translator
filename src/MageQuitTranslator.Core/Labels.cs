using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using SkiaSharp;

namespace MageQuitTranslator.Core;

/// <summary>A menu label that the game draws as an image instead of text (e.g. "Couch", "PLAY").</summary>
public sealed class LabelEntry
{
    /// <summary>Unity texture name; XUnity matches replacement PNGs by this name.</summary>
    [JsonPropertyName("texture")] public string Texture { get; set; } = "";
    [JsonPropertyName("en")] public string En { get; set; } = "";
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
    /// <summary>"left" or "center": where narrower text sits inside the original image bounds.</summary>
    [JsonPropertyName("align")] public string Align { get; set; } = "center";
    [JsonPropertyName("status")] public string Status { get; set; } = "new";
    [JsonPropertyName("edited")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Edited { get; set; }

    /// <summary>File name XUnity looks for: "name [SHA1(name)[:10]].png" (see TextureTranslationCache).</summary>
    [JsonIgnore]
    public string FileName =>
        $"{Texture} [{Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(Texture)))[..10]}].png";
}

public sealed class LabelFile
{
    [JsonPropertyName("labels")] public List<LabelEntry> Labels { get; set; } = [];

    public static LabelFile Load(string path) => Json.Read<LabelFile>(path);
    public static LabelFile Parse(string json) => Json.Parse<LabelFile>(json);
    public void Save(string path) => Json.WriteAtomic(path, this);

    public void MergeShipped(LabelFile shipped)
    {
        var mine = Labels.ToDictionary(l => l.Texture);
        Labels = shipped.Labels.Select(s => mine.TryGetValue(s.Texture, out var m) && m.Edited ? m : s).ToList();
    }

    public LabelFile ToTemplate() => new()
    {
        Labels = Labels.Select(l => new LabelEntry
        {
            Texture = l.Texture, En = l.En, Width = l.Width, Height = l.Height, Align = l.Align,
        }).ToList(),
    };

    public LabelFile ForExport()
    {
        var copy = Json.Parse<LabelFile>(Json.Serialize(this));
        foreach (var l in copy.Labels)
            l.Edited = false;
        return copy;
    }
}

/// <summary>
/// Renders translated label images in the game's own label typeface (MageQuit-Body, with the
/// patched accented letters). The English label is laid out first to recover the scale,
/// horizontal stretch and baseline of the original artwork; the translation reuses them,
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
        var text = string.IsNullOrWhiteSpace(label.Text) ? label.En : label.Text;
        using var font = new SKFont(_typeface, refSize);
        var en = Measure(font, label.En);
        var tr = Measure(font, text);

        // Scale and stretch that make the English text fill the original image exactly.
        float scale = label.Height / en.Height;
        float stretch = label.Width / (en.Width * scale);

        // Translated ink must stay inside the English vertical extent (e.g. accents on capitals).
        float top = Math.Min(en.Top, tr.Top), bottom = Math.Max(en.Bottom, tr.Bottom);
        float s = scale * (en.Height / (bottom - top));
        float sx = stretch;
        float width = tr.Width * s * sx;
        if (width > label.Width)
        {
            sx *= label.Width / width;
            width = label.Width;
        }

        var info = new SKImageInfo(label.Width, label.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(label.Align == "left" ? 0 : (label.Width - width) / 2f, 0);
        canvas.Scale(s * sx, s);
        canvas.Translate(-tr.Left, -top);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawText(text, 0, 0, SKTextAlign.Left, font, paint);
        canvas.Flush();
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Renders a word in the label typeface, tightly cropped, at the given ink height (for previews).</summary>
    public byte[] RenderWord(string text, int inkHeight, uint argb = 0xFFFFFFFF)
    {
        using var font = new SKFont(_typeface, 100f);
        var b = Measure(font, string.IsNullOrEmpty(text) ? " " : text);
        float s = inkHeight / Math.Max(1f, b.Height);
        int w = Math.Max(1, (int)Math.Ceiling(b.Width * s) + 2), h = Math.Max(1, inkHeight + 2);
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(1, 1);
        canvas.Scale(s);
        canvas.Translate(-b.Left, -b.Top);
        using var paint = new SKPaint { Color = new SKColor(argb), IsAntialias = true };
        canvas.DrawText(text, 0, 0, SKTextAlign.Left, font, paint);
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
