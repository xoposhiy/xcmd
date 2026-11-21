// @implements ChooseViewMode, OpenViewers
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

internal sealed partial class StructuredJsonReader
{
    private readonly Queue<PeerCommand> peerCommands = new();
    private bool processingPeers;
    private sealed record PeerCommand(Func<StructuredJsonFolds, StructuredJsonTree, int, CancellationToken, JsonPeerResult> Run, bool Move);

    public bool HandleKey(KeyEventArgs e)
    {
        if (!IsActive || !textContent.IsKeyboardFocusWithin || textContent.ContextMenu?.IsOpen == true) return false;
        var modifiers = e.KeyModifiers;
        if (modifiers == KeyModifiers.Control)
        {
            if (e.Key is Key.C or Key.Insert) { Copy(); return true; }
            if (e.Key == Key.A) { SelectAll(); return true; }
        }
        if (e.Key == Key.Space && modifiers is KeyModifiers.None or KeyModifiers.Control)
        {
            if (modifiers == KeyModifiers.Control) StartStructuredPeerCommand(JsonPeerAction.ApplyLayout);
            else ToggleStructured(CaptureStructuredCursor().Element);
            return true;
        }
        if (e.Key is not (Key.Up or Key.Down or Key.Left or Key.Right)) return false;
        if (modifiers == KeyModifiers.Alt) MoveStructuredElement(e.Key);
        else if (modifiers == (KeyModifiers.Control | KeyModifiers.Alt)) MoveStructuredPeer(e.Key);
        else
        {
            if (modifiers is KeyModifiers.None or KeyModifiers.Shift) ContinueStructuredCaret(e.Key);
            return false;
        }
        return true;
    }

    private void MoveStructuredElement(Key key)
    {
        if (structured is not { } source) return;
        var cursor = CaptureStructuredCursor();
        var tree = source.Tree;
        var current = tree[cursor.Element];
        var target = key switch
        {
            Key.Up => tree.Sibling(cursor.Element, -1),
            Key.Down => tree.Sibling(cursor.Element, 1),
            Key.Left when current.Parent >= 0 => current.Parent,
            Key.Right when current.CanFold => current.Children[0],
            _ => cursor.Element
        };
        if (target == cursor.Element) return;
        CancelStructuredCommand();
        structuredFolds = structuredFolds.Reveal(tree, target);
        MoveStructuredCaret(target, cursor.Caret);
    }

    private void MoveStructuredCaret(int target, int previous)
    {
        var position = structured!.Tree[target].Start;
        PlaceStructured(StructuredCursor.At(position, target), position, StructuredScreenY(previous));
    }

    private void ToggleStructured(int id)
    {
        if (structured is not { } source || !source.Tree[id].CanFold) return;
        CancelStructuredCommand();
        structuredNotice = null;
        var cursor = CaptureStructuredCursor();
        var position = cursor.Element == id ? cursor.Caret : source.Tree[id].Start;
        structuredFolds = structuredFolds.Set(id, !structuredFolds.IsExpanded(id));
        var next = cursor.Start == cursor.End ? StructuredCursor.At(position, id) : cursor with { Element = id, Caret = position };
        PlaceStructured(next, position, StructuredScreenY(position));
    }

    private void ClickStructuredFold(object? sender, PointerPressedEventArgs e)
    {
        if (structuredPage is not { } page || presenter == null || inputBlocked() ||
            e.KeyModifiers != KeyModifiers.None || !e.GetCurrentPoint(textContent).Properties.IsLeftButtonPressed) return;
        var hit = presenter.TextLayout.HitTestPoint(e.GetPosition(presenter));
        if (page.FoldAt(hit.TextPosition) is not { } id) return;
        textContent.Focus();
        ToggleStructured(id);
        e.Handled = true;
    }

    private void MoveStructuredPeer(Key key)
    {
        if (key is Key.Left or Key.Right)
            StartStructuredPeerCommand(key == Key.Left ? JsonPeerAction.Collapse : JsonPeerAction.Expand);
        else
            EnqueuePeerCommand(new((folds, tree, id, token) => folds.Navigate(tree, id, key == Key.Up ? -1 : 1, token), Move: true));
    }

    private void StartStructuredPeerCommand(JsonPeerAction action) => EnqueuePeerCommand(new(
        (folds, tree, id, token) => folds.Apply(tree, id, action, token), Move: false));

    private async void EnqueuePeerCommand(PeerCommand command)
    {
        peerCommands.Enqueue(command);
        if (processingPeers) return;
        processingPeers = true;
        try
        {
            while (peerCommands.TryDequeue(out var next)) await RunPeerCommandAsync(next);
        }
        finally { processingPeers = false; }
    }

    private async Task RunPeerCommandAsync(PeerCommand command)
    {
        if (structured is not { } source) return;
        using var request = new CancellationTokenSource();
        structuredCommand = request;
        var folds = structuredFolds;
        var id = CaptureStructuredCursor().Element;
        structuredNotice = "Updating peers…";
        UpdateStructuredCue();
        try
        {
            var result = await Task.Run(() => command.Run(folds, source.Tree, id, request.Token), request.Token);
            if (request.IsCancellationRequested || structured != source) return;
            structuredNotice = DescribePeerResult(result, id, command.Move);
            ApplyStructuredPeerResult(result, id, command.Move);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        finally { if (structuredCommand == request) structuredCommand = null; }
    }

    private string? DescribePeerResult(JsonPeerResult result, int id, bool move)
    {
        if (!result.Available) return move ? "No peer group for this element" : "No peer layout for this element";
        if (result.Skipped > 0) return $"Skipped {result.Skipped:N0} {(result.Skipped == 1 ? "peer" : "peers")}";
        if (move) return result.Element == id ? "No further matching record" : null;
        return result.Folds == structuredFolds ? null : "Peer layout updated";
    }

    private void ApplyStructuredPeerResult(JsonPeerResult result, int id, bool move)
    {
        var cursor = CaptureStructuredCursor();
        if (move && cursor.Element != id) { structuredNotice = null; UpdateStructuredCue(); return; }
        var changed = result.Folds != structuredFolds;
        structuredFolds = result.Folds;
        if (move && result.Element != id) MoveStructuredCaret(result.Element, cursor.Caret);
        else if (changed) PlaceStructured(cursor, cursor.Caret, StructuredScreenY(cursor.Caret));
        else UpdateStructuredCue();
    }

    private void CancelStructuredCommand()
    {
        peerCommands.Clear();
        structuredCommand?.Cancel();
        structuredNotice = null;
    }

    public void SelectAll()
    {
        if (structured is not { } source) { textContent.SelectAll(); return; }
        var cursor = CaptureStructuredCursor();
        SetStructuredCursor(cursor with { Start = 0, End = source.Text.Length });
        UpdateStructuredCue();
    }

    public void Copy()
    {
        if (structured is not { } source) { textContent.Copy(); return; }
        var cursor = CaptureStructuredCursor();
        if (cursor.Start == cursor.End || TopLevel.GetTopLevel(textContent)?.Clipboard is not { } clipboard) return;
        var start = Math.Min(cursor.Start, cursor.End);
        var end = Math.Max(cursor.Start, cursor.End);
        runExternal(() => clipboard.SetTextAsync(source.Text[start..end]));
    }
}
