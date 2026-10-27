using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FreePin.Native;
using FreePin.Services;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingBitmapData = System.Drawing.Imaging.BitmapData;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingImageLockMode = System.Drawing.Imaging.ImageLockMode;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRectangle = System.Drawing.Rectangle;

namespace FreePin.UI
{
    public sealed class LongCaptureResult
    {
        public DrawingBitmap Image { get; set; }
        public BitmapSource Source { get; set; }
    }

    /// <summary>
    /// A small non-activating toolbar that captures a fixed screen region while
    /// the user scrolls the underlying window, then stitches the frames.
    /// </summary>
    public sealed class LongCaptureWindow : Window
    {
        private const int MaxStitchedHeight = 20000;
        private readonly DrawingRectangle _bounds;
        private readonly Action<LongCaptureResult> _completed;
        private readonly DispatcherTimer _timer;
        private readonly TextBlock _status;
        private DrawingBitmap _stitched;
        private DrawingBitmap _lastCaptured;
        private bool _finished;
        private int _segments;

        public LongCaptureWindow(DrawingRectangle screenBounds, DrawingBitmap initial,
            Action<LongCaptureResult> completed)
        {
            if (initial == null)
                throw new ArgumentNullException(nameof(initial));
            if (screenBounds.Width <= 0 || screenBounds.Height <= 0)
                throw new ArgumentException("长截图区域无效。", nameof(screenBounds));

            _bounds = screenBounds;
            _completed = completed;
            _stitched = new DrawingBitmap(initial);
            _lastCaptured = new DrawingBitmap(initial);

            Title = "FreePin - 长截图";
            Width = 380;
            Height = 116;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            Focusable = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            PositionNearSelection();

            var panel = new StackPanel();
            var title = UiTheme.Text("长截图", 15, null, true);
            title.Margin = new Thickness(0, 0, 0, 3);
            panel.Children.Add(title);

            var hint = UiTheme.Text("滚动页面，停下后会自动拼接；完成时生成一张长图。", 12, UiTheme.Muted);
            hint.TextWrapping = TextWrapping.Wrap;
            panel.Children.Add(hint);

            _status = UiTheme.Text("等待滚动内容…", 12, UiTheme.Accent);
            _status.Margin = new Thickness(0, 3, 0, 5);
            panel.Children.Add(_status);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(UiTheme.Button("完成", "结束采集并进入编辑器", (s, e) => Complete(), true));
            buttons.Children.Add(UiTheme.Button("取消", "放弃本次长截图", (s, e) => Cancel()));
            panel.Children.Add(buttons);

            Content = new Border
            {
                Background = UiTheme.Brush(UiTheme.Panel),
                BorderBrush = UiTheme.Brush(UiTheme.Accent),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(13, 10, 13, 10),
                Child = panel
            };

            SourceInitialized += (s, e) =>
            {
                try
                {
                    IntPtr handle = new WindowInteropHelper(this).Handle;
                    if (!NativeMethods.SetWindowDisplayAffinity(handle, NativeMethods.WdaExcludeFromCapture))
                        Log.Warn("长截图工具条无法从截图中排除，请把工具条放在选区外。");
                }
                catch (Exception ex)
                {
                    Log.Warn("设置长截图工具条排除失败: " + ex.Message);
                }
            };

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _timer.Tick += OnTick;
            Loaded += (s, e) => _timer.Start();
            Closed += OnClosed;
        }

        private void PositionNearSelection()
        {
            double scale = ScreenCapture.PrimaryScale;
            if (scale <= 0)
                scale = 1;
            DrawingRectangle screen = ScreenCapture.VirtualBounds;

            double left = _bounds.Left / scale + 14;
            double top = _bounds.Bottom / scale + 12;
            double minLeft = screen.Left / scale + 6;
            double minTop = screen.Top / scale + 6;
            double maxLeft = screen.Right / scale - Width - 6;
            double maxTop = screen.Bottom / scale - Height - 6;

            if (top > maxTop)
                top = _bounds.Top / scale - Height - 12;
            if (top < minTop)
                top = minTop;
            if (left > maxLeft)
                left = maxLeft;
            if (left < minLeft)
                left = minLeft;

            Left = left;
            Top = top;
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_finished)
                return;

