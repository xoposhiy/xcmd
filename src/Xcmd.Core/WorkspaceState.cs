namespace Xcmd.Core;

public sealed record PanelState(string DirectoryPath, SortOrder Sort, string? Cursor, string[] Selected,
    string[] History, Dictionary<string, string> Cursors, ColumnState[] Columns, double ScrollX, double ScrollY, DirectoryMeasurement[]? Sizes = null, bool HasMarks = false);
public sealed record ColumnState(double Width, bool Star);
public sealed record WindowPlacement(int X, int Y, double Width, double Height, bool Maximized, bool Minimized = false, bool Active = true);
public sealed record FeedbackInteraction(string Action, string ElementId, string ElementName);
public sealed record FeedbackNote(string Id, string Release, string ElementId, string ElementName,
    string PanelPath, string Text, DateTimeOffset CreatedAt, string? ResolvedIn = null, string? Resolution = null, FeedbackInteraction[]? Interactions = null);
public sealed record WorkspaceState(int Version, PanelState Left, PanelState Right, string ActivePanel,
    WindowPlacement Window, FeedbackNote[] Notes, DirectoryUsage[]? Usage = null, FavoriteDirectory[]? Favorites = null, int UsageVersion = 0, CommandLineState? CommandLine = null, string ViewerThemeId = "dark")
{
    public void Validate()
    {
        if (Version != 1 || Left == null || Right == null || Window == null || Notes == null ||
            Notes.Any(note => note == null || string.IsNullOrWhiteSpace(note.Text) || note.Id == null || note.ElementId == null || note.Release == null))
            throw new InvalidDataException("The saved session format is not supported.");
        ValidatePanel(Left);
        ValidatePanel(Right);
        if (!ValidSize(Window.Width) || !ValidSize(Window.Height)) throw new InvalidDataException("The saved window size is invalid.");
        ValidateCommandLine();
        ValidateUsage();
        ValidateFavorites();
    }

    private void ValidateUsage()
    {
        if (Usage is { } usage && (usage.Length > 512 || usage.Any(location => location == null ||
            !Path.IsPathFullyQualified(location.Path) || location.Visits < 0 || !double.IsFinite(location.Weight) || location.Weight < 0)))
            throw new InvalidDataException("The saved directory statistics are invalid.");
    }

    private static void ValidatePanel(PanelState panel)
    {
        if (!ValidDirectoryPath(panel.DirectoryPath) || !ValidPanelNavigation(panel) ||
            !ValidPanelHistory(panel.History) || !ValidPanelLayout(panel))
            throw new InvalidDataException("The saved panel state is invalid.");
        ValidateDirectorySizes(panel.Sizes);
    }

    private static bool ValidDirectoryPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path);

    private static bool ValidPanelNavigation(PanelState panel) =>
        panel.Sort != null && Enum.IsDefined(panel.Sort.Field) && panel.Selected != null && panel.Cursors != null;

    private static bool ValidPanelHistory(string[]? history) =>
        history != null && history.Length <= 100 && history.All(ValidDirectoryPath);

    private static bool ValidPanelLayout(PanelState panel) =>
        panel.Columns != null && panel.Columns.All(column => column != null && ValidSize(column.Width)) &&
        ValidOffset(panel.ScrollX) && ValidOffset(panel.ScrollY);

    private static void ValidateDirectorySizes(DirectoryMeasurement[]? measurements)
    {
        if (measurements is { } sizes && sizes.Any(item => item == null || string.IsNullOrWhiteSpace(item.Name) ||
            item.Name is "." or ".." || Path.GetFileName(item.Name) != item.Name || item.Size == null ||
            item.Size.Bytes < 0 || item.Size.Files < 0 || item.Size.Errors < 0 || item.Size.SkippedLinks < 0))
            throw new InvalidDataException("The saved folder sizes are invalid.");
    }

    private void ValidateCommandLine()
    {
        if (CommandLine is { } command && (command.Draft == null || command.Draft.Length > 4096 || command.History == null || command.History.Length > 100 ||
            command.History.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 4096)))
            throw new InvalidDataException("The saved command line is invalid.");
    }

    private void ValidateFavorites()
    {
        if (Favorites is not { } favorites) return;
        if (favorites.Length > 256 || !favorites.All(ValidFavorite) || !HasUniqueFavorites(favorites))
            throw new InvalidDataException("The saved favorites are invalid.");
    }

    private static bool ValidFavorite(FavoriteDirectory? favorite)
    {
        if (favorite == null) return false;
        return !string.IsNullOrWhiteSpace(favorite.Id) && !string.IsNullOrWhiteSpace(favorite.Name) &&
            favorite.Name.Length <= 100 && !favorite.Name.Any(char.IsControl) && ValidDirectoryPath(favorite.Path);
    }

    private static bool HasUniqueFavorites(FavoriteDirectory[] favorites) =>
        favorites.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() == favorites.Length &&
        favorites.Select(item => Path.TrimEndingDirectorySeparator(item.Path).Replace('/', '\\'))
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() == favorites.Length;

    private static bool ValidSize(double value) => double.IsFinite(value) && value > 0 && value < 100000;
    private static bool ValidOffset(double value) => double.IsFinite(value) && value >= 0;
}

public sealed record ReleaseInfo(string Id, string Executable);
public sealed record RestartReadiness(bool Busy, bool Editing, bool DialogOpen, bool FeedbackActive, TimeSpan IdleFor)
{
    public bool CanRestart => !Busy && !Editing && !DialogOpen && !FeedbackActive && IdleFor >= TimeSpan.FromSeconds(2);
}
