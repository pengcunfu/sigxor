using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MouseClickVoice
{
    public class KeyboardHook : IDisposable
    {
        private const int WhKeyboardLl = 13;
        private const int WmKeydown = 0x0100;
        private const int WmKeyup = 0x0101;
        private const int WmSyskeydown = 0x0104;
        private const int WmSyskeyup = 0x0105;
        private const int VkRMenu = 0xA5; // 右 Alt

        private IntPtr _hookId = IntPtr.Zero;
        private readonly LowLevelKeyboardProc _proc;
        private bool _isHooked;
        private bool _isKeyDown;

        public event EventHandler? ShortcutPressed;
        public event EventHandler? ShortcutReleased;

        public KeyboardHook()
        {
            _proc = HookCallback;
        }

        public void Start()
        {
            if (_isHooked)
                return;

            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule!;
            {
                _hookId = SetWindowsHookEx(WhKeyboardLl, _proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }

            _isHooked = _hookId != IntPtr.Zero;
        }

        public void Stop()
        {
            if (!_isHooked)
                return;

            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _isHooked = false;
            _isKeyDown = false;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && lParam != IntPtr.Zero)
            {
                var hookStruct = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                var isKeyDown = wParam == (IntPtr)WmKeydown || wParam == (IntPtr)WmSyskeydown;
                var isKeyUp = wParam == (IntPtr)WmKeyup || wParam == (IntPtr)WmSyskeyup;

                if (hookStruct.vkCode == VkRMenu)
                {
                    if (isKeyDown && !_isKeyDown)
                    {
                        _isKeyDown = true;
                        ShortcutPressed?.Invoke(this, EventArgs.Empty);
                    }
                    else if (isKeyUp && _isKeyDown)
                    {
                        _isKeyDown = false;
                        ShortcutReleased?.Invoke(this, EventArgs.Empty);
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose() => Stop();

        #region Windows API

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [StructLayout(LayoutKind.Sequential)]
        private struct KbdLlHookStruct
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        #endregion
    }
}
