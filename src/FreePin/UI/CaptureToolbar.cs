using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfColor = System.Windows.Media.Color;
using WpfColorDialog = System.Windows.Forms.ColorDialog;
using WpfDialogResult = System.Windows.Forms.DialogResult;

namespace FreePin.UI
{
    internal sealed class CaptureToolbar : Border
    {
        private readonly CaptureStyle _style;
        private readonly Dictionary<CaptureTool, Button> _toolButtons = new Dictionary<CaptureTool, Button>();
        private readonly List<Border> _colorSwatches = new List<Border>();
        private readonly TextBlock _sizeText;
        private readonly TextBlock _thicknessText;
        private readonly Ellipse _thicknessDot;
        private readonly Button _roundedButton;
        private readonly Button _undoButton;
        private readonly Button _redoButton;
        private CaptureTool _tool = CaptureTool.Select;
        private Color _selectedColor;

        public CaptureToolbar(CaptureStyle style)
        {
            _style = style ?? throw new ArgumentNullException(nameof(style));
            _selectedColor = style.Color;

            Background = UiTheme.Brush(UiTheme.Panel);
            BorderBrush = UiTheme.Brush(UiTheme.Border);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(9);
            Padding = new Thickness(9, 7, 9, 7);
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            SnapsToDevicePixels = true;
            MaxWidth = 1180;

            var content = new StackPanel();
            var tools = new WrapPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(tools);

            AddTool(tools, "↖", "选择 (V)", CaptureTool.Select);
            AddTool(tools, "✎", "画笔 (P)", CaptureTool.Pen);
            AddTool(tools, "╱", "直线 (L)", CaptureTool.Line);
            AddTool(tools, "↗", "箭头 (A)", CaptureTool.Arrow);
            AddTool(tools, "□", "矩形 (R)，滚轮调粗细", CaptureTool.Rectangle);
            AddTool(tools, "T", "文字 (T)", CaptureTool.Text);
            AddTool(tools, "◎", "取色器 (I)", CaptureTool.Eyedropper);

            tools.Children.Add(MakeSeparator());

            foreach (Color color in new[]
            {
                Color.FromRgb(180, 78, 50), Color.FromRgb(217, 119, 46),
                Color.FromRgb(217, 162, 27), Color.FromRgb(94, 140, 97),
                Color.FromRgb(60, 140, 140), Color.FromRgb(63, 111, 168),
                Color.FromRgb(123, 90, 166), Color.FromRgb(47, 42, 37),
                Color.FromRgb(255, 253, 247)
            })
            {
                AddColorSwatch(tools, color);
            }

            var custom = UiTheme.ToolButton("＋", "自定义颜色", (s, e) => PickCustomColor());
            custom.FontSize = 15;
            tools.Children.Add(custom);
            tools.Children.Add(MakeSeparator());

            var thickness = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0)
            };
            var dotHost = new Grid { Width = 28, Height = 28, ToolTip = "笔触大小；滚轮调整" };
            var dotBack = new Ellipse { Fill = UiTheme.Brush(Color.FromArgb(30, 47, 42, 37)) };
            _thicknessDot = new Ellipse { Fill = UiTheme.Brush(_style.Color) };
            dotHost.Children.Add(dotBack);
            dotHost.Children.Add(_thicknessDot);
            dotHost.MouseWheel += (s, e) =>
            {
                ChangeThickness(e.Delta > 0 ? 1 : -1);
                e.Handled = true;
            };
            thickness.Children.Add(dotHost);
            _thicknessText = UiTheme.Text("3 px", 12, UiTheme.Foreground, true);
            _thicknessText.MinWidth = 40;
            _thicknessText.ToolTip = "笔触大小；滚轮调整";
            _thicknessText.MouseWheel += (s, e) =>
            {
                ChangeThickness(e.Delta > 0 ? 1 : -1);
                e.Handled = true;
            };
            thickness.Children.Add(_thicknessText);
            tools.Children.Add(thickness);

            _roundedButton = UiTheme.ToolButton("圆", "矩形直角 / 圆角", (s, e) =>
            {
                _style.RoundedRectangle = !_style.RoundedRectangle;
                UpdateStyleVisuals();
                RoundedChanged?.Invoke(_style.RoundedRectangle);
            });
            _roundedButton.Width = 48;
            _roundedButton.ToolTip = "直角 / 圆角矩形";
            tools.Children.Add(_roundedButton);

            _undoButton = UiTheme.ToolButton("↶", "撤销 (Ctrl+Z)", (s, e) => UndoRequested?.Invoke());
            _redoButton = UiTheme.ToolButton("↷", "重做 (Ctrl+Y)", (s, e) => RedoRequested?.Invoke());
            tools.Children.Add(_undoButton);
            tools.Children.Add(_redoButton);

            var actionRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _sizeText = UiTheme.Text(string.Empty, 12, UiTheme.Muted);
            _sizeText.Margin = new Thickness(4, 0, 10, 0);
            actionRow.Children.Add(_sizeText);
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            actions.Children.Add(UiTheme.Button("复制", "复制到剪贴板 (Enter)", (s, e) => ActionRequested?.Invoke(CaptureAction.Copy), true));
            actions.Children.Add(UiTheme.Button("保存", "另存为 PNG (Ctrl+S)", (s, e) => ActionRequested?.Invoke(CaptureAction.Save)));
            actions.Children.Add(UiTheme.Button("贴图", "贴到桌面最上层", (s, e) => ActionRequested?.Invoke(CaptureAction.Pin)));
            actions.Children.Add(UiTheme.Button("文字识别", "识别选区文字", (s, e) => ActionRequested?.Invoke(CaptureAction.Ocr)));
            actions.Children.Add(UiTheme.Button("长截图", "滚动页面拼接成长截图", (s, e) => ActionRequested?.Invoke(CaptureAction.LongCapture)));
            actions.Children.Add(UiTheme.Button("取消", "取消截图 (Esc)", (s, e) => ActionRequested?.Invoke(CaptureAction.Cancel)));
            Grid.SetColumn(actions, 1);
            actionRow.Children.Add(actions);
            content.Children.Add(actionRow);

