using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Markdowner.Rendering;

public sealed record TableCellSpec(Control Content, bool IsHeader, int ColumnSpan = 1, int RowSpan = 1);

/// <summary>Builds bordered tables for both Markdown pipe tables and raw &lt;table&gt; markup.</summary>
public static class TableFactory
{
    public static Control Build(PreviewTheme theme, IReadOnlyList<IReadOnlyList<TableCellSpec>> rows)
    {
        if (rows.Count == 0) return new Border();

        var columns = 0;
        foreach (var row in rows)
        {
            var width = 0;
            foreach (var cell in row) width += cell.ColumnSpan;
            columns = Math.Max(columns, width);
        }
        if (columns == 0) return new Border();

        var grid = new Grid();
        for (var c = 0; c < columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }
        for (var r = 0; r < rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        // Tracks cells still spanning down from an earlier row.
        var occupied = new HashSet<(int Row, int Column)>();

        for (var r = 0; r < rows.Count; r++)
        {
            var column = 0;
            foreach (var cell in rows[r])
            {
                while (occupied.Contains((r, column)) && column < columns) column++;
                if (column >= columns) break;

                for (var dr = 0; dr < cell.RowSpan; dr++)
                {
                    for (var dc = 0; dc < cell.ColumnSpan; dc++)
                    {
                        occupied.Add((r + dr, column + dc));
                    }
                }

                var host = new Border
                {
                    BorderBrush = theme.Border,
                    // Shared edges: every cell draws its right and bottom line only.
                    BorderThickness = new Thickness(column == 0 ? 1 : 0, r == 0 ? 1 : 0, 1, 1),
                    Background = cell.IsHeader ? theme.TableHeaderBackground : null,
                    Padding = new Thickness(13, 7),
                    Child = cell.Content,
                };

                Grid.SetRow(host, r);
                Grid.SetColumn(host, column);
                if (cell.ColumnSpan > 1) Grid.SetColumnSpan(host, Math.Min(cell.ColumnSpan, columns - column));
                if (cell.RowSpan > 1) Grid.SetRowSpan(host, Math.Min(cell.RowSpan, rows.Count - r));

                grid.Children.Add(host);
                column += cell.ColumnSpan;
            }
        }

        return new ScrollViewer
        {
            Content = new Border { Child = grid, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left },
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }
}
