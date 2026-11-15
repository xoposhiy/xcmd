using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;
using Xcmd.Core;

namespace Xcmd.Desktop;

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

    internal void SetSyntaxTheme(ReaderTheme theme)
    {
        palette = SyntaxPalette.Create(theme);
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