            Child = content;
            UpdateStyleVisuals();
            SetTool(CaptureTool.Select, false);
        }

        public event Action<CaptureTool> ToolChanged;
        public event Action<Color> ColorChanged;
        public event Action<double> ThicknessChanged;
        public event Action<double> FontSizeChanged;
        public event Action<bool> RoundedChanged;
        public event Action<CaptureAction> ActionRequested;
        public event Action UndoRequested;
        public event Action RedoRequested;

        public void SetSelectionSize(int width, int height)
        {
            _sizeText.Text = width + " × " + height + " px";
        }

        public void SetTool(CaptureTool tool, bool raise = true)
        {
            _tool = tool;
            foreach (KeyValuePair<CaptureTool, Button> pair in _toolButtons)
                UiTheme.SetSelected(pair.Value, pair.Key == tool);
            UpdateStyleVisuals();
            if (raise)
                ToolChanged?.Invoke(tool);
        }

        public void SetStyle()
        {
            _selectedColor = _style.Color;
            UpdateStyleVisuals();
        }

        public void SetHistory(bool canUndo, bool canRedo)
        {
            _undoButton.IsEnabled = canUndo;
            _redoButton.IsEnabled = canRedo;
            _undoButton.Opacity = canUndo ? 1 : 0.45;
            _redoButton.Opacity = canRedo ? 1 : 0.45;
        }

        private void AddTool(Panel parent, string glyph, string tooltip, CaptureTool tool)
        {
            var button = UiTheme.ToolButton(glyph, tooltip, (s, e) => SetTool(tool));
            AutomationProperties.SetName(button, tooltip);
            parent.Children.Add(button);
            _toolButtons[tool] = button;
        }

        private void AddColorSwatch(Panel parent, Color color)
        {
            var swatch = new Border
            {
                Width = 20,
                Height = 20,
                Margin = new Thickness(2),
                Background = UiTheme.Brush(color),
                BorderBrush = UiTheme.Brush(UiTheme.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Cursor = Cursors.Hand,
                ToolTip = string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B)
            };
            swatch.MouseLeftButtonDown += (s, e) =>
            {
                SetColor(color);
                e.Handled = true;
            };
            parent.Children.Add(swatch);
            _colorSwatches.Add(swatch);
        }

        private void SetColor(Color color)
        {
            _style.Color = color;
            _selectedColor = color;
            UpdateStyleVisuals();
            ColorChanged?.Invoke(color);
        }

        private void PickCustomColor()
        {
            using (var dialog = new WpfColorDialog())
            {
                dialog.FullOpen = true;
                dialog.Color = System.Drawing.Color.FromArgb(_style.Color.R, _style.Color.G, _style.Color.B);
                if (dialog.ShowDialog() == WpfDialogResult.OK)
                {
                    SetColor(Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B));
                }
            }
        }

        private void ChangeThickness(double delta)
        {
            if (_tool == CaptureTool.Text)
            {
                _style.FontSize = Math.Max(10, Math.Min(96, _style.FontSize + delta));
                UpdateStyleVisuals();
                FontSizeChanged?.Invoke(_style.FontSize);
            }
            else
            {
                _style.Thickness = Math.Max(1, Math.Min(40, _style.Thickness + delta));
                UpdateStyleVisuals();
                ThicknessChanged?.Invoke(_style.Thickness);
            }
        }

        private void UpdateStyleVisuals()
        {
            if (_thicknessDot == null)
                return;
            _thicknessDot.Fill = UiTheme.Brush(_selectedColor);
            double dot = Math.Max(3, Math.Min(24, _style.Thickness + 2));
            _thicknessDot.Width = dot;
            _thicknessDot.Height = dot;
            _thicknessText.Text = (_tool == CaptureTool.Text
                ? Math.Round(_style.FontSize)
                : Math.Round(_style.Thickness)) + " px";
            _roundedButton.Content = _style.RoundedRectangle ? "圆角" : "直角";
            _roundedButton.ToolTip = _style.RoundedRectangle ? "当前圆角矩形，点击切换直角" : "当前直角矩形，点击切换圆角";
            AutomationProperties.SetName(_roundedButton, _roundedButton.ToolTip as string ?? "矩形圆角");
            foreach (Border swatch in _colorSwatches)
            {
                SolidColorBrush brush = swatch.Background as SolidColorBrush;
                bool selected = brush != null && brush.Color == _selectedColor;
                swatch.BorderBrush = UiTheme.Brush(selected ? UiTheme.Accent : UiTheme.Border);
                swatch.BorderThickness = new Thickness(selected ? 2 : 1);
            }
        }

        private static Border MakeSeparator()
        {
            return new Border
            {
                Width = 1,
                Height = 22,
                Background = UiTheme.Brush(UiTheme.Border),
                Margin = new Thickness(5, 3, 5, 3),
                VerticalAlignment = VerticalAlignment.Center
            };
        }
    }
}
