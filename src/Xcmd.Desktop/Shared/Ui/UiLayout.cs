// @implements SharedControls
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Xcmd.Desktop.Shared.Ui;

internal static class UiLayout
{
    public static TextBlock Error()
    {
        var text = new TextBlock { Foreground = UiPalette.Error, TextWrapping = TextWrapping.Wrap, IsVisible = false }.WithFeedbackId("UiLayout.text");
        text.PropertyChanged += (_, change) =>
        {
            if (change.Property == TextBlock.TextProperty) text.IsVisible = !string.IsNullOrEmpty(text.Text);
        };
        return text;
    }

    public static WrapPanel Actions(params Control[] actions)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }.WithFeedbackId("UiLayout.row");
        foreach (var action in actions)
        {
            action.Margin = new Thickness(8, 0, 0, 4);
            row.Children.Add(action);
        }
        return row;
    }

    public static void Table(TableView table, bool panel = false)
    {
        table.FontSize = UiPalette.BodySize;
        table.Background = panel ? Brushes.Transparent : UiPalette.Surface;
        table.ClipToBounds = true;
        table.Resources["TableViewRowPadding"] = new Thickness(0);
        table.Resources["TableViewColumnResizerMargin"] = new Thickness(0);
        table.Styles.Add(new Style(s => s.OfType<TableViewRow>())
        {
            Setters = { new Setter(Layoutable.MinHeightProperty, UiPalette.RowHeight), new Setter(TemplatedControl.PaddingProperty, new Thickness(0)) }
        });
        table.Styles.Add(new Style(s => s.OfType<TableViewCell>())
        {
            Setters = { new Setter(TemplatedControl.PaddingProperty, UiPalette.CellPadding), new Setter(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center) }
        });
        table.Styles.Add(new Style(s => s.OfType<TableViewColumnHeader>())
        {
            Setters =
            {
                new Setter(Layoutable.MinHeightProperty, UiPalette.RowHeight),
                new Setter(TemplatedControl.BackgroundProperty, UiPalette.Raised),
                new Setter(TemplatedControl.ForegroundProperty, UiPalette.Text),
                new Setter(TemplatedControl.FontSizeProperty, UiPalette.BodySize),
                new Setter(TemplatedControl.PaddingProperty, panel ? new Thickness(0) : UiPalette.CellPadding),
                new Setter(ContentControl.HorizontalContentAlignmentProperty, panel ? (object)HorizontalAlignment.Stretch
                    : new Binding("Column.HorizontalContentAlignment") { RelativeSource = new RelativeSource(RelativeSourceMode.Self) }),
                new Setter(ContentControl.VerticalContentAlignmentProperty, panel ? VerticalAlignment.Stretch : VerticalAlignment.Center)
            }
        });
    }
}
