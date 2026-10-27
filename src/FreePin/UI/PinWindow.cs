using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using FreePin.Imaging;
using FreePin.Services;
using Microsoft.Win32;

namespace FreePin.UI
{
    public sealed class PinWindow : Window
    {
        private readonly BitmapSource _image;

        public PinWindow(BitmapSource image, double dpiScale)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
            if (dpiScale <= 0)
                dpiScale = 1;

            double width = image.PixelWidth / dpiScale;
            double height = image.PixelHeight / dpiScale;

            Title = "FreePin - 贴图";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            Width = width + 4;
            Height = height + 4;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Focusable = true;

            var border = new Border
            {
                Background = UiTheme.Brush(UiTheme.CanvasBackground),
                BorderBrush = new SolidColorBrush(Color.FromArgb(220, 223, 208, 188)),
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 20,
                    ShadowDepth = 2,
                    Opacity = 0.35,
                    Color = Colors.Black
                }
            };

            var imageControl = new Image
            {
                Source = image,
                Width = width,
                Height = height,
                Stretch = Stretch.Fill
            };
            RenderOptions.SetBitmapScalingMode(imageControl, BitmapScalingMode.HighQuality);
            border.Child = imageControl;
            Content = border;

            var menu = UiTheme.ContextMenu(
                UiTheme.MenuItem("复制图片", (s, e) => CopyImage()),
                UiTheme.MenuItem("另存为...", (s, e) => SaveAs()),
                new Separator(),
                UiTheme.MenuItem("关闭贴图", (s, e) => Close()));
            ContextMenu = menu;
            border.ContextMenu = menu;

            MouseLeftButtonDown += (s, e) =>
            {
                Activate();
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    try { DragMove(); }
                    catch { /* ignored */ }
                }
            };
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape || e.Key == Key.Delete)
                {
                    Close();
                    e.Handled = true;
                }
            };
            Loaded += (s, e) => Activate();
        }

        private void CopyImage()
        {
            try
            {
                ImageUtil.CopyToClipboard(_image);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "复制失败：" + ex.Message, "FreePin",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveAs()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "保存贴图",
                    Filter = "PNG 图片 (*.png)|*.png",
                    DefaultExt = ".png",
                    AddExtension = true,
                    FileName = "FreePin_Pin_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png"
                };
                if (dialog.ShowDialog(this) == true)
                    ImageUtil.SavePng(_image, dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存失败：" + ex.Message, "FreePin",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
