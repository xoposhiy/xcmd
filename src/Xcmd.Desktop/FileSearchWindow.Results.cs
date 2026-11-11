using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed partial class FileSearchWindow
{
    private const string LocationSearchHint = "Double-click a row to search only that location and replace current results.";
    private readonly Expander deferredDetails = new() { Name = "SearchDeferredDetails", IsVisible = false, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0) };
    private readonly Expander failureDetails = new() { Name = "SearchFailureDetails", IsVisible = false, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0) };
    private readonly TableView deferredList = new() { Name = "SearchDeferredLocations", MaxHeight = 180 };
    private readonly TableView failureList = new() { Name = "SearchFailedLocations", MaxHeight = 180 };
    private int deferredShown = -1;
    private int failuresShown = -1;

    private void ConfigureResults()
    {
        SearchTables.Configure(Results);
        Results.Columns.Add(SearchTables.Column<FileSearchResult>("Path", new(3, GridUnitType.Star), 120, result =>
        {
            var text = SearchTables.Text(result?.RelativePath + (result?.Entry.IsDirectory == true ? " /" : ""), "SearchResultPath");
            ToolTip.SetTip(text, result == null ? null : ArchivePath.Display(result.Entry.FullPath)); return text;
        }));
        Results.Columns.Add(SearchTables.Column<FileSearchResult>("Line", new(64), 40,
            result => SearchTables.Text(result?.Match?.Line.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        Results.Columns.Add(SearchTables.Column<FileSearchResult>("Preview", new(2, GridUnitType.Star), 100, result =>
        {
            var text = SearchTables.Text(result?.Match?.Excerpt, "SearchResultPreview"); text.FontSize = 12;
            text.Foreground = Brushes.LightGray; text.FontFamily = new FontFamily("Consolas");
            text.TextTrimming = TextTrimming.CharacterEllipsis; ToolTip.SetTip(text, result?.Match?.Excerpt); return text;
        }));
    }

    private Control BuildCoverageDetails()
    {
        SearchTables.Configure(deferredList);
        deferredList.Columns.Add(SearchTables.Column<DeferredSearchLocation>("Reason", new(112), 70,
            location => SearchTables.Text(SkippedReason(location))));
        deferredList.Columns.Add(SearchTables.Column<DeferredSearchLocation>("Path", new(3, GridUnitType.Star), 120,
            location => LocationCell(location?.Path)));
        deferredList.Columns.Add(SearchTables.Column<DeferredSearchLocation>("Rule / source", new(2, GridUnitType.Star), 120,
            location => DetailCell(location?.Rule ?? location?.Reason)));
        ConfigureFailureTable();
        ToolTip.SetTip(deferredList, LocationSearchHint); ToolTip.SetTip(failureList, LocationSearchHint);
        ToolTip.SetTip(failureDetails, LocationSearchHint);
        deferredList.DoubleTapped += (_, e) =>
        { if (IsTableRow(e) && deferredList.SelectedItem is DeferredSearchLocation location) SearchLocation(location.Path, !location.Directory); };
        failureList.DoubleTapped += (_, e) =>
        { if (IsTableRow(e) && failureList.SelectedItem is SearchFailure failure) SearchLocation(failure.Path, !failure.Directory); };
        deferredDetails.Content = deferredList; failureDetails.Content = failureList;
        return new StackPanel { Spacing = 2, Children = { details, deferredDetails, failureDetails } };
    }

    private TextBlock LocationCell(string? path)
    {
        var text = SearchTables.Text(path == null ? "" : ArchivePath.Display(Path.GetRelativePath(root, path)));
        ToolTip.SetTip(text, path == null ? null : ArchivePath.Display(path) + "\n" + LocationSearchHint); return text;
    }

    private static TextBlock DetailCell(string? value)
    {
        var text = SearchTables.Text(value); ToolTip.SetTip(text, value + "\n" + LocationSearchHint); return text;
    }

    private static bool IsTableRow(RoutedEventArgs e) => e.Source is Control control && control.GetSelfAndVisualAncestors().OfType<TableViewRow>().Any();

    private static string? SkippedReason(DeferredSearchLocation? location) => location?.Reason switch
    {
        "Git ignored" => location.Rule?.Contains(".gitignore:", StringComparison.OrdinalIgnoreCase) == true ? ".gitignore" : "Git",
        "Version-control data" => "Metadata",
        { } reason when reason.StartsWith("Dependencies", StringComparison.Ordinal) => "Dependency",
        { } reason when reason.Contains("environment", StringComparison.Ordinal) => "Environment",
        _ => "Cache"
    };

    private void ConfigureFailureTable()
    {
        SearchTables.Configure(failureList);
        failureList.Columns.Add(SearchTables.Column<SearchFailure>("Reason", new(112), 70,
            failure => SearchTables.Text(failure?.Reason)));
        failureList.Columns.Add(SearchTables.Column<SearchFailure>("Path", new(3, GridUnitType.Star), 120,
            failure => LocationCell(failure?.Path)));
        failureList.Columns.Add(SearchTables.Column<SearchFailure>("Explanation", new(2, GridUnitType.Star), 120,
            failure => DetailCell(failure?.Message)));
    }

    private void RestoreSkippedColumns(ColumnState[]? saved)
    {
        if (saved?.Length == 2)
        { deferredList.Columns[1].Width = new GridLength(saved[0].Width, saved[0].Star ? GridUnitType.Star : GridUnitType.Pixel); return; }
        SearchTables.Restore(deferredList, saved);
    }

    private void UpdateCoverageDetails(IReadOnlyList<SearchFailure> failures)
    {
        if (failures.Count == failuresShown) return;
        failuresShown = failures.Count;
        failureDetails.IsVisible = failures.Count > 0;
        failureDetails.Header = $"{failures.Count:N0} locations could not be searched · Details";
        failureList.ItemsSource = failures;
    }

    private void UpdateDeferredSummary()
    {
        if (search == null || search.DeferredCount == deferredShown) return;
        deferredShown = search.DeferredCount;
        var locations = search.DeferredLocations;
        var gitFiles = locations.Count(location => location.Reason == "Git ignored" && !location.Directory);
        var gitFolders = locations.Count(location => location.Reason == "Git ignored" && location.Directory);
        var standard = locations.Count - gitFiles - gitFolders;
        var parts = new List<string>();
        if (gitFiles > 0) parts.Add($"{gitFiles:N0} Git ignored files");
        if (gitFolders > 0) parts.Add($"{gitFolders:N0} Git ignored folders");
        if (standard > 0) parts.Add($"{standard:N0} standard folders");
        deferredDetails.IsVisible = locations.Count > 0;
        deferredDetails.Header = "Skipped: " + string.Join(" · ", parts);
        deferredList.ItemsSource = locations;
        ToolTip.SetTip(deferredDetails, "Known files and folder roots skipped by this search. Files inside skipped folders have not been counted. " + LocationSearchHint);
    }
}
