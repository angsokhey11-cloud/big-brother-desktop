using System;
using System.Windows;
using Velopack;

namespace BBDesktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.DispatcherUnhandledException += (_, e) =>
        {
            MessageBox.Show("Workstation error: " + e.Exception.Message,
                "BIG BROTHER Desktop", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
        app.Run(new MainWindow());
    }
}
