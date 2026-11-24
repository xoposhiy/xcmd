// @implements InspectAgentSession
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Viewer;

using static InspectorStyle;

internal sealed class ToolCallDialog : Window
{
    internal ToolCallDialog(TraceCall call)
    {
        Title = "Tool call";
        Width = 820;
        Height = 550;
        MinWidth = 360;
        MinHeight = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var layout = Grid("SessionInspector.toolCallDialogContent", "*", "Auto,*");
        layout.Margin = UiPalette.FormMargin;
        layout.RowSpacing = 10;
        Put(layout, TechnicalInfo(call));
        var tabs = BuildTabs(call);
        Put(layout, tabs, 0, 1);
        Content = layout;
        Opened += (_, _) => (tabs.SelectedItem as TabItem)?.Focus();
    }

    private static Control TechnicalInfo(TraceCall call)
    {
        var info = Grid("SessionInspector.toolCallFields", "140,*", "");
        info.ColumnSpacing = 10;
        info.RowSpacing = 4;
        Add("SessionInspector.toolCallNameLabel", "SessionInspector.toolCallName", "Tool name",
            string.IsNullOrWhiteSpace(call.ToolName) ? "No tool name in the call record." : call.ToolName);
        if (!string.IsNullOrWhiteSpace(call.RecordType))
            Add("SessionInspector.toolCallTypeLabel", "SessionInspector.toolCallRecord", "Record type", RecordDescription(call.RecordType));
        if (!string.IsNullOrWhiteSpace(call.Shell))
            Add("SessionInspector.toolCallShellLabel", "SessionInspector.toolCallShell", "Shell executable", ShellDescription(call.Shell));
        if (call.IsStandaloneJavaScript)
            Add("SessionInspector.toolCallInputFormatLabel", "SessionInspector.toolCallInputFormat", "Input format", "JavaScript source passed to the tool.");
        Add("SessionInspector.toolCallAgentLabel", "SessionInspector.toolCallAgent", "Agent", call.Agent);
        if (!string.IsNullOrWhiteSpace(call.WorkingDirectory))
            Add("SessionInspector.toolCallDirectoryLabel", "SessionInspector.toolCallDirectory", "Working directory", call.WorkingDirectory);
        if (call.Id > 0)
            Add("SessionInspector.toolCallRecordNumberLabel", "SessionInspector.toolCallRecordNumber", "Trace record number", Number(call.Id));
        return new ScrollViewer
        {
            Content = info,
            Background = Subtle,
            Padding = new Thickness(10, 8),
            MaxHeight = 192,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        }.WithFeedbackId("SessionInspector.toolCallMetadata");

        void Add([FeedbackId] string labelId, [FeedbackId] string valueId, string label, string value)
        {
            var row = info.RowDefinitions.Count;
            info.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var heading = Text(labelId, label, 13, UiPalette.Secondary);
            heading.VerticalAlignment = VerticalAlignment.Top;
            Put(info, heading, 0, row);
            Put(info, new SelectableTextBlock
            {
                Text = value,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = UiPalette.Text
            }.WithFeedbackId(valueId), 1, row);
        }
    }

    private static string RecordDescription(string type) => type switch
    {
        "Command execution" => "Command execution. The transcript records a command run in a shell.",
        "Function call" => "Function call. The agent passed structured arguments to a tool.",
        "Custom tool call" => "Custom tool call. The agent passed raw input to a tool.",
        "File change" => "File change. The transcript records an operation that creates, edits, or removes files.",
        _ => type
    };

    private static string ShellDescription(string shell)
    {
        var executable = Path.GetFileNameWithoutExtension(shell);
        return executable.Equals("pwsh", StringComparison.OrdinalIgnoreCase) ||
            executable.Equals("powershell", StringComparison.OrdinalIgnoreCase) ? $"PowerShell ({shell})" : shell;
    }

    private static TabControl BuildTabs(TraceCall call)
    {
        var input = Payload("SessionInspector.toolCallInputTab", "SessionInspector.toolCallInput", "Input", call.Input);
        var output = Payload("SessionInspector.toolCallOutputTab", "SessionInspector.toolCallOutput", "Output", call.Output);
        var tabs = new TabControl().WithFeedbackId("SessionInspector.toolCallTabs");
        tabs.Items.Add(input);
        tabs.Items.Add(output);
        tabs.SelectedItem = string.IsNullOrEmpty(call.Output) ? input : output;
        return tabs;
    }

    private static TabItem Payload([FeedbackId] string tabId, [FeedbackId] string fieldId, string label, string? value)
    {
        var hasValue = !string.IsNullOrEmpty(value);
        var field = new TextBox
        {
            Text = value,
            PlaceholderText = $"No {label.ToLowerInvariant()} recorded.",
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 15,
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(8),
            VerticalAlignment = VerticalAlignment.Stretch,
            Focusable = hasValue
        }.WithFeedbackId(fieldId);
        AutomationProperties.SetName(field, label);
        ScrollViewer.SetVerticalScrollBarVisibility(field, ScrollBarVisibility.Auto);
        return new TabItem { Header = label, Content = field, FontSize = 13 }.WithFeedbackId(tabId);
    }
}
