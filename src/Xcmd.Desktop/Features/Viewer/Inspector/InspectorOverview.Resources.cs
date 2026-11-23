// @implements InspectAgentSession, OpenViewers
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal sealed partial class InspectorOverview
{
    private void RenderResources()
    {
        resources.Children.Clear();
        var count = summary.Resources.Count;
        resources.Children.Add(SectionTitle("Touched resources", $"{count:N0} {(count == 1 ? "resource" : "resources")}"));
        var missing = summary.Results.Count(c => c.Classification == null);
        if (missing > 0)
            resources.Children.Add(Note($"Resource lists unavailable for {missing:N0} of {summary.Results.Count:N0} calls in this range."));
        if (count == 0)
        {
            if (missing == 0) resources.Children.Add(Note(summary.Results.Count == 0
                ? "No tool calls finished in this range." : "Luna identified no resources in this range."));
            return;
        }
        var header = ResourceRow("Resource", "Kind", "Calls", UiPalette.Secondary);
        header.Background = Subtle;
        resources.Children.Add(header);
        foreach (var resource in summary.Resources.Take(16)) resources.Children.Add(ResourceButton(resource));
        if (count > 16) resources.Children.Add(Action($"Browse all {count:N0} resources  ›", ShowAllResources));
    }

    private static Grid ResourceRow(string value, string kind, string calls, IBrush brush)
    {
        var row = Grid("*,72,44");
        foreach (var (label, column) in new[] { value, kind, calls }.Select((s, i) => (s, i)))
        {
            var cell = Text(label, 12, brush);
            cell.Margin = new Thickness(column == 0 ? 11 : 2, 3, 8, 3);
            if (column == 2) cell.HorizontalAlignment = HorizontalAlignment.Right;
            Put(row, cell, column);
        }
        return row;
    }

    private Button ResourceButton(TouchedResource row)
    {
        var resource = row.Resource;
        var button = Action("", () => ShowResource(row), resource.Value);
        button.Content = ResourceRow(resource.Value, resource.Kind, Number(row.Calls.Count), UiPalette.Text);
        button.HorizontalAlignment = button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.Padding = new Thickness(0);
        button.Classes.Add("quiet");
        AutomationProperties.SetName(button, $"{resource.Value}, {resource.Kind}, {row.Calls.Count:N0} calls");
        return button;
    }

    private void ShowAllResources()
    {
        var list = new ListBox
        {
            ItemsSource = summary.Resources,
            ItemTemplate = new FuncDataTemplate<TouchedResource>((r, _) => r == null ? null : ResourceButton(r))
        }.WithFeedbackId("SessionInspector.allResources");
        list.KeyDown += (_, e) =>
        {
            if (!e.Handled && e.Key == Key.Enter && list.SelectedItem is TouchedResource selected)
            { e.Handled = true; ShowResource(selected); }
        };
        detail("Resources in the selected range", list);
    }

    private void ShowResource(TouchedResource row)
    {
        var body = Grid("*", "Auto,Auto,*");
        Put(body, SessionInspectorWindow.Paragraph(row.Resource.Value));
        Put(body, SectionTitle("Tool calls", $"{row.Resource.Kind}   ·   {Number(row.Calls.Count)}"), 0, 1);
        Put(body, CallList(row.Calls, showResources: false), 0, 2);
        detail("Resource details", body);
    }
}
