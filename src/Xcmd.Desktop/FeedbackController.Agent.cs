// @implements KeepFeedback
using Avalonia.Controls;
using Xcmd.Core;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

public sealed partial class FeedbackController
{
    public FeedbackAgent Agent { get; private set; }
    internal event Func<Task>? AgentChanged;

    internal void RestoreAgent(FeedbackAgent agent) => Agent = Enum.IsDefined(agent) ? agent : FeedbackAgent.Codex;

    private SplitButton BuildAgentButton(TextBlock errorText)
    {
        var codex = new MenuItem { Header = "Run Codex" }.WithFeedbackId("FeedbackController.runCodex");
        var claude = new MenuItem { Header = "Run Claude Code" }.WithFeedbackId("FeedbackController.runClaudeCode");
        var button = new SplitButton
        {
            Content = "Run " + FeedbackAgentLauncher.Name(Agent),
            Flyout = new MenuFlyout { ItemsSource = new[] { codex, claude } }
        }.WithFeedbackId("FeedbackController.runAgent");
        ToolTip.SetTip(button, "Open a console agent to fix a batch of related issues.");

        async Task RunAsync(FeedbackAgent selected)
        {
            errorText.Text = null;
            button.IsEnabled = false;
            try
            {
                if (Agent != selected)
                {
                    Agent = selected;
                    button.Content = "Run " + FeedbackAgentLauncher.Name(Agent);
                    if (AgentChanged != null) await AgentChanged();
                }
                var store = Store ?? throw new IOException("The issue store has not been configured.");
                await owner.LaunchExternalAsync(FeedbackAgentLauncher.Prepare(selected, store));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                errorText.Text = error.Message;
                owner.Trace.Add(error.ToString());
            }
            finally { button.IsEnabled = true; }
        }

        button.Click += (_, _) => owner.Execute(() => RunAsync(Agent));
        codex.Click += (_, _) => owner.Execute(() => RunAsync(FeedbackAgent.Codex));
        claude.Click += (_, _) => owner.Execute(() => RunAsync(FeedbackAgent.ClaudeCode));
        return button;
    }
}
