// @implements NavigatePanels, ReadablePanels, ViewRecentAgentSessions
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Xcmd.Core;

namespace Xcmd.Desktop.Features.Panels;

internal sealed partial class AgentSessionView : Grid
{
    internal TextBlock Heading { get; } = new TextBlock { Margin = UiPalette.CellPadding, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center }.WithFeedbackId("AgentSessionView.heading");
    internal PanelTable Table { get; } = new PanelTable().WithFeedbackId("AgentSessionView.table");
    private readonly TextBlock status = new TextBlock { Margin = new Thickness(8, 4), Foreground = UiPalette.Secondary, TextWrapping = TextWrapping.Wrap, FontSize = UiPalette.MetaSize }.WithFeedbackId("AgentSessionView.status");
    private readonly DispatcherTimer updatedTimeTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private event Action? UpdateTimes;
    private bool updatingRows;
    private bool userSelected;

    internal AgentSessionView()
    {
        poll.Tick += PollRecent;
        updatedTimeTimer.Tick += (_, _) => UpdateTimes?.Invoke();
        Background = UiPalette.AgentSessions;
        RowDefinitions = new RowDefinitions("34,*,Auto");
        Table.Background = Background;
        Table.SelectionChanged += (_, _) => { if (!updatingRows) userSelected = true; };
        AddColumn("Project", new GridLength(1, GridUnitType.Star), row => Cell(row.ProjectName, tip: row.Directory));
        AddColumn("Topic", new GridLength(2, GridUnitType.Star), TopicCell);
        AddColumn("Agent", new GridLength(96), row => Cell(AgentSession.AgentName(row.Agent),
            row.Agent == SessionAgent.Codex ? UiPalette.Focus : UiPalette.Mark));
        AddColumn("Updated", new GridLength(132), UpdatedCell);
        Children.Add(Heading);
        SetRow(Table, 1); Children.Add(Table);
        SetRow(status, 2); Children.Add(status);
    }

    private void AddColumn(string title, GridLength width, Func<AgentSession, TextBlock> cell)
    {
        var header = new TextBlock
        {
            Text = title,
            Margin = UiPalette.CellPadding,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold
        }.WithFeedbackId("AgentSessionView.columnHeader");
        Table.Columns.Add(new TableViewColumn
        {
            Header = header,
            Width = width,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            CellTemplate = new FuncDataTemplate<AgentSession>((row, _) => row == null ? null : cell(row))
        });
    }

    private void UpdateRows(Action change, bool preserveSelection)
    {
        var selected = Table.SelectedItem as AgentSession;
        var index = Table.SelectedIndex;
        var focus = Table.IsKeyboardFocusWithin;
        updatingRows = true;
        try
        {
            change();
            var keep = (preserveSelection || userSelected) && selected != null;
            var current = keep ? sessions.Find(selected) ?? sessions.Rows.ElementAtOrDefault(Math.Clamp(index, 0, Math.Max(0, sessions.Rows.Count - 1)))
                : sessions.Rows.FirstOrDefault();
            Table.SelectedItem = current;
            if (focus && selected != current) Table.FocusRow(Table.SelectedIndex, scrollIntoView: false);
        }
        finally { updatingRows = false; }
    }

    private static TextBlock TopicCell(AgentSession row)
    {
        var color = row.Activity switch
        {
            SessionActivity.Working => UiPalette.Text,
            SessionActivity.Waiting => UiPalette.Mark,
            SessionActivity.Failed => UiPalette.Error,
            _ => UiPalette.Secondary
        };
        var topic = row.Activity == SessionActivity.Unknown ? row.DisplayTopic : $"{row.Activity} · {row.DisplayTopic}";
        return Cell(topic, color);
    }

    private static TextBlock Cell(string value, IBrush? color = null, string? tip = null)
    {
        var text = new TextBlock
        {
            Text = value,
            MaxLines = 1,
            TextTrimming = TextTrimming.CharacterEllipsis
        }.WithFeedbackId("AgentSessionView.cell");
        if (color != null) text.Foreground = color;
        ToolTip.SetTip(text, tip ?? value);
        return text;
    }

    private TextBlock UpdatedCell(AgentSession row)
    {
        var text = Cell(row.LocalUpdatedTime);
        text.AttachedToVisualTree += (_, _) =>
        {
            Refresh();
            UpdateTimes += Refresh;
        };
        text.DetachedFromVisualTree += (_, _) => UpdateTimes -= Refresh;
        return text;

        void Refresh() => text.Text = row.DisplayUpdatedTime(DateTimeOffset.UtcNow);
    }
}
