using System;
using System.Drawing;
using System.IO;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace MouseClickVoice
{
    public class TrayIconManager : IDisposable
    {
        private readonly WinForms.NotifyIcon _notifyIcon;
        private readonly WinForms.ToolStripMenuItem _startItem;
        private readonly WinForms.ToolStripMenuItem _stopItem;

        public event EventHandler? ShowWindowRequested;
        public event EventHandler? StartServiceRequested;
        public event EventHandler? StopServiceRequested;
        public event EventHandler? AboutRequested;
        public event EventHandler? ExitRequested;

        public TrayIconManager()
        {
            _startItem = new WinForms.ToolStripMenuItem("开始服务", null, (_, _) => StartServiceRequested?.Invoke(this, EventArgs.Empty));
            _stopItem = new WinForms.ToolStripMenuItem("停止服务", null, (_, _) => StopServiceRequested?.Invoke(this, EventArgs.Empty));

            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add(new WinForms.ToolStripMenuItem("显示主窗口", null, (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty)));
            menu.Items.Add(_startItem);
            menu.Items.Add(_stopItem);
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add(new WinForms.ToolStripMenuItem("关于", null, (_, _) => AboutRequested?.Invoke(this, EventArgs.Empty)));
            menu.Items.Add(new WinForms.ToolStripMenuItem("退出", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty)));

            _notifyIcon = new WinForms.NotifyIcon
            {
                Text = "语音输入",
                Icon = LoadTrayIcon(),
                Visible = true,
                ContextMenuStrip = menu
            };

            _notifyIcon.DoubleClick += (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        }

        public void SetServiceRunning(bool running)
        {
            _startItem.Enabled = !running;
            _stopItem.Enabled = running;
            _notifyIcon.Text = running ? "语音输入 - 服务运行中" : "语音输入 - 服务已停止";
        }

        public void ShowBalloon(string title, string message)
        {
            _notifyIcon.ShowBalloonTip(3000, title, message, WinForms.ToolTipIcon.Info);
        }

        private static Icon LoadTrayIcon()
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.png");
            if (File.Exists(iconPath))
            {
                using var bitmap = new Bitmap(iconPath);
                return Icon.FromHandle(bitmap.GetHicon());
            }

            return SystemIcons.Application;
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
