// @implements InspectAgentSession
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Xcmd.Desktop.Features.Viewer;

internal sealed class ResourceFilter
{
    private readonly string name;
    private readonly Dictionary<string, CheckBox> choices = [];
    private readonly HashSet<string> selected = [];
    private readonly CheckBox all = new CheckBox { Content = "All", FontSize = 12, MinHeight = 26 }
        .WithFeedbackId("SessionInspector.resourceFilterAll");

    internal Button Button { get; }
    internal IReadOnlySet<string> Selected => selected;
    internal event Action? Changed;

    internal ResourceFilter([FeedbackId] string id, string name, IEnumerable<(string Key, string Caption)> options, string? initial = null, string? hint = null)
    {
        this.name = name;
        var list = new StackPanel().WithFeedbackId("SessionInspector.resourceFilterList");
        list.Children.Add(all);
        foreach (var (key, caption) in options)
        {
            var check = new CheckBox { Content = caption, FontSize = 12, MinHeight = 26 }
                .WithFeedbackId("SessionInspector.resourceKind");
            choices.Add(key, check);
            list.Children.Add(check);
            check.IsCheckedChanged += (_, _) => ChoiceChanged(key, check.IsChecked == true);
        }
        if (hint != null) list.Children.Add(InspectorStyle.Text("SessionInspector.resourceFilterHelp", hint, 11, UiPalette.Secondary));
        var flyout = new Flyout { Content = list, Placement = PlacementMode.BottomEdgeAlignedLeft };
        Button = new Button { Flyout = flyout, FontSize = 12, MinWidth = 100, MinHeight = 26, Padding = new Thickness(8, 2) }
            .WithFeedbackId(id);
        AutomationProperties.SetName(Button, "Resource " + name.ToLowerInvariant());
        FeedbackTarget.SetScope(Button, "Resource " + name.ToLowerInvariant());
        all.Click += (_, _) =>
        {
            SetSelection(selected.Count == choices.Count ? [] : choices.Keys);
            Changed?.Invoke();
        };
        list.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            flyout.Hide();
            Button.Focus();
        };
        SetSelection(initial == null ? choices.Keys : [initial]);
    }

    internal void CopySelection(ResourceFilter prior) => SetSelection(prior.selected);

    private void SetSelection(IEnumerable<string> keys)
    {
        selected.Clear();
        selected.UnionWith(keys);
        foreach (var (key, check) in choices) check.IsChecked = selected.Contains(key);
        UpdateCaption();
    }

    private void ChoiceChanged(string key, bool isChecked)
    {
        if (!(isChecked ? selected.Add(key) : selected.Remove(key))) return;
        UpdateCaption();
        Changed?.Invoke();
    }

    private void UpdateCaption()
    {
        all.IsChecked = selected.Count == choices.Count ? true : selected.Count == 0 ? false : null;
        var summary = selected.Count == choices.Count ? "All" : selected.Count switch
        {
            0 => "None",
            1 => (string)choices[selected.Single()].Content!,
            _ => $"{selected.Count} selected"
        };
        Button.Content = $"{name}: {summary}  ▾";
        AutomationProperties.SetHelpText(Button, summary);
    }
}
