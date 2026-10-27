using System;
using System.Windows;

namespace FreePin.UI
{
    internal enum SelectionHandle
    {
        None,
        NorthWest,
        North,
        NorthEast,
        East,
        SouthEast,
        South,
        SouthWest,
        West
    }

    internal static class CaptureGeometry
    {
        public static SelectionHandle HitTest(Rect selection, Point point, double tolerance)
        {
            if (selection.IsEmpty)
                return SelectionHandle.None;

            bool nearLeft = Math.Abs(point.X - selection.Left) <= tolerance;
            bool nearRight = Math.Abs(point.X - selection.Right) <= tolerance;
            bool nearTop = Math.Abs(point.Y - selection.Top) <= tolerance;
            bool nearBottom = Math.Abs(point.Y - selection.Bottom) <= tolerance;
            bool inX = point.X >= selection.Left - tolerance && point.X <= selection.Right + tolerance;
            bool inY = point.Y >= selection.Top - tolerance && point.Y <= selection.Bottom + tolerance;

            if (nearLeft && nearTop) return SelectionHandle.NorthWest;
            if (nearRight && nearTop) return SelectionHandle.NorthEast;
            if (nearLeft && nearBottom) return SelectionHandle.SouthWest;
            if (nearRight && nearBottom) return SelectionHandle.SouthEast;
            if (nearLeft && inY) return SelectionHandle.West;
            if (nearRight && inY) return SelectionHandle.East;
            if (nearTop && inX) return SelectionHandle.North;
            if (nearBottom && inX) return SelectionHandle.South;
            return SelectionHandle.None;
        }

        public static Point HandlePoint(Rect selection, SelectionHandle handle)
        {
            double x = handle == SelectionHandle.NorthWest || handle == SelectionHandle.West ||
                       handle == SelectionHandle.SouthWest
                ? selection.Left
                : handle == SelectionHandle.NorthEast || handle == SelectionHandle.East ||
                  handle == SelectionHandle.SouthEast
                    ? selection.Right
                    : selection.Left + selection.Width / 2;
            double y = handle == SelectionHandle.NorthWest || handle == SelectionHandle.North ||
                       handle == SelectionHandle.NorthEast
                ? selection.Top
                : handle == SelectionHandle.SouthWest || handle == SelectionHandle.South ||
                  handle == SelectionHandle.SouthEast
                    ? selection.Bottom
                    : selection.Top + selection.Height / 2;
            return new Point(x, y);
        }

        public static Rect Move(Rect selection, double dx, double dy, Rect bounds, double minSize = 2)
        {
            if (selection.IsEmpty)
                return selection;

            double x = selection.X + dx;
            double y = selection.Y + dy;
            double maxX = Math.Max(bounds.Left, bounds.Right - selection.Width);
            double maxY = Math.Max(bounds.Top, bounds.Bottom - selection.Height);
            x = Math.Max(bounds.Left, Math.Min(maxX, x));
            y = Math.Max(bounds.Top, Math.Min(maxY, y));
            return new Rect(x, y, Math.Max(minSize, selection.Width), Math.Max(minSize, selection.Height));
        }

        public static Rect Resize(Rect start, SelectionHandle handle, double dx, double dy,
            Rect bounds, double minSize = 2)
        {
            if (start.IsEmpty || handle == SelectionHandle.None)
                return start;

            double left = start.Left;
            double top = start.Top;
            double right = start.Right;
            double bottom = start.Bottom;

            if (handle == SelectionHandle.NorthWest || handle == SelectionHandle.West ||
                handle == SelectionHandle.SouthWest)
                left = Math.Min(right - minSize, left + dx);
            if (handle == SelectionHandle.NorthEast || handle == SelectionHandle.East ||
                handle == SelectionHandle.SouthEast)
                right = Math.Max(left + minSize, right + dx);
            if (handle == SelectionHandle.NorthWest || handle == SelectionHandle.North ||
                handle == SelectionHandle.NorthEast)
                top = Math.Min(bottom - minSize, top + dy);
            if (handle == SelectionHandle.SouthWest || handle == SelectionHandle.South ||
                handle == SelectionHandle.SouthEast)
                bottom = Math.Max(top + minSize, bottom + dy);

            left = Math.Max(bounds.Left, Math.Min(bounds.Right - minSize, left));
            top = Math.Max(bounds.Top, Math.Min(bounds.Bottom - minSize, top));
            right = Math.Max(left + minSize, Math.Min(bounds.Right, right));
            bottom = Math.Max(top + minSize, Math.Min(bounds.Bottom, bottom));
            return new Rect(left, top, Math.Max(minSize, right - left), Math.Max(minSize, bottom - top));
        }
    }
}
