using System;
using FreePin.Services;

namespace FreePin.Models
{
    public sealed class AppSettings
    {
        public int Version { get; set; } = 1;
        public bool CacheEnabled { get; set; } = true;
        public int CacheRetentionDays { get; set; } = 7;
        public int CacheMaxCount { get; set; } = 100;
        public string CachePath { get; set; } = string.Empty;

        public bool RememberCaptureStyle { get; set; } = true;
        public string CaptureColor { get; set; } = "#B44E32";
        public double CaptureThickness { get; set; } = 3;
        public bool PinDoubleClickCloses { get; set; } = true;
        public bool SmartSelectionEnabled { get; set; } = true;
        public bool StartWithWindows { get; set; }

        public HotkeyBinding ScreenshotHotkey { get; set; } = HotkeyBinding.DefaultScreenshot();
        public HotkeyBinding PinHotkey { get; set; } = HotkeyBinding.DefaultPin();
        public HotkeyBinding CacheHotkey { get; set; } = HotkeyBinding.DefaultCache();

        public bool WelcomeShown { get; set; }

        public void EnsureDefaults()
        {
            if (CacheRetentionDays < 1)
                CacheRetentionDays = 7;
            if (CacheRetentionDays > 3650)
                CacheRetentionDays = 3650;
            if (CacheMaxCount < 1)
                CacheMaxCount = 100;
            if (CacheMaxCount > 1000)
                CacheMaxCount = 1000;
            if (string.IsNullOrWhiteSpace(CachePath))
                CachePath = SettingsService.DefaultCachePath;
            if (string.IsNullOrWhiteSpace(CaptureColor))
                CaptureColor = "#B44E32";
            if (double.IsNaN(CaptureThickness) || double.IsInfinity(CaptureThickness))
                CaptureThickness = 3;
            if (CaptureThickness < 1)
                CaptureThickness = 1;
            if (CaptureThickness > 30)
                CaptureThickness = 30;
            if (ScreenshotHotkey == null)
                ScreenshotHotkey = HotkeyBinding.DefaultScreenshot();
            if (PinHotkey == null)
                PinHotkey = HotkeyBinding.DefaultPin();
            if (CacheHotkey == null)
                CacheHotkey = HotkeyBinding.DefaultCache();
        }

        public AppSettings Clone()
        {
            EnsureDefaults();
            return new AppSettings
            {
                Version = Version,
                CacheEnabled = CacheEnabled,
                CacheRetentionDays = CacheRetentionDays,
                CacheMaxCount = CacheMaxCount,
                CachePath = CachePath,
                RememberCaptureStyle = RememberCaptureStyle,
                CaptureColor = CaptureColor,
                CaptureThickness = CaptureThickness,
                PinDoubleClickCloses = PinDoubleClickCloses,
                SmartSelectionEnabled = SmartSelectionEnabled,
                StartWithWindows = StartWithWindows,
                ScreenshotHotkey = ScreenshotHotkey.Clone(),
                PinHotkey = PinHotkey.Clone(),
                CacheHotkey = CacheHotkey.Clone(),
                WelcomeShown = WelcomeShown
            };
        }
    }
}
