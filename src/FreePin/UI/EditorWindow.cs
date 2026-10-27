using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FreePin.Annotations;
using FreePin.Imaging;
using FreePin.Services;
using Microsoft.Win32;

namespace FreePin.UI
{
    public sealed class EditorWindow : Window
    {
        private readonly System.Drawing.Bitmap _baseBitmap;
        private readonly OcrService _ocr;
        private readonly Action<BitmapSource> _pinAction;
        private readonly AnnotationCanvas _canvas;
        private readonly TextBlock _status;
        private readonly Dictionary<EditorTool, Button> _toolButtons = new Dictionary<EditorTool, Button>();
        private readonly List<Button> _colorButtons = new List<Button>();
        private Button _ocrButton;
        private Button _undoButton;
        private Button _redoButton;
        private Color _currentColor = Colors.Red;
        private bool _ocrBusy;
        private bool _disposed;

        public EditorWindow(System.Drawing.Bitmap baseBitmap, BitmapSource source, double dpiScale,
            OcrService ocr, Action<BitmapSource> pinAction, bool openOcrOnLoad)
        {
            _baseBitmap = baseBitmap ?? throw new ArgumentNullException(nameof(baseBitmap));
            _ocr = ocr;
            _pinAction = pinAction;
            if (dpiScale <= 0)
                dpiScale = 1;

            Title = "FreePin - 编辑截图";
            Background = UiTheme.Brush(UiTheme.Background);
            Foreground = UiTheme.Brush(UiTheme.Foreground);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = true;
            MinWidth = 640;
            MinHeight = 480;

            double displayedWidth = source.PixelWidth / dpiScale;
            double displayedHeight = source.PixelHeight / dpiScale;
            Width = Math.Min(1680, Math.Max(720, displayedWidth + 80));
            Height = Math.Min(960, Math.Max(520, displayedHeight + 160));

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var toolbar = BuildToolbar();
            Grid.SetRow(toolbar, 0);
            root.Children.Add(toolbar);

            _canvas = new AnnotationCanvas();
            _canvas.SetImage(source, baseBitmap);
            _canvas.LayoutTransform = new ScaleTransform(1.0 / dpiScale, 1.0 / dpiScale);
            _canvas.HistoryChanged += (s, e) => UpdateHistoryButtons();

            var scroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = UiTheme.Brush(Color.FromRgb(13, 15, 18)),
                Padding = new Thickness(10)
            };
            var canvasHost = new Border
            {
                Background = UiTheme.Brush(Color.FromRgb(13, 15, 18)),
                BorderBrush = UiTheme.Brush(UiTheme.Border),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = _canvas
            };
            scroll.Content = canvasHost;
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            var statusBar = new Border
            {
                Background = UiTheme.Brush(UiTheme.Panel),
                BorderBrush = UiTheme.Brush(UiTheme.Border),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(12, 5, 12, 5)
            };
            _status = UiTheme.Text(string.Empty, 12, UiTheme.Muted);
            statusBar.Child = _status;
            Grid.SetRow(statusBar, 2);
            root.Children.Add(statusBar);

            Content = root;
            UpdateStatus("拖动鼠标绘制标注；选择工具可拖动已添加的标注。");
            UpdateHistoryButtons();

            PreviewKeyDown += OnPreviewKeyDown;
            Closed += (s, e) => DisposeBitmap();

            if (openOcrOnLoad)
            {
                Loaded += async (s, e) => await RunOcrAsync();
            }
        }

