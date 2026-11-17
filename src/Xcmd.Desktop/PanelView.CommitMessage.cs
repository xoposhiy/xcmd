// @implements ReadCommitMessage
using Avalonia.Controls;
using Avalonia.Input;

namespace Xcmd.Desktop;

public sealed partial class PanelView
{
    private GitCommitMessageView? commitMessage;
    public TextBox CommitMessage => commitMessage!.Editor;
    public bool IsCommitMessageLoading => commitMessage?.IsLoading == true;
    public bool IsCommitMessageVisible => commitMessage?.IsVisible == true;
    public bool HasCommitMessageFocus => commitMessage?.IsKeyboardFocusWithin == true;

    private void BuildCommitMessageArea(Grid content)
    {
        commitMessage = new GitCommitMessageView(Id, () => Activated?.Invoke(this)).WithFeedbackId("PanelView.commitMessageArea");
        commitMessage.Attach(content);
    }

    private void UpdateCommitMessage() => commitMessage?.Update(Session, Current?.Entry);
    public void HandleCommitMessageKey(KeyEventArgs e) => commitMessage?.HandleKey(e, () => FocusCurrent());
}
