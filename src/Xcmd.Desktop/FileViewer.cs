// @implements ReusableFileViewer, OpenViewers, QuickViewFile, ChooseViewMode, ReadLargeFiles, FindViewerText, ReadWorkingFileDiff, ReportControl
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed partial class FileViewer : UserControl
{
    private readonly IViewerFiles files;
    private readonly FeedbackController feedback;
    private readonly string[] paths;
    private readonly Func<string, Task> edit;
    private readonly Func<string, Task> open;
    private readonly Action close;
    private readonly ThemeVariantScope themeScope = new ThemeVariantScope().WithFeedbackId("FileViewer.themeScope");
    private string? previewPath;
    private bool IsEmbedded => paths.Length == 0;
    private bool HasFile => !IsEmbedded || previewPath != null;
    internal event Action<string>? CaptionChanged;
    internal event Action? FileListFocusRequested;
    private readonly TextBlock information = new TextBlock() { FontSize = UiPalette.MetaSize, TextWrapping = TextWrapping.Wrap, IsVisible = false, Margin = new Thickness(10, 4) }.WithFeedbackId("FileViewerWindow.information");
    private readonly TextBlock error = new TextBlock() { Foreground = UiPalette.Error, TextWrapping = TextWrapping.Wrap, IsVisible = false, Margin = new Thickness(10, 4) }.WithFeedbackId("FileViewerWindow.error");
    private readonly TextBlock searchStatus = new TextBlock() { FontSize = UiPalette.MetaSize, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }.WithFeedbackId("FileViewerWindow.searchStatus");
    private readonly TextBlock readingStatus = new TextBlock() { Name = "ViewerReadingStatus", FontSize = UiPalette.MetaSize, Margin = new Thickness(8, 2), TextTrimming = TextTrimming.CharacterEllipsis }.WithFeedbackId("FileViewerWindow.readingStatus");
    private readonly TextBlock feedbackStatus = new TextBlock() { Name = "ViewerFeedbackStatus", FontSize = UiPalette.MetaSize, Margin = new Thickness(8, 2), TextTrimming = TextTrimming.CharacterEllipsis, IsVisible = false }.WithFeedbackId("FileViewerWindow.feedbackStatus");
    private readonly Border statusBar = new Border() { Name = "ViewerStatusBar", BorderThickness = new Thickness(0, 1, 0, 0) }.WithFeedbackId("FileViewerWindow.statusBar");
    private readonly Image image = new Image() { Stretch = Stretch.Uniform }.WithFeedbackId("FileViewerWindow.image");
    private readonly Grid viewerBody = new Grid() { ColumnDefinitions = new ColumnDefinitions("*,Auto") }.WithFeedbackId("FileViewerWindow.viewerBody");
    private readonly Grid searchPanel = new Grid() { ColumnDefinitions = new ColumnDefinitions("Auto,260,*"), ColumnSpacing = 8, IsVisible = false, Margin = new Thickness(8, 4) }.WithFeedbackId("FileViewerWindow.searchPanel");
    private readonly Dictionary<FileViewMode, MenuItem> modeItems = [];
    private readonly Dictionary<int, MenuItem> encodingItems = [];
    private MenuItem wrapItem = null!;
    private MenuItem informationItem = null!;
    private MenuItem previousPage = null!;
    private MenuItem nextPage = null!;
    private MenuItem? previousFile;
    private MenuItem? nextFile;
    private bool wrapLines = true;
    private EncodingChoice selectedEncoding = EncodingChoice.All[0];
    private FileSlice? slice;
    private ViewerEncoding detected = new(Encoding.UTF8, "UTF-8");
    private ViewerImage? decodedImage;
    private CancellationTokenSource? loading;
    private int index;
    private bool updating;
    private bool closed;
    private IDisposable? feedbackNotice;
    private bool automaticMode = true;
    private bool imageSignature;
    private bool binaryFile;
    private string? imageUnavailable;
    public FileViewMode Mode { get; private set; }
    public bool IsLoading { get; private set; }
    public bool IsArchiveReport { get; private set; }
    public bool IsSearching { get; private set; }
    public bool IsSearchVisible => searchPanel.IsVisible;
    public bool IsInformationVisible => information.IsVisible;
    public string SearchStatus => searchStatus.Text ?? "";
    public string CurrentPath => IsEmbedded ? previewPath ?? "" : paths[index];
    public string? Error => error.IsVisible ? error.Text : null;
    public long ByteOffset => slice?.Offset ?? 0;
    public long FileLength => slice?.Length ?? 0;
    public string Information => information.Text ?? "";
    public bool HasImage => decodedImage != null;
    public string ReadingStatus => readingStatus.Text ?? "";
    public TextBlock Progress { get; } = new TextBlock() { Name = "ViewerProgress", MinWidth = 68, FontSize = UiPalette.MetaSize, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) }.WithFeedbackId("FileViewerWindow.Progress");
    public Grid TitleBar { get; } = new Grid() { Name = "ViewerTitleBar", Height = UiPalette.TitleHeight, ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), Background = UiPalette.Canvas }.WithFeedbackId("FileViewerWindow.TitleBar");
    private readonly TextBlock titlePath = new TextBlock() { FontSize = UiPalette.MetaSize, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(12, 0, 4, 0) }.WithFeedbackId("FileViewerWindow.titlePath");
    public long VisibleEnd { get; private set; }
    public double ViewedPercent => FileLength == 0 ? 100 : 100d * VisibleEnd / FileLength;
    public ScrollBar FileScroll { get; } = new ScrollBar() { Name = "ViewerFileScroll", Orientation = Orientation.Vertical, Width = 16 }.WithFeedbackId("FileViewerWindow.FileScroll");
    public Menu Commands { get; } = new Menu() { Name = "ViewerMenu", MinHeight = 24 }.WithFeedbackId("FileViewerWindow.Commands");
    public SyntaxTextBox TextContent { get; } = new SyntaxTextBox()
    {
        Name = "ViewerContent",
        IsReadOnly = true,
        AcceptsReturn = true,
        IsUndoEnabled = false,
        FontFamily = new FontFamily("Consolas"),
        FontSize = UiPalette.BodySize,
        TextWrapping = TextWrapping.Wrap,
        Padding = new Thickness(10, 6),
        BorderThickness = new Thickness(0),
        Background = Brushes.Transparent
    }.WithFeedbackId("FileViewerWindow.TextContent");
    public TextBox Search { get; } = new TextBox() { Name = "ViewerSearch", PlaceholderText = "Find", MaxLength = 1024, Margin = new Thickness(0, 0, 12, 0) }.WithFeedbackId("FileViewerWindow.Search");

    internal FileViewer(string[] paths, int index, IViewerFiles files, FeedbackController feedback,
        Func<string, Task> edit, Func<string, Task> open, Action close, ViewerDocument? document = null)
    {
        this.paths = paths; this.index = index; this.files = files; this.feedback = feedback; this.edit = edit; this.open = open;
        this.close = close;
        this.document = document;
        Focusable = true;
        Background = UiPalette.Canvas;
        BuildMenu();
        ConfigureFollowing();
        PreserveSelectionInMenus();
        ConfigureReaderAppearance();
        themeScope.Child = BuildContent();
        Content = themeScope;
        UpdateControls();
        AutomationProperties.SetAutomationId(TextContent, "viewer.content");
        AutomationProperties.SetAutomationId(information, "viewer.status");
        AutomationProperties.SetAutomationId(error, "viewer.error");
        AutomationProperties.SetAutomationId(Search, "viewer.search");
        AutomationProperties.SetAutomationId(searchStatus, "viewer.search-status");
        AutomationProperties.SetAutomationId(readingStatus, "viewer.reading-status");
        AutomationProperties.SetAutomationId(Progress, "viewer.progress");
        AutomationProperties.SetAutomationId(FileScroll, "viewer.file-scroll");
        if (IsEmbedded)
        {
            AddHandler(KeyDownEvent, HandleDiffKey, RoutingStrategies.Tunnel);
            AddHandler(KeyUpEvent, ReleaseDiffKey, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
        AddHandler(KeyDownEvent, OnViewerKey, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnViewerKeyUp, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnSearchText, RoutingStrategies.Tunnel);
        AddHandler(PointerWheelChangedEvent, OnViewerWheel, RoutingStrategies.Tunnel);
        image.PointerPressed += (_, _) => FocusReader();
        TextContent.SizeChanged += (_, _) => UpdateReadingPosition();
        viewerBody.PointerMoved += UpdateDiffHint;
        viewerBody.PointerExited += (_, _) => ClearDiffHint();
        FileScroll.PropertyChanged += (_, e) => { if (e.Property == ScrollBar.ValueProperty && !updatingScroll && slice != null) SeekFromScrollBar((long)FileScroll.Value); };
        Search.TextChanging += (_, _) => SearchChanged();
    }

    private Grid BuildContent()
    {
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto,Auto") }.WithFeedbackId("FileViewerWindow.layout");
        layout.Children.Add(BuildTitleBar());
        ScrollViewer.SetHorizontalScrollBarVisibility(TextContent, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(TextContent, ScrollBarVisibility.Hidden);
        diffOverlay.IsVisible = false;
        viewerBody.Children.Add(diffOverlay); viewerBody.Children.Add(TextContent); viewerBody.Children.Add(image);
        Grid.SetColumn(FileScroll, 1); viewerBody.Children.Add(FileScroll);
        Grid.SetRow(viewerBody, 1); layout.Children.Add(viewerBody);
        if (IsEmbedded)
        {
            searchPanel.ColumnDefinitions = new ColumnDefinitions("Auto,*");
            searchPanel.RowDefinitions = new RowDefinitions("Auto,Auto");
        }
        searchPanel.Children.Add(new TextBlock { Text = "Find", VerticalAlignment = VerticalAlignment.Center }.WithFeedbackId("FileViewerWindow.BuildContent"));
        Grid.SetColumn(Search, 1); searchPanel.Children.Add(Search);
        if (IsEmbedded) { Grid.SetRow(searchStatus, 1); Grid.SetColumnSpan(searchStatus, 2); }
        else Grid.SetColumn(searchStatus, 2);
        searchPanel.Children.Add(searchStatus);
        Grid.SetRow(searchPanel, 2); layout.Children.Add(searchPanel);
        Grid.SetRow(error, 3); layout.Children.Add(error);
        Grid.SetRow(information, 4); layout.Children.Add(information);
        statusBar.Child = BuildStatusBar();
        Grid.SetRow(statusBar, 5); layout.Children.Add(statusBar);
        return layout;
    }

    private async void RunExternal(Func<Task> action)
    {
        try { await action(); }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException)
        { ShowError(failure.Message); }
    }

    private void ShowError(string message) { error.Text = message; error.IsVisible = true; }

    internal void FocusReader()
    {
        if (!TextContent.IsVisible || !TextContent.Focus()) Focus();
    }

    internal void Stop()
    {
        closed = true;
        IsFollowing = false;
        followTimer.Stop();
        feedbackNotice?.Dispose();
        ClearDiffHint();
        loading?.Cancel();
        searching?.Cancel();
        ReleaseImage();
    }

    internal void SetFeedbackHint(string text)
    {
        feedbackNotice?.Dispose(); feedbackNotice = null;
        feedbackStatus.Text = text; feedbackStatus.IsVisible = text.Length > 0;
        readingStatus.IsVisible = text.Length == 0;
    }

    internal void ShowFeedbackSaved()
    {
        SetFeedbackHint("Comment saved.");
        feedbackNotice = Avalonia.Threading.DispatcherTimer.RunOnce(() =>
        {
            feedbackNotice = null;
            if (!closed && !feedback.IsActive) SetFeedbackHint("");
        }, TimeSpan.FromSeconds(3));
    }

    private sealed record EncodingChoice(string Label, int CodePage)
    {
        public static EncodingChoice[] All { get; } = [new("Auto", 0), new("UTF-8", 65001), new("UTF-16 LE", 1200), new("UTF-16 BE", 1201),
            new("UTF-32 LE", 12000), new("UTF-32 BE", 12001), new("Windows-1251", 1251), new("Windows-1252", 1252), new("DOS 866", 866), new("KOI8-R", 20866)];
    }
}
