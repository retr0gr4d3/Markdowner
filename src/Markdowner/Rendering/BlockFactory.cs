using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Markdowner.Rendering;

/// <summary>Block-level chrome shared by both preview skins.</summary>
public static class BlockFactory
{
    public static StackPanel Stack(double spacing = 8) => new() { Spacing = spacing };

    /// <summary>A fenced code block: monospaced, boxed, horizontally scrollable.</summary>
    public static Control CodeBlock(PreviewTheme theme, string code, string? language, bool showLanguage = true)
    {
        var text = new SelectableTextBlock
        {
            Text = code.TrimEnd('\n'),
            FontFamily = theme.MonoFont,
            FontSize = theme.FontSize - 1.5,
            Foreground = theme.CodeText,
            TextWrapping = TextWrapping.NoWrap,
            LineHeight = theme.FontSize + 5,
        };

        // Padding lives on an inner Border rather than on the ScrollViewer so
        // the scrolled content measures at its natural size.
        var body = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(12, 9), Child = text },
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var inner = new DockPanel();

        if (showLanguage && !string.IsNullOrWhiteSpace(language))
        {
            var label = new Border
            {
                Background = theme.SubtleBackground,
                BorderBrush = theme.CodeBorder,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 3),
                Child = new TextBlock
                {
                    Text = language,
                    FontFamily = theme.MonoFont,
                    FontSize = theme.FontSize - 3,
                    Foreground = theme.Muted,
                },
            };
            DockPanel.SetDock(label, Dock.Top);
            inner.Children.Add(label);
        }

        inner.Children.Add(body);

        return new Border
        {
            Background = theme.CodeBackground,
            BorderBrush = theme.CodeBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Child = inner,
        };
    }

    /// <summary>A blockquote: coloured bar on the left, content indented beside it.</summary>
    public static Control Quote(PreviewTheme theme, IEnumerable<Control> children, IBrush? bar = null)
    {
        var content = Stack(6);
        foreach (var child in children) content.Children.Add(child);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
        };

        var rule = new Border
        {
            Width = 4,
            CornerRadius = new CornerRadius(2),
            Background = bar ?? theme.QuoteBar,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Grid.SetColumn(rule, 0);

        var host = new Border
        {
            Padding = new Thickness(12, 0, 0, 0),
            Child = content,
        };
        Grid.SetColumn(host, 1);

        grid.Children.Add(rule);
        grid.Children.Add(host);
        return grid;
    }

    public static Control Rule(PreviewTheme theme) => new Border
    {
        Height = 1,
        Background = theme.Border,
        Margin = new Thickness(0, 6),
    };

    /// <summary>One row of a list: marker in a fixed gutter, content beside it.</summary>
    public static Control ListRow(PreviewTheme theme, string marker, Control content, int level)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(level * 22, 0, 0, 0),
        };

        var bullet = new TextBlock
        {
            Text = marker,
            Foreground = theme.Text,
            FontFamily = theme.BodyFont,
            FontSize = theme.FontSize,
            LineHeight = theme.LineHeight,
            MinWidth = 22,
            TextAlignment = marker.EndsWith('.') ? TextAlignment.Right : TextAlignment.Left,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Grid.SetColumn(bullet, 0);
        Grid.SetColumn(content, 1);

        grid.Children.Add(bullet);
        grid.Children.Add(content);
        return grid;
    }

    /// <summary>A placeholder box for content the renderer cannot draw natively (images, video).</summary>
    public static Control Placeholder(PreviewTheme theme, string glyph, string caption, string? detail = null)
    {
        var stack = Stack(2);
        stack.Children.Add(new TextBlock
        {
            Text = $"{glyph}  {caption}",
            Foreground = theme.Muted,
            FontSize = theme.FontSize - 1,
            TextWrapping = TextWrapping.Wrap,
        });

        if (!string.IsNullOrWhiteSpace(detail))
        {
            stack.Children.Add(new TextBlock
            {
                Text = detail,
                Foreground = theme.Muted,
                FontFamily = theme.MonoFont,
                FontSize = theme.FontSize - 3,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        return new Border
        {
            Background = theme.SubtleBackground,
            BorderBrush = theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 9),
            Child = stack,
        };
    }
}
