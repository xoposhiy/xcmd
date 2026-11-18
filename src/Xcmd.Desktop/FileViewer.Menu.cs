// @implements OpenViewers, QuickViewFile, ChooseViewMode, ReadLargeFiles, FindViewerText, HighlightSource, ReaderThemes, FollowLog, OpenViewedFile, ReportControl, ReportHighlighting
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed partial class FileViewer
{
    private readonly List<MenuItem> textCommands = [];
    private MenuItem editItem = null!;
    private MenuItem openItem = null!;

    private MenuItem TextCommand(string title, string id, Action action, KeyGesture? gesture = null)
    {
        var item = MenuCommand(title, id, action, gesture);
        textCommands.Add(item);
        return item;
    }

    private MenuItem MenuCommand(string title, string id, Action action, KeyGesture? gesture = null)
    {
        var item = new MenuItem { Header = title, Name = id, InputGesture = gesture }.WithFeedbackId("FileViewerWindow.Menu.item");
        AutomationProperties.SetAutomationId(item, id);
        item.Click += (_, _) => action();
        if (id is "viewer.edit" or "viewer.open")
        {
            item.IsEnabled = !IsDocumentReadOnly;
            ToolTip.SetTip(item, IsDocumentReadOnly ? "This Git view is read-only." : null);
        }
        return item;
    }

    private void BuildMenu()
    {
        var view = new List<MenuItem>();
        foreach (var mode in Enum.GetValues<FileViewMode>())
        {
            var item = MenuCommand(mode.ToString(), "viewer.mode." + mode.ToString().ToLowerInvariant(),
                () => Run(token => ChangeModeAsync(mode, token)), new((Key)((int)Key.D1 + (int)mode)));
            item.ToggleType = MenuItemToggleType.Radio; item.GroupName = "mode";
            modeItems[mode] = item; view.Add(item);
        }
        wrapItem = MenuCommand("Word wrap", "viewer.wrap", ToggleWrapping, new(Key.W));
        wrapItem.ToggleType = MenuItemToggleType.CheckBox; view.Add(wrapItem);
        informationItem = MenuCommand("File information", "viewer.information", ToggleInformation, new(Key.I, KeyModifiers.Control));
        informationItem.ToggleType = MenuItemToggleType.CheckBox; view.Add(informationItem);
        followItem = MenuCommand("Follow end of file", "viewer.follow", ToggleFollowing, new(Key.F, KeyModifiers.Control | KeyModifiers.Shift));
        followItem.ToggleType = MenuItemToggleType.CheckBox; view.Add(followItem);
        view.Add(BuildThemeMenu());
        view.Add(BuildSyntaxMenu());
        reportSyntax = MenuCommand("Report highlighting problem…", "viewer.syntax-report", () => ReportSyntaxProblem(menuSelection), new(Key.F12, KeyModifiers.Control | KeyModifiers.Shift));
        ConfigureSyntaxReporting();
        foreach (var choice in EncodingChoice.All)
        {
            var item = MenuCommand(choice.Label, "viewer.encoding." + choice.CodePage, () => SelectEncoding(choice));
            item.ToggleType = MenuItemToggleType.Radio; item.GroupName = "encoding";
            encodingItems[choice.CodePage] = item;
        }
        previousPage = MenuCommand("Page up", "viewer.previous-page", () => MoveViewport(-Math.Max(20, (textScroll?.Viewport.Height ?? 500) - 20)), new(Key.PageUp));
        nextPage = MenuCommand("Page down", "viewer.next-page", () => MoveViewport(Math.Max(20, (textScroll?.Viewport.Height ?? 500) - 20)), new(Key.PageDown));
        editItem = MenuCommand("Edit in Notepad++", "viewer.edit", () => RunExternal(() => edit(CurrentPath)), new(Key.F4));
        openItem = MenuCommand("Open externally", "viewer.open", () => RunExternal(() => open(CurrentPath)), new(Key.Enter, KeyModifiers.Control));
        Commands.ItemsSource = new MenuItem[]
        {
            new MenuItem() { Header = "File", ItemsSource = new MenuItem[]
            {
                editItem, openItem,
                TextCommand("Copy selection", "viewer.copy", TextContent.Copy, new(Key.C, KeyModifiers.Control)),
                TextCommand("Select all", "viewer.select-all", () => { TextContent.Focus(); TextContent.SelectAll(); }, new(Key.A, KeyModifiers.Control)),
                MenuCommand("Close", "viewer.close", close, new(Key.Escape))
            } }.WithFeedbackId("FileViewerWindow.Menu.BuildMenu"),
            new MenuItem() { Header = "View", ItemsSource = view }.WithFeedbackId("FileViewerWindow.Menu.BuildMenu.2"),
            new MenuItem() { Header = "Encoding", ItemsSource = encodingItems.Values }.WithFeedbackId("FileViewerWindow.Menu.BuildMenu.3"),
            new MenuItem() { Header = "Search", ItemsSource = new MenuItem[]
            {
                TextCommand("Find (also Alt+letter)", "viewer.find", FocusSearch, new(Key.F, KeyModifiers.Control)),
                TextCommand("Next match (also Tab)", "viewer.find-next", () => RunExternal(() => SearchAsync(1)), new(Key.F3)),
                TextCommand("Previous match (also Shift+Tab)", "viewer.find-previous", () => RunExternal(() => SearchAsync(-1)), new(Key.F3, KeyModifiers.Shift)),
                TextCommand("Clear search", "viewer.clear-search", () => ClearSearch(), new(Key.Escape))
            } }.WithFeedbackId("FileViewerWindow.Menu.BuildMenu.4"),
            new MenuItem() { Header = "Navigate", ItemsSource = BuildNavigationItems() }.WithFeedbackId("FileViewerWindow.Menu.BuildMenu.5"),
            new MenuItem() { Header = "Help", ItemsSource = new[] { reportSyntax, MenuCommand("Comment on an element", "viewer.feedback", BeginFeedback, new(Key.F12)) } }.WithFeedbackId("FileViewerWindow.Menu.BuildMenu.6")
        };
    }

    private IReadOnlyList<MenuItem> BuildNavigationItems()
    {
        List<MenuItem> items =
        [
            previousPage, nextPage,
            TextCommand("Beginning of file", "viewer.first-page", () => Run(token => SeekBoundaryAsync(false, token)), new(Key.Home)),
            TextCommand("End of file", "viewer.last-page", () => Run(token => SeekBoundaryAsync(true, token)), new(Key.End))
        ];
        if (IsEmbedded)
        {
            items.Add(MenuCommand("Return to file list", "viewer.focus-file-list", () => FileListFocusRequested?.Invoke(), new(Key.Tab, KeyModifiers.Control)));
        }
        else
        {
            previousFile = MenuCommand("Previous file", "viewer.previous-file", () => RunExternal(() => NavigateAsync(-1)), new(Key.Left, KeyModifiers.Alt));
            nextFile = MenuCommand("Next file", "viewer.next-file", () => RunExternal(() => NavigateAsync(1)), new(Key.Right, KeyModifiers.Alt));
            items.Add(previousFile);
            items.Add(nextFile);
        }
        return items;
    }

    private void UpdateExternalItems() => editItem.IsEnabled = openItem.IsEnabled = HasFile && !IsDocumentReadOnly;

    private void BeginFeedback()
    {
        if (TopLevel.GetTopLevel(this) is Window window) feedback.Begin(window);
    }

    private void ToggleWrapping()
    {
        var anchor = FirstVisibleByte;
        wrapLines = !wrapLines; UpdateControls(); TextContent.UpdateLayout();
        RestoreTopByte(anchor); TextContent.Focus();
    }

    private void ToggleInformation() { information.IsVisible = !information.IsVisible; UpdateControls(); TextContent.Focus(); }

    private void SelectEncoding(EncodingChoice choice)
    {
        selectedEncoding = choice;
        ClearSearch();
        Run(RenderPageAsync);
        TextContent.Focus();
    }
}
