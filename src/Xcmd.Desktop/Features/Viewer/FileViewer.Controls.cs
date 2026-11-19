// @implements OpenViewers, ChooseViewMode, ReadLargeFiles, FindViewerText, HighlightSource, FollowLog, OpenViewedFile, InspectArchives, ReadWorkingFileDiff
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

public sealed partial class FileViewer
{
    private void UpdateControls()
    {
        UpdateModeItems();
        UpdateEncodingItems();
        UpdateSyntaxControls();
        var text = Mode != FileViewMode.Image;
        UpdateTextItems(text);
        if (diff == null)
        {
            previousPage.IsEnabled = text && FirstVisibleByte > 0;
            nextPage.IsEnabled = text && VisibleEnd < FileLength;
        }
        UpdateDiffCommands();
        if (previousFile != null) previousFile.IsEnabled = index > 0;
        if (nextFile != null) nextFile.IsEnabled = index + 1 < paths.Length;
        UpdateWrapping();
    }

    private void UpdateModeItems()
    {
        foreach (var pair in modeItems)
        {
            pair.Value.IsChecked = pair.Key == Mode;
            pair.Value.IsEnabled = slice != null && (!IsArchiveReport || pair.Key == FileViewMode.Text);
            ToolTip.SetTip(pair.Value, IsArchiveReport ? "This view shows an archive report." : null);
        }
        modeItems[FileViewMode.Image].IsEnabled = CanUseImage;
        ToolTip.SetTip(modeItems[FileViewMode.Image], imageUnavailable);
    }

    private void UpdateEncodingItems()
    {
        var enabled = Mode == FileViewMode.Text && slice != null && !IsArchiveReport;
        foreach (var pair in encodingItems) { pair.Value.IsChecked = pair.Key == selectedEncoding.CodePage; pair.Value.IsEnabled = enabled; }
    }

    private void UpdateTextItems(bool text)
    {
        foreach (var item in textCommands) item.IsEnabled = text && slice != null;
        TextContent.IsVisible = text; image.IsVisible = !text;
        FileScroll.IsVisible = text;
        wrapItem.IsEnabled = Mode == FileViewMode.Text && slice != null;
        wrapItem.IsChecked = wrapLines;
        informationItem.IsChecked = information.IsVisible;
        followItem.IsEnabled = slice != null && text && diff == null && !IsArchiveReport && !IsDocumentReadOnly && !ArchivePath.IsEntry(CurrentPath);
        Search.IsEnabled = text && slice != null;
        UpdateExternalItems();
    }

    private void UpdateWrapping()
    {
        var wrap = Mode == FileViewMode.Text && wrapLines;
        TextContent.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        ScrollViewer.SetHorizontalScrollBarVisibility(TextContent, wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);
    }
}
