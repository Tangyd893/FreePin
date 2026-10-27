using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FreePin.Annotations
{
    public sealed class LineAnnotation : Annotation
    {
        public Point Start { get; set; }
        public Point End { get; set; }

        public override Rect Bounds
        {
            get
            {
                double padding = Thickness;
                return new Rect(
                    Math.Min(Start.X, End.X) - padding,
                    Math.Min(Start.Y, End.Y) - padding,
                    Math.Abs(End.X - Start.X) + padding * 2,
                    Math.Abs(End.Y - Start.Y) + padding * 2);
            }
        }

        public override bool HitTest(Point point, double tolerance)
        {
            return DistanceToSegment(point, Start, End) <= Math.Max(4, Thickness + tolerance);
        }

        public override void Draw(DrawingContext dc, BitmapSource source)
        {
            dc.DrawLine(CreatePen(Color, Thickness), Start, End);
        }

        public override void Move(double dx, double dy)
        {
            Start = new Point(Start.X + dx, Start.Y + dy);
            End = new Point(End.X + dx, End.Y + dy);
        }

        public override Annotation Clone()
        {
            var clone = new LineAnnotation { Start = Start, End = End };
            CopyStyleTo(clone);
            return clone;
        }
    }

    public sealed class ArrowAnnotation : Annotation
    {
        public Point Start { get; set; }
        public Point End { get; set; }

        public override Rect Bounds
        {
            get
            {
                double padding = Math.Max(8, Thickness * 3);
                return new Rect(
                    Math.Min(Start.X, End.X) - padding,
                    Math.Min(Start.Y, End.Y) - padding,
                    Math.Abs(End.X - Start.X) + padding * 2,
                    Math.Abs(End.Y - Start.Y) + padding * 2);
            }
        }

        public override bool HitTest(Point point, double tolerance)
        {
            return DistanceToSegment(point, Start, End) <= Math.Max(5, Thickness + tolerance);
        }

        public override void Draw(DrawingContext dc, BitmapSource source)
        {
            double dx = End.X - Start.X;
            double dy = End.Y - Start.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1)
                return;

            double ux = dx / length;
            double uy = dy / length;
            double head = Math.Max(10, Thickness * 3.6);
            if (head > length * 0.6)
                head = length * 0.6;

            var shaftEnd = new Point(End.X - ux * head * 0.82, End.Y - uy * head * 0.82);
            var pen = CreatePen(Color, Thickness);
            dc.DrawLine(pen, Start, shaftEnd);

            double halfWidth = head * 0.42;
            var perpendicular = new Vector(-uy, ux);
            var left = new Point(End.X - ux * head + perpendicular.X * halfWidth,
                End.Y - uy * head + perpendicular.Y * halfWidth);
            var right = new Point(End.X - ux * head - perpendicular.X * halfWidth,
                End.Y - uy * head - perpendicular.Y * halfWidth);

            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(End, true, true);
                context.LineTo(left, true, false);
                context.LineTo(right, true, false);
            }
            geometry.Freeze();
            dc.DrawGeometry(CreateBrush(Color), null, geometry);
        }

        public override void Move(double dx, double dy)
        {
            Start = new Point(Start.X + dx, Start.Y + dy);
            End = new Point(End.X + dx, End.Y + dy);
        }

        public override Annotation Clone()
        {
            var clone = new ArrowAnnotation { Start = Start, End = End };
            CopyStyleTo(clone);
            return clone;
        }
    }

    public sealed class RectangleAnnotation : Annotation
    {
        public Rect Area { get; set; }
        public double CornerRadius { get; set; }

        public override Rect Bounds
        {
            get
            {
                Rect rect = Area;
                rect.Inflate(Thickness, Thickness);
                return rect;
            }
        }

        public override bool HitTest(Point point, double tolerance)
        {
            Rect outer = Area;
            outer.Inflate(Math.Max(4, tolerance + Thickness), Math.Max(4, tolerance + Thickness));
            if (!outer.Contains(point))
                return false;
            Rect inner = Area;
            double inset = Math.Max(3, Thickness + tolerance);
            inner.Inflate(-inset, -inset);
            return inner.Width <= 0 || inner.Height <= 0 || !inner.Contains(point);
        }

        public override void Draw(DrawingContext dc, BitmapSource source)
        {
            if (CornerRadius <= 0.1)
            {
                dc.DrawRectangle(null, CreatePen(Color, Thickness), Area);
                return;
            }
            double radius = Math.Min(CornerRadius, Math.Min(Area.Width, Area.Height) / 2);
            if (radius <= 0.1)
                dc.DrawRectangle(null, CreatePen(Color, Thickness), Area);
            else
                dc.DrawRoundedRectangle(null, CreatePen(Color, Thickness), Area, radius, radius);
        }

        public override void Move(double dx, double dy)
        {
            Area = new Rect(Area.X + dx, Area.Y + dy, Area.Width, Area.Height);
        }

        public override Annotation Clone()
        {
            var clone = new RectangleAnnotation { Area = Area, CornerRadius = CornerRadius };
            CopyStyleTo(clone);
            return clone;
        }
    }

    public sealed class EllipseAnnotation : Annotation
    {
        public Rect Area { get; set; }

        public override Rect Bounds
        {
            get
            {
                Rect rect = Area;
                rect.Inflate(Thickness, Thickness);
                return rect;
            }
        }

        public override bool HitTest(Point point, double tolerance)
        {
            Rect outer = Area;
            outer.Inflate(Math.Max(3, tolerance + Thickness), Math.Max(3, tolerance + Thickness));
            if (!outer.Contains(point))
                return false;
            double cx = Area.X + Area.Width / 2;
            double cy = Area.Y + Area.Height / 2;
            double rx = Math.Max(1, Area.Width / 2);
            double ry = Math.Max(1, Area.Height / 2);
            double dx = (point.X - cx) / rx;
            double dy = (point.Y - cy) / ry;
            return dx * dx + dy * dy <= 1.08;
        }

        public override void Draw(DrawingContext dc, BitmapSource source)
        {
            dc.DrawEllipse(null, CreatePen(Color, Thickness),
                new Point(Area.X + Area.Width / 2, Area.Y + Area.Height / 2),
                Area.Width / 2, Area.Height / 2);
        }

        public override void Move(double dx, double dy)
        {
            Area = new Rect(Area.X + dx, Area.Y + dy, Area.Width, Area.Height);
        }

        public override Annotation Clone()
        {
            var clone = new EllipseAnnotation { Area = Area };
            CopyStyleTo(clone);
            return clone;
        }
    }
}
