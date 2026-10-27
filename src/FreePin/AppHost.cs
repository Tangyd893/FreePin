using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FreePin.Imaging;
using FreePin.Models;
using FreePin.Services;
using FreePin.UI;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;

namespace FreePin
{
    public sealed class AppHost : IDisposable
    {
        private readonly Application _application;
        private readonly SettingsService _settingsService;
        private readonly OcrService _ocr;
        private AppSettings _settings;
        private CacheService _cache;
        private Window _messageWindow;
        private HotkeyManager _hotkeys;
        private TaskbarIcon _tray;
        private CaptureOverlayWindow _overlay;
        private LongCaptureWindow _longCapture;
        private CacheWindow _cacheWindow;
        private SettingsWindow _settingsWindow;
        private EditorWindow _editor;
        private BitmapSource _lastSource;
        private double _pinOffset;
        private bool _disposed;

        public AppHost(Application application)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _settingsService = new SettingsService();
            _settings = _settingsService.Load();
            _cache = new CacheService(_settings);
            _ocr = new OcrService();
        }

        public void Start()
        {
            try
            {
                CreateMessageWindow();
                CreateTray();
                _hotkeys = new HotkeyManager(_messageWindow);
                _hotkeys.Pressed += OnHotkey;
                var errors = new List<string>();
                if (!_hotkeys.TryApply(_settings, out errors))
                {
                    Log.Warn("初始快捷键注册失败: " + string.Join("; ", errors));
                    Notify("快捷键注册失败", string.Join("\n", errors));
                }

                _cache.Cleanup();
                Log.Info("FreePin 已启动，OCR: " + (_ocr.IsAvailable ? _ocr.LanguageName : "不可用"));
            }
            catch (Exception ex)
            {
                Log.Error("FreePin 启动失败", ex);
                MessageBox.Show("FreePin 启动失败：\n" + ex.Message, "FreePin",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _application.Shutdown();
            }
        }

        private void CreateMessageWindow()
        {
            _messageWindow = new Window
            {
                Title = "FreePin",
                Width = 1,
                Height = 1,
                Left = -30000,
                Top = -30000,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
                ResizeMode = ResizeMode.NoResize
            };
            _messageWindow.Show();
        }

        private void CreateTray()
        {
            _tray = new TaskbarIcon
            {
                Icon = ImageUtil.LoadTrayIcon(),
                ToolTipText = "FreePin · 左键截图，右键菜单",
                NoLeftClickDelay = true
            };
            _tray.TrayLeftMouseUp += (s, e) => StartScreenshotFromTray();
            UpdateTrayMenu();
        }

        private void UpdateTrayMenu()
        {
            if (_tray == null)
                return;

            var menu = new ContextMenu();
            menu.Items.Add(TrayItem("截图", _settings.ScreenshotHotkey.Display, StartScreenshotFromTray));
            menu.Items.Add(TrayItem("贴图", _settings.PinHotkey.Display, PinFromClipboard));
            menu.Items.Add(TrayItem("最近截图", _settings.CacheHotkey.Display, ShowCache));
            menu.Items.Add(new Separator());
            menu.Items.Add(TrayItem("设置", null, ShowSettings));
            menu.Items.Add(new Separator());
            menu.Items.Add(TrayItem("退出 FreePin", null, Exit));
            UiTheme.StyleContextMenu(menu);
            _tray.ContextMenu = menu;
            _tray.ToolTipText = "FreePin · 左键截图，右键菜单";
        }

        private static MenuItem TrayItem(string header, string gesture, Action action)
        {
            var item = new MenuItem
            {
                Header = header,
                InputGestureText = gesture ?? string.Empty,
                FontFamily = UiTheme.UiFont,
                Foreground = UiTheme.Brush(UiTheme.Foreground)
            };
            item.Click += (s, e) => action();
            return item;
        }

        private void OnHotkey(HotkeyAction action)
        {
            if (_disposed || _settingsWindow != null)
                return;

            switch (action)
            {
                case HotkeyAction.Screenshot:
                    StartScreenshotFromTray();
                    break;
                case HotkeyAction.Pin:
                    PinFromClipboard();
                    break;
                case HotkeyAction.Cache:
                    ShowCache();
                    break;
            }
        }

        private async void StartScreenshotFromTray()
        {
            if (_disposed || _settingsWindow != null)
                return;

            // Let the tray menu close before capturing the screen.
            await System.Threading.Tasks.Task.Delay(120);
            StartScreenshot();
        }

        private void StartScreenshot()
        {
            if (_disposed)
                return;
            if (_overlay != null)
            {
                _overlay.Activate();
                return;
            }

            try
            {
                ScreenFrame frame = ScreenCapture.CaptureVirtualScreen();
                _overlay = new CaptureOverlayWindow(frame, frame.Scale, OnCaptureResult);
                _overlay.Closed += (s, e) => _overlay = null;
                _overlay.Show();
                _overlay.Activate();
            }
            catch (Exception ex)
            {
                Log.Error("启动截图失败", ex);
                Notify("截图失败", ex.Message);
            }
        }

        private void OnCaptureResult(CaptureResult result)
        {
            if (result == null || result.Image == null || result.Source == null)
                return;

            _lastSource = result.Source;
            _cache.Save(result.Image);

            try
            {
                switch (result.Action)
                {
                    case CaptureAction.Copy:
                        ImageUtil.CopyToClipboard(result.Source);
                        Notify("截图已复制", result.Source.PixelWidth + " × " + result.Source.PixelHeight);
                        result.Image.Dispose();
                        break;

                    case CaptureAction.Pin:
                        ShowPin(result.Source);
                        result.Image.Dispose();
                        break;

                    case CaptureAction.Save:
                        SaveCapture(result.Source);
                        result.Image.Dispose();
                        break;

                    case CaptureAction.Edit:
                    case CaptureAction.Ocr:
                        OpenEditor(result, result.Action == CaptureAction.Ocr);
                        break;

                    case CaptureAction.LongCapture:
                        StartLongCapture(result);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error("处理截图结果失败", ex);
                Notify("截图处理失败", ex.Message);
                result.Image.Dispose();
            }
        }

        private void OpenEditor(CaptureResult result, bool ocr)
        {
            try
            {
                _editor = new EditorWindow(result.Image, result.Source, ScreenCapture.PrimaryScale, _ocr,
                    ShowPin, ocr);
                _editor.Closed += (s, e) => _editor = null;
                _editor.Show();
                _editor.Activate();
            }
            catch (Exception ex)
            {
                result.Image.Dispose();
                throw new InvalidOperationException("打开编辑器失败：" + ex.Message, ex);
            }
        }

        private void StartLongCapture(CaptureResult result)
        {
            if (_longCapture != null)
            {
                result.Image.Dispose();
                Notify("长截图正在进行", "请先完成或取消当前长截图。");
                return;
            }

            try
            {
                _longCapture = new LongCaptureWindow(result.ScreenBounds, result.Image, OnLongCaptureCompleted);
                _longCapture.Closed += (s, e) => _longCapture = null;
                _longCapture.Show();
            }
            catch (Exception ex)
            {
                result.Image.Dispose();
                Log.Error("启动长截图失败", ex);
                Notify("长截图失败", ex.Message);
            }
        }

        private void OnLongCaptureCompleted(LongCaptureResult result)
        {
            if (result == null || result.Image == null || result.Source == null)
                return;

            _lastSource = result.Source;
            _cache.Save(result.Image);
            try
            {
                OpenEditor(new CaptureResult
                {
                    Image = result.Image,
                    Source = result.Source,
                    Action = CaptureAction.Edit
                }, false);
            }
            catch (Exception ex)
            {
                Log.Error("打开长截图编辑器失败", ex);
                Notify("长截图失败", ex.Message);
            }
        }

        private void SaveCapture(BitmapSource source)
        {
            var dialog = new SaveFileDialog
            {
                Title = "保存截图",
                Filter = "PNG 图片 (*.png)|*.png",
                DefaultExt = ".png",
                AddExtension = true,
                FileName = "FreePin_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png"
            };
            if (dialog.ShowDialog() != true)
                return;
            ImageUtil.SavePng(source, dialog.FileName);
            Notify("截图已保存", dialog.FileName);
        }

        private void PinFromClipboard()
        {
            if (_disposed)
                return;
            try
            {
                BitmapSource source = _lastSource;
                if (source == null && Clipboard.ContainsImage())
                {
                    source = Clipboard.GetImage();
                    if (source != null && source.CanFreeze)
                        source.Freeze();
                }

                if (source == null)
                {
                    var latest = _cache.List().FirstOrDefault();
                    if (latest != null)
                        source = ImageUtil.LoadBitmapSource(latest.Path);
                }

                if (source == null)
                {
                    Notify("没有可贴图的内容", "先按 Ctrl+1 截图，或把图片复制到剪贴板。");
                    return;
                }

                ShowPin(source);
            }
            catch (Exception ex)
            {
                Log.Error("贴图失败", ex);
                Notify("贴图失败", ex.Message);
            }
        }

        private void ShowPin(BitmapSource source)
        {
            if (source == null)
                return;

            var pin = new PinWindow(source, ScreenCapture.PrimaryScale)
            {
                WindowStartupLocation = WindowStartupLocation.Manual
            };
            Rect area = SystemParameters.WorkArea;
            _pinOffset = (_pinOffset + 22) % 154;
            pin.Left = area.Left + Math.Max(0, (area.Width - pin.Width) / 2) + _pinOffset;
            pin.Top = area.Top + Math.Max(0, (area.Height - pin.Height) / 2) + _pinOffset;
            pin.Show();
            pin.Activate();
        }

        private void ShowCache()
        {
            if (_disposed)
                return;
            try
            {
                if (_cacheWindow != null)
                {
                    if (!_cacheWindow.IsVisible)
                        _cacheWindow.Show();
                    _cacheWindow.Activate();
                    _cacheWindow.Refresh();
                    return;
                }

                _cacheWindow = new CacheWindow(_cache);
                _cacheWindow.Closed += (s, e) => _cacheWindow = null;
                _cacheWindow.Show();
                _cacheWindow.Activate();
            }
            catch (Exception ex)
            {
                Log.Error("打开最近截图失败", ex);
                Notify("打开最近截图失败", ex.Message);
            }
        }

        public void OpenSettings()
        {
            ShowSettings();
        }

        private void ShowSettings()
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }

            var draft = _settings.Clone();
            _settingsWindow = new SettingsWindow(draft, ApplySettings);
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            _settingsWindow.ShowDialog();
        }

