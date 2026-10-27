using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FreePin.Annotations
{
    public sealed class TextAnnotation : Annotation
    {
        public Point Position { get; set; }
        public string Content { get; set; } = string.Empty;
        public double FontSize { get; set; } = 22.0;

        private FormattedText CreateText()
        {
            var typeface = new Typeface(new FontFamily("Microsoft YaHei"), FontStyles.Normal,
                FontWeights.Normal, FontStretches.Normal);
            return new FormattedText(Content ?? string.Empty, CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, typeface, Math.Max(8, FontSize), CreateBrush(Color), 1.0);
        }

        public override Rect Bounds
        {
            get
            {
                FormattedText text = CreateText();
                return new Rect(Position.X - 2, Position.Y - 2,
                    Math.Max(4, text.WidthIncludingTrailingWhitespace + 4), Math.Max(4, text.Height + 4));
            }
        }

        public override bool HitTest(Point point, double tolerance)
        {
            Rect bounds = Bounds;
            bounds.Inflate(tolerance, tolerance);
            return bounds.Contains(point);
        }

        public override void Draw(DrawingContext dc, BitmapSource source)
        {
            FormattedText text = CreateText();
            var background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(150, 255, 255, 255));
            background.Freeze();
            var padding = new Rect(Position.X - 3, Position.Y - 2,
                text.WidthIncludingTrailingWhitespace + 6, text.Height + 4);
            dc.DrawRoundedRectangle(background, null, padding, 3, 3);
            dc.DrawText(text, Position);
        }

        public override void Move(double dx, double dy)
        {
            Position = new Point(Position.X + dx, Position.Y + dy);
        }

        public override Annotation Clone()
        {
            var clone = new TextAnnotation
            {
                Position = Position,
                Content = Content,
                FontSize = FontSize
            };
            CopyStyleTo(clone);
            return clone;
        }
    }
}