            DrawingBitmap frame = null;
            try
            {
                frame = ScreenCapture.CaptureRegion(_bounds);
                if (IsSameFrame(_lastCaptured, frame))
                {
                    frame.Dispose();
                    return;
                }

                int overlap = FindOverlap(_stitched, frame);
                int add = overlap > 0 ? frame.Height - overlap : frame.Height;
                if (add < 2)
                {
                    frame.Dispose();
                    return;
                }

                DrawingBitmap combined = Stitch(_stitched, frame, overlap);
                if (combined == null)
                {
                    frame.Dispose();
                    return;
                }

                DrawingBitmap old = _stitched;
                _stitched = combined;
                old.Dispose();

                old = _lastCaptured;
                _lastCaptured = frame;
                old.Dispose();
                frame = null;

                _segments++;
                _status.Text = "已拼接 " + _segments + " 段 · 当前 " + combined.Height + " px";
                if (combined.Height >= MaxStitchedHeight)
                {
                    _status.Text += "（已达高度上限）";
                    _timer.Stop();
                }
            }
            catch (Exception ex)
            {
                Log.Error("长截图采集失败", ex);
                if (frame != null)
                    frame.Dispose();
                _status.Text = "采集失败：" + ex.Message;
            }
        }

        private static unsafe bool IsSameFrame(DrawingBitmap previous, DrawingBitmap current)
        {
            if (previous == null || current == null)
                return false;
            if (previous.Width != current.Width || previous.Height != current.Height)
                return false;

            DrawingBitmapData pd = null;
            DrawingBitmapData nd = null;
            try
            {
                pd = previous.LockBits(new DrawingRectangle(0, 0, previous.Width, previous.Height),
                    DrawingImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppArgb);
                nd = current.LockBits(new DrawingRectangle(0, 0, current.Width, current.Height),
                    DrawingImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppArgb);

                int left = previous.Width * 8 / 100;
                int right = previous.Width * 88 / 100;
                if (right - left < 8)
                {
                    left = 0;
                    right = previous.Width;
                }
                int stepX = Math.Max(1, (right - left) / 20);
                int stepY = Math.Max(1, previous.Height / 12);
                long total = 0;
                int samples = 0;
                byte* pp = (byte*)pd.Scan0;
                byte* np = (byte*)nd.Scan0;
                for (int y = 0; y < previous.Height; y += stepY)
                {
                    byte* prow = pp + y * pd.Stride;
                    byte* nrow = np + y * nd.Stride;
                    for (int x = left; x < right; x += stepX)
                    {
                        int offset = x * 4;
                        total += Math.Abs(prow[offset] - nrow[offset]);
                        total += Math.Abs(prow[offset + 1] - nrow[offset + 1]);
                        total += Math.Abs(prow[offset + 2] - nrow[offset + 2]);
                        samples += 3;
                    }
                }
                return samples > 0 && total / (double)samples <= 2.0;
            }
            finally
            {
                if (pd != null)
                    previous.UnlockBits(pd);
                if (nd != null)
                    current.UnlockBits(nd);
            }
        }

        private static unsafe int FindOverlap(DrawingBitmap previous, DrawingBitmap next)
        {
            if (previous == null || next == null)
                return -1;
            int width = Math.Min(previous.Width, next.Width);
            int prevHeight = previous.Height;
            int nextHeight = next.Height;
            int minOverlap = Math.Max(16, nextHeight * 10 / 100);
            int maxOverlap = Math.Min(nextHeight - 4, prevHeight - 4);
            maxOverlap = Math.Min(maxOverlap, nextHeight * 90 / 100);
            if (maxOverlap <= minOverlap)
                return -1;

            int left = width * 6 / 100;
            int right = width * 88 / 100;
            if (right - left < 12)
            {
                left = 0;
                right = width;
            }
            int xStep = Math.Max(2, (right - left) / 36);

            int coarse = Math.Max(2, (maxOverlap - minOverlap) / 24);
            int bestOverlap = -1;
            double bestScore = double.MaxValue;
            for (int overlap = maxOverlap; overlap >= minOverlap; overlap -= coarse)
            {
                double score = ScoreOverlap(previous, next, overlap, left, right, xStep);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestOverlap = overlap;
                }
            }

            if (bestOverlap < 0)
                return -1;

