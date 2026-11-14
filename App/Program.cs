using System.ServiceProcess;
using System.Windows;

namespace AAA;

static class Program
{
  [STAThread]
  static int Main(string[] args)
  {
    if (args.Any(a => a.Equals("--service", StringComparison.OrdinalIgnoreCase)))
    {
      ServiceBase.Run(new WatchdogService());
      return 0;
    }
    // הרצת הוואצ'דוג בחלון קונסול (לבדיקות בלבד)
    if (args.Any(a => a.Equals("--watch-console", StringComparison.OrdinalIgnoreCase)))
    {
      using var cts = new CancellationTokenSource();
      Watchdog.Loop(cts.Token);
      return 0;
    }

    var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
    app.DispatcherUnhandledException += (s, e) =>
    {
      Log.Write("UI exception: " + e.Exception);
      Dialogs.Say("שגיאה: " + e.Exception.Message, warn: true);
      e.Handled = true;
    };
    Theme.Apply(app);
    AppPaths.EnsureRoot();
    return app.Run(new MainWindow());
  }
}
