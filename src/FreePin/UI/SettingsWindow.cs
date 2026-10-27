using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FreePin.Models;
using FreePin.Services;

namespace FreePin.UI
{
    public sealed class SettingsWindow : Window
    {
        private readonly AppSettings _draft;
        private readonly Func<AppSettings, string> _save;
        private readonly Dictionary<HotkeyAction, CheckBox> _hotkeyChecks =
            new Dictionary<HotkeyAction, CheckBox>();
        private readonly Dictionary<HotkeyAction, Button> _hotkeyButtons =
            new Dictionary<HotkeyAction, Button>();
        private readonly TextBlock _status;
        private readonly TextBox _days;
        private readonly TextBox _maxCount;
        private readonly TextBox _path;
        private readonly CheckBox _cacheEnabled;
        private readonly CheckBox _rememberCaptureStyle;
        private readonly CheckBox _smartSelection;
        private readonly CheckBox _pinDoubleClickCloses;
        private readonly CheckBox _startWithWindows;
        private HotkeyAction? _captureAction;

        public SettingsWindow(AppSettings draft, Func<AppSettings, string> save)
        {
            _draft = draft ?? throw new ArgumentNullException(nameof(draft));
            _save = save;
            _draft.EnsureDefaults();

            Title = "FreePin - 设置";
            Width = 650;
            Height = 760;
            MinWidth = 580;
            MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            // Shown from the tray; make sure it is visible above a maximized app.
            Topmost = true;
            ShowInTaskbar = false;
            UiTheme.ApplyWindow(this);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(20)
            };
            var content = new StackPanel();

            var title = UiTheme.Text("设置", 22, null, true);
            content.Children.Add(title);
            var subtitle = UiTheme.Text("所有设置保存在本机，不上传任何数据。", 12, UiTheme.Muted);
            subtitle.Margin = new Thickness(0, 3, 0, 16);
            content.Children.Add(subtitle);

            content.Children.Add(SectionTitle("启动"));
            _startWithWindows = new CheckBox
            {
                Content = "开机自启动",
                IsChecked = _draft.StartWithWindows,
                Foreground = UiTheme.Brush(UiTheme.Foreground),
                Margin = new Thickness(0, 0, 0, 6)
            };
            content.Children.Add(_startWithWindows);
            var startupHint = UiTheme.Text("登录 Windows 后自动在托盘运行；仅当前用户，不需要管理员权限。",
                12, UiTheme.Muted);
            startupHint.TextWrapping = TextWrapping.Wrap;
            startupHint.Margin = new Thickness(0, 0, 0, 14);
            content.Children.Add(startupHint);

            content.Children.Add(SectionTitle("截图标注"));
            _rememberCaptureStyle = new CheckBox
            {
                Content = "记住上次使用的颜色和笔触大小",
                IsChecked = _draft.RememberCaptureStyle,
                Foreground = UiTheme.Brush(UiTheme.Foreground),
                Margin = new Thickness(0, 0, 0, 6)
            };
            content.Children.Add(_rememberCaptureStyle);
            var styleHint = UiTheme.Text("关闭后每次截图恢复默认颜色和 3 px 笔触；开启后沿用上一次截图设置。",
                12, UiTheme.Muted);
            styleHint.TextWrapping = TextWrapping.Wrap;
            styleHint.Margin = new Thickness(0, 0, 0, 14);
            content.Children.Add(styleHint);

            _smartSelection = new CheckBox
            {
                Content = "启用智能选区（悬停窗口/控件，单击选中）",
                IsChecked = _draft.SmartSelectionEnabled,
                Foreground = UiTheme.Brush(UiTheme.Foreground),
                Margin = new Thickness(0, 0, 0, 6)
            };
            content.Children.Add(_smartSelection);
            var smartHint = UiTheme.Text("单击高亮区域直接选中；拖动鼠标仍可自由框选。关闭后只保留自由框选。",
                12, UiTheme.Muted);
            smartHint.TextWrapping = TextWrapping.Wrap;
            smartHint.Margin = new Thickness(0, 0, 0, 14);
            content.Children.Add(smartHint);

            content.Children.Add(SectionTitle("贴图"));
            _pinDoubleClickCloses = new CheckBox
            {
                Content = "双击贴图窗口关闭",
                IsChecked = _draft.PinDoubleClickCloses,
                Foreground = UiTheme.Brush(UiTheme.Foreground),
                Margin = new Thickness(0, 0, 0, 6)
            };
            content.Children.Add(_pinDoubleClickCloses);
            var pinHint = UiTheme.Text("关闭后双击只做激活，不会关闭贴图；右键菜单仍可关闭。", 12, UiTheme.Muted);
            pinHint.TextWrapping = TextWrapping.Wrap;
            pinHint.Margin = new Thickness(0, 0, 0, 14);
            content.Children.Add(pinHint);

