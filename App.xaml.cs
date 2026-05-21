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
            // 仅开机自启动（注册表带 --silent）时静默；用户双击启动始终显示窗口
            LaunchSilent = e.Args.Contains("--silent");

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