            int refineLow = Math.Max(minOverlap, bestOverlap - coarse);
            int refineHigh = Math.Min(maxOverlap, bestOverlap + coarse);
            for (int overlap = refineLow; overlap <= refineHigh; overlap++)
            {
                double score = ScoreOverlap(previous, next, overlap, left, right, xStep);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestOverlap = overlap;
                }
            }

            return bestScore <= 18.0 ? bestOverlap : -1;
        }

        private static unsafe double ScoreOverlap(DrawingBitmap previous, DrawingBitmap next, int overlap,
            int left, int right, int xStep)
        {
            DrawingBitmapData pd = null;
            DrawingBitmapData nd = null;
            try
            {
                pd = previous.LockBits(new DrawingRectangle(0, 0, previous.Width, previous.Height),
                    DrawingImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppArgb);
                nd = next.LockBits(new DrawingRectangle(0, 0, next.Width, next.Height),
                    DrawingImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppArgb);

                int yStep = Math.Max(2, overlap / 24);
                int baseY = previous.Height - overlap;
                long total = 0;
                int samples = 0;
                byte* pp = (byte*)pd.Scan0;
                byte* np = (byte*)nd.Scan0;
                for (int y = 0; y < overlap; y += yStep)
                {
                    int py = baseY + y;
                    if (py < 0 || py >= previous.Height || y >= next.Height)
                        continue;
                    byte* prow = pp + py * pd.Stride;
                    byte* nrow = np + y * nd.Stride;
                    for (int x = left; x < right; x += xStep)
                    {
                        int offset = x * 4;
                        total += Math.Abs(prow[offset] - nrow[offset]);
                        total += Math.Abs(prow[offset + 1] - nrow[offset + 1]);
                        total += Math.Abs(prow[offset + 2] - nrow[offset + 2]);
                        samples += 3;
                    }
                }
                return samples > 0 ? total / (double)samples : double.MaxValue;
            }
            finally
            {
                if (pd != null)
                    previous.UnlockBits(pd);
                if (nd != null)
                    next.UnlockBits(nd);
            }
        }

        private static DrawingBitmap Stitch(DrawingBitmap previous, DrawingBitmap next, int overlap)
        {
            int add = overlap > 0 ? next.Height - overlap : next.Height;
            if (add < 2)
                return null;

            int targetHeight = Math.Min(MaxStitchedHeight, previous.Height + add);
            int usable = targetHeight - previous.Height;
            if (usable <= 0)
                return null;

            var combined = new DrawingBitmap(previous.Width, targetHeight, DrawingPixelFormat.Format32bppArgb);
            try
            {
                using (DrawingGraphics graphics = DrawingGraphics.FromImage(combined))
                {
                    graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    graphics.DrawImage(previous,
                        new DrawingRectangle(0, 0, previous.Width, previous.Height));
                    int sourceY = overlap > 0 ? overlap : 0;
                    int copyHeight = Math.Min(usable, next.Height - sourceY);
                    if (copyHeight > 0)
                    {
                        graphics.DrawImage(next,
                            new DrawingRectangle(0, previous.Height, previous.Width, copyHeight),
                            new DrawingRectangle(0, sourceY, next.Width, copyHeight),
                            System.Drawing.GraphicsUnit.Pixel);
                    }
                }
                return combined;
            }
            catch
            {
                combined.Dispose();
                throw;
            }
        }

        private void Complete()
        {
            if (_finished)
                return;
            _finished = true;
            _timer.Stop();

            DrawingBitmap image = _stitched;
            _stitched = null;
            BitmapSource source = ScreenCapture.ToBitmapSource(image);
            source.Freeze();
            var result = new LongCaptureResult { Image = image, Source = source };

            // Hand the result over before closing so the editor can take
            // foreground focus while this window is still active.
            try
            {
                if (_completed != null)
                    _completed(result);
            }
            catch (Exception ex)
            {
                image.Dispose();
                Log.Error("完成长截图失败", ex);
            }

            Close();
        }

        private void Cancel()
        {
            if (_finished)
                return;
            _finished = true;
            _timer.Stop();
            Close();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _timer.Stop();
            if (_stitched != null)
            {
                _stitched.Dispose();
                _stitched = null;
            }
            if (_lastCaptured != null)
            {
                _lastCaptured.Dispose();
                _lastCaptured = null;
            }
        }
    }
}
