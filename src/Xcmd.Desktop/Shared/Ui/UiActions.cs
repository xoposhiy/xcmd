// @implements SharedControls
namespace Xcmd.Desktop.Shared.Ui;

internal sealed record UiActions(Action<Func<Task>> Execute, Action<string> SetStatus, Action<string?> SetError, Action<string> Trace);