            content.Children.Add(SectionTitle("截图缓存"));
            var cachePanel = new StackPanel();
            _cacheEnabled = new CheckBox
            {
                Content = "启用最近截图缓存",
                IsChecked = _draft.CacheEnabled,
                Foreground = UiTheme.Brush(UiTheme.Foreground),
                Margin = new Thickness(0, 0, 0, 10)
            };
            cachePanel.Children.Add(_cacheEnabled);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _days = AddTextRow(grid, 0, "保留天数", _draft.CacheRetentionDays.ToString(),
                "超过天数的缓存会被自动清理");
            _maxCount = AddTextRow(grid, 1, "最多保留", _draft.CacheMaxCount.ToString(),
                "最多保留的图片数量");
            _path = AddTextRow(grid, 2, "缓存位置", _draft.CachePath, "图片缓存的保存目录");
            var browse = UiTheme.Button("浏览...", "选择缓存目录", (s, e) => BrowsePath());
            Grid.SetRow(browse, 2);
            Grid.SetColumn(browse, 2);
            grid.Children.Add(browse);
            cachePanel.Children.Add(grid);
            content.Children.Add(cachePanel);

            content.Children.Add(SectionTitle("快捷键"));
            var hint = UiTheme.Text("单个快捷键可以单独启用或停用。点击按键组合后，直接按下新的组合键；冲突会在保存时检查。",
                12, UiTheme.Muted);
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 0, 0, 10);
            content.Children.Add(hint);

            var hotkeyGrid = new Grid();
            hotkeyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hotkeyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hotkeyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            AddHotkeyRow(hotkeyGrid, 0, HotkeyAction.Screenshot);
            AddHotkeyRow(hotkeyGrid, 1, HotkeyAction.Pin);
            AddHotkeyRow(hotkeyGrid, 2, HotkeyAction.Cache);
            content.Children.Add(hotkeyGrid);

