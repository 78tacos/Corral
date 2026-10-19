using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace Corral;

public partial class App : Application
{
    private const string QuitEventName = "Corral.Quit";

    private Mutex? _singleInstance;
    private EventWaitHandle? _quitSignal;
    private FenceManager? _manager;
    private TrayController? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        bool quitRequested = e.Args.Contains("--quit");

        _singleInstance = new Mutex(true, "Corral.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            // `Corral.exe --quit` asks the running copy to exit cleanly (used by the installer/uninstaller).
            if (quitRequested)
            {
                try
                {
                    using var signal = EventWaitHandle.OpenExisting(QuitEventName);
                    signal.Set();
                }
                catch (Exception ex) when (ex is WaitHandleCannotBeOpenedException or UnauthorizedAccessException)
                {
                    // Nothing to signal.
                }
            }
            Shutdown();
            return;
        }

        if (quitRequested)
        {
            Shutdown(); // nothing is running, nothing to quit
            return;
        }

        // `Corral.exe --config <file>` lets you run a throwaway profile (handy for testing).
        string? configPath = null;
        for (int i = 0; i < e.Args.Length - 1; i++)
            if (e.Args[i] == "--config") configPath = e.Args[i + 1];

        var store = new ConfigStore(configPath);
        Log.FilePath = System.IO.Path.Combine(store.Directory, "corral.log");

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Write("Unhandled UI exception", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Write("Fatal exception", args.ExceptionObject as Exception);
            Native.SetDesktopIconsVisible(true);
        };

        _manager = new FenceManager(store);
        _manager.Start();
        _tray = new TrayController(_manager, ExitApp);

        _quitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEventName);
        var waiter = new Thread(() =>
        {
            try
            {
                _quitSignal.WaitOne();
                Dispatcher.BeginInvoke(ExitApp);
            }
            catch (ObjectDisposedException)
            {
                // App is already shutting down.
            }
        })
        { IsBackground = true, Name = "QuitSignal" };
        waiter.Start();
    }

    private void ExitApp()
    {
        _tray?.Dispose();
        _manager?.Shutdown();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _quitSignal?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
