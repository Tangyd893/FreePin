using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace FreePin.Services
{
    /// <summary>
    /// Per-user startup registration under HKCU. No elevation is required.
    /// </summary>
    public static class StartupService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "FreePin";

        public static string CurrentExecutablePath()
        {
            try
            {
                string path = Process.GetCurrentProcess().MainModule.FileName;
                if (!string.IsNullOrWhiteSpace(path))
                    return Path.GetFullPath(path);
            }
            catch
            {
                // Fall through to the assembly location.
            }

            Assembly assembly = Assembly.GetEntryAssembly();
            if (assembly != null && !string.IsNullOrWhiteSpace(assembly.Location))
                return Path.GetFullPath(assembly.Location);

            return string.Empty;
        }

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null)
                        return false;
                    string value = key.GetValue(ValueName) as string;
                    return !string.IsNullOrWhiteSpace(value);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns null on success, or a user-facing error message.
        /// </summary>
        public static string Apply(bool enabled)
        {
            try
            {
                if (!enabled)
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                    {
                        if (key != null)
                            key.DeleteValue(ValueName, false);
                    }
                    return null;
                }

                string exe = CurrentExecutablePath();
                if (string.IsNullOrWhiteSpace(exe))
                    return "无法获取 FreePin 的程序路径。";

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null)
                        return "无法打开当前用户的启动项注册表。";
                    key.SetValue(ValueName, "\"" + exe + "\"", RegistryValueKind.String);
                }
                return null;
            }
            catch (Exception ex)
            {
                return "设置开机自启动失败：" + ex.Message;
            }
        }

        /// <summary>
        /// If the entry exists, refresh its path so moving the portable folder
        /// does not leave a stale command behind.
        /// </summary>
        public static void RefreshCommandIfEnabled()
        {
            if (!IsEnabled())
                return;
            Apply(true);
        }
    }
}
