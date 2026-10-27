using System;
using System.Windows.Input;
using Newtonsoft.Json;

namespace FreePin.Models
{
    public sealed class HotkeyBinding
    {
        public bool Enabled { get; set; } = true;
        public int Modifiers { get; set; }
        public int VirtualKey { get; set; }

        [JsonIgnore]
        public string Display
        {
            get { return HotkeyFormatter.Format(Modifiers, VirtualKey); }
        }

        [JsonIgnore]
        public bool IsValid
        {
            get { return VirtualKey > 0 && (Modifiers != 0 || HotkeyFormatter.IsFunctionKey(VirtualKey)); }
        }

        public HotkeyBinding Clone()
        {
            return new HotkeyBinding
            {
                Enabled = Enabled,
                Modifiers = Modifiers,
                VirtualKey = VirtualKey
            };
        }

        public static HotkeyBinding Create(ModifierKeys modifiers, Key key)
        {
            return new HotkeyBinding
            {
                Enabled = true,
                Modifiers = (int)modifiers,
                VirtualKey = KeyInterop.VirtualKeyFromKey(key)
            };
        }

        public static HotkeyBinding DefaultScreenshot()
        {
            return new HotkeyBinding { Enabled = true, Modifiers = (int)ModifierKeys.Control, VirtualKey = 0x31 };
        }

        public static HotkeyBinding DefaultPin()
        {
            return new HotkeyBinding { Enabled = true, Modifiers = (int)ModifierKeys.Control, VirtualKey = 0x32 };
        }

        public static HotkeyBinding DefaultCache()
        {
            return new HotkeyBinding { Enabled = true, Modifiers = (int)ModifierKeys.Control, VirtualKey = 0x33 };
        }
    }

    public enum HotkeyAction
    {
        Screenshot,
        Pin,
        Cache
    }

    public static class HotkeyFormatter
    {
        public static string Format(int modifiers, int virtualKey)
        {
            if (virtualKey <= 0)
                return "未设置";

            string text = string.Empty;
            if ((modifiers & (int)ModifierKeys.Control) != 0)
                text += "Ctrl+";
            if ((modifiers & (int)ModifierKeys.Alt) != 0)
                text += "Alt+";
            if ((modifiers & (int)ModifierKeys.Shift) != 0)
                text += "Shift+";
            if ((modifiers & (int)ModifierKeys.Windows) != 0)
                text += "Win+";
            return text + KeyName(virtualKey);
        }

        public static string KeyName(int virtualKey)
        {
            Key key = KeyInterop.KeyFromVirtualKey(virtualKey);
            switch (key)
            {
                case Key.D0: return "0";
                case Key.D1: return "1";
                case Key.D2: return "2";
                case Key.D3: return "3";
                case Key.D4: return "4";
                case Key.D5: return "5";
                case Key.D6: return "6";
                case Key.D7: return "7";
                case Key.D8: return "8";
                case Key.D9: return "9";
                case Key.NumPad0: return "Num0";
                case Key.NumPad1: return "Num1";
                case Key.NumPad2: return "Num2";
                case Key.NumPad3: return "Num3";
                case Key.NumPad4: return "Num4";
                case Key.NumPad5: return "Num5";
                case Key.NumPad6: return "Num6";
                case Key.NumPad7: return "Num7";
                case Key.NumPad8: return "Num8";
                case Key.NumPad9: return "Num9";
                case Key.OemPlus: return "=";
                case Key.OemMinus: return "-";
                case Key.OemComma: return ",";
                case Key.OemPeriod: return ".";
                case Key.OemQuestion: return "/";
                case Key.OemSemicolon: return ";";
                case Key.OemQuotes: return "'";
                case Key.OemOpenBrackets: return "[";
                case Key.OemCloseBrackets: return "]";
                case Key.OemPipe: return "\\";
                case Key.OemTilde: return "`";
                case Key.Space: return "Space";
                case Key.Return: return "Enter";
                case Key.Escape: return "Esc";
                case Key.Back: return "Backspace";
                case Key.Delete: return "Delete";
                case Key.Insert: return "Insert";
                case Key.Home: return "Home";
                case Key.End: return "End";
                case Key.PageUp: return "PageUp";
                case Key.PageDown: return "PageDown";
                case Key.Left: return "Left";
                case Key.Right: return "Right";
                case Key.Up: return "Up";
                case Key.Down: return "Down";
                case Key.PrintScreen: return "PrintScreen";
                case Key.CapsLock: return "CapsLock";
                case Key.None: return "无";
                default:
                    if (key >= Key.F1 && key <= Key.F24)
                        return key.ToString();
                    return key.ToString();
            }
        }

        public static bool IsFunctionKey(int virtualKey)
        {
            Key key = KeyInterop.KeyFromVirtualKey(virtualKey);
            return key >= Key.F1 && key <= Key.F24;
        }

        public static bool IsModifierKey(Key key)
        {
            switch (key)
            {
                case Key.LeftCtrl:
                case Key.RightCtrl:
                case Key.LeftAlt:
                case Key.RightAlt:
                case Key.LeftShift:
                case Key.RightShift:
                case Key.LWin:
                case Key.RWin:
                case Key.System:
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryFromKeyboard(ModifierKeys modifiers, Key key, out HotkeyBinding binding, out string error)
        {
            binding = null;
            error = null;
            if (key == Key.System)
                key = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) ? Key.System : key;

            if (IsModifierKey(key) || key == Key.None)
            {
                error = "请再按一个非修饰键。";
                return false;
            }

            int virtualKey = KeyInterop.VirtualKeyFromKey(key);
            if (virtualKey <= 0)
            {
                error = "无法识别这个按键。";
                return false;
            }

            if (modifiers == ModifierKeys.None && !IsFunctionKey(virtualKey))
            {
                error = "为避免误触，请至少使用 Ctrl、Alt、Shift、Win 中的一个修饰键（F1-F24 可单独使用）。";
                return false;
            }

            binding = new HotkeyBinding
            {
                Enabled = true,
                Modifiers = (int)modifiers,
                VirtualKey = virtualKey
            };
            return true;
        }
    }
}