        private UIElement BuildToolbar()
        {
            var border = new Border
            {
                Background = UiTheme.Brush(UiTheme.Panel),
                BorderBrush = UiTheme.Brush(UiTheme.Border),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(10, 7, 10, 7)
            };

            // Keep the actions pinned to the right. The drawing tools can wrap
            // independently when the editor window is narrow, instead of
            // splitting the action labels across two rows.
            var dock = new DockPanel();
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            actions.Children.Add(Separator());
            _ocrButton = ActionButton("识别文字", "使用 Windows OCR 识别文字", async (s, e) => await RunOcrAsync());
            actions.Children.Add(_ocrButton);
            actions.Children.Add(ActionButton("复制", "复制到剪贴板 (Ctrl+C)", (s, e) => CopyToClipboard(), true));
            actions.Children.Add(ActionButton("贴图", "把当前图片贴到桌面最上层", (s, e) => Pin()));
            actions.Children.Add(ActionButton("保存", "保存为 PNG (Ctrl+S)", (s, e) => SaveAs(), true));
            actions.Children.Add(ActionButton("关闭", "关闭编辑器 (Esc)", (s, e) => Close()));
            DockPanel.SetDock(actions, Dock.Right);
            dock.Children.Add(actions);

            var main = new WrapPanel { Orientation = Orientation.Horizontal };
            AddTool(main, "↖", "选择/移动 (V)", EditorTool.Select);
            AddTool(main, "□", "矩形 (R)", EditorTool.Rectangle);
            AddTool(main, "○", "椭圆 (O)", EditorTool.Ellipse);
            AddTool(main, "↗", "箭头 (A)", EditorTool.Arrow);
            AddTool(main, "╱", "直线 (L)", EditorTool.Line);
            AddTool(main, "✎", "自由画笔 (P)", EditorTool.Freehand);
            AddTool(main, "T", "文字 (T)", EditorTool.Text);
            AddTool(main, "▦", "马赛克 (M)", EditorTool.Mosaic);

            main.Children.Add(Separator());

            var colors = new[]
            {
                Color.FromRgb(240, 62, 62), Color.FromRgb(255, 146, 43),
                Color.FromRgb(250, 176, 5), Color.FromRgb(64, 192, 87),
                Color.FromRgb(34, 184, 207), Color.FromRgb(34, 139, 230),
                Color.FromRgb(121, 80, 242), Color.FromRgb(33, 37, 41),
                Color.FromRgb(255, 255, 255)
            };
            foreach (Color color in colors)
                AddColor(main, color);

            main.Children.Add(Separator());
            var thicknessLabel = UiTheme.Text("线宽", 12, UiTheme.Muted);
            thicknessLabel.Margin = new Thickness(5, 0, 3, 0);
            main.Children.Add(thicknessLabel);
            var thickness = new ComboBox
            {
                Width = 56,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center,
                SelectedIndex = 2
            };
            foreach (string item in new[] { "1", "2", "3", "5", "8" })
                thickness.Items.Add(item);
            thickness.SelectionChanged += (s, e) =>
            {
                double value;
                if (double.TryParse(thickness.SelectedItem as string, out value))
                    _canvas.StrokeThickness = value;
            };
            main.Children.Add(thickness);

            main.Children.Add(Separator());
            _undoButton = UiTheme.ToolButton("↶", "撤销 (Ctrl+Z)", (s, e) => _canvas.Undo());
            _redoButton = UiTheme.ToolButton("↷", "重做 (Ctrl+Y)", (s, e) => _canvas.Redo());
            main.Children.Add(_undoButton);
            main.Children.Add(_redoButton);
            dock.Children.Add(main);

            border.Child = dock;
            return border;
        }

        private static Button ActionButton(string text, string toolTip, RoutedEventHandler click, bool accent = false)
        {
            Button button = UiTheme.Button(text, toolTip, click, accent);
            button.Padding = new Thickness(8, 5, 8, 5);
            button.Margin = new Thickness(2, 0, 2, 0);
            System.Windows.Automation.AutomationProperties.SetName(button, text);
            System.Windows.Automation.AutomationProperties.SetHelpText(button, toolTip);
            return button;
        }

        private void AddTool(Panel panel, string glyph, string tooltip, EditorTool tool)
        {
            Button button = UiTheme.ToolButton(glyph, tooltip, (s, e) => SetTool(tool));
            string name = tooltip;
            int suffix = name.IndexOf(" (", StringComparison.Ordinal);
            if (suffix > 0)
                name = name.Substring(0, suffix);
            System.Windows.Automation.AutomationProperties.SetName(button, name);
            _toolButtons[tool] = button;
            panel.Children.Add(button);
        }

        private void AddColor(Panel panel, Color color)
        {
            var button = new Button
            {
                Width = 20,
                Height = 20,
                Margin = new Thickness(1, 0, 1, 0),
                Background = UiTheme.Brush(color),
                BorderBrush = UiTheme.Brush(UiTheme.Border),
                BorderThickness = new Thickness(2),
                ToolTip = "设置颜色",
                Cursor = Cursors.Hand,
                Padding = new Thickness(0)
            };
            System.Windows.Automation.AutomationProperties.SetName(button, "颜色");
            System.Windows.Automation.AutomationProperties.SetHelpText(button, "设置标注颜色");
            button.Click += (s, e) => SetColor(color);
            _colorButtons.Add(button);
            panel.Children.Add(button);
        }

