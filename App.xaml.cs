using System.Linq;
using System.Windows;
using Application = System.Windows.Application;

namespace MouseClickVoice
{
    public partial class App : Application
    {
        public static bool LaunchSilent { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            LaunchSilent = e.Args.Contains("--silent") || Config.Instance.SilentStart;

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;

            if (LaunchSilent)
                mainWindow.PrepareSilentStartup();
            else
                mainWindow.Show();

            base.OnStartup(e);
        }
    }
}
