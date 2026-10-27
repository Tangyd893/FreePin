using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FreePin.UI
{
    public sealed class TextResultWindow : Window
    {
        public TextResultWindow(BitmapSource preview, string text, string language)
        {
            Title = "FreePin - 文字识别";
            Width = 760;
            Height = 520;
            MinWidth = 520;
            MinHeight = 360;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;
            Topmost = true;
            UiTheme.ApplyWindow(this);

            var root = new DockPanel { Margin = new Thickness(14) };

            var toolbar = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var title = UiTheme.Text("识别结果", 16, null, true);
            DockPanel.SetDock(title, Dock.Left);
            toolbar.Children.Add(title);

            var close = UiTheme.Button("关闭", "关闭窗口", (s, e) => Close());
            DockPanel.SetDock(close, Dock.Right);
            toolbar.Children.Add(close);

            var copy = UiTheme.Button("复制文字", "复制全部识别文字", (s, e) =>
            {
                try
                {
                    Clipboard.SetText(_text.Text ?? string.Empty);
                    _status.Text = "已复制到剪贴板";
                }
                catch (Exception ex)
                {
                    _status.Text = "复制失败：" + ex.Message;
                }
            }, true);
            DockPanel.SetDock(copy, Dock.Right);
            toolbar.Children.Add(copy);

            var languageLabel = UiTheme.Text(language, 12, UiTheme.Muted);
            languageLabel.Margin = new Thickness(16, 0, 0, 0);
            DockPanel.SetDock(languageLabel, Dock.Left);
            toolbar.Children.Add(languageLabel);

            DockPanel.SetDock(toolbar, Dock.Top);
            root.Children.Add(toolbar);

            var status = UiTheme.Text("识别结果可直接编辑，改完再复制。", 12, UiTheme.Muted);
            status.Margin = new Thickness(0, 8, 0, 0);
            _status = status;
            DockPanel.SetDock(status, Dock.Bottom);
            root.Children.Add(status);

            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var imageBorder = new Border
            {
                Background = UiTheme.Brush(UiTheme.Panel),
                BorderBrush = UiTheme.Brush(UiTheme.CanvasBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6)
            };
            var image = new Image
            {
                Source = preview,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            imageBorder.Child = image;
            Grid.SetColumn(imageBorder, 0);
            body.Children.Add(imageBorder);

            var textBox = new TextBox
            {
                Text = text ?? string.Empty,
                IsReadOnly = false,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = UiTheme.Brush(UiTheme.Panel),
                Foreground = UiTheme.Brush(UiTheme.Foreground),
                BorderBrush = UiTheme.Brush(UiTheme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                FontFamily = UiTheme.UiFont,
                FontSize = 15
            };
            _text = textBox;
            Grid.SetColumn(textBox, 2);
            body.Children.Add(textBox);

            root.Children.Add(body);
            Content = root;
            Loaded += (s, e) =>
            {
                textBox.Focus();
                textBox.CaretIndex = textBox.Text.Length;
            };
        }

        private TextBox _text;
        private TextBlock _status;
    }
}
