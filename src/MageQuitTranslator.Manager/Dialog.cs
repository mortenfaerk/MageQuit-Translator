using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MageQuitTranslator.Manager;

/// <summary>Minimal modal message box; returns the index of the clicked button (last one on close).</summary>
static class Dialog
{
    public static async Task<int> Ask(Window owner, string title, string message, params string[] buttons)
    {
        int result = buttons.Length - 1;
        var dialog = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = owner.Background,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            var b = new Button { Content = buttons[i], MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            if (i == 0)
                b.Classes.Add("primary");
            b.Click += (_, _) => { result = index; dialog.Close(); };
            row.Children.Add(b);
        }
        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 18,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                row,
            },
        };
        await dialog.ShowDialog(owner);
        return result;
    }
}
