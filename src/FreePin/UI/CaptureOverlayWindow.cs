using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FreePin.Imaging;
using FreePin.Services;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingRectangle = System.Drawing.Rectangle;

namespace FreePin.UI
{
    public enum CaptureAction
    {
        Edit,
        Copy,
        Pin,
        Save,
        Ocr,
        LongCapture
    }

    public sealed class CaptureResult
    {
        public DrawingBitmap Image { get; set; }
        public BitmapSource Source { get; set; }
        public CaptureAction Action { get; set; }
        public DrawingRectangle ScreenBounds { get; set; }
    }

    public sealed class CaptureOverlayWindow : Window
    {
        private readonly ScreenFrame _frame;
        private readonly double _scale;
        private readonly Action<CaptureResult> _completed;
        private readonly CaptureSurface _surface;
        private readonly Border _toolbar;
        private readonly TextBlock _size;
        private bool _finished;

        public CaptureOverlayWindow(ScreenFrame frame, double dpiScale, Action<CaptureResult> completed)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _completed = completed;
            _scale = dpiScale <= 0 ? 1 : dpiScale;

            Title = "FreePin - 截图";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.Manual;

            Left = frame.Bounds.Left / _scale;
            Top = frame.Bounds.Top / _scale;
            Width = frame.Bounds.Width / _scale;
            Height = frame.Bounds.Height / _scale;

            var root = new Grid { ClipToBounds = true };
            _surface = new CaptureSurface(frame, _scale);
            _surface.SelectionChanged += (s, e) => UpdateToolbar();
            root.Children.Add(_surface);

            _toolbar = new Border
            {
                Background = UiTheme.Brush(UiTheme.Panel),
                BorderBrush = UiTheme.Brush(UiTheme.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 7, 10, 7),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0),
                Visibility = Visibility.Collapsed
            };

            var content = new StackPanel { Orientation = Orientation.Horizontal };
            _size = UiTheme.Text(string.Empty, 12, UiTheme.Muted);
            _size.Margin = new Thickness(0, 0, 12, 0);
            content.Children.Add(_size);
            content.Children.Add(UiTheme.Button("编辑", "进入标注编辑器", (s, e) => Finish(CaptureAction.Edit)));
            content.Children.Add(UiTheme.Button("复制", "复制到剪贴板 (Enter)", (s, e) => Finish(CaptureAction.Copy), true));
            content.Children.Add(UiTheme.Button("贴图", "把选区贴到桌面最上层", (s, e) => Finish(CaptureAction.Pin)));
            content.Children.Add(UiTheme.Button("保存", "另存为 PNG", (s, e) => Finish(CaptureAction.Save)));
            content.Children.Add(UiTheme.Button("长截图", "滚动页面拼接成长截图", (s, e) => Finish(CaptureAction.LongCapture)));
            content.Children.Add(UiTheme.Button("识别文字", "识别选区文字", (s, e) => Finish(CaptureAction.Ocr)));
            content.Children.Add(UiTheme.Button("取消", "取消截图 (Esc)", (s, e) => Cancel()));
            _toolbar.Child = content;
            root.Children.Add(_toolbar);

