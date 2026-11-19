// @implements SyntaxSpans, ReadLargeFiles
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;
using Xcmd.Core;
using Xcmd.Desktop.Features.Viewer;

namespace Xcmd.Desktop.Features.Viewer.Syntax;

public sealed class SyntaxTextBox : TextBox
{
    private SyntaxTextPresenter? syntaxPresenter;
    private IReadOnlyDictionary<SyntaxKind, IBrush> palette = SyntaxPalette.Create(ReaderThemes.Default);
    public SyntaxDocument? Syntax { get; private set; }
    protected override Type StyleKeyOverride => typeof(TextBox);

    public SyntaxTextBox()
    {
        Template = new FuncControlTemplate<SyntaxTextBox>((box, scope) =>
        {
            syntaxPresenter = new SyntaxTextPresenter { Name = "PART_TextPresenter" }.WithFeedbackId("SyntaxTextBox.presenter");
            scope.Register("PART_TextPresenter", syntaxPresenter);
            BindPresenter(box, syntaxPresenter);
            syntaxPresenter.SetSyntax(Syntax, palette);
            var scroll = new ScrollViewer { Name = "PART_ScrollViewer", Content = syntaxPresenter }.WithFeedbackId("SyntaxTextBox.scroll");
            scope.Register("PART_ScrollViewer", scroll);
            scroll.Bind(ScrollViewer.HorizontalScrollBarVisibilityProperty, box.GetObservable(ScrollViewer.HorizontalScrollBarVisibilityProperty));
            scroll.Bind(ScrollViewer.VerticalScrollBarVisibilityProperty, box.GetObservable(ScrollViewer.VerticalScrollBarVisibilityProperty));
            var border = new Border { Name = "PART_BorderElement", Child = scroll }.WithFeedbackId("SyntaxTextBox.border");
            scope.Register("PART_BorderElement", border);
            border.Bind(BackgroundProperty, box.GetObservable(BackgroundProperty));
            border.Bind(PaddingProperty, box.GetObservable(PaddingProperty));
            return border;
        });
    }

    public void SetSyntax(SyntaxDocument? value)
    {
        Syntax = value;
        syntaxPresenter?.SetSyntax(value, palette);
    }

    internal (int First, int End) VisibleCharacters(double top, double bottom)
    {
        if (syntaxPresenter == null || bottom <= top) return (0, 0);
        var positions = syntaxPresenter.LinePositions;
        var lines = positions.Use(syntaxPresenter.TextLayout);
        if (lines.Count == 0) return (0, 0);
        var last = lines[positions.At(Math.BitDecrement(bottom))];
        return (lines[positions.At(top)].FirstTextSourceIndex,
            Math.Min(last.FirstTextSourceIndex + last.Length, Text?.Length ?? 0));
    }

    internal void SetSyntaxTheme(ReaderTheme theme, IReadOnlyList<Color>? highlights)
    {
        palette = SyntaxPalette.Create(theme, highlights);
        syntaxPresenter?.SetSyntax(Syntax, palette);
    }

    private static void BindPresenter(TextBox box, TextPresenter presenter)
    {
        presenter.Bind(TextPresenter.TextProperty, new Binding(nameof(Text)) { Source = box, Mode = BindingMode.TwoWay });
        presenter.Bind(TextPresenter.CaretIndexProperty, box.GetObservable(CaretIndexProperty));
        presenter.Bind(TextPresenter.SelectionStartProperty, box.GetObservable(SelectionStartProperty));
        presenter.Bind(TextPresenter.SelectionEndProperty, box.GetObservable(SelectionEndProperty));
        presenter.Bind(TextPresenter.TextWrappingProperty, box.GetObservable(TextWrappingProperty));
        presenter.Bind(TextPresenter.SelectionBrushProperty, box.GetObservable(SelectionBrushProperty));
        presenter.Bind(TextPresenter.SelectionForegroundBrushProperty, box.GetObservable(SelectionForegroundBrushProperty));
        presenter.Bind(TextPresenter.CaretBrushProperty, box.GetObservable(CaretBrushProperty));
        presenter.Bind(TextPresenter.CaretBlinkIntervalProperty, box.GetObservable(CaretBlinkIntervalProperty));
    }
}
