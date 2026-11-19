// @implements ReleaseHandoff, DiagnoseFailures
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Xcmd.Infrastructure;

namespace Xcmd.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args is ["issues", ..]) return IssueCli.Run(args[1..], Console.Out, Console.Error);
        if (args is ["--console-window", var processId, var report]) return ConsoleWindowLookup.RunHelper(processId, report);
        TaskbarIdentity.ConfigureProcess();
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseDesktopPlatform();
}

public sealed class App : Application
{
    public Func<CommanderWindow, LaunchSettings, WorkspaceLifecycle>? LifecycleFactory { get; init; }
    public Action<CommanderWindow> WindowPresenter { get; init; } = window => window.Show();
    public Task StartupTask { get; private set; } = Task.CompletedTask;
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
        UiStyles.Install(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args ?? [];
            var settings = LaunchSettings.Parse(args);
            var window = new CommanderWindow().WithFeedbackId("Program.window");
            var lifecycle = LifecycleFactory?.Invoke(window, settings) ?? new WorkspaceLifecycle(window, new SessionStore(settings.SessionPath), ReleaseCatalog.ForApplication(), issues: IssueStore.ForApplication());
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.UIThread.Post(async () =>
            {
                StartupTask = StartWindowAsync(desktop, window, lifecycle, settings);
                await StartupTask;
            });
        }
        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartWindowAsync(IClassicDesktopStyleApplicationLifetime desktop, CommanderWindow window, WorkspaceLifecycle lifecycle, LaunchSettings settings)
    {
        try
        {
            var handoff = settings.WaitForProcess == null ? null : settings.HandoffPath;
            await lifecycle.InitializeAsync(settings.Directories, requireSavedSession: handoff != null, handoffPath: handoff);
            TaskbarIdentity.Configure(window);
            await RestartProcess.WaitForPreviousAsync(settings);
            if (settings.WaitForProcess == null) window.ShowActivated = true;
            desktop.MainWindow = window;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            WindowPresenter(window);
            window.RestoreView();
            if (handoff != null) lifecycle.CompleteRestart(handoff);
        }
        catch (Exception error)
        {
            lifecycle.Dispose();
            await StartupDiagnostics.SaveAsync(settings.SessionPath, error);
            desktop.Shutdown(1);
        }
    }
}
