using System;
using System.IO;
using Microsoft.Win32;
using System.Runtime.Versioning;

namespace MouseClickVoice;

public static class StartupHelper
{
    public static bool IsSupported =>
        OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    public static bool IsEnabled()
    {
        if (OperatingSystem.IsWindows())
            return WindowsStartupHelper.IsEnabled();
        if (OperatingSystem.IsLinux())
            return LinuxStartupHelper.IsEnabled();
        if (OperatingSystem.IsMacOS())
            return MacStartupHelper.IsEnabled();
        return false;
    }

    public static void SetEnabled(bool enabled, bool silentOnAutoStart = false)
    {
        if (OperatingSystem.IsWindows())
            WindowsStartupHelper.SetEnabled(enabled, silentOnAutoStart);
        else if (OperatingSystem.IsLinux())
            LinuxStartupHelper.SetEnabled(enabled, silentOnAutoStart);
        else if (OperatingSystem.IsMacOS())
            MacStartupHelper.SetEnabled(enabled, silentOnAutoStart);
    }
}

internal static class WindowsStartupHelper
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "MouseClickVoice";

    [SupportedOSPlatform("windows")]
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

    [SupportedOSPlatform("windows")]
    public static void SetEnabled(bool enabled, bool silentOnAutoStart)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, true);

        if (enabled)
        {
            var exePath = Environment.ProcessPath
                ?? Path.Combine(AppContext.BaseDirectory, "MouseClickVoice");
            var command = silentOnAutoStart ? $"\"{exePath}\" --silent" : $"\"{exePath}\"";
            key.SetValue(AppName, command);
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
    }
}

internal static class LinuxStartupHelper
{
    private static string DesktopFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "autostart", "mouseclickvoice.desktop");

    public static bool IsEnabled() => File.Exists(DesktopFilePath);

    public static void SetEnabled(bool enabled, bool silentOnAutoStart)
    {
        if (enabled)
        {
            var exePath = Environment.ProcessPath
                ?? Path.Combine(AppContext.BaseDirectory, "MouseClickVoice");
            var args = silentOnAutoStart ? " --silent" : "";
            var dir = Path.GetDirectoryName(DesktopFilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(DesktopFilePath,
                $"""
                 [Desktop Entry]
                 Type=Application
                 Name=语音输入
                 Exec="{exePath}"{args}
                 X-GNOME-Autostart-enabled=true
                 """);
        }
        else if (File.Exists(DesktopFilePath))
        {
            File.Delete(DesktopFilePath);
        }
    }
}

internal static class MacStartupHelper
{
    private static string PlistPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "LaunchAgents", "com.mouseclickvoice.plist");

    public static bool IsEnabled() => File.Exists(PlistPath);

    public static void SetEnabled(bool enabled, bool silentOnAutoStart)
    {
        if (enabled)
        {
            var exePath = Environment.ProcessPath
                ?? Path.Combine(AppContext.BaseDirectory, "MouseClickVoice");
            var argsLine = silentOnAutoStart ? "    <string>--silent</string>\n" : "";
            var dir = Path.GetDirectoryName(PlistPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(PlistPath,
                $"""
                 <?xml version="1.0" encoding="UTF-8"?>
                 <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                 <plist version="1.0">
                 <dict>
                   <key>Label</key><string>com.mouseclickvoice</string>
                   <key>ProgramArguments</key>
                   <array>
                     <string>{exePath}</string>
                 {argsLine}  </array>
                   <key>RunAtLoad</key><true/>
                 </dict>
                 </plist>
                 """);
        }
        else if (File.Exists(PlistPath))
        {
            File.Delete(PlistPath);
        }
    }
}
