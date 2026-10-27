using System;
using System.IO;
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
    public sealed class CacheWindow : Window
    {
        private readonly CacheService _cache;
        private readonly WrapPanel _items;
        private readonly TextBlock _count;
        private readonly TextBlock _status;
        private bool _refreshing;

        public CacheWindow(CacheService cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));

            Title = "FreePin - 最近截图";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            Width = 980;
            Height = 680;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;
            Topmost = true;
            FontFamily = UiTheme.UiFont;
            Foreground = UiTheme.Brush(UiTheme.Foreground);
            UiTheme.ApplyWindowIcon(this);

            var root = new Border
            {
                Background = UiTheme.Brush(UiTheme.Background),
                BorderBrush = UiTheme.Brush(UiTheme.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(12),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 30,
                    ShadowDepth = 2,
                    Opacity = 0.28,
                    Color = Color.FromRgb(70, 55, 40)
                }
            };

            var panel = new DockPanel();

            var header = new Border
            {
                Background = UiTheme.Brush(UiTheme.Panel),
                CornerRadius = new CornerRadius(11, 11, 0, 0),
                Padding = new Thickness(18, 12, 12, 12)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal };
            titleStack.Children.Add(UiTheme.Text("最近截图", 17, null, true));
            _count = UiTheme.Text(string.Empty, 12, UiTheme.Muted);
            _count.Margin = new Thickness(12, 0, 0, 0);
            titleStack.Children.Add(_count);
            headerGrid.Children.Add(titleStack);

            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            actions.Children.Add(UiTheme.Button("刷新", "重新读取缓存目录", (s, e) => Refresh()));
            var close = UiTheme.Button("关闭", "关闭最近截图 (Esc)", (s, e) => Close());
            actions.Children.Add(close);
            Grid.SetColumn(actions, 1);
            headerGrid.Children.Add(actions);
            header.Child = headerGrid;
            DockPanel.SetDock(header, Dock.Top);
            panel.Children.Add(header);

            var footer = new Border
            {
                Background = UiTheme.Brush(UiTheme.Panel),
                CornerRadius = new CornerRadius(0, 0, 11, 11),
                Padding = new Thickness(18, 8, 18, 8)
            };
            _status = UiTheme.Text("右键点击图片可复制、另存为或删除。", 12, UiTheme.Muted);
            footer.Child = _status;
            DockPanel.SetDock(footer, Dock.Bottom);
            panel.Children.Add(footer);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(12)
            };
            _items = new WrapPanel { Orientation = Orientation.Horizontal };
            scroll.Content = _items;
            panel.Children.Add(scroll);

            root.Child = panel;
            Content = root;

            header.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    try { DragMove(); }
                    catch { /* ignored */ }
                }
            };
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            };
            Loaded += (s, e) => Refresh();
        }

        public void Refresh()
        {
            if (_refreshing)
                return;
            _refreshing = true;
            try
            {
                _items.Children.Clear();
                var entries = _cache.List();
                _count.Text = entries.Count + " 张";
                if (entries.Count == 0)
                {
                    var empty = new StackPanel
                    {
                        Margin = new Thickness(40),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var icon = UiTheme.Text("▣", 44, UiTheme.Border);
                    icon.HorizontalAlignment = HorizontalAlignment.Center;
                    empty.Children.Add(icon);
                    var text = UiTheme.Text("最近还没有截图缓存", 15, UiTheme.Muted);
                    text.HorizontalAlignment = HorizontalAlignment.Center;
                    text.Margin = new Thickness(0, 8, 0, 0);
                    empty.Children.Add(text);
                    var hint = UiTheme.Text("按 Ctrl+1 截图后，图片会自动保存在这里。", 12, UiTheme.Border);
                    hint.HorizontalAlignment = HorizontalAlignment.Center;
                    hint.Margin = new Thickness(0, 6, 0, 0);
                    empty.Children.Add(hint);
                    _items.Children.Add(empty);
                    return;
                }

                foreach (CacheEntry entry in entries)
                    _items.Children.Add(BuildCard(entry));
            }
            catch (Exception ex)
            {
                Log.Error("刷新最近截图失败", ex);
                _status.Text = "读取缓存失败：" + ex.Message;
            }
            finally
            {
                _refreshing = false;
            }
        }

        private UIElement BuildCard(CacheEntry entry)
        {
            var image = new Image
            {
                Width = 176,
                Height = 106,
                Stretch = Stretch.Uniform,
                Source = _cache.LoadThumbnail(entry, 240)
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

            var imageBorder = new Border
            {
                Background = UiTheme.Brush(UiTheme.CanvasBackground),
                Child = image,
                CornerRadius = new CornerRadius(5)
            };

            var time = UiTheme.Text(entry.DisplayTime, 12, UiTheme.Foreground);
            var size = UiTheme.Text(ImageUtil.HumanFileSize(entry.Size), 11, UiTheme.Muted);
            size.Margin = new Thickness(0, 2, 0, 0);

            var info = new StackPanel { Margin = new Thickness(2, 6, 2, 0) };
            info.Children.Add(time);
            info.Children.Add(size);

            var stack = new StackPanel();
            stack.Children.Add(imageBorder);
            stack.Children.Add(info);

            var card = new Border
            {
                Width = 196,
                Height = 166,
                Margin = new Thickness(6),
                Padding = new Thickness(8),
                Background = UiTheme.Brush(UiTheme.Panel),
                BorderBrush = UiTheme.Brush(UiTheme.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Child = stack,
                Tag = entry,
                ToolTip = entry.FileName
            };

            var menu = UiTheme.ContextMenu(
                UiTheme.MenuItem("复制图片", (s, e) => CopyEntry(entry)),
                UiTheme.MenuItem("另存为...", (s, e) => SaveEntry(entry)),
                new Separator(),
                UiTheme.MenuItem("删除", (s, e) => DeleteEntry(entry)));
            card.ContextMenu = menu;
            card.MouseEnter += (s, e) =>
            {
                card.Background = UiTheme.Brush(UiTheme.PanelHover);
                card.BorderBrush = UiTheme.Brush(UiTheme.Accent);
            };
            card.MouseLeave += (s, e) =>
            {
                card.Background = UiTheme.Brush(UiTheme.Panel);
                card.BorderBrush = UiTheme.Brush(UiTheme.Border);
            };
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    CopyEntry(entry);
                    e.Handled = true;
                }
            };
            return card;
        }

        private void CopyEntry(CacheEntry entry)
        {
            try
            {
                BitmapSource source = ImageUtil.LoadBitmapSource(entry.Path);
                ImageUtil.CopyToClipboard(source);
                _status.Text = "已复制到剪贴板：" + entry.FileName;
            }
            catch (Exception ex)
            {
                _status.Text = "复制失败：" + ex.Message;
            }
        }

        private void SaveEntry(CacheEntry entry)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "另存截图",
                    Filter = "PNG 图片 (*.png)|*.png",
                    DefaultExt = ".png",
                    AddExtension = true,
                    FileName = entry.FileName
                };
                if (dialog.ShowDialog(this) != true)
                    return;
                File.Copy(entry.Path, dialog.FileName, true);
                _status.Text = "已另存为：" + dialog.FileName;
            }
            catch (Exception ex)
            {
                _status.Text = "另存为失败：" + ex.Message;
            }
        }

        private void DeleteEntry(CacheEntry entry)
        {
            MessageBoxResult result = MessageBox.Show(this,
                "确定要删除这张缓存图片吗？\n" + entry.FileName + "\n\n删除后无法恢复。",
                "FreePin", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;
            if (_cache.Delete(entry))
            {
                _status.Text = "已删除：" + entry.FileName;
                Refresh();
            }
            else
            {
                _status.Text = "删除失败，文件可能被占用。";
            }
        }
    }
}