            var note = UiTheme.Text("建议保留 Ctrl+1 / Ctrl+2 / Ctrl+3；如被其他程序占用，保存时会提示。", 12, UiTheme.Muted);
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 8, 0, 0);
            content.Children.Add(note);

            scroll.Content = content;
            Grid.SetRow(scroll, 0);
            root.Children.Add(scroll);

            var footer = new Border
            {
                Background = UiTheme.Brush(UiTheme.Panel),
                BorderBrush = UiTheme.Brush(UiTheme.Border),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(20, 10, 20, 10)
            };
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _status = UiTheme.Text(string.Empty, 12, UiTheme.Muted);
            _status.TextWrapping = TextWrapping.Wrap;
            footerGrid.Children.Add(_status);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            var cancel = UiTheme.Button("取消", "关闭设置", (s, e) => Close());
            buttons.Children.Add(cancel);
            var saveButton = UiTheme.Button("保存", "保存设置并应用快捷键", (s, e) => SaveSettings(), true);
            buttons.Children.Add(saveButton);
            Grid.SetColumn(buttons, 1);
            footerGrid.Children.Add(buttons);
            footer.Child = footerGrid;
            Grid.SetRow(footer, 1);
            root.Children.Add(footer);

            Content = root;
            PreviewKeyDown += OnPreviewKeyDown;
            Closed += (s, e) => _captureAction = null;
            UpdateHotkeyButtons();
        }

        private static TextBlock SectionTitle(string text)
        {
            TextBlock block = UiTheme.Text(text, 15, null, true);
            block.Margin = new Thickness(0, 18, 0, 8);
            return block;
        }

        private static TextBox AddTextRow(Grid grid, int row, string label, string value, string toolTip)
        {
            var labelBlock = UiTheme.Text(label, 13, UiTheme.Foreground);
            labelBlock.Margin = new Thickness(0, 5, 8, 5);
            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            var textBox = new TextBox
            {
                Text = value ?? string.Empty,
                ToolTip = toolTip,
                Margin = new Thickness(0, 2, 6, 2),
                Padding = new Thickness(5, 4, 5, 4),
                Background = UiTheme.Brush(UiTheme.Panel),
                Foreground = UiTheme.Brush(UiTheme.Foreground),
                BorderBrush = UiTheme.Brush(UiTheme.Border),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(textBox, row);
            Grid.SetColumn(textBox, 1);
            grid.Children.Add(textBox);
            return textBox;
        }

        private void AddHotkeyRow(Grid grid, int row, HotkeyAction action)
        {
            HotkeyBinding binding = HotkeyManager.GetBinding(_draft, action);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var check = new CheckBox
            {
                IsChecked = binding.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 8, 10, 8),
                ToolTip = "是否启用这个快捷键"
            };
            Grid.SetRow(check, row);
            Grid.SetColumn(check, 0);
            grid.Children.Add(check);
            _hotkeyChecks[action] = check;

            var label = UiTheme.Text(HotkeyManager.GetActionName(action), 13, UiTheme.Foreground);
            label.Margin = new Thickness(0, 8, 8, 8);
            Grid.SetRow(label, row);
            Grid.SetColumn(label, 1);
            grid.Children.Add(label);

            var button = UiTheme.Button(binding.Display, "点击后按下新的快捷键", (s, e) => BeginCapture(action));
            button.MinWidth = 150;
            Grid.SetRow(button, row);
            Grid.SetColumn(button, 2);
            grid.Children.Add(button);
            _hotkeyButtons[action] = button;
        }

        private void BeginCapture(HotkeyAction action)
        {
            _captureAction = action;
            _status.Text = "请按下新的快捷键组合，Esc 取消。";
            _status.Foreground = UiTheme.Brush(UiTheme.Accent);
            UpdateHotkeyButtons();
            Focus();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_captureAction == null)
                return;

            e.Handled = true;
            if (e.Key == Key.Escape)
            {
                _captureAction = null;
                SetStatus("已取消快捷键修改。");
                UpdateHotkeyButtons();
                return;
            }

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            ModifierKeys modifiers = Keyboard.Modifiers;
            HotkeyBinding binding;
            string error;
            if (!HotkeyFormatter.TryFromKeyboard(modifiers, key, out binding, out error))
            {
                SetStatus(error);
                return;
            }

            binding.Enabled = true;
            HotkeyManager.SetBinding(_draft, _captureAction.Value, binding);
            _captureAction = null;
            SetStatus("已设置为 " + binding.Display + "，保存后生效。");
            UpdateHotkeyButtons();
        }

        private void UpdateHotkeyButtons()
        {
            foreach (var pair in _hotkeyButtons)
            {
                HotkeyBinding binding = HotkeyManager.GetBinding(_draft, pair.Key);
                pair.Value.Content = _captureAction == pair.Key ? "请按快捷键…" : binding.Display;
                pair.Value.IsEnabled = _captureAction == null || _captureAction == pair.Key;
            }
        }

        private void BrowsePath()
        {
            try
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dialog.Description = "选择最近截图缓存的保存目录";
                    dialog.SelectedPath = _path.Text;
                    dialog.ShowNewFolderButton = true;
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        _path.Text = dialog.SelectedPath;
                }
            }
            catch (Exception ex)
            {
                SetStatus("选择目录失败：" + ex.Message);
            }
        }

        private void SaveSettings()
        {
            int days;
            if (!int.TryParse(_days.Text.Trim(), out days) || days < 1 || days > 3650)
            {
                SetStatus("保留天数需要是 1 到 3650 之间的整数。");
                return;
            }

            int maxCount;
            if (!int.TryParse(_maxCount.Text.Trim(), out maxCount) || maxCount < 1 || maxCount > 1000)
            {
                SetStatus("最多保留数量需要是 1 到 1000 之间的整数。");
                return;
            }

            string path = _path.Text.Trim();
            string pathError;
            if (!SettingsService.ValidateCachePath(path, out pathError))
            {
                SetStatus(pathError);
                return;
            }

            _draft.CacheEnabled = _cacheEnabled.IsChecked == true;
            _draft.CacheRetentionDays = days;
            _draft.CacheMaxCount = maxCount;
            _draft.CachePath = path;
            _draft.RememberCaptureStyle = _rememberCaptureStyle.IsChecked == true;
            _draft.SmartSelectionEnabled = _smartSelection.IsChecked == true;
            _draft.PinDoubleClickCloses = _pinDoubleClickCloses.IsChecked == true;
            _draft.StartWithWindows = _startWithWindows.IsChecked == true;

            foreach (var pair in _hotkeyChecks)
                HotkeyManager.GetBinding(_draft, pair.Key).Enabled = pair.Value.IsChecked == true;

            string error = BuildHotkeyError();
            if (error != null)
            {
                SetStatus(error);
                return;
            }

            string saveError = _save(_draft);
            if (saveError != null)
            {
                SetStatus(saveError);
                return;
            }

            DialogResult = true;
            Close();
        }

        private string BuildHotkeyError()
        {
            var seen = new Dictionary<string, HotkeyAction>();
            foreach (HotkeyAction action in new[] { HotkeyAction.Screenshot, HotkeyAction.Pin, HotkeyAction.Cache })
            {
                HotkeyBinding binding = HotkeyManager.GetBinding(_draft, action);
                if (!binding.Enabled)
                    continue;
                if (!binding.IsValid)
                    return HotkeyManager.GetActionName(action) + "：请先设置一个快捷键。";
                string key = binding.Modifiers + ":" + binding.VirtualKey;
                HotkeyAction existing;
                if (seen.TryGetValue(key, out existing))
                {
                    return "快捷键冲突：" + HotkeyManager.GetActionName(existing) + " 和 " +
                           HotkeyManager.GetActionName(action) + " 都使用了 " + binding.Display + "。";
                }
                seen[key] = action;
            }
            return null;
        }

        private void SetStatus(string message)
        {
            _status.Text = message;
            _status.Foreground = UiTheme.Brush(UiTheme.Danger);
        }
    }
}
