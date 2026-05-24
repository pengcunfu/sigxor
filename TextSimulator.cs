using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace MouseClickVoice;

public class TextSimulator
{
    private readonly double _typingDelay;
    private Window? _ownerWindow;

    public TextSimulator(double typingDelay = 0.05)
    {
        _typingDelay = typingDelay;
    }

    public void SetOwnerWindow(Window? window) => _ownerWindow = window;

    public async Task TypeTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        await Task.Delay(100);

        if (OperatingSystem.IsWindows())
            await WindowsTextInput.TypeAsync(text);
        else if (OperatingSystem.IsLinux())
            await LinuxTextInput.TypeAsync(text);
        else if (OperatingSystem.IsMacOS())
            await MacTextInput.TypeAsync(text);
        else
            throw new PlatformNotSupportedException("当前平台不支持键盘模拟输入，请启用「使用剪贴板粘贴」。");
    }

    public async Task InsertTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var clipboard = GetClipboard();
        if (clipboard == null)
            throw new InvalidOperationException("无法访问剪贴板");

        var originalText = await clipboard.GetTextAsync();

        await clipboard.SetTextAsync(text);
        await Task.Delay(50);

        if (OperatingSystem.IsWindows())
            await WindowsTextInput.PasteAsync();
        else if (OperatingSystem.IsLinux())
            await LinuxTextInput.PasteAsync();
        else if (OperatingSystem.IsMacOS())
            await MacTextInput.PasteAsync();
        else
            throw new PlatformNotSupportedException("当前平台不支持粘贴模拟");

        await Task.Delay(50);

        if (!string.IsNullOrEmpty(originalText))
            await clipboard.SetTextAsync(originalText);
        else
            await clipboard.ClearAsync();
    }

    private IClipboard? GetClipboard() =>
        _ownerWindow?.Clipboard ?? TopLevel.GetTopLevel(_ownerWindow)?.Clipboard;
}

internal static class WindowsTextInput
{
    public static Task TypeAsync(string text)
    {
        foreach (var ch in text)
            SendChar(ch);

        return Task.CompletedTask;
    }

    public static Task PasteAsync()
    {
        SendKeyCombo(0x11, 0x56); // Ctrl+V
        return Task.CompletedTask;
    }

    private static void SendChar(char ch)
    {
        var inputs = new INPUT[2];
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].U.ki.wVk = 0;
        inputs[0].U.ki.wScan = ch;
        inputs[0].U.ki.dwFlags = KEYEVENTF_UNICODE;

        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].U.ki.wVk = 0;
        inputs[1].U.ki.wScan = ch;
        inputs[1].U.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;

        SendInput(2, inputs, INPUT.Size);
    }

    private static void SendKeyCombo(ushort modifier, ushort key)
    {
        var inputs = new INPUT[4];
        inputs[0] = KeyDown(modifier);
        inputs[1] = KeyDown(key);
        inputs[2] = KeyUp(key);
        inputs[3] = KeyUp(modifier);
        SendInput(4, inputs, INPUT.Size);
    }

    private static INPUT KeyDown(ushort vk) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion { ki = new KEYBDINPUT { wVk = vk } }
    };

    private static INPUT KeyUp(ushort vk) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } }
    };

    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion U;

        public static readonly int Size = System.Runtime.InteropServices.Marshal.SizeOf<INPUT>();
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
    private struct InputUnion
    {
        [System.Runtime.InteropServices.FieldOffset(0)] public KEYBDINPUT ki;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }
}

internal static class LinuxTextInput
{
    public static Task TypeAsync(string text) =>
        RunAsync("xdotool", $"type --delay 1 -- {EscapeArg(text)}");

    public static Task PasteAsync() =>
        RunAsync("xdotool", "key ctrl+v");

    private static string EscapeArg(string text) =>
        "'" + text.Replace("'", "'\\''") + "'";

    private static Task RunAsync(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动 xdotool，请安装: sudo apt install xdotool");
        process.WaitForExit();
        return Task.CompletedTask;
    }
}

internal static class MacTextInput
{
    public static Task TypeAsync(string text)
    {
        var escaped = text.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return RunAsync("osascript", $"-e \"tell application \\\"System Events\\\" to keystroke \\\"{escaped}\\\"\"");
    }

    public static Task PasteAsync() =>
        RunAsync("osascript", "-e 'tell application \"System Events\" to keystroke \"v\" using command down'");

    private static Task RunAsync(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("无法执行 osascript");
        process.WaitForExit();
        return Task.CompletedTask;
    }
}
