using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FreePin.Annotations
{
    public sealed class FreehandAnnotation : Annotation
    {
        public List<Point> Points { get; set; } = new List<Point>();

        public override Rect Bounds
        {
            get
            {
                if (Points == null || Points.Count == 0)
                    return Rect.Empty;
                double minX = Points.Min(p => p.X) - Thickness;
                double minY = Points.Min(p => p.Y) - Thickness;
                double maxX = Points.Max(p => p.X) + Thickness;
                double maxY = Points.Max(p => p.Y) + Thickness;
                return new Rect(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
            }
        }

        public override bool HitTest(Point point, double tolerance)
        {
            if (Points == null || Points.Count == 0)
                return false;
            double threshold = Math.Max(5, Thickness + tolerance);
            if (Points.Count == 1)
                return (point - Points[0]).Length <= threshold;
            for (int i = 1; i < Points.Count; i++)
            {
                if (DistanceToSegment(point, Points[i - 1], Points[i]) <= threshold)
                    return true;
            }
            return false;
        }

        public override void Draw(DrawingContext dc, BitmapSource source)
        {
            if (Points == null || Points.Count < 2)
                return;
            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(Points[0], false, false);
                context.PolyLineTo(Points.Skip(1).ToList(), true, true);
            }
            geometry.Freeze();
            dc.DrawGeometry(null, CreatePen(Color, Thickness), geometry);
        }

        public override void Move(double dx, double dy)
        {
            if (Points == null)
                return;
            for (int i = 0; i < Points.Count; i++)
                Points[i] = new Point(Points[i].X + dx, Points[i].Y + dy);
        }

        public override Annotation Clone()
        {
            var clone = new FreehandAnnotation { Points = Points == null ? new List<Point>() : new List<Point>(Points) };
            CopyStyleTo(clone);
            return clone;
        }
    }
}
