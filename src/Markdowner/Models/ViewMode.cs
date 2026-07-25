namespace Markdowner.Models;

/// <summary>
/// Which panes are showing. Mirrors the Code / Split / Design switch that
/// classic visual HTML editors put on the document toolbar.
/// </summary>
public enum ViewMode
{
    Source,
    Split,
    Preview,
}
