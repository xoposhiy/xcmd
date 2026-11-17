// @implements HeadlessVerification
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xcmd.Core;

namespace Xcmd.Probe.Infrastructure;

public static class PriorityBatchProbe
{
    public static async Task WaitAsync(ProbeContext context, Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("The requested feature state was not reached.");
            await Task.Delay(25); await context.SettleAsync();
        }
    }

    public static Task ChooseErrorAsync(ProbeContext context, string name)
    {
        var dialog = context.Window.OperationDialog!;
        return context.ClickAsync(dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name), target: dialog);
    }

    public static string Summary(ProbeContext context) => context.Window.Left.GetVisualDescendants().OfType<TextBlock>()
        .Single(text => Avalonia.Automation.AutomationProperties.GetAutomationId(text) == "left.summary").Text ?? "";

    public static Task CopyAsync(ProbeContext context, string name)
    {
        var entry = context.Window.Left.Rows.Single(row => row.Name == name).Entry;
        context.Window.Operations.Enqueue(new(FileOperationKind.Copy, [entry], context.RightPath));
        return context.SettleAsync();
    }
}
