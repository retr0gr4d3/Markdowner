using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Markdowner.Models;
using Markdowner.Rendering;

namespace Markdowner.Views;

/// <summary>
/// A reference for whichever dialect the preview is currently showing. Each
/// example is real Markdown pushed through the same renderer as the main
/// preview, so the documentation cannot fall out of step with the behaviour.
/// </summary>
public partial class FormattingHelpWindow : Window
{
    private MarkdownFlavor _flavor = MarkdownFlavor.GitHub;

    public FormattingHelpWindow()
    {
        InitializeComponent();
        FilterBox.TextChanged += (_, _) => Rebuild();
    }

    /// <summary>Points the window at a dialect and rebuilds its contents.</summary>
    public void ShowFlavor(MarkdownFlavor flavor)
    {
        _flavor = flavor;
        Title = $"{flavor.DisplayName()} Formatting Help";
        HeadingText.Text = Title;
        SubheadingText.Text = flavor.Description();
        Rebuild();
    }

    private void Rebuild()
    {
        var renderer = CreateRenderer();
        var theme = renderer.Theme;
        var filter = FilterBox.Text?.Trim() ?? string.Empty;

        ContentHost.Children.Clear();

        var shown = 0;
        var total = 0;

        foreach (var section in FormattingReference.For(_flavor))
        {
            var matches = section.Entries.Where(entry => Matches(section, entry, filter)).ToList();
            total += section.Entries.Count;
            shown += matches.Count;

            if (matches.Count == 0) continue;

            ContentHost.Children.Add(SectionHeader(section.Name));

            foreach (var entry in matches)
            {
                ContentHost.Children.Add(EntryCard(entry, renderer, theme));
            }
        }

        if (shown == 0)
        {
            ContentHost.Children.Add(new TextBlock
            {
                Text = $"Nothing matches “{filter}”.",
                Foreground = Brush.Parse("#8F969F"),
                Margin = new Thickness(2, 8),
            });
        }

        CountText.Text = shown == total
            ? $"{total} formats"
            : $"{shown} of {total} formats";
    }

    private IMarkdownRenderer CreateRenderer() =>
        _flavor == MarkdownFlavor.Discord ? new DiscordRenderer() : new GitHubRenderer();

    private static bool Matches(FormatSection section, FormatEntry entry, string filter)
    {
        if (filter.Length == 0) return true;

        return Has(entry.Name) || Has(entry.Syntax) || Has(entry.Notes) || Has(section.Name);

        bool Has(string value) => value.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private static Control SectionHeader(string name)
    {
        var stack = new StackPanel { Spacing = 5 };

        stack.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 14.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush.Parse("#D6DAE1"),
        });

        stack.Children.Add(new Border { Height = 1, Background = Brush.Parse("#3C4149") });
        return stack;
    }

    private static Control EntryCard(FormatEntry entry, IMarkdownRenderer renderer, PreviewTheme theme)
    {
        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
        };

        var syntax = new Border
        {
            Background = Brush.Parse("#1E2024"),
            BorderBrush = Brush.Parse("#3C4149"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(11, 8),
            Margin = new Thickness(0, 0, 6, 0),
            Child = new SelectableTextBlock
            {
                Text = entry.Syntax,
                FontFamily = theme.MonoFont,
                FontSize = 12.5,
                Foreground = Brush.Parse("#C7CEDA"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18,
            },
        };
        Grid.SetColumn(syntax, 0);

        var preview = new Border
        {
            Background = theme.Background,
            BorderBrush = Brush.Parse("#3C4149"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(12, 9),
            Margin = new Thickness(6, 0, 0, 0),
            ClipToBounds = true,
            Child = SafeRender(renderer, entry.Syntax),
        };
        Grid.SetColumn(preview, 1);

        body.Children.Add(syntax);
        body.Children.Add(preview);

        var stack = new StackPanel { Spacing = 6 };

        stack.Children.Add(new TextBlock
        {
            Text = entry.Name,
            FontWeight = FontWeight.SemiBold,
            FontSize = 12.5,
            Foreground = Brush.Parse("#D6DAE1"),
        });

        stack.Children.Add(body);

        if (!string.IsNullOrWhiteSpace(entry.Notes))
        {
            stack.Children.Add(new TextBlock
            {
                Text = entry.Notes,
                Foreground = Brush.Parse("#8F969F"),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        return new Border
        {
            Background = Brush.Parse("#2B2E33"),
            BorderBrush = Brush.Parse("#3C4149"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = stack,
        };
    }

    private static Control SafeRender(IMarkdownRenderer renderer, string syntax)
    {
        try
        {
            return renderer.Render(syntax);
        }
        catch (Exception ex)
        {
            return new TextBlock
            {
                Text = ex.Message,
                Foreground = Brushes.IndianRed,
                TextWrapping = TextWrapping.Wrap,
            };
        }
    }
}
