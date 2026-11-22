// @implements SharedControls
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Styling;

namespace Xcmd.Desktop.Features.Panels;

internal static class PanelRowTheme
{
    public static ControlTheme Create(bool fileRows) => new(typeof(TableViewRow))
    {
        Setters =
        {
            new Setter(Layoutable.MinHeightProperty, UiPalette.RowHeight),
            new Setter(TemplatedControl.PaddingProperty, new Thickness(0)),
            new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0)),
            new Setter(Control.FocusAdornerProperty, null),
            new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<TableViewRow>((row, scope) =>
            {
                var cells = new TableViewCellsPresenter { Name = "PART_CellsPresenter" }.WithFeedbackId("FileRowTheme.cells");
                if (fileRows) cells.Bind(Visual.OpacityProperty, new Binding(nameof(FileRow.ContentOpacity)));
                scope.Register("PART_CellsPresenter", cells);
                var border = new Border { Name = "Border", Child = cells }.WithFeedbackId("FileRowTheme.border");
                scope.Register("Border", border);
                border.Bind(Border.BackgroundProperty, fileRows ? new Binding(nameof(FileRow.RowBackground))
                    : new Binding(nameof(TableViewRow.Background)) { Source = row });
                return border;
            }))
        }
    };
}
