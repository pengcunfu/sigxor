using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MouseClickVoice
{
    public class MouseHook : IDisposable
    {
        private static readonly IntPtr WH_MOUSE_LL = (IntPtr)14;
        private static readonly IntPtr WM_LBUTTONDOWN = 0x0201;
        private static readonly IntPtr WM_LBUTTONUP = 0x0202;

        private IntPtr _hookID = IntPtr.Zero;
        private LowLevelMouseProc _proc;
        private bool _isHooked;
        private bool _isMouseDown;
        private DateTime _mouseDownTime;
        private CancellationTokenSource? _longPressCancellationToken;
        private int _longPressDurationMs = 1500;

        public int LongPressDurationMs
        {
            get => _longPressDurationMs;
            set => _longPressDurationMs = Math.Max(100, value);
        }

        public event EventHandler<MouseEventArgs>? MousePressed;
        public event EventHandler<MouseEventArgs>? MouseReleased;
        public event EventHandler<MouseEventArgs>? LongPressDetected;

        public MouseHook()
        {
            _proc = HookCallback;
            _isHooked = false;
            _isMouseDown = false;
        }

        public void Start()
        {
            if (_isHooked)
                return;

            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                _hookID = SetWindowsHookEx(WH_MOUSE_LL, _proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }

            _isHooked = _hookID != IntPtr.Zero;
        }

        public void Stop()
        {
            if (!_isHooked)
                return;

            UnhookWindowsHookEx(_hookID);
            _hookID = IntPtr.Zero;
            _isHooked = false;
            _longPressCancellationToken?.Cancel();
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && lParam != IntPtr.Zero)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var mouseArgs = new MouseEventArgs(hookStruct.pt.x, hookStruct.pt.y);

                if (wParam == WM_LBUTTONDOWN)
                {
                    HandleMouseDown(mouseArgs);
                }
                else if (wParam == WM_LBUTTONUP)
                {
                    HandleMouseUp(mouseArgs);
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void HandleMouseDown(MouseEventArgs args)
        {
            if (_isMouseDown)
                return;

            _isMouseDown = true;
            _mouseDownTime = DateTime.Now;
            MousePressed?.Invoke(this, args);

            // 启动长按检测
            _longPressCancellationToken?.Cancel();
            _longPressCancellationToken = new CancellationTokenSource();

            Task.Delay(_longPressDurationMs, _longPressCancellationToken.Token).ContinueWith(task =>
            {
                if (!task.IsCanceled && _isMouseDown)
                {
                    LongPressDetected?.Invoke(this, args);
                }
            }, TaskScheduler.Default);
        }

        private void HandleMouseUp(MouseEventArgs args)
        {
            if (!_isMouseDown)
                return;

            _isMouseDown = false;
            _longPressCancellationToken?.Cancel();
            MouseReleased?.Invoke(this, args);
        }

        public void Dispose()
        {
            Stop();
            _longPressCancellationToken?.Dispose();
        }

        #region Windows API

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(IntPtr idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        #endregion
    }

    public class MouseEventArgs : EventArgs
    {
        public int X { get; }
        public int Y { get; }

        public MouseEventArgs(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}