using System;
using System.IO;
using Microsoft.Win32;

namespace MouseClickVoice
{
    public static class StartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "MouseClickVoice";

        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(AppName) is string;
            }
            catch
            {
                return false;
            }
        }

        public static void SetEnabled(bool enabled, bool silentOnAutoStart = false)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, true);

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                    exePath = Path.Combine(AppContext.BaseDirectory, "MouseClickVoice.exe");

                var command = silentOnAutoStart
                    ? $"\"{exePath}\" --silent"
                    : $"\"{exePath}\"";
                key.SetValue(AppName, command);
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
    }
}
