using System.Windows.Media;

namespace FreePin.UI
{
    internal sealed class CaptureStyle
    {
        public Color Color { get; set; } = Color.FromRgb(180, 78, 50);
        public double Thickness { get; set; } = 3;
        public double FontSize { get; set; } = 22;
        public bool RoundedRectangle { get; set; }
        public double CornerRadius { get; set; } = 8;
    }

    internal enum CaptureTool
    {
        Select,
        Pen,
        Line,
        Arrow,
        Rectangle,
        Text,
        Eyedropper
    }
}
