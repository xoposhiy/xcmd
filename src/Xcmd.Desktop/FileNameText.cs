// @implements JumpToName
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;

namespace Xcmd.Desktop;

internal sealed class FileNameText : TextBlock
{
    public static readonly StyledProperty<int> MatchStartProperty = AvaloniaProperty.Register<FileNameText, int>(nameof(MatchStart));
    public static readonly StyledProperty<int> MatchLengthProperty = AvaloniaProperty.Register<FileNameText, int>(nameof(MatchLength));
    public FileNameText() => Background = Brushes.Transparent;
    public int MatchStart { get => GetValue(MatchStartProperty); set => SetValue(MatchStartProperty, value); }
    public int MatchLength { get => GetValue(MatchLengthProperty); set => SetValue(MatchLengthProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MatchStartProperty || change.Property == MatchLengthProperty) InvalidateTextLayout();
    }

    protected override TextLayout CreateTextLayout(string? text)
    {
        if (text == null || MatchLength == 0) return base.CreateTextLayout(text);
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var size = GetMaxSizeFromConstraint();
        var highlight = new GenericTextRunProperties(typeface, FontSize, TextDecorations, UiPalette.Text, UiPalette.Match);
        return new TextLayout(text, typeface, FontSize, Foreground, TextAlignment, TextWrapping, TextTrimming, TextDecorations, FlowDirection,
            size.Width, size.Height, LineHeight, LetterSpacing, MaxLines, FontFeatures,
            new[] { new ValueSpan<TextRunProperties>(MatchStart, MatchLength, highlight) }, textRunCache: null);
    }
}
