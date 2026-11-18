// @implements FeedbackIdentity, ReportControl
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

    public static string Id(Control control) => Ancestors(control)
        .Select(item => item.GetValue(IdProperty)).FirstOrDefault(id => id != null)
        ?? throw new InvalidOperationException("Feedback target has no application identity: " + control.GetType().Name);

    internal static IEnumerable<Control> Ancestors(Control control)
    {
        for (Visual? visual = control; visual != null; visual = visual.GetVisualParent())
        {
            if (visual is OverlayPopupHost { Parent: Popup popup })
            {
                yield return popup;
                visual = popup.PlacementTarget ?? popup.Parent as Control;
                if (visual == null) yield break;
            }
            if (visual is Control ancestor) yield return ancestor;
        }
    }
}
