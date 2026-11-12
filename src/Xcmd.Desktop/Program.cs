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
            var window = new CommanderWindow();
            var lifecycle = LifecycleFactory?.Invoke(window, settings) ?? new WorkspaceLifecycle(window, new SessionStore(settings.SessionPath), ReleaseCatalog.ForApplication());
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
            await lifecycle.InitializeAsync(settings.Directories, requireSavedSession: settings.WaitForProcess != null);
            TaskbarIdentity.Configure(window);
            await RestartProcess.WaitForPreviousAsync(settings);
            if (settings.WaitForProcess == null) window.ShowActivated = true;
            desktop.MainWindow = window;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            WindowPresenter(window);
            window.RestoreView();
        }
        catch (Exception error)
        {
            lifecycle.Dispose();
            await StartupDiagnostics.SaveAsync(settings.SessionPath, error);
            desktop.Shutdown(1);
        }
    }
}
