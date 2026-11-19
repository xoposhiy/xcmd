// @implements SharedControls
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Xcmd.Desktop.Shared.Ui;

internal static class UiStyles
{
    public static void Install(Application app)
    {
        app.Styles.Add(new Style(s => s.Is<Window>())
        {
            Setters =
            {
                new Setter(TemplatedControl.FontFamilyProperty, new FontFamily("Segoe UI")),
                new Setter(TemplatedControl.FontSizeProperty, UiPalette.BodySize),
                new Setter(TemplatedControl.ForegroundProperty, UiPalette.Text),
                new Setter(TemplatedControl.BackgroundProperty, UiPalette.Canvas)
            }
        });
        // Static text needs a hit-test surface for feedback, including disabled content.
        app.Styles.Add(new Style(s => s.Is<TextBlock>())
        { Setters = { new Setter(TextBlock.BackgroundProperty, Brushes.Transparent) } });
        app.Styles.Add(new Style(s => s.Is<TemplatedControl>())
        { Setters = { new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(0)) } });
        app.Styles.Add(new Style(s => s.OfType<Button>())
        {
            Setters =
            {
                new Setter(Layoutable.MinHeightProperty, UiPalette.ControlHeight),
                new Setter(TemplatedControl.PaddingProperty, UiPalette.ButtonPadding),
                new Setter(TemplatedControl.FontSizeProperty, UiPalette.BodySize),
                new Setter(TemplatedControl.BackgroundProperty, UiPalette.Raised),
                new Setter(TemplatedControl.BorderBrushProperty, UiPalette.ControlBorder),
                new Setter(TemplatedControl.ForegroundProperty, UiPalette.Text)
            }
        });
        foreach (var type in new[] { typeof(TextBox), typeof(ComboBox) })
            app.Styles.Add(new Style(s => s.OfType(type))
            {
                Setters =
                {
                    new Setter(Layoutable.MinHeightProperty, UiPalette.ControlHeight),
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(8, 4)),
                    new Setter(TemplatedControl.BorderBrushProperty, UiPalette.ControlBorder)
                }
            });
        app.Styles.Add(new Style(s => s.OfType<ListBox>())
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, UiPalette.Surface),
                new Setter(TemplatedControl.ForegroundProperty, UiPalette.Text),
                new Setter(TemplatedControl.BorderBrushProperty, UiPalette.Divider)
            }
        });
        app.Styles.Add(new Style(s => s.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Setter(Layoutable.MinHeightProperty, UiPalette.RowHeight),
                new Setter(TemplatedControl.PaddingProperty, UiPalette.CellPadding),
                new Setter(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center)
            }
        });
        AddPrimary(app, s => s.OfType<Button>().PropertyEquals(Button.IsDefaultProperty, true));
        AddPrimary(app, s => s.OfType<Button>().Class("primary"));
        AddQuiet(app);
        AddListRows(app);
        AddResources(app);
    }

    private static void AddListRows(Application app)
    {
        app.Styles.Add(new Style(s => s.OfType<ListBoxItem>())
        { Setters = { new Setter(Control.FocusAdornerProperty, null) } });
        foreach (var selected in new[] { false, true })
            app.Styles.Add(new Style(s => s.OfType<ListBoxItem>().PropertyEquals(ListBoxItem.IsSelectedProperty, selected)
                .Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
            {
                Setters =
                {
                    new Setter(ContentPresenter.BackgroundProperty, selected ? UiPalette.Cursor : Brushes.Transparent),
                    new Setter(ContentPresenter.ForegroundProperty, UiPalette.Text)
                }
            });
    }

    private static void AddPrimary(Application app, Func<Selector?, Selector> selector) => app.Styles.Add(new Style(selector)
    {
        Setters =
        {
            new Setter(TemplatedControl.BackgroundProperty, UiPalette.Primary),
            new Setter(TemplatedControl.BorderBrushProperty, UiPalette.Focus),
            new Setter(TemplatedControl.ForegroundProperty, Brushes.White)
        }
    });

    private static void AddQuiet(Application app)
    {
        app.Styles.Add(new Style(s => s.OfType<Button>().Class("quiet"))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
                new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0)),
                new Setter(TemplatedControl.ForegroundProperty, UiPalette.Secondary)
            }
        });
        app.Styles.Add(new Style(s => s.OfType<Button>().Class(":disabled"))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, Brush.Parse("#202938")),
                new Setter(TemplatedControl.BorderBrushProperty, Brush.Parse("#536074")),
                new Setter(TemplatedControl.ForegroundProperty, Brush.Parse("#8E9AAA"))
            }
        });
    }

    private static void AddResources(Application app)
    {
        var dark = new ResourceDictionary();
        dark["TextControlBackground"] = UiPalette.Canvas;
        dark["TextControlForeground"] = UiPalette.Text;
        dark["TextControlPlaceholderForeground"] = UiPalette.Secondary;
        dark["TextControlBorderBrush"] = UiPalette.ControlBorder;
        dark["TextControlBorderBrushFocused"] = UiPalette.Focus;
        dark["ListBoxBackground"] = UiPalette.Surface;
        dark["ListBoxItemBackgroundSelected"] = UiPalette.Cursor;
        dark["ListBoxItemBackgroundSelectedPointerOver"] = UiPalette.Cursor;
        dark["TableViewRowBackgroundSelected"] = UiPalette.Cursor;
        app.Resources.ThemeDictionaries[ThemeVariant.Dark] = dark;
    }
}
