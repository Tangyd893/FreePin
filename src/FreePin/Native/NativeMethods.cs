using System;
using System.Runtime.InteropServices;

namespace FreePin.Native
{
    internal static class NativeMethods
    {
        public const int SmXVirtualScreen = 76;
        public const int SmYVirtualScreen = 77;
        public const int SmCxVirtualScreen = 78;
        public const int SmCyVirtualScreen = 79;

        public const uint ModAlt = 0x0001;
        public const uint ModControl = 0x0002;
        public const uint ModShift = 0x0004;
        public const uint ModWin = 0x0008;
        public const uint ModNoRepeat = 0x4000;

        public const int WmHotkey = 0x0312;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);

        public const uint WdaExcludeFromCapture = 0x00000011;

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT point);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);

        private const int GwlExStyle = -20;
        private const long WsExToolWindow = 0x00000080L;
        private const long WsExAppWindow = 0x00040000L;

        public static void MarkToolWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return;
            long style = GetWindowLongPtr(hWnd, GwlExStyle).ToInt64();
            style = (style | WsExToolWindow) & ~WsExAppWindow;
            SetWindowLongPtr(hWnd, GwlExStyle, new IntPtr(style));
        }

        /// <summary>
        /// WPF's message-only helper window is 1x1 but still shows up in Alt+Tab on
        /// some systems. Mark tiny top-level windows as tool windows so Windows hides
        /// them from Alt+Tab and Task View.
        /// </summary>
        public static void HideUtilityWindowsFromTaskSwitcher(int processId)
        {
            EnumWindows((hWnd, lParam) =>
            {
                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                if (pid != (uint)processId)
                    return true;

                RECT rect;
                if (!GetWindowRect(hWnd, out rect))
                    return true;
                if (rect.Right - rect.Left <= 2 && rect.Bottom - rect.Top <= 2)
                    MarkToolWindow(hWnd);
                return true;
            }, IntPtr.Zero);
        }

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        public static void EnableDpiAwareness()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
                if (SetProcessDpiAwarenessContext(new IntPtr(-4)))
                    return;
            }
            catch
            {
                // Fall through for older Windows versions.
            }

            try
            {
                SetProcessDPIAware();
            }
            catch
            {
                // ignored
            }
        }

        public static double GetSystemDpiScale()
        {
            try
            {
                uint dpi = GetDpiForSystem();
                if (dpi > 0)
                    return dpi / 96.0;
            }
            catch
            {
                // ignored
            }

            using (var graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
            {
                return graphics.DpiX / 96.0;
            }
        }

        public static System.Drawing.Rectangle GetVirtualScreenBounds()
        {
            return new System.Drawing.Rectangle(
                GetSystemMetrics(SmXVirtualScreen),
                GetSystemMetrics(SmYVirtualScreen),
                GetSystemMetrics(SmCxVirtualScreen),
                GetSystemMetrics(SmCyVirtualScreen));
        }
    }
}