            Content = root;
            PreviewKeyDown += OnPreviewKeyDown;
            Closed += (s, e) =>
            {
                _frame.Dispose();
                if (!_finished)
                {
                    // Dialog closed without a result; nothing else to do.
                }
            };
            Loaded += (s, e) =>
            {
                Activate();
                _surface.Focus();
                Keyboard.Focus(_surface);
                PlaceToolbar();
            };
        }

        private void UpdateToolbar()
        {
            DrawingRectangle selection = _surface.Selection;
            bool hasSelection = selection.Width >= 2 && selection.Height >= 2 && !_surface.IsDragging;
            _toolbar.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
            if (hasSelection)
            {
                _size.Text = selection.Width + " × " + selection.Height + "  ";
                SchedulePlaceToolbar();
            }
        }

        private void SchedulePlaceToolbar()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(PlaceToolbar));
        }

        private void PlaceToolbar()
        {
            if (_finished || _toolbar.Visibility != Visibility.Visible)
                return;

            double overlayWidth = ActualWidth > 0 ? ActualWidth : Width;
            double overlayHeight = ActualHeight > 0 ? ActualHeight : Height;
            if (overlayWidth <= 0 || overlayHeight <= 0)
                return;

            _toolbar.MaxWidth = Math.Max(1, overlayWidth - 16);
            _toolbar.Measure(new Size(_toolbar.MaxWidth, double.PositiveInfinity));
            Size toolbarSize = _toolbar.DesiredSize;
            if (toolbarSize.Width <= 0 || toolbarSize.Height <= 0)
                toolbarSize = new Size(_toolbar.ActualWidth, _toolbar.ActualHeight);
            if (toolbarSize.Width <= 0 || toolbarSize.Height <= 0)
                return;

            DrawingRectangle selection = _surface.Selection;
            var selectionRect = new Rect(
                selection.X / _scale,
                selection.Y / _scale,
                selection.Width / _scale,
                selection.Height / _scale);
            Rect placed = ComputeToolbarRect(
                new Size(overlayWidth, overlayHeight), selectionRect, toolbarSize);
            _toolbar.Margin = new Thickness(placed.X, placed.Y, 0, 0);
        }

        internal static Rect ComputeToolbarRect(
            Size overlaySize, Rect selection, Size toolbarSize,
            double gap = 8, double edge = 8)
        {
            if (overlaySize.Width <= 0 || overlaySize.Height <= 0)
                return new Rect(0, 0, 0, 0);

            double toolbarWidth = Math.Max(1, toolbarSize.Width);
            double toolbarHeight = Math.Max(1, toolbarSize.Height);
            double maxX = Math.Max(edge, overlaySize.Width - toolbarWidth - edge);
            double x = selection.X + (selection.Width - toolbarWidth) / 2;
            x = Math.Max(edge, Math.Min(maxX, x));

            double below = selection.Bottom + gap;
            double above = selection.Top - gap - toolbarHeight;
            double y;
            if (below + toolbarHeight <= overlaySize.Height - edge)
            {
                y = below;
            }
            else if (above >= edge)
            {
                y = above;
            }
            else
            {
                double inside = selection.Bottom - toolbarHeight - gap;
                double maxY = Math.Max(edge, overlaySize.Height - toolbarHeight - edge);
                y = Math.Max(edge, Math.Min(maxY, inside));
            }

            return new Rect(
                Math.Round(x), Math.Round(y),
                Math.Round(toolbarWidth), Math.Round(toolbarHeight));
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Cancel();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (_surface.HasSelection)
                    Finish(CaptureAction.Copy);
                e.Handled = true;
            }
        }

        private void Cancel()
        {
            _finished = true;
            Close();
        }

        private void Finish(CaptureAction action)
        {
            if (_finished || !_surface.HasSelection)
                return;

            DrawingRectangle selection = _surface.Selection;
            System.Drawing.Bitmap cropped;
            try
            {
                cropped = ScreenCapture.Crop(_frame.Bitmap, selection);
            }
            catch (Exception ex)
            {
                Log.Error("裁剪截图失败", ex);
                MessageBox.Show(this, "裁剪截图失败：" + ex.Message, "FreePin",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BitmapSource source = ScreenCapture.ToBitmapSource(cropped);
            source.Freeze();
            var result = new CaptureResult
            {
                Image = cropped,
                Source = source,
                Action = action,
                ScreenBounds = new DrawingRectangle(
                    _frame.Bounds.Left + selection.X,
                    _frame.Bounds.Top + selection.Y,
                    selection.Width,
                    selection.Height)
            };
            _finished = true;

            // Keep the overlay in the foreground while the result is handed over,
            // so editors/pins opened from here can take foreground focus.
            try
            {
                if (_completed != null)
                    _completed(result);
            }
            catch (Exception ex)
            {
                Log.Error("处理截图结果失败", ex);
            }

            Hide();
            Close();
        }
    }

    internal sealed class CaptureSurface : FrameworkElement
    {
        private readonly ScreenFrame _frame;
        private readonly double _scale;
        private Point _startLogical;
        private Point _currentLogical;
        private bool _dragging;
        private Point _mouseLogical;
        private DrawingRectangle _selection;

        public CaptureSurface(ScreenFrame frame, double scale)
        {
            _frame = frame;
            _scale = scale <= 0 ? 1 : scale;
            Focusable = true;
            Cursor = Cursors.Cross;
            ClipToBounds = true;
        }

        public event EventHandler SelectionChanged;

        public DrawingRectangle Selection
        {
            get { return _selection; }
        }

        public bool HasSelection
        {
            get { return _selection.Width >= 2 && _selection.Height >= 2; }
        }

        public bool IsDragging
        {
            get { return _dragging; }
        }

        protected override void OnRender(DrawingContext dc)
        {
            var full = new Rect(0, 0, ActualWidth, ActualHeight);
            dc.DrawImage(_frame.ImageSource, full);

            var dim = new SolidColorBrush(Color.FromArgb(105, 0, 0, 0));
            dim.Freeze();
            dc.DrawRectangle(dim, null, full);

            if (HasSelection)
            {
                Rect selected = ToLogical(_selection);
                dc.PushClip(new RectangleGeometry(selected));
                dc.DrawImage(_frame.ImageSource, full);
                dc.Pop();

                var borderBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                borderBrush.Freeze();
                var borderPen = new Pen(borderBrush, 1.5);
                borderPen.Freeze();
                dc.DrawRectangle(null, borderPen, selected);

                DrawHandles(dc, selected);
            }
            else
            {
                DrawHint(dc, full);
            }

            if (_dragging)
            {
                var lineBrush = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255));
                lineBrush.Freeze();
                var linePen = new Pen(lineBrush, 1) { DashStyle = new DashStyle(new double[] { 4, 4 }, 0) };
                linePen.Freeze();
                dc.DrawLine(linePen, new Point(_mouseLogical.X, 0), new Point(_mouseLogical.X, ActualHeight));
                dc.DrawLine(linePen, new Point(0, _mouseLogical.Y), new Point(ActualWidth, _mouseLogical.Y));
            }
        }

        private void DrawHint(DrawingContext dc, Rect full)
        {
            var text = new FormattedText("拖动鼠标选择截图区域 · Enter 复制 · Esc 取消",
                System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei"), 15, Brushes.White, 1.0);
            double width = text.WidthIncludingTrailingWhitespace + 28;
            double height = text.Height + 16;
            var box = new Rect((full.Width - width) / 2, 28, width, height);
            var background = new SolidColorBrush(Color.FromArgb(190, 20, 23, 28));
            background.Freeze();
            dc.DrawRoundedRectangle(background, null, box, 6, 6);
            dc.DrawText(text, new Point(box.X + 14, box.Y + 8));
        }

        private static void DrawHandles(DrawingContext dc, Rect rect)
        {
            var fill = new SolidColorBrush(Colors.White);
            fill.Freeze();
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(0, 120, 215)), 1);
            pen.Freeze();
            double s = 6;
            Point[] points =
            {
                new Point(rect.Left, rect.Top), new Point(rect.Right, rect.Top),
                new Point(rect.Left, rect.Bottom), new Point(rect.Right, rect.Bottom)
            };
            foreach (Point point in points)
                dc.DrawRectangle(fill, pen, new Rect(point.X - s / 2, point.Y - s / 2, s, s));
        }

        private Rect ToLogical(DrawingRectangle rect)
        {
            return new Rect(rect.X / _scale, rect.Y / _scale, rect.Width / _scale, rect.Height / _scale);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            _dragging = true;
            _startLogical = e.GetPosition(this);
            _currentLogical = _startLogical;
            _selection = new DrawingRectangle(0, 0, 0, 0);
            CaptureMouse();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point position = e.GetPosition(this);
            _mouseLogical = position;
            if (_dragging)
            {
                _currentLogical = position;
                UpdateSelection();
            }
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!_dragging)
                return;
            _dragging = false;
            _currentLogical = e.GetPosition(this);
            UpdateSelection();
            ReleaseMouseCapture();
            if (!HasSelection)
            {
                _selection = new DrawingRectangle(0, 0, 0, 0);
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
            InvalidateVisual();
            e.Handled = true;
        }

        private void UpdateSelection()
        {
            double x1 = Math.Min(_startLogical.X, _currentLogical.X);
            double y1 = Math.Min(_startLogical.Y, _currentLogical.Y);
            double x2 = Math.Max(_startLogical.X, _currentLogical.X);
            double y2 = Math.Max(_startLogical.Y, _currentLogical.Y);

            int left = (int)Math.Round(x1 * _scale);
            int top = (int)Math.Round(y1 * _scale);
            int right = (int)Math.Round(x2 * _scale);
            int bottom = (int)Math.Round(y2 * _scale);
            left = Math.Max(0, Math.Min(_frame.Bounds.Width, left));
            top = Math.Max(0, Math.Min(_frame.Bounds.Height, top));
            right = Math.Max(0, Math.Min(_frame.Bounds.Width, right));
            bottom = Math.Max(0, Math.Min(_frame.Bounds.Height, bottom));
            _selection = new DrawingRectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
