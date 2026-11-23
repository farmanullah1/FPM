using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PCManager.Cli;
using PCManager.Core;
using PCManager.Services;
using PCManager.Services.Abstractions;
using PCManager.Services.DependencyInjection;
using PCManager.ViewModels;
using PCManager.Views;

namespace PCManager;

/// <summary>
/// Composition root.
///
/// Identity rules enforced here:
///   - AppUserModelID "FarmanullahPCManager.Desktop.8A4F2C1E" (separate taskbar identity),
///   - single-instance mutex "Global\FarmanullahPCManager_SingleInstance_8A4F2C1E",
///   - tray tooltip "Farmanullah PC Manager",
///   - all state under %LOCALAPPDATA%\FarmanullahPCManager.
/// No URI protocol is registered anywhere in the app.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;
    private Mutex? _singleInstance;
    private System.Windows.Forms.NotifyIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Own taskbar group / notification identity.
        AppIdentity.ApplyAppUserModelId(AppInfo.AppUserModelId);

        if (!TryAcquireSingleInstance())
        {
            // Another copy is already running; do not start a second one.
            Shutdown();
            return;
        }

        // Closing the main window exits the app even if the floating toolbar is open.
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        var collection = new ServiceCollection();

        collection.AddPCManagerServices();

        // WPF-only services.
        collection.AddSingleton<IThemeService, ThemeService>();
        collection.AddSingleton<IFloatingToolbarHost, FloatingToolbarHost>();
        collection.AddSingleton<IUserConfirmationService, MessageBoxConfirmationService>();

        // Navigation is a broker so pages never reference the shell.
        collection.AddSingleton<NavigationService>();
        collection.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());

        // Page view models.
        collection.AddSingleton<HomeViewModel>();
        collection.AddSingleton<ProtectionViewModel>();
        collection.AddSingleton<StorageViewModel>();
        collection.AddSingleton<AppsViewModel>();
        collection.AddSingleton<ToolboxViewModel>();
        collection.AddSingleton<MemoryViewModel>();
        collection.AddSingleton<SettingsViewModel>();

        collection.AddSingleton<MainViewModel>();
        collection.AddSingleton<MainWindow>();

        // Floating toolbar window + its view model.
        collection.AddSingleton<FloatingToolbarViewModel>();
        collection.AddSingleton<FloatingToolbarWindow>();

        _services = collection.BuildServiceProvider();

        // Run legacy data migration preflight (FarmanullahPCManager -> FPM)
        try
        {
            var migrationService = _services.GetService<ILegacyIdentityMigrationService>();
            migrationService?.MigrateIfNecessary();
        }
        catch (Exception ex)
        {
            _services.GetService<IAppLogger>()?.Error($"Legacy migration failed: {ex.Message}", ex);
        }

        // Cleanup CLI shares the exact same engine as the UI. --scan deletes nothing;
        // --clean requires an explicit --ids list.
        if (CleanupCli.IsCleanupCommand(e.Args))
        {
            // Run on the thread pool: awaiting on the UI thread would capture the WPF
            // dispatcher context and deadlock before the first continuation.
            int exitCode = Task.Run(() => CleanupCli.RunAsync(
                    e.Args,
                    _services.GetRequiredService<ICleanupScanner>(),
                    _services.GetRequiredService<ICleanupCommandParser>(),
                    _services.GetRequiredService<ICleanupExecutor>(),
                    _services.GetRequiredService<ICleanupLog>(),
                    _services.GetRequiredService<IRestorePointService>()))
                .GetAwaiter()
                .GetResult();

            try
            {
                Console.Out.Flush();
            }
            catch
            {
                // No console attached.
            }

            Environment.Exit(exitCode);
            return;
        }

        EnableDiagnostics();

        CreateTrayIcon();

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();

        _services.GetRequiredService<IAppLogger>()
            .Info($"{AppInfo.ProductName} started (pid {Environment.ProcessId}).");
    }

    /// <summary>
    /// Global\... mutex: a machine-wide single-instance guard, entirely distinct from
    /// Microsoft PC Manager's, so the two utilities can run at the same time.
    /// </summary>
    private bool TryAcquireSingleInstance()
    {
        try
        {
            _singleInstance = new Mutex(initiallyOwned: true, AppInfo.SingleInstanceMutexName, out bool created);

            if (created)
            {
                return true;
            }

            _singleInstance.Dispose();
            _singleInstance = null;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // A locked-down token cannot create a Global object; fall back to a session name.
            _singleInstance = new Mutex(initiallyOwned: true, "FarmanullahPCManager_SingleInstance_8A4F2C1E", out bool created);
            return created;
        }
    }

    private void CreateTrayIcon()
    {
        try
        {
            _tray = new System.Windows.Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Shield,
                Text = AppInfo.ProductName, // shell tooltip, max 63 characters
                Visible = true
            };

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Open Farmanullah PC Manager", null, (_, _) => ShowMainWindow());
            menu.Items.Add("Boost now", null, async (_, _) => await TrayBoostAsync());
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => Shutdown());
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += (_, _) => ShowMainWindow();
        }
        catch (Exception ex)
        {
            _services?.GetService<IAppLogger>()?.Error("Tray icon creation failed.", ex);
        }
    }

    private void ShowMainWindow()
    {
        if (MainWindow is null)
        {
            return;
        }

        MainWindow.Show();

        if (MainWindow.WindowState == WindowState.Minimized)
        {
            MainWindow.WindowState = WindowState.Normal;
        }

        MainWindow.Activate();
    }

    private async Task TrayBoostAsync()
    {
        if (_services?.GetService<IPcBoostService>() is not { } boost)
        {
            return;
        }

        try
        {
            var trim = await boost.OptimizeAsync();
            _tray?.ShowBalloonTip(3000, AppInfo.ProductName,
                $"{trim.WorkingSetsTrimmed} working sets trimmed. Available memory {trim.AvailableDeltaText}.",
                System.Windows.Forms.ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _services?.GetService<IAppLogger>()?.Error("Tray boost failed.", ex);
        }
    }

    /// <summary>
    /// Diagnostics switch (off by default): PCMANAGER_TRACE=&lt;file&gt; records WPF binding
    /// warnings there; unhandled exceptions always go to the app log.
    /// </summary>
    private void EnableDiagnostics()
    {
        string? path = Environment.GetEnvironmentVariable("PCMANAGER_TRACE");

        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                PresentationTraceSources.Refresh();
                var listener = new TextWriterTraceListener(path) { TraceOutputOptions = TraceOptions.None };
                PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
                PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
                PresentationTraceSources.ResourceDictionarySource.Listeners.Add(listener);
                PresentationTraceSources.ResourceDictionarySource.Switch.Level = SourceLevels.Warning;
                Trace.AutoFlush = true;
                listener.Flush();
            }
            catch
            {
                // Diagnostics must never prevent the app from starting.
            }
        }

        DispatcherUnhandledException += (_, args) =>
            _services?.GetService<IAppLogger>()?.Error("Unhandled UI exception.", args.Exception);

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                _services?.GetService<IAppLogger>()?.Error("Unhandled exception.", exception);
            }
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.GetService<IAppLogger>()?.Info($"{AppInfo.ProductName} exiting.");

        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }

        _services?.Dispose();
        _services = null;

        if (_singleInstance is not null)
        {
            try { _singleInstance.ReleaseMutex(); } catch { /* not owned */ }
            _singleInstance.Dispose();
            _singleInstance = null;
        }

        base.OnExit(e);
    }
}
