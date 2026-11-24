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
    private readonly SessionStatisticsBar statistics = new SessionStatisticsBar().WithFeedbackId("AgentSessionView.statistics");
    private readonly TextBlock status = new TextBlock { Foreground = UiPalette.Secondary, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = UiPalette.MetaSize, VerticalAlignment = VerticalAlignment.Center }.WithFeedbackId("AgentSessionView.status");
    private readonly DispatcherTimer updatedTimeTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private event Action? UpdateTimes;
    private bool updatingRows;
    private bool userSelected;

    internal AgentSessionView()
    {
        poll.Tick += PollRecent;
        updatedTimeTimer.Tick += (_, _) => UpdateTimes?.Invoke();
        Background = UiPalette.AgentSessions;
        RowDefinitions = new RowDefinitions("34,*,29");
        Table.Background = Background;
        Table.SelectionChanged += (_, _) =>
        {
            if (updatingRows) return;
            userSelected = true;
            ShowStatistics();
        };
        AddColumn("Project", new GridLength(1, GridUnitType.Star), row => Cell(row.ProjectName, tip: row.Directory)).MinWidth = 66;
        AddColumn("Topic", new GridLength(2, GridUnitType.Star), TopicCell);
        AddColumn("Agent", new GridLength(74), AgentCell);
        AddColumn("Size", new GridLength(82), row => row.TranscriptBytes is { } bytes
            ? Cell($"{bytes / 1024m:N0} K", tip: $"Main transcript: {bytes:N0} bytes")
            : Cell("—", UiPalette.Secondary, "Transcript size unavailable"), TextAlignment.Right);
        AddColumn("Updated", new GridLength(1.5, GridUnitType.Star), UpdatedCell).MaxWidth = 132;
        Children.Add(Heading);
        SetRow(Table, 1); Children.Add(Table);
        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 }.WithFeedbackId("AgentSessionView.footerLine");
        line.Children.Add(statistics);
        SetColumn(status, 1); line.Children.Add(status);
        var footer = new Border
        {
            Child = line,
            Padding = UiPalette.CellPadding,
            BorderBrush = UiPalette.Divider,
            BorderThickness = new Thickness(0, 1, 0, 0),
            ClipToBounds = true
        }.WithFeedbackId("AgentSessionView.footer");
        SetRow(footer, 2); Children.Add(footer);
    }

    private TableViewColumn AddColumn(string title, GridLength width, Func<AgentSession, TextBlock> cell, TextAlignment alignment = TextAlignment.Left)
    {
        var header = new TextBlock
        {
            Text = title,
            TextAlignment = alignment,
            Margin = UiPalette.CellPadding,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold
        }.WithFeedbackId("AgentSessionView.columnHeader");
        var column = new TableViewColumn
        {
            Header = header,
            Width = width,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            CellTemplate = new FuncDataTemplate<AgentSession>((row, _) =>
            {
                if (row == null) return null;
                var text = cell(row);
                text.TextAlignment = alignment;
                return text;
            })
        };
        Table.Columns.Add(column);
        return column;
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
        ShowStatistics();
    }

    private void ShowStatistics() => _ = statistics.ShowAsync(Table.SelectedItem as AgentSession, source);

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
