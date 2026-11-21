// @implements ChooseViewMode, ReadLargeFiles, FindViewerText, HighlightSource
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Xcmd.Core;
using Xcmd.Desktop.Features.Viewer.Syntax;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop.Features.Viewer;

internal sealed partial class StructuredJsonReader
{
    internal const string ShortcutHelp = "Alt+arrow keys: move between JSON elements.\nSpace: fold or unfold the current container.\n" +
        "Ctrl+Alt+Up/Down: visit the same field in another record.\nCtrl+Alt+Left/Right: fold or unfold matching array fields.\n" +
        "Ctrl+Space: copy the current container's layout to matching array fields.";

    private readonly SyntaxTextBox textContent;
    private readonly IViewerFiles files;
    private readonly TextBlock readingStatus;
    private readonly TextBlock searchStatus;
    private readonly StructuredJsonHighlight highlight = new StructuredJsonHighlight().WithFeedbackId("FileViewer.structuredHighlight");
    private readonly Func<bool> inputBlocked;
    private readonly Action<Func<Task>> runExternal;
    private StructuredJsonSource? structured;
    private StructuredJsonPage? structuredPage;
    private StructuredJsonFolds structuredFolds = StructuredJsonFolds.Initial;
    private StructuredCursor structuredCursor;
    private LocalCursor structuredProjection;
    private ScrollViewer? textScroll;
    private TextPresenter? presenter;
    private bool placingStructured;
    private bool structuredCuePending;
    private string? structuredNotice;
    private string accent = ReaderThemes.Default.Accent;
    private string encodingName = "";
    private CancellationTokenSource? structuredCommand;

    public bool IsActive => structured != null;
    public long? Length => structured?.Bytes.Length;
    public Control Highlight => highlight;
    public event Action? ReadingPositionChanged;
    public event Action? IndexReady;
    public event Action<string>? IndexFailed;
    private readonly record struct StructuredCursor(int Caret, int Start, int End, int Element)
    {
        public static StructuredCursor At(int caret, int element) => new(caret, caret, caret, element);
    }
    private readonly record struct LocalCursor(int Caret, int Start, int End);

    public StructuredJsonReader(IViewerFiles files, SyntaxTextBox textContent, TextBlock readingStatus, TextBlock searchStatus,
        Func<bool> inputBlocked, Action<Func<Task>> runExternal)
    {
        this.files = files;
        this.textContent = textContent;
        this.readingStatus = readingStatus;
        this.searchStatus = searchStatus;
        this.inputBlocked = inputBlocked;
        this.runExternal = runExternal;
        textContent.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, ClickStructuredFold, RoutingStrategies.Tunnel);
        textContent.PropertyChanged += (_, change) =>
        {
            if (!IsActive || placingStructured) return;
            if (change.Property != TextBox.CaretIndexProperty && change.Property != TextBox.SelectionStartProperty &&
                change.Property != TextBox.SelectionEndProperty) return;
            CancelStructuredCommand();
            if (structuredCuePending) return;
            structuredCuePending = true;
            Dispatcher.UIThread.Post(() =>
            {
                structuredCuePending = false;
                if (!IsActive) return;
                CaptureStructuredCursor();
                UpdateStructuredCue();
            }, DispatcherPriority.Input);
        };
    }

    public void Reset()
    {
        CancelStructuredCommand();
        structured = null;
        structuredPage = null;
        structuredFolds = StructuredJsonFolds.Initial;
        structuredMatch = null;
        structuredNotice = null;
        highlight.IsVisible = false;
    }

    public void AttachViewport(ScrollViewer? scroll, TextPresenter? textPresenter)
    {
        textScroll = scroll;
        presenter = textPresenter;
    }

    public async Task OpenAsync(string path, Encoding encoding, long caret, long start, long end, CancellationToken token)
    {
        StructuredJsonSource source;
        try { source = await Task.Run(() => StructuredJsonSource.ReadAsync(files, path, encoding, token), token); }
        catch (Exception failure) when (failure is FormatException or ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            token.ThrowIfCancellationRequested();
            IndexFailed?.Invoke("Could not open Structured view. " + failure.Message);
            return;
        }
        token.ThrowIfCancellationRequested();
        encodingName = encoding.WebName;
        IndexReady?.Invoke();
        Enter(source, caret, start, end);
    }

    public async Task<bool> HasChangedAsync(string path, CancellationToken token)
    {
        var version = await Task.Run(() => files.VersionAsync(path, token), token);
        token.ThrowIfCancellationRequested();
        return version != structured?.Version;
    }

    private void Enter(StructuredJsonSource source, long caret, long start, long end)
    {
        structured = source;
        var position = source.Bytes.CharacterAt(caret);
        var selectionStart = source.Bytes.CharacterAt(start);
        var selectionEnd = source.Bytes.CharacterAt(end);
        var element = source.Tree.ElementAt(position);
        structuredFolds = StructuredJsonFolds.Initial.Reveal(source.Tree, element)
            .Reveal(source.Tree, source.Tree.ElementAt(selectionStart)).Reveal(source.Tree, source.Tree.ElementAt(selectionEnd));
        PlaceStructured(new(position, selectionStart, selectionEnd, element), position, 0);
    }

    public void CapturePosition(out long caret, out long start, out long end)
    {
        var source = structured!;
        var cursor = CaptureStructuredCursor();
        caret = source.Bytes.At(cursor.Caret);
        start = source.Bytes.At(cursor.Start);
        end = source.Bytes.At(cursor.End);
    }

    public void ApplyAccent(string color)
    {
        accent = color;
        UpdateStructuredCue();
    }

    private void UpdateStructuredCue()
    {
        if (structured is not { } source || structuredPage is not { } page) return;
        var id = structuredCursor.Element;
        var element = source.Tree[id];
        var siblings = element.Parent < 0 ? "Root" :
            $"{(element.Member == null ? "Item" : "Field")} {element.Index + 1:N0} of {source.Tree[element.Parent].Children.Count:N0}";
        var groupText = DescribePeerGroups(source.Tree, id);
        var groupStatus = structuredNotice == null ? groupText : $"{structuredNotice} · {groupText}";
        readingStatus.Text = $"Structured · {source.Tree.Path(id)} · {siblings} · {encodingName}\n{groupStatus}";
        ToolTip.SetTip(readingStatus, readingStatus.Text + "\n\n" + ShortcutHelp);

        var line = page.LineAt(textContent.CaretIndex);
        var inPage = structuredCursor.Caret >= page.Start && structuredCursor.Caret <= page.End;
        var start = line.Row.Element == id ? Math.Max(line.Start, page.ToDisplay(line.SourceStart)) : page.ToDisplay(element.Start);
        var end = line.Row.Element == id ? line.End : Math.Min(line.End, page.ToDisplay(element.End, trailing: true));
        highlight.Update(inPage ? presenter : null, start, end - start, Brush.Parse(accent));
    }

    private static string DescribePeerGroups(StructuredJsonTree tree, int id)
    {
        var navigation = tree.PeerGroup(id, includeObjectRecords: true);
        if (navigation == null) return "Group commands unavailable";
        var path = tree.Path(id, navigation.Item);
        if (tree[navigation.Container].Kind == JsonElementKind.Array) return $"Group: {path}";
        var layout = tree.PeerGroup(id);
        var description = $"Navigation group: {path}";
        return layout == null ? description : $"{description} · Layout group: {tree.Path(id, layout.Item)}";
    }
}
