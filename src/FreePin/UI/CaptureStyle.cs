using System.Windows.Media;

namespace FreePin.UI
{
    public sealed class CaptureStyle
    {
        public Color Color { get; set; } = Color.FromRgb(180, 78, 50);
        public double Thickness { get; set; } = 3;
        public double FontSize { get; set; } = 22;
        public bool RoundedRectangle { get; set; }
        public double CornerRadius { get; set; } = 8;

        public static Color ParseColor(string value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;
            try
            {
                object converted = ColorConverter.ConvertFromString(value);
                if (converted is Color color)
                    return color;
            }
            catch
            {
                // use fallback
            }
            return fallback;
        }

        public static string ToHex(Color color)
        {
            return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }
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
