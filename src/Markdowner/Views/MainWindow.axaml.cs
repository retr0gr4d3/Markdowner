using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit;
using CommunityToolkit.Mvvm.Input;
using Markdowner.Editing;
using Markdowner.Models;
using Markdowner.Rendering;
using Markdowner.ViewModels;

namespace Markdowner.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType MarkdownFileType = new("Markdown")
    {
        Patterns = ["*.md", "*.markdown", "*.mdown", "*.mkd", "*.txt"],
    };

    private readonly GitHubRenderer _github = new();
    private readonly DiscordRenderer _discord = new();
    private readonly DispatcherTimer _previewDebounce;

    private MainWindowViewModel? _model;
    private FormattingHelpWindow? _help;

    public MainWindow()
    {
        InitializeComponent();

        // Re-rendering on every keystroke is wasteful on large documents;
        // a short idle delay keeps typing smooth while still feeling live.
        _previewDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        _previewDebounce.Tick += (_, _) =>
        {
            _previewDebounce.Stop();
            RenderPreview();
        };

        // The two panes scroll independently — no offset mirroring between them.
        Editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_model is not null)
        {
            _model.SnippetRequested -= OnSnippetRequested;
            _model.PreviewInvalidated -= OnPreviewInvalidated;
            _model.PropertyChanged -= OnModelPropertyChanged;
        }

        _model = DataContext as MainWindowViewModel;
        if (_model is null) return;

        _model.SnippetRequested += OnSnippetRequested;
        _model.PreviewInvalidated += OnPreviewInvalidated;
        _model.PropertyChanged += OnModelPropertyChanged;

        ApplyFlavor();
        ApplyViewMode();
        RebuildShortcuts();
        RenderPreview();
    }

    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.Flavor):
                ApplyFlavor();
                RebuildShortcuts();
                break;
            case nameof(MainWindowViewModel.ViewMode):
                ApplyViewMode();
                break;
        }
    }

    // --------------------------------------------------------------- preview

    private void OnPreviewInvalidated()
    {
        _previewDebounce.Stop();
        _previewDebounce.Start();
    }

    private void RenderPreview()
    {
        if (_model is null) return;

        IMarkdownRenderer renderer = _model.Flavor == MarkdownFlavor.Discord ? _discord : _github;
        var theme = renderer.Theme;

        PreviewScroll.Background = theme.Background;
        PreviewHost.Background = theme.Background;
        PreviewHost.Padding = theme.Padding;

        try
        {
            PreviewHost.Child = renderer.Render(_model.Document.Text);
        }
        catch (Exception ex)
        {
            // A rendering failure should never take the editor down with it.
            PreviewHost.Child = new SelectableTextBlock
            {
                Text = $"The preview could not be rendered.\n\n{ex.Message}",
                Foreground = Brushes.IndianRed,
                TextWrapping = TextWrapping.Wrap,
            };
        }
    }

    private void ApplyFlavor()
    {
        if (_model is null) return;
        Editor.SyntaxHighlighting = MarkdownHighlighting.For(_model.Flavor);
        RenderPreview();
    }

    private void ApplyViewMode()
    {
        if (_model is null) return;

        var showEditor = _model.ViewMode != ViewMode.Preview;
        var showPreview = _model.ViewMode != ViewMode.Source;
        var split = _model.ViewMode == ViewMode.Split;

        EditorPane.IsVisible = showEditor;
        PreviewPane.IsVisible = showPreview;
        Splitter.IsVisible = split;

        SplitGrid.ColumnDefinitions[0].Width = showEditor ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        SplitGrid.ColumnDefinitions[1].Width = split ? new GridLength(4) : new GridLength(0);
        SplitGrid.ColumnDefinitions[2].Width = showPreview ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    // -------------------------------------------------------------- snippets

    private void OnSnippetRequested(Snippet snippet) => SnippetApplier.Apply(Editor, snippet);

    /// <summary>Registers the accelerators declared by the current flavor's snippets.</summary>
    private void RebuildShortcuts()
    {
        KeyBindings.Clear();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Bind("Ctrl+N", OnNewCore);
        Bind("Ctrl+O", () => _ = OpenAsync());
        Bind("Ctrl+S", () => _ = SaveAsync(forcePrompt: false));
        Bind("Ctrl+Shift+S", () => _ = SaveAsync(forcePrompt: true));
        Bind("F1", OpenFormattingHelp);

        if (_model is null) return;

        var snippets = _model.Categories
            .SelectMany(category => category.Groups)
            .SelectMany(group => group.Snippets);

        foreach (var snippet in snippets)
        {
            if (snippet.Gesture is null) continue;

            var captured = snippet;
            Bind(snippet.Gesture, () => SnippetApplier.Apply(Editor, captured));
        }

        void Bind(string gesture, Action action)
        {
            if (!seen.Add(gesture)) return;

            try
            {
                KeyBindings.Add(new KeyBinding
                {
                    Gesture = KeyGesture.Parse(gesture),
                    Command = new RelayCommand(action),
                });
            }
            catch (FormatException)
            {
                // An unparseable gesture just means no accelerator for that button.
            }
        }
    }

    // ---------------------------------------------------------------- status

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (_model is null) return;
        _model.CaretLine = Editor.TextArea.Caret.Line;
        _model.CaretColumn = Editor.TextArea.Caret.Column;
    }

    // ----------------------------------------------------------------- help

    /// <summary>
    /// Opens the reference for whichever renderer is active right now. A single
    /// window is reused and retargeted so repeated presses don't pile up.
    /// </summary>
    private void OnFormattingHelp(object? sender, RoutedEventArgs e) => OpenFormattingHelp();

    private void OpenFormattingHelp()
    {
        if (_model is null) return;

        if (_help is null)
        {
            _help = new FormattingHelpWindow();
            _help.Closed += (_, _) => _help = null;
            _help.ShowFlavor(_model.Flavor);
            _help.Show(this);
            return;
        }

        _help.ShowFlavor(_model.Flavor);
        _help.Activate();
    }

    // ------------------------------------------------------------- file menu

    private void OnNew(object? sender, RoutedEventArgs e) => OnNewCore();

    private void OnNewCore()
    {
        _model?.NewDocument();
        Editor.Focus();
    }

    private void OnOpen(object? sender, RoutedEventArgs e) => _ = OpenAsync();

    private async Task OpenAsync()
    {
        if (_model is null) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Markdown file",
            AllowMultiple = false,
            FileTypeFilter = [MarkdownFileType, FilePickerFileTypes.All],
        });

        if (files.Count == 0) return;

        try
        {
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            var text = await reader.ReadToEndAsync();

            _model.LoadDocument(files[0].TryGetLocalPath() ?? files[0].Name, text);
        }
        catch (Exception ex)
        {
            _model.StatusMessage = $"Could not open file: {ex.Message}";
        }
    }

    private void OnSave(object? sender, RoutedEventArgs e) => _ = SaveAsync(forcePrompt: false);

    private void OnSaveAs(object? sender, RoutedEventArgs e) => _ = SaveAsync(forcePrompt: true);

    private async Task SaveAsync(bool forcePrompt)
    {
        if (_model is null) return;

        var path = _model.FilePath;

        if (forcePrompt || path is null)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Markdown file",
                SuggestedFileName = _model.DocumentName,
                DefaultExtension = "md",
                FileTypeChoices = [MarkdownFileType],
            });

            if (file is null) return;
            path = file.TryGetLocalPath() ?? file.Path.LocalPath;
        }

        try
        {
            await File.WriteAllTextAsync(path, _model.Document.Text);
            _model.MarkSaved(path);
        }
        catch (Exception ex)
        {
            _model.StatusMessage = $"Could not save file: {ex.Message}";
        }
    }

    private void OnExit(object? sender, RoutedEventArgs e) => Close();

    // ------------------------------------------------------------- edit menu

    private void OnUndo(object? sender, RoutedEventArgs e) => Editor.Undo();
    private void OnRedo(object? sender, RoutedEventArgs e) => Editor.Redo();
    private void OnCut(object? sender, RoutedEventArgs e) => Editor.Cut();
    private void OnCopy(object? sender, RoutedEventArgs e) => Editor.Copy();
    private void OnPaste(object? sender, RoutedEventArgs e) => Editor.Paste();
    private void OnSelectAll(object? sender, RoutedEventArgs e) => Editor.SelectAll();

    // ------------------------------------------------------- view / renderer

    private void OnViewSource(object? sender, RoutedEventArgs e) => SetMode(ViewMode.Source);
    private void OnViewSplit(object? sender, RoutedEventArgs e) => SetMode(ViewMode.Split);
    private void OnViewPreview(object? sender, RoutedEventArgs e) => SetMode(ViewMode.Preview);

    private void SetMode(ViewMode mode)
    {
        if (_model is not null) _model.ViewMode = mode;
    }

    private void OnRendererGitHub(object? sender, RoutedEventArgs e) => SetFlavor(MarkdownFlavor.GitHub);
    private void OnRendererDiscord(object? sender, RoutedEventArgs e) => SetFlavor(MarkdownFlavor.Discord);

    private void SetFlavor(MarkdownFlavor flavor)
    {
        if (_model is not null) _model.Flavor = flavor;
    }

    private void OnHelpGitHub(object? sender, RoutedEventArgs e) =>
        LinkLauncher.Open(this, "https://docs.github.com/get-started/writing-on-github");

    private void OnHelpDiscord(object? sender, RoutedEventArgs e) =>
        LinkLauncher.Open(this, "https://support.discord.com/hc/en-us/articles/210298617");
}
