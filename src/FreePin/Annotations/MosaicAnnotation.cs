using System;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FreePin.Imaging;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingGraphicsUnit = System.Drawing.GraphicsUnit;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRectangle = System.Drawing.Rectangle;

namespace FreePin.Annotations
{
    public sealed class MosaicAnnotation : Annotation
    {
        private BitmapSource _pixelated;
        private bool _dirty = true;

        public Rect Area { get; set; }

        public override Rect Bounds
        {
            get { return Area; }
        }

        public override bool HitTest(Point point, double tolerance)
        {
            Rect outer = Area;
            outer.Inflate(tolerance, tolerance);
            return outer.Contains(point);
        }

        public override void Draw(DrawingContext dc, BitmapSource source)
        {
            if (_pixelated == null)
            {
                var brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 60, 60, 60));
                brush.Freeze();
                dc.DrawRectangle(brush, null, Area);
                return;
            }

            dc.DrawImage(_pixelated, Area);
        }

        public void EnsureMosaic(System.Drawing.Bitmap source)
        {
            if (!_dirty && _pixelated != null)
                return;
            _dirty = false;
            _pixelated = null;

            if (source == null || Area.Width < 2 || Area.Height < 2)
                return;

            try
            {
                int x = (int)Math.Round(Area.X);
                int y = (int)Math.Round(Area.Y);
                int width = (int)Math.Round(Area.Width);
                int height = (int)Math.Round(Area.Height);
                if (x < 0)
                {
                    width += x;
                    x = 0;
                }
                if (y < 0)
                {
                    height += y;
                    y = 0;
                }
                width = Math.Min(width, source.Width - x);
                height = Math.Min(height, source.Height - y);
                if (width < 2 || height < 2)
                    return;

                int block = Math.Max(5, Math.Min(12, Math.Min(width, height) / 24));
                int smallWidth = Math.Max(1, width / block);
                int smallHeight = Math.Max(1, height / block);

                using (var small = new System.Drawing.Bitmap(smallWidth, smallHeight, DrawingPixelFormat.Format32bppArgb))
                {
                    using (DrawingGraphics graphics = DrawingGraphics.FromImage(small))
                    {
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.Half;
                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        graphics.DrawImage(source, new DrawingRectangle(0, 0, smallWidth, smallHeight),
                            new DrawingRectangle(x, y, width, height), DrawingGraphicsUnit.Pixel);
                    }

                    using (var final = new System.Drawing.Bitmap(width, height, DrawingPixelFormat.Format32bppArgb))
                    {
                        using (DrawingGraphics graphics = DrawingGraphics.FromImage(final))
                        {
                            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                            graphics.PixelOffsetMode = PixelOffsetMode.Half;
                            graphics.CompositingMode = CompositingMode.SourceCopy;
                            graphics.DrawImage(small, new DrawingRectangle(0, 0, width, height));
                        }

                        _pixelated = ImageUtil.ToBitmapSource(final);
                    }
                }
            }
            catch (Exception ex)
            {
                FreePin.Services.Log.Error("生成马赛克失败", ex);
                _pixelated = null;
            }
        }

        public override void Move(double dx, double dy)
        {
            Area = new Rect(Area.X + dx, Area.Y + dy, Area.Width, Area.Height);
            _dirty = true;
        }

        public override Annotation Clone()
        {
            var clone = new MosaicAnnotation { Area = Area, _dirty = true };
            CopyStyleTo(clone);
            return clone;
        }
    }
}
