using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FreePin.Annotations
{
    public abstract class Annotation
    {
        public Color Color { get; set; } = Colors.Red;
        public double Thickness { get; set; } = 3.0;
        public bool IsSelected { get; set; }

        public abstract Rect Bounds { get; }
        public abstract bool HitTest(Point point, double tolerance);
        public abstract void Draw(DrawingContext dc, BitmapSource source);
        public abstract void Move(double dx, double dy);
        public abstract Annotation Clone();

        protected static Pen CreatePen(Color color, double thickness)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var pen = new Pen(brush, Math.Max(1.0, thickness))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            pen.Freeze();
            return pen;
        }

        protected static Brush CreateBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        protected void CopyStyleTo(Annotation target)
        {
            target.Color = Color;
            target.Thickness = Thickness;
            target.IsSelected = false;
        }

        protected static double DistanceToSegment(Point point, Point start, Point end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double lengthSquared = dx * dx + dy * dy;
            if (lengthSquared < 0.0001)
                return (point - start).Length;

            double t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared;
            t = Math.Max(0, Math.Min(1, t));
            var projection = new Point(start.X + t * dx, start.Y + t * dy);
            return (point - projection).Length;
        }

        protected static void DrawSelection(DrawingContext dc, Rect bounds)
        {
            if (bounds.IsEmpty)
                return;
            var brush = new SolidColorBrush(Color.FromArgb(255, 0, 120, 215));
            brush.Freeze();
            var pen = new Pen(brush, 1.0)
            {
                DashStyle = new DashStyle(new double[] { 4, 3 }, 0)
            };
            pen.Freeze();
            Rect selection = bounds;
            selection.Inflate(4, 4);
            dc.DrawRectangle(null, pen, selection);
        }

        protected static Rect NormalizeRect(Rect rect)
        {
            double x = Math.Min(rect.Left, rect.Right);
            double y = Math.Min(rect.Top, rect.Bottom);
            double w = Math.Abs(rect.Width);
            double h = Math.Abs(rect.Height);
            return new Rect(x, y, w, h);
        }
    }
}
