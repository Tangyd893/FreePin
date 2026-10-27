using System;
using System.IO;
using System.Text;
using FreePin.Models;
using Newtonsoft.Json;

namespace FreePin.Services
{
    public sealed class SettingsService
    {
        public static string DefaultAppDataPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FreePin");
            }
        }

        public static string DefaultCachePath
        {
            get { return Path.Combine(DefaultAppDataPath, "Cache"); }
        }

        public string SettingsPath { get; private set; }

        public SettingsService()
        {
            SettingsPath = Path.Combine(DefaultAppDataPath, "settings.json");
        }

        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return CreateDefault();

                string json = File.ReadAllText(SettingsPath, Encoding.UTF8);
                AppSettings settings = JsonConvert.DeserializeObject<AppSettings>(json);
                if (settings == null)
                    return CreateDefault();

                Normalize(settings);
                return settings;
            }
            catch (Exception ex)
            {
                Log.Error("读取设置失败，将使用默认设置。", ex);
                TryBackupBrokenSettings();
                return CreateDefault();
            }
        }

        public void Save(AppSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            Normalize(settings);
            Directory.CreateDirectory(DefaultAppDataPath);
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            string temp = SettingsPath + ".tmp";
            File.WriteAllText(temp, json, new UTF8Encoding(false));

            if (File.Exists(SettingsPath))
                File.Replace(temp, SettingsPath, null);
            else
                File.Move(temp, SettingsPath);
        }

        public static AppSettings CreateDefault()
        {
            var settings = new AppSettings
            {
                CacheEnabled = true,
                CacheRetentionDays = 7,
                CacheMaxCount = 100,
                CachePath = DefaultCachePath,
                ScreenshotHotkey = HotkeyBinding.DefaultScreenshot(),
                PinHotkey = HotkeyBinding.DefaultPin(),
                CacheHotkey = HotkeyBinding.DefaultCache()
            };
            return settings;
        }

        public static void Normalize(AppSettings settings)
        {
            settings.EnsureDefaults();
            try
            {
                settings.CachePath = Environment.ExpandEnvironmentVariables(settings.CachePath.Trim());
                settings.CachePath = Path.GetFullPath(settings.CachePath);
            }
            catch
            {
                settings.CachePath = DefaultCachePath;
            }
        }

        public static bool ValidateCachePath(string path, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "缓存位置不能为空。";
                return false;
            }

            try
            {
                string full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
                Directory.CreateDirectory(full);
                string probe = Path.Combine(full, ".freepin-write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch (Exception ex)
            {
                error = "无法使用该目录：" + ex.Message;
                return false;
            }
        }

        private void TryBackupBrokenSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return;
                string backup = SettingsPath + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Copy(SettingsPath, backup, true);
            }
            catch
            {
                // ignored
            }
        }
    }
}
