using Avalonia;
using Avalonia.Controls;

namespace MageQuitTranslator.Manager;

/// <summary>
/// Stacks children vertically and shares the available height between them by weight
/// (a chapter's extent), so the whole thumb index fits the book's height without scrolling.
/// </summary>
public sealed class WeightedStackPanel : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<WeightedStackPanel, double>(nameof(Spacing), 6);

    public static readonly StyledProperty<double> MinItemHeightProperty =
        AvaloniaProperty.Register<WeightedStackPanel, double>(nameof(MinItemHeight), 44);

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }
    public double MinItemHeight { get => GetValue(MinItemHeightProperty); set => SetValue(MinItemHeightProperty, value); }

    static double WeightOf(Control c) => (c.DataContext as Chapter)?.Weight ?? 1;

    double[] Heights(double available)
    {
        var kids = Children.Where(c => c.IsVisible).ToList();
        var heights = new double[kids.Count];
        if (kids.Count == 0)
            return heights;
        double free = Math.Max(0, available - Spacing * (kids.Count - 1) - MinItemHeight * kids.Count);
        double total = kids.Sum(WeightOf);
        for (int i = 0; i < kids.Count; i++)
            heights[i] = MinItemHeight + (total > 0 ? free * WeightOf(kids[i]) / total : free / kids.Count);
        return heights;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double available = double.IsInfinity(availableSize.Height) ? 720 : availableSize.Height;
        var heights = Heights(available);
        double width = 0;
        int i = 0;
        foreach (var child in Children.Where(c => c.IsVisible))
        {
            child.Measure(new Size(availableSize.Width, heights[i++]));
            width = Math.Max(width, child.DesiredSize.Width);
        }
        return new Size(width, available);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var heights = Heights(finalSize.Height);
        double y = 0;
        int i = 0;
        foreach (var child in Children.Where(c => c.IsVisible))
        {
            child.Arrange(new Rect(0, y, finalSize.Width, heights[i]));
            y += heights[i++] + Spacing;
        }
        return finalSize;
    }
}
