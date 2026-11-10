using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Styling;

namespace Xcmd.Desktop;

internal static class FileRowTheme
{
    public static ControlTheme Create() => new(typeof(TableViewRow))
    {
        Setters =
        {
            new Setter(Layoutable.MinHeightProperty, 26d),
            new Setter(TemplatedControl.PaddingProperty, new Thickness(0)),
            new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0)),
            new Setter(Control.FocusAdornerProperty, null),
            new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<TableViewRow>((_, scope) =>
            {
                var cells = new TableViewCellsPresenter { Name = "PART_CellsPresenter" };
                scope.Register(cells.Name, cells);
                var border = new Border { Name = "Border", Child = cells };
                scope.Register(border.Name, border);
                border.Bind(Border.BackgroundProperty, new Binding(nameof(FileRow.RowBackground)));
                return border;
            }))
        }
    };
}
