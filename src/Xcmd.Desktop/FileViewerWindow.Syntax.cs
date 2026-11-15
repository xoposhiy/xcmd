using Avalonia.Controls;
using Avalonia.Input;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed partial class FileViewerWindow
{
    private readonly SyntaxHighlighter highlighter = new();
    private readonly Dictionary<string, MenuItem> syntaxItems = new(StringComparer.Ordinal);
    private string? syntaxLanguage;
    private MenuItem reportSyntax = null!;
    private MenuItem reportSyntaxContext = null!;
    public string SyntaxStatus => TextContent.Syntax?.Notice ?? TextContent.Syntax?.Language?.Name ?? "Plain text";

    private MenuItem BuildSyntaxMenu()
    {
        AddSyntaxChoice("auto", "Automatic");
        AddSyntaxChoice("plain", "Plain text");
        foreach (var language in SyntaxHighlighter.Languages) AddSyntaxChoice(language.Id, language.Name);
        return new MenuItem { Header = "Syntax language", ItemsSource = syntaxItems.Values }.WithFeedbackId("FileViewerWindow.Syntax.languages");
    }

    private void AddSyntaxChoice(string id, string name)
    {
        var item = MenuCommand(name, "viewer.syntax." + id, () =>
        {
            syntaxLanguage = id == "auto" ? null : id;
            Run(async token => { await HighlightPageAsync(token); UpdateReadingPosition(); });
            TextContent.Focus();
        });
        item.ToggleType = MenuItemToggleType.Radio; item.GroupName = "syntax";
        syntaxItems.Add(id, item);
    }

    private void ConfigureSyntaxReporting()
    {
        reportSyntaxContext = MenuCommand("Report highlighting problem…", "viewer.syntax-report-context", ReportSyntaxProblem);
        TextContent.ContextMenu = new ContextMenu
        {
            ItemsSource = new[]
            {
                MenuCommand("Copy selection", "viewer.context-copy", TextContent.Copy, new(Key.C, KeyModifiers.Control)),
                MenuCommand("Select all", "viewer.context-select-all", TextContent.SelectAll, new(Key.A, KeyModifiers.Control)),
                reportSyntaxContext
            }
        }.WithFeedbackId("FileViewerWindow.Syntax.context");
        TextContent.ContextFlyout = null;
    }

    private void UpdateSyntaxControls()
    {
        var enabled = Mode == FileViewMode.Text && slice != null && !IsArchiveReport;
        foreach (var pair in syntaxItems)
        {
            pair.Value.IsEnabled = enabled;
            pair.Value.IsChecked = pair.Key == (syntaxLanguage ?? "auto");
        }
        reportSyntax.IsEnabled = enabled;
        reportSyntaxContext.IsEnabled = enabled;
    }

    private async Task HighlightPageAsync(CancellationToken token)
    {
        TextContent.SetSyntax(null);
        if (Mode != FileViewMode.Text || slice == null || IsArchiveReport) return;
        SyntaxDocument syntax;
        try { syntax = await highlighter.ReadAsync(files, CurrentPath, slice, CurrentEncoding(), syntaxLanguage, token); }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        { syntax = new(TextContent.Text ?? "", null, [], "Plain text: syntax context could not be read"); }
        token.ThrowIfCancellationRequested();
        TextContent.SetSyntax(syntax);
    }

    private void ReportSyntaxProblem() => ReportSyntaxProblem(null);

    private void ReportSyntaxProblem(ReaderSelection? selection)
    {
        if (feedback.IsActive || Mode != FileViewMode.Text || slice == null || IsArchiveReport) return;
        var start = selection != null && selection.Text == TextContent.Text ? Math.Min(selection.Start, selection.End) : Math.Min(TextContent.SelectionStart, TextContent.SelectionEnd);
        var end = selection != null && selection.Text == TextContent.Text ? Math.Max(selection.Start, selection.End) : Math.Max(TextContent.SelectionStart, TextContent.SelectionEnd);
        var text = TextContent.Text ?? "";
        var sampleLength = Math.Min(end - start, 4096);
        if (sampleLength > 0 && start + sampleLength < end && char.IsHighSurrogate(text[start + sampleLength - 1])) sampleLength--;
        var sample = text.Substring(start, sampleLength);
        var details = (sample.Length == 0 ? "No code selected. Select code before reporting to include a sample." : "Selected code" + (end - start > sample.Length ? " (first 4,096 characters)" : "") + ":\n" + sample) +
            $"\n\nSyntax highlighting\nFile: {Path.GetFileName(CurrentPath)}\nLanguage: {TextContent.Syntax?.Language?.Name ?? "Plain text"} ({syntaxLanguage ?? "automatic"})\n" +
            $"Engine: {SyntaxHighlighter.Version}\nTheme: {ThemeId}\nEncoding: {CurrentEncoding().WebName}\n" +
            $"Page byte offset: {ByteOffset}\nSelection: characters {start}–{end}; file byte offset {ByteAtCharacter(start)}\nStatus: {SyntaxStatus}";
        menuSelection = null;
        var caret = TextContent.CaretIndex;
        var comment = feedback.Report(this, TextContent, details);
        if (comment != null) comment.Closed += (_, _) => RestoreReaderSelection(ThemeId, text, caret, start, end);
    }
}
