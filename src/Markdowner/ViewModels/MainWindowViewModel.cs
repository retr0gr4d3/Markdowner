using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Markdowner.Models;

namespace Markdowner.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private static readonly Regex WordRe = new(@"[^\s]+", RegexOptions.Compiled);

    /// <summary>The editor buffer. Owned here so document state and status counters stay together.</summary>
    public TextDocument Document { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FlavorSummary))]
    [NotifyPropertyChangedFor(nameof(CharacterCountText))]
    [NotifyPropertyChangedFor(nameof(IsOverCharacterLimit))]
    [NotifyPropertyChangedFor(nameof(FlavorIndex))]
    [NotifyPropertyChangedFor(nameof(FormattingHelpTitle))]
    private MarkdownFlavor _flavor = MarkdownFlavor.GitHub;

    [ObservableProperty]
    private IReadOnlyList<SnippetCategory> _categories = [];

    [ObservableProperty]
    private SnippetCategory? _selectedCategory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSourceMode))]
    [NotifyPropertyChangedFor(nameof(IsSplitMode))]
    [NotifyPropertyChangedFor(nameof(IsPreviewMode))]
    private ViewMode _viewMode = ViewMode.Split;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DocumentName))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string? _filePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private bool _isModified;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaretPositionText))]
    private int _caretLine = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaretPositionText))]
    private int _caretColumn = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WordCountText))]
    private int _wordCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CharacterCountText))]
    [NotifyPropertyChangedFor(nameof(IsOverCharacterLimit))]
    private int _characterCount;

    [ObservableProperty] private bool _wordWrap = true;
    [ObservableProperty] private bool _showLineNumbers = true;
    [ObservableProperty] private string _statusMessage = "Ready";

    public MainWindowViewModel()
    {
        Categories = SnippetLibrary.For(Flavor);
        SelectedCategory = Categories.FirstOrDefault();

        // Assigned before subscribing so the starter document doesn't count as an edit.
        Document.Text = SampleDocuments.Default;
        Document.TextChanged += (_, _) => OnDocumentTextChanged();
        UpdateCounts();
    }

    // ------------------------------------------------------------- derived

    /// <summary>
    /// Drives the renderer dropdown. Bound as an index rather than as the enum
    /// itself so selection can never depend on when ItemsSource is applied.
    /// </summary>
    public int FlavorIndex
    {
        get => (int)Flavor;
        set
        {
            if (Enum.IsDefined((MarkdownFlavor)value)) Flavor = (MarkdownFlavor)value;
        }
    }

    public string FormattingHelpTitle => $"{Flavor.DisplayName()} Formatting Help";

    public string CaretPositionText => $"Ln {CaretLine}, Col {CaretColumn}";

    public string WordCountText => $"{WordCount:N0} words";

    public string DocumentName => FilePath is null ? "Untitled.md" : Path.GetFileName(FilePath);

    public string WindowTitle => $"{DocumentName}{(IsModified ? " *" : string.Empty)} — Markdowner";

    public string FlavorSummary => Flavor.Description();

    public bool IsSourceMode
    {
        get => ViewMode == ViewMode.Source;
        set { if (value) ViewMode = ViewMode.Source; }
    }

    public bool IsSplitMode
    {
        get => ViewMode == ViewMode.Split;
        set { if (value) ViewMode = ViewMode.Split; }
    }

    public bool IsPreviewMode
    {
        get => ViewMode == ViewMode.Preview;
        set { if (value) ViewMode = ViewMode.Preview; }
    }

    /// <summary>Discord caps a message at 2000 characters, so show progress toward it.</summary>
    public string CharacterCountText => Flavor.CharacterLimit() is { } limit
        ? $"{CharacterCount:N0} / {limit:N0} chars"
        : $"{CharacterCount:N0} chars";

    public bool IsOverCharacterLimit =>
        Flavor.CharacterLimit() is { } limit && CharacterCount > limit;

    // ------------------------------------------------------------- events

    /// <summary>Raised when an insert-bar button is pressed; the view applies it to the editor.</summary>
    public event Action<Snippet>? SnippetRequested;

    /// <summary>Raised when the document or flavor changed and the preview needs rebuilding.</summary>
    public event Action? PreviewInvalidated;

    // ----------------------------------------------------------- commands

    [RelayCommand]
    private void ApplySnippet(Snippet? snippet)
    {
        if (snippet is not null) SnippetRequested?.Invoke(snippet);
    }

    // ------------------------------------------------------------ plumbing

    partial void OnFlavorChanged(MarkdownFlavor value)
    {
        // Only the interpretation changes — the document is never rewritten, so
        // the renderer can be swapped back and forth freely while editing.
        Categories = SnippetLibrary.For(value);
        SelectedCategory = Categories.FirstOrDefault();

        StatusMessage = $"Renderer: {value.DisplayName()}";
        PreviewInvalidated?.Invoke();
    }

    private void OnDocumentTextChanged()
    {
        IsModified = true;
        UpdateCounts();
        PreviewInvalidated?.Invoke();
    }

    public void UpdateCounts()
    {
        var text = Document.Text;
        CharacterCount = text.Length;
        WordCount = WordRe.Count(text);
    }

    public void MarkSaved(string path)
    {
        FilePath = path;
        IsModified = false;
        StatusMessage = $"Saved {Path.GetFileName(path)}";
    }

    public void LoadDocument(string path, string text)
    {
        Document.Text = text;
        FilePath = path;
        IsModified = false;
        StatusMessage = $"Opened {Path.GetFileName(path)}";
        UpdateCounts();
    }

    public void NewDocument()
    {
        Document.Text = string.Empty;
        FilePath = null;
        IsModified = false;
        StatusMessage = "New document";
        UpdateCounts();
    }
}
