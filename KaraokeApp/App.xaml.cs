using System;
using System.Windows;
using KaraokeApp.Windows;
using LibVLCSharp.Shared;

namespace KaraokeApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Prevent unhandled exceptions on background threads from killing the app
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                System.Diagnostics.Debug.WriteLine(
                    "Unhandled: " + ex.ExceptionObject?.ToString());
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                ex.SetObserved(); // Mark as handled so it doesn't crash the app
                System.Diagnostics.Debug.WriteLine(
                    "Unobserved task exception: " + ex.Exception?.Message);
            };

            Core.Initialize();
            base.OnStartup(e);

            // Show splash screen first
            var splash = new SplashScreenWindow();
            splash.Show();

            // When splash closes, show main window
            splash.Closed += (s, ev) =>
            {
                var main = new MainWindow();

                // Switch back to normal shutdown mode now that main window is open
                Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;

                main.Show();
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
        }
    }
}
