// @implements FeedbackIdentity
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Xcmd.Desktop;

public static class FeedbackTarget
{
    public static readonly AttachedProperty<string?> IdProperty = AvaloniaProperty.RegisterAttached<Control, string?>("FeedbackId", typeof(FeedbackTarget));

    public static T WithFeedbackId<T>(this T control, string id) where T : Control
    {
        control.SetValue(IdProperty, id);
        return control;
    }

    public static string Id(Control control) => control.GetSelfAndVisualAncestors().OfType<Control>()
        .Select(item => item.GetValue(IdProperty)).FirstOrDefault(id => id != null)
        ?? throw new InvalidOperationException("Feedback target has no application identity: " + control.GetType().Name);
}
