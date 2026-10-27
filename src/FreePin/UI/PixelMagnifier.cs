using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FreePin.Services;

namespace FreePin.UI
{
    internal sealed class PixelMagnifier : FrameworkElement
    {
        private const int SampleSize = 11;
        private const int CellSize = 10;
        private const int ImageSize = SampleSize * CellSize;
        private const int Padding = 8;

        private BitmapSource _crop;
        private Color _color = Colors.Transparent;
        private int _pixelX;
        private int _pixelY;

        public PixelMagnifier()
        {
            IsHitTestVisible = false;
            SnapsToDevicePixels = true;
            Width = Padding * 2 + ImageSize;
            Height = Padding * 2 + ImageSize + 48;
            Visibility = Visibility.Collapsed;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        }

        public void Update(ScreenFrame frame, Point physicalPoint)
        {
            if (frame == null || frame.Bitmap == null)
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            int width = frame.Bitmap.Width;
            int height = frame.Bitmap.Height;
            int x = (int)Math.Round(physicalPoint.X);
            int y = (int)Math.Round(physicalPoint.Y);
            x = Math.Max(0, Math.Min(width - 1, x));
            y = Math.Max(0, Math.Min(height - 1, y));
            _pixelX = x;
            _pixelY = y;

            int left = Math.Max(0, Math.Min(width - SampleSize, x - SampleSize / 2));
            int top = Math.Max(0, Math.Min(height - SampleSize, y - SampleSize / 2));
            int sampleWidth = Math.Max(1, Math.Min(SampleSize, width - left));
            int sampleHeight = Math.Max(1, Math.Min(SampleSize, height - top));
            _crop = new CroppedBitmap(frame.ImageSource, new Int32Rect(left, top, sampleWidth, sampleHeight));
            _crop.Freeze();
            System.Drawing.Color drawing = frame.Bitmap.GetPixel(x, y);
            _color = Color.FromArgb(drawing.A, drawing.R, drawing.G, drawing.B);
            InvalidateVisual();
            Visibility = Visibility.Visible;
        }

        public void Hide()
        {
            Visibility = Visibility.Collapsed;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(Width, Height);
        }

        protected override void OnRender(DrawingContext dc)
        {
            var paper = UiTheme.Brush(Color.FromArgb(248, 255, 251, 243));
            var border = UiTheme.Brush(UiTheme.Border);
            var ink = UiTheme.Brush(UiTheme.Foreground);
            var muted = UiTheme.Brush(UiTheme.Muted);
            var panel = new Rect(0.5, 0.5, Width - 1, Height - 1);
            dc.DrawRoundedRectangle(paper, new Pen(border, 1), panel, 8, 8);

            var imageRect = new Rect(Padding, Padding, ImageSize, ImageSize);
            dc.DrawRectangle(UiTheme.Brush(Color.FromRgb(35, 32, 28)), null, imageRect);
            if (_crop != null)
                dc.DrawImage(_crop, imageRect);

            var grid = new Pen(UiTheme.Brush(Color.FromArgb(42, 255, 255, 255)), 1);
            for (int i = 1; i < SampleSize; i++)
            {
                double offset = Padding + i * CellSize - 0.5;
                dc.DrawLine(grid, new Point(offset, Padding), new Point(offset, Padding + ImageSize));
                dc.DrawLine(grid, new Point(Padding, offset), new Point(Padding + ImageSize, offset));
            }

            int center = SampleSize / 2;
            var centerRect = new Rect(Padding + center * CellSize + 1, Padding + center * CellSize + 1,
                CellSize - 2, CellSize - 2);
            var accent = UiTheme.Brush(UiTheme.Accent);
            dc.DrawRectangle(null, new Pen(accent, 1.5), centerRect);

            double swatchY = Padding + ImageSize + 8;
            var swatch = new Rect(Padding, swatchY, 16, 16);
            dc.DrawRoundedRectangle(UiTheme.Brush(_color), new Pen(border, 1), swatch, 3, 3);
            DrawText(dc, string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}",
                _color.R, _color.G, _color.B), new Point(Padding + 24, swatchY), 12, ink, true);
            DrawText(dc, string.Format(CultureInfo.InvariantCulture, "R{0}  G{1}  B{2}",
                _color.R, _color.G, _color.B), new Point(Padding + 24, swatchY + 18), 10.5, muted, false);
            DrawText(dc, string.Format(CultureInfo.InvariantCulture, "x {0}   y {1}", _pixelX, _pixelY),
                new Point(Padding, swatchY + 34), 10.5, muted, false);
        }

        private static void DrawText(DrawingContext dc, string value, Point point, double size, Brush brush, bool bold)
        {
            var typeface = new Typeface(UiTheme.UiFont, FontStyles.Normal,
                bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal);
            var text = new FormattedText(value ?? string.Empty, CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, typeface, size, brush, 1.0);
            dc.DrawText(text, point);
        }
    }
}
