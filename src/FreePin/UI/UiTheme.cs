using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FreePin.UI
{
    public static class UiTheme
    {
        public static readonly Color Background = Color.FromRgb(26, 30, 36);
        public static readonly Color Panel = Color.FromRgb(38, 43, 52);
        public static readonly Color PanelHover = Color.FromRgb(51, 58, 69);
        public static readonly Color Border = Color.FromRgb(63, 71, 84);
        public static readonly Color Foreground = Color.FromRgb(238, 241, 245);
        public static readonly Color Muted = Color.FromRgb(157, 168, 184);
        public static readonly Color Accent = Color.FromRgb(47, 128, 237);
        public static readonly Color AccentHover = Color.FromRgb(72, 151, 255);
        public static readonly Color Danger = Color.FromRgb(224, 82, 82);

        public static SolidColorBrush Brush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public static Button Button(string text, string toolTip, RoutedEventHandler click, bool accent = false)
        {
            var button = new Button
            {
                Content = text,
                ToolTip = toolTip,
                Padding = new Thickness(11, 6, 11, 6),
                Margin = new Thickness(3, 0, 3, 0),
                Background = Brush(accent ? Accent : Panel),
                Foreground = Brush(Foreground),
                BorderBrush = Brush(accent ? Accent : Border),
                BorderThickness = new Thickness(1),
                FontSize = 13,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (click != null)
                button.Click += click;
            return button;
        }

        public static Button ToolButton(string glyph, string toolTip, RoutedEventHandler click)
        {
            var button = new Button
            {
                Content = glyph,
                ToolTip = toolTip,
                Width = 32,
                Height = 30,
                Margin = new Thickness(1, 0, 1, 0),
                Background = Brush(Panel),
                Foreground = Brush(Foreground),
                BorderBrush = Brush(Border),
                BorderThickness = new Thickness(1),
                FontSize = 16,
                FontFamily = new FontFamily("Segoe UI Symbol"),
                Cursor = Cursors.Hand,
                Padding = new Thickness(0)
            };
            if (click != null)
                button.Click += click;
            return button;
        }

        public static TextBlock Text(string text, double size = 13, Color? color = null, bool bold = false)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = Brush(color ?? Foreground),
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        public static Border Card(UIElement child)
        {
            return new Border
            {
                Background = Brush(Panel),
                BorderBrush = Brush(Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14),
                Child = child
            };
        }

        public static MenuItem MenuItem(string header, RoutedEventHandler click)
        {
            var item = new MenuItem { Header = header };
            if (click != null)
                item.Click += click;
            return item;
        }

        public static ContextMenu ContextMenu(params UIElement[] items)
        {
            var menu = new ContextMenu();
            foreach (UIElement item in items)
                menu.Items.Add(item);
            return menu;
        }

        public static void SetSelected(Button button, bool selected)
        {
            if (button == null)
                return;
            button.Background = Brush(selected ? Accent : Panel);
            button.BorderBrush = Brush(selected ? AccentHover : Border);
        }
    }
}
