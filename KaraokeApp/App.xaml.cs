using System.Windows;
using LibVLCSharp.Shared;

namespace KaraokeApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Must initialize LibVLC before any media operations
            Core.Initialize();
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
        }
    }
}