        private static UIElement Separator()
        {
            return new Border
            {
                Width = 1,
                Height = 24,
                Margin = new Thickness(7, 0, 7, 0),
                Background = UiTheme.Brush(UiTheme.Border),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private void SetTool(EditorTool tool)
        {
            _canvas.Tool = tool;
            foreach (var pair in _toolButtons)
                UiTheme.SetSelected(pair.Value, pair.Key == tool);
            UpdateStatus(tool == EditorTool.Text
                ? "点击图片位置后输入文字，回车确认。"
                : "拖动鼠标绘制；选择箭头可拖动已有标注，Delete 删除。");
        }

        private void SetColor(Color color)
        {
            _currentColor = color;
            _canvas.StrokeColor = color;
            foreach (Button button in _colorButtons)
            {
                bool selected = button.Background is SolidColorBrush &&
                                ((SolidColorBrush)button.Background).Color == color;
                button.BorderBrush = UiTheme.Brush(selected ? UiTheme.Accent : UiTheme.Border);
                button.BorderThickness = new Thickness(selected ? 3 : 2);
            }
        }

        private void UpdateHistoryButtons()
        {
            if (_undoButton != null)
                _undoButton.IsEnabled = _canvas.CanUndo;
            if (_redoButton != null)
                _redoButton.IsEnabled = _canvas.CanRedo;
        }

        private void UpdateStatus(string message)
        {
            if (_status != null)
                _status.Text = message;
        }

        private async System.Threading.Tasks.Task RunOcrAsync()
        {
            if (_ocrBusy)
                return;
            if (_ocr == null || !_ocr.IsAvailable)
            {
                MessageBox.Show(this, "系统 OCR 不可用。\n请确认 Windows 已安装中文 OCR 语言包。",
                    "FreePin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _ocrBusy = true;
            if (_ocrButton != null)
                _ocrButton.IsEnabled = false;
            UpdateStatus("正在识别文字…");
            try
            {
                BitmapSource rendered = _canvas.RenderFlattened();
                BitmapSource preview = AnnotationRenderer.EncodePngSource(rendered);
                string text = await _ocr.RecognizeAsync(rendered);
                if (string.IsNullOrWhiteSpace(text))
                {
                    UpdateStatus("没有识别到文字。");
                    MessageBox.Show(this, "没有识别到文字。可以尝试放大截图区域后重试。",
                        "FreePin", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = new TextResultWindow(preview, text, "Windows OCR · " + _ocr.LanguageName);
                result.Show();
                UpdateStatus("识别完成，已打开结果窗口。");
            }
            catch (Exception ex)
            {
                Log.Error("OCR 识别失败", ex);
                UpdateStatus("识别失败：" + ex.Message);
                MessageBox.Show(this, "识别失败：" + ex.Message, "FreePin",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _ocrBusy = false;
                if (_ocrButton != null)
                    _ocrButton.IsEnabled = true;
            }
        }

        private void CopyToClipboard()
        {
            try
            {
                BitmapSource image = _canvas.RenderFlattened();
                ImageUtil.CopyToClipboard(image);
                UpdateStatus("已复制到剪贴板。");
            }
            catch (Exception ex)
            {
                Log.Error("复制截图失败", ex);
                UpdateStatus("复制失败：" + ex.Message);
            }
        }

        private void Pin()
        {
            try
            {
                BitmapSource image = _canvas.RenderFlattened();
                Action<BitmapSource> action = _pinAction;
                Close();
                if (action != null)
                    Dispatcher.BeginInvoke(new Action(() => action(image)));
            }
            catch (Exception ex)
            {
                Log.Error("贴图失败", ex);
                UpdateStatus("贴图失败：" + ex.Message);
            }
        }

        private void SaveAs()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "保存截图",
                    Filter = "PNG 图片 (*.png)|*.png",
                    DefaultExt = ".png",
                    AddExtension = true,
                    FileName = "FreePin_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png"
                };
                if (dialog.ShowDialog(this) != true)
                    return;
                BitmapSource image = _canvas.RenderFlattened();
                ImageUtil.SavePng(image, dialog.FileName);
                UpdateStatus("已保存：" + dialog.FileName);
            }
            catch (Exception ex)
            {
                Log.Error("保存截图失败", ex);
                UpdateStatus("保存失败：" + ex.Message);
                MessageBox.Show(this, "保存失败：" + ex.Message, "FreePin",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool textEditing = Keyboard.FocusedElement is TextBox;
            if (e.Key == Key.Escape && !textEditing)
            {
                Close();
                e.Handled = true;
                return;
            }

            if (!textEditing && Keyboard.Modifiers == ModifierKeys.None)
            {
                switch (e.Key)
                {
                    case Key.V:
                        SetTool(EditorTool.Select);
                        e.Handled = true;
                        return;
                    case Key.R:
                        SetTool(EditorTool.Rectangle);
                        e.Handled = true;
                        return;
                    case Key.O:
                        SetTool(EditorTool.Ellipse);
                        e.Handled = true;
                        return;
                    case Key.A:
                        SetTool(EditorTool.Arrow);
                        e.Handled = true;
                        return;
                    case Key.L:
                        SetTool(EditorTool.Line);
                        e.Handled = true;
                        return;
                    case Key.P:
                        SetTool(EditorTool.Freehand);
                        e.Handled = true;
                        return;
                    case Key.T:
                        SetTool(EditorTool.Text);
                        e.Handled = true;
                        return;
                    case Key.M:
                        SetTool(EditorTool.Mosaic);
                        e.Handled = true;
                        return;
                }
            }

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (e.Key == Key.S)
                {
                    SaveAs();
                    e.Handled = true;
                }
                else if (e.Key == Key.C && !textEditing)
                {
                    CopyToClipboard();
                    e.Handled = true;
                }
                else if (e.Key == Key.Z)
                {
                    _canvas.Undo();
                    e.Handled = true;
                }
                else if (e.Key == Key.Y)
                {
                    _canvas.Redo();
                    e.Handled = true;
                }
            }
        }

        private void DisposeBitmap()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_baseBitmap != null)
                _baseBitmap.Dispose();
        }
    }
}
