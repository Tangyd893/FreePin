using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using FreePin.Models;
using FreePin.Native;

namespace FreePin.Services
{
    public sealed class HotkeyManager : IDisposable
    {
        private const int HotkeyBaseId = 0x4650;
        private readonly Window _window;
        private HwndSource _source;
        private readonly Dictionary<HotkeyAction, int> _registered = new Dictionary<HotkeyAction, int>();
        private readonly Dictionary<HotkeyAction, HotkeyBinding> _applied = new Dictionary<HotkeyAction, HotkeyBinding>();
        private bool _disposed;

        public event Action<HotkeyAction> Pressed;

        public HotkeyManager(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }

        public void Attach()
        {
            if (_source != null)
                return;
            if (_window.IsLoaded)
            {
                AttachCore();
                return;
            }
            _window.SourceInitialized += (sender, args) => AttachCore();
        }

        private void AttachCore()
        {
            if (_source != null)
                return;
            _source = HwndSource.FromHwnd(new WindowInteropHelper(_window).Handle);
            if (_source != null)
                _source.AddHook(WndProc);
        }

        public bool TryApply(AppSettings settings, out List<string> errors)
        {
            errors = new List<string>();
            if (settings == null)
            {
                errors.Add("设置无效。");
                return false;
            }

            Attach();
            settings.EnsureDefaults();

            var previous = new Dictionary<HotkeyAction, HotkeyBinding>();
            foreach (var pair in _applied)
                previous[pair.Key] = pair.Value.Clone();

            UnregisterAll();

            var newlyRegistered = new List<HotkeyAction>();
            foreach (HotkeyAction action in GetActions())
            {
                HotkeyBinding binding = GetBinding(settings, action);
                string actionName = GetActionName(action);
                if (!binding.Enabled)
                    continue;
                if (!binding.IsValid)
                {
                    errors.Add(actionName + "：快捷键未设置。");
                    continue;
                }

                uint modifiers = (uint)binding.Modifiers;
                if ((modifiers & ~(NativeMethods.ModAlt | NativeMethods.ModControl | NativeMethods.ModShift | NativeMethods.ModWin)) != 0)
                {
                    errors.Add(actionName + "：快捷键包含不支持的修饰键。");
                    continue;
                }

                int id = HotkeyBaseId + (int)action;
                if (!NativeMethods.RegisterHotKey(new WindowInteropHelper(_window).Handle, id, modifiers,
                        (uint)binding.VirtualKey))
                {
                    int code = Marshal.GetLastWin32Error();
                    errors.Add(actionName + "：" + binding.Display + " 无法注册（" +
                               new Win32Exception(code).Message + "），可能已被其他程序占用。");
                    continue;
                }

                _registered[action] = id;
                _applied[action] = binding.Clone();
                newlyRegistered.Add(action);
            }

            if (errors.Count > 0)
            {
                foreach (HotkeyAction action in newlyRegistered)
                {
                    int id;
                    if (_registered.TryGetValue(action, out id))
                    {
                        NativeMethods.UnregisterHotKey(new WindowInteropHelper(_window).Handle, id);
                        _registered.Remove(action);
                    }
                }

                _applied.Clear();
                foreach (var pair in previous)
                    _applied[pair.Key] = pair.Value.Clone();
                RegisterPrevious(previous);
                errors.Insert(0, "快捷键设置未生效，已恢复原来的设置。");
                return false;
            }

            return true;
        }

        public IReadOnlyDictionary<HotkeyAction, HotkeyBinding> Applied
        {
            get { return _applied; }
        }

        private void RegisterPrevious(Dictionary<HotkeyAction, HotkeyBinding> previous)
        {
            foreach (var pair in previous)
            {
                HotkeyBinding binding = pair.Value;
                if (!binding.Enabled || !binding.IsValid)
                    continue;
                int id = HotkeyBaseId + (int)pair.Key;
                if (NativeMethods.RegisterHotKey(new WindowInteropHelper(_window).Handle, id, (uint)binding.Modifiers,
                        (uint)binding.VirtualKey))
                {
                    _registered[pair.Key] = id;
                }
            }
        }

        private void UnregisterAll()
        {
            IntPtr handle = _window.IsLoaded ? new WindowInteropHelper(_window).Handle : IntPtr.Zero;
            if (handle == IntPtr.Zero)
                return;
            foreach (var pair in _registered)
                NativeMethods.UnregisterHotKey(handle, pair.Value);
            _registered.Clear();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WmHotkey)
            {
                int id = wParam.ToInt32();
                HotkeyAction action = (HotkeyAction)(id - HotkeyBaseId);
                if (Enum.IsDefined(typeof(HotkeyAction), action))
                {
                    Action<HotkeyAction> handler = Pressed;
                    if (handler != null)
                        handler(action);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private static IEnumerable<HotkeyAction> GetActions()
        {
            yield return HotkeyAction.Screenshot;
            yield return HotkeyAction.Pin;
            yield return HotkeyAction.Cache;
        }

        public static HotkeyBinding GetBinding(AppSettings settings, HotkeyAction action)
        {
            switch (action)
            {
                case HotkeyAction.Screenshot: return settings.ScreenshotHotkey;
                case HotkeyAction.Pin: return settings.PinHotkey;
                case HotkeyAction.Cache: return settings.CacheHotkey;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        public static void SetBinding(AppSettings settings, HotkeyAction action, HotkeyBinding binding)
        {
            switch (action)
            {
                case HotkeyAction.Screenshot: settings.ScreenshotHotkey = binding; break;
                case HotkeyAction.Pin: settings.PinHotkey = binding; break;
                case HotkeyAction.Cache: settings.CacheHotkey = binding; break;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        public static string GetActionName(HotkeyAction action)
        {
            switch (action)
            {
                case HotkeyAction.Screenshot: return "截图";
                case HotkeyAction.Pin: return "贴图";
                case HotkeyAction.Cache: return "最近截图";
                default: return "快捷键";
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_window.IsLoaded)
                UnregisterAll();
            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }
        }
    }
}
