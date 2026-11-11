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

public sealed partial class FileViewerWindow : Window
{
    private readonly IViewerFiles files;
    private readonly FeedbackController feedback;
    private readonly string[] paths;
    private readonly Func<string, Task> edit;
    private readonly Func<string, Task> open;
    private readonly TextBlock information = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, IsVisible = false, Margin = new Thickness(10, 4) };
    private readonly TextBlock error = new() { Foreground = Brushes.Salmon, TextWrapping = TextWrapping.Wrap, IsVisible = false, Margin = new Thickness(10, 4) };
    private readonly TextBlock searchStatus = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock readingStatus = new() { Name = "ViewerReadingStatus", FontSize = 11, Margin = new Thickness(8, 2), TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock feedbackStatus = new() { Name = "ViewerFeedbackStatus", FontSize = 11, Margin = new Thickness(8, 2), TextTrimming = TextTrimming.CharacterEllipsis, IsVisible = false };
    private readonly Border statusBar = new() { Name = "ViewerStatusBar", BorderThickness = new Thickness(0, 1, 0, 0) };
    private readonly Image image = new() { Stretch = Stretch.Uniform };
    private readonly Grid viewerBody = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
    private readonly Grid searchPanel = new() { ColumnDefinitions = new ColumnDefinitions("260,*"), IsVisible = false, Margin = new Thickness(8, 4) };
    private readonly Dictionary<FileViewMode, MenuItem> modeItems = [];
    private readonly Dictionary<int, MenuItem> encodingItems = [];
    private MenuItem wrapItem = null!;
    private MenuItem informationItem = null!;
    private MenuItem previousPage = null!;
    private MenuItem nextPage = null!;
    private MenuItem previousFile = null!;
    private MenuItem nextFile = null!;
    private bool wrapLines = true;
    private EncodingChoice selectedEncoding = EncodingChoice.All[0];
    private FileSlice? slice;
    private ViewerEncoding detected = new(Encoding.UTF8, "UTF-8");
    private ViewerImage? decodedImage;
    private CancellationTokenSource? loading;
    private bool editKeyHeld;
    private int index;
    private bool updating;
    private bool closed;
    private bool automaticMode = true;
    private bool imageSignature;
    private string? imageUnavailable;
    public FileViewMode Mode { get; private set; }
    public bool IsLoading { get; private set; }
    public bool IsSearching { get; private set; }
    public bool IsSearchVisible => searchPanel.IsVisible;
    public bool IsInformationVisible => information.IsVisible;
    public string SearchStatus => searchStatus.Text ?? "";
    public string CurrentPath => paths[index];
    public string? Error => error.IsVisible ? error.Text : null;
    public long ByteOffset => slice?.Offset ?? 0;
    public long FileLength => slice?.Length ?? 0;
    public string Information => information.Text ?? "";
    public bool HasImage => decodedImage != null;
    public string ReadingStatus => readingStatus.Text ?? "";
    public TextBlock Progress { get; } = new() { Name = "ViewerProgress", MinWidth = 68, FontSize = 12, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };
    public Grid TitleBar { get; } = new() { Name = "ViewerTitleBar", Height = 32, ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), Background = new SolidColorBrush(Color.Parse("#101722")) };
    private readonly TextBlock titlePath = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(12, 0, 4, 0) };
    public long VisibleEnd { get; private set; }
    public double ViewedPercent => FileLength == 0 ? 100 : 100d * VisibleEnd / FileLength;
    public ScrollBar FileScroll { get; } = new() { Name = "ViewerFileScroll", Orientation = Orientation.Vertical, Width = 16 };
    public Menu Commands { get; } = new() { Name = "ViewerMenu", MinHeight = 24 };
    public TextBox TextContent { get; } = new()
    {
        Name = "ViewerContent",
        IsReadOnly = true,
        AcceptsReturn = true,
        IsUndoEnabled = false,
        FontFamily = new FontFamily("Consolas"),
        FontSize = 14,
        TextWrapping = TextWrapping.Wrap,
        Padding = new Thickness(10, 6),
        BorderThickness = new Thickness(0),
        Background = Brushes.Transparent
    };
    public TextBox Search { get; } = new() { Name = "ViewerSearch", PlaceholderText = "Find", MaxLength = 1024, Margin = new Thickness(0, 0, 12, 0) };

    public FileViewerWindow(string[] paths, int index, IViewerFiles files, FeedbackController feedback,
        Func<string, Task> edit, Func<string, Task> open, string themeId = "dark")
    {
        this.paths = paths; this.index = index; this.files = files; this.feedback = feedback; this.edit = edit; this.open = open;
        Title = CurrentPath + " — Xcmd Viewer";
        Width = 1120; Height = 760; MinWidth = 640; MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowHeader.Configure(this);
        Background = new SolidColorBrush(Color.Parse("#101722"));
        BuildMenu();
        ConfigureReaderAppearance();
        Content = BuildContent();
        ApplyTheme(themeId);
        UpdateControls();
        AutomationProperties.SetAutomationId(this, "viewer.window");
        AutomationProperties.SetAutomationId(TextContent, "viewer.content");
        AutomationProperties.SetAutomationId(information, "viewer.status");
        AutomationProperties.SetAutomationId(error, "viewer.error");
        AutomationProperties.SetAutomationId(Search, "viewer.search");
        AutomationProperties.SetAutomationId(searchStatus, "viewer.search-status");
        AutomationProperties.SetAutomationId(readingStatus, "viewer.reading-status");
        AutomationProperties.SetAutomationId(Progress, "viewer.progress");
        AutomationProperties.SetAutomationId(FileScroll, "viewer.file-scroll");
        AddHandler(KeyDownEvent, OnViewerKey, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) => { if (e.Key == Key.F4) editKeyHeld = false; if (e.Key is Key.LeftAlt or Key.RightAlt) { altSearchArmed = false; e.Handled = true; } }, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => { editKeyHeld = false; altSearchArmed = false; suppressSearchText = false; };
        AddHandler(TextInputEvent, OnSearchText, RoutingStrategies.Tunnel);
        AddHandler(PointerWheelChangedEvent, OnViewerWheel, RoutingStrategies.Tunnel);
        TextContent.SizeChanged += (_, _) => UpdateReadingPosition();
        FileScroll.PropertyChanged += (_, e) => { if (e.Property == ScrollBar.ValueProperty && !updatingScroll && slice != null) Run(token => SeekByteAsync((long)FileScroll.Value, token)); };
        Search.TextChanged += (_, _) => SearchChanged();
        Closed += (_, _) => { closed = true; loading?.Cancel(); searching?.Cancel(); image.Source = null; decodedImage?.Bitmap.Dispose(); };
    }

    private Grid BuildContent()
    {
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto,Auto") };
        layout.Children.Add(BuildTitleBar());
        ScrollViewer.SetHorizontalScrollBarVisibility(TextContent, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(TextContent, ScrollBarVisibility.Hidden);
        viewerBody.Children.Add(TextContent); viewerBody.Children.Add(image);
        Grid.SetColumn(FileScroll, 1); viewerBody.Children.Add(FileScroll);
        Grid.SetRow(viewerBody, 1); layout.Children.Add(viewerBody);
        searchPanel.Children.Add(Search);
        Grid.SetColumn(searchStatus, 1); searchPanel.Children.Add(searchStatus);
        Grid.SetRow(searchPanel, 2); layout.Children.Add(searchPanel);
        Grid.SetRow(error, 3); layout.Children.Add(error);
        Grid.SetRow(information, 4); layout.Children.Add(information);
        statusBar.Child = new Grid { Children = { readingStatus, feedbackStatus } };
        Grid.SetRow(statusBar, 5); layout.Children.Add(statusBar);
        return layout;
    }

    private async void RunExternal(Func<Task> action)
    {
        try { await action(); }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException)
        { ShowError(failure.Message); }
    }

    private void UpdateWrapping()
    {
        var wrap = Mode == FileViewMode.Text && wrapLines;
        TextContent.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        ScrollViewer.SetHorizontalScrollBarVisibility(TextContent, wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);
    }

    private void ShowError(string message) { error.Text = message; error.IsVisible = true; }

    internal void SetFeedbackHint(string text)
    {
        feedbackStatus.Text = text; feedbackStatus.IsVisible = text.Length > 0;
        readingStatus.IsVisible = text.Length == 0;
    }

    private sealed record EncodingChoice(string Label, int CodePage)
    {
        public static EncodingChoice[] All { get; } = [new("Auto", 0), new("UTF-8", 65001), new("UTF-16 LE", 1200), new("UTF-16 BE", 1201),
            new("UTF-32 LE", 12000), new("UTF-32 BE", 12001), new("Windows-1251", 1251), new("Windows-1252", 1252), new("DOS 866", 866), new("KOI8-R", 20866)];
    }
}
