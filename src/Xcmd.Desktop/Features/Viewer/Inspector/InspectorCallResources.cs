// @implements InspectAgentSession
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal static class InspectorCallResources
{
    internal static Control Build(SessionTrace trace, TraceCall call)
    {
        if (call.Classification is not { Resources.Length: > 0 } classification) return Targets(call);
        var resources = Groups(trace, classification);
        return classification.Resources.Length <= 3 ? resources : WithDisclosure(Targets(call), resources, classification.Resources.Length);
    }

    private static Control Targets(TraceCall call)
    {
        var targets = new WrapPanel { ItemSpacing = 5, LineSpacing = 3 }.WithFeedbackId("SessionInspector.callTargets");
        foreach (var group in call.Labels.GroupBy(label => label.Target))
        {
            var target = group.Key;
            var badge = Stack("SessionInspector.callTarget", 4);
            badge.Orientation = Orientation.Horizontal;
            badge.Children.Add(Text("SessionInspector.callTargetName", TargetName(target), 13, Color(target), true));
            AddActions(badge, target, group.Select(label => label.Action));
            targets.Children.Add(Box("SessionInspector.callTargetBadge", badge, Subtle, new Thickness(5, 1)));
        }

        return targets;
    }

    private static Control WithDisclosure(Control targets, Control resources, int resourceCount)
    {
        var caption = new ContentControl { Content = targets, HorizontalContentAlignment = HorizontalAlignment.Stretch }
            .WithFeedbackId("SessionInspector.callResourcesCaption");
        var count = Text("SessionInspector.callResourceCount", $"{Number(resourceCount)} resources", 13, UiPalette.Secondary);
        var arrow = Text("SessionInspector.callResourcesArrow", "▸", 13, UiPalette.Secondary);
        var header = Grid("SessionInspector.callResourcesHeader", "Auto,*");
        header.ColumnSpacing = 5;
        Put(header, arrow);
        Put(header, caption, 1);
        var disclosure = new Button
        {
            Content = header,
            Background = Brushes.Transparent,
            BorderThickness = default,
            Padding = new Thickness(2),
            MinHeight = 24,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        }.WithFeedbackId("SessionInspector.callResourceDisclosure");
        disclosure.Classes.Add("quiet");
        var countLabel = count.Text!;
        AutomationProperties.SetName(disclosure, $"Expand {countLabel}");
        resources.IsVisible = false;
        var body = Stack("SessionInspector.callResourceDisclosureContent", 2);
        body.Children.Add(disclosure);
        body.Children.Add(resources);
        disclosure.Click += (_, _) =>
        {
            var expanded = !resources.IsVisible;
            arrow.Text = expanded ? "▾" : "▸";
            caption.Content = expanded ? count : targets;
            AutomationProperties.SetName(disclosure, expanded ? $"Collapse {countLabel}" : $"Expand {countLabel}");
            resources.IsVisible = expanded;
            if (expanded) Dispatcher.UIThread.Post(() =>
            {
                if (resources.IsVisible && TopLevel.GetTopLevel(body) != null) body.BringIntoView();
            }, DispatcherPriority.Loaded);
        };
        return body;
    }

    private static Control Groups(SessionTrace trace, ToolClassification classification)
    {
        var groups = Stack("SessionInspector.callResourceGroups", 2);
        var paths = classification.Resources.Where(IsPath)
            .ToLookup(resource => trace.FileKey(FileName(resource.Value)));
        var byTarget = classification.Resources
            .SelectMany(resource => SessionResourceActivity.Labels(trace, resource, classification).Distinct()
                .Select(label => (Resource: resource, Label: label)))
            .GroupBy(entry => entry.Label.Target);
        foreach (var group in byTarget)
        {
            var target = group.Key;
            var row = Grid("SessionInspector.callResourceGroup", "116,*");
            row.ColumnSpacing = 5;
            var name = Text("SessionInspector.callResourceTarget", TargetName(target), 13, Color(target), true);
            name.Margin = new Thickness(5, 2, 0, 0);
            name.VerticalAlignment = VerticalAlignment.Top;
            Put(row, name);
            var resources = new WrapPanel { ItemSpacing = 6, LineSpacing = 1 }.WithFeedbackId("SessionInspector.callResourceList");
            resources.Margin = new Thickness(0, 2, 4, 2);
            var entries = group.GroupBy(item => item.Resource).ToArray();
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                var resource = entry.Key;
                var fileName = FileName(resource.Value);
                var display = IsPath(resource) && paths[trace.FileKey(fileName)].Select(r => PathKey(trace, r.Value)).Distinct().Count() == 1
                    ? fileName : resource.Value;
                var separator = index + 1 < entries.Length ? "," : "";
                resources.Children.Add(ResourceRow(target, resource, display, separator, entry.Select(item => item.Label.Action)));
            }
            Put(row, resources, 1);
            var panel = Box("SessionInspector.callResourceGroupPanel", row, Subtle);
            panel.BorderBrush = Color(target, group.Any(entry => entry.Label.Action is "write" or "exec"));
            panel.BorderThickness = new Thickness(2, 0, 0, 0);
            groups.Children.Add(panel);
        }
        return groups;
    }

    private static Control ResourceRow(string target, ToolResource resource, string display, string separator, IEnumerable<string> actions)
    {
        var row = Grid("SessionInspector.callResource", "Auto,*");
        row.ColumnSpacing = 4;
        var icons = Stack("SessionInspector.callResourceActions", 3);
        icons.Orientation = Orientation.Horizontal;
        icons.VerticalAlignment = VerticalAlignment.Top;
        icons.Margin = new Thickness(0, 3, 0, 0);
        AddActions(icons, target, actions);
        Put(row, icons);
        var name = Text("SessionInspector.callResourceName", display + separator, 13);
        name.TextWrapping = TextWrapping.Wrap;
        name.TextTrimming = TextTrimming.None;
        if (resource.Kind != "file")
        {
            name.Text = null;
            name.Inlines!.Add(new Avalonia.Controls.Documents.Run(display));
            name.Inlines.Add(new Avalonia.Controls.Documents.Run($" ({resource.Kind})") { Foreground = UiPalette.Secondary });
            name.Inlines.Add(new Avalonia.Controls.Documents.Run(separator));
        }
        ToolTip.SetTip(name, resource.Value);
        AutomationProperties.SetName(name, $"{resource.Value} ({resource.Kind})");
        Put(row, name, 1);
        return row;
    }

    private static void AddActions(Panel panel, string target, IEnumerable<string> actions)
    {
        foreach (var action in actions.Distinct())
        {
            if (ActionGlyph("SessionInspector.callActionIcon", action, target) is not { } icon) continue;
            ToolTip.SetTip(icon, AutomationProperties.GetName(icon));
            panel.Children.Add(icon);
        }
    }

    private static bool IsPath(ToolResource resource) => resource.Kind is "file" or "directory";
    private static string PathKey(SessionTrace trace, string value) => trace.FileKey(value.Replace('\\', '/').TrimEnd('/'));
    private static string FileName(string value) => value.Replace('\\', '/').TrimEnd('/').Split('/')[^1] is { Length: > 0 } name ? name : value;
    private static string TargetName(string target) => target.Length == 0 ? "Unclassified" : TimelineCaption.Name(target);
}