        private string ApplySettings(AppSettings draft)
        {
            if (draft == null)
                return "设置无效。";

            SettingsService.Normalize(draft);
            var backup = _settings.Clone();
            var errors = new List<string>();
            if (!_hotkeys.TryApply(draft, out errors))
                return string.Join("\n", errors);

            try
            {
                _settingsService.Save(draft);
            }
            catch (Exception ex)
            {
                Log.Error("保存设置失败", ex);
                _hotkeys.TryApply(backup, out errors);
                return "保存设置失败：" + ex.Message;
            }

            _settings = draft;
            _cache.UpdateSettings(draft);
            UpdateTrayMenu();
            Log.Info("设置已保存");
            return null;
        }

        public void Notify(string title, string message)
        {
            try
            {
                if (_tray != null)
                {
                    _tray.ShowBalloonTip(title, message, BalloonIcon.Info);
                }
            }
            catch (Exception ex)
            {
                Log.Error("显示通知失败", ex);
            }
        }

        private void Exit()
        {
            if (_disposed)
                return;
            Dispose();
            _application.Shutdown();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            try
            {
                if (_hotkeys != null)
                    _hotkeys.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error("释放快捷键失败", ex);
            }

            try
            {
                if (_overlay != null)
                    _overlay.Close();
            }
            catch
            {
                // ignored
            }

            try
            {
                if (_longCapture != null)
                    _longCapture.Close();
            }
            catch
            {
                // ignored
            }

            try
            {
                if (_tray != null)
                {
                    _tray.Dispose();
                    _tray = null;
                }
            }
            catch (Exception ex)
            {
                Log.Error("释放托盘失败", ex);
            }

            try
            {
                if (_messageWindow != null)
                    _messageWindow.Close();
            }
            catch
            {
                // ignored
            }
        }
    }
}
