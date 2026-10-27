using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using FreePin.Imaging;
using WpfBorder = System.Windows.Controls.Border;
using WpfButton = System.Windows.Controls.Button;
using WpfContentPresenter = System.Windows.Controls.ContentPresenter;
using WpfControl = System.Windows.Controls.Control;

namespace FreePin.UI
{
    public static class UiTheme
    {
        // Paper visual system: warm paper surfaces, ink text, terracotta accent.
        public static readonly Color Background = Color.FromRgb(247, 241, 230);
        public static readonly Color Panel = Color.FromRgb(255, 251, 243);
        public static readonly Color PanelHover = Color.FromRgb(239, 228, 212);
        public static readonly Color Border = Color.FromRgb(223, 208, 188);
        public static readonly Color Foreground = Color.FromRgb(47, 42, 37);
        public static readonly Color Muted = Color.FromRgb(111, 98, 85);
        public static readonly Color Accent = Color.FromRgb(180, 78, 50);
        public static readonly Color AccentHover = Color.FromRgb(154, 63, 39);
        public static readonly Color AccentSoft = Color.FromRgb(241, 217, 206);
        public static readonly Color Danger = Color.FromRgb(169, 67, 54);
        public static readonly Color CanvasBackground = Color.FromRgb(35, 32, 28);
        public static readonly Color CanvasBorder = Color.FromRgb(59, 52, 44);
        public static readonly Color PaperText = Color.FromRgb(255, 249, 240);

        public static readonly FontFamily UiFont = new FontFamily("Microsoft YaHei UI");
        public static readonly FontFamily GlyphFont = new FontFamily("Segoe UI Symbol");

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
                Foreground = Brush(accent ? PaperText : Foreground),
                BorderBrush = Brush(accent ? Accent : Border),
                BorderThickness = new Thickness(1),
                FontSize = 13,
                FontFamily = UiFont,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Style = CreateButtonStyle(accent, false)
            };
            AutomationProperties.SetName(button, text ?? string.Empty);
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
                FontFamily = GlyphFont,
                Cursor = Cursors.Hand,
                Padding = new Thickness(0),
                Style = CreateButtonStyle(false, true)
            };
            AutomationProperties.SetName(button, toolTip ?? string.Empty);
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
                FontFamily = UiFont,
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
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14),
                Child = child,
                SnapsToDevicePixels = true
            };
        }

        public static MenuItem MenuItem(string header, RoutedEventHandler click)
        {
            var item = new MenuItem
            {
                Header = header,
                FontFamily = UiFont,
                Foreground = Brush(Foreground)
            };
            if (click != null)
                item.Click += click;
            return item;
        }

        public static ContextMenu ContextMenu(params UIElement[] items)
        {
            var menu = new ContextMenu();
            foreach (UIElement item in items)
                menu.Items.Add(item);
            StyleContextMenu(menu);
            return menu;
        }

        public static void StyleContextMenu(ContextMenu menu)
        {
            if (menu == null)
                return;
            menu.Background = Brush(Panel);
            menu.Foreground = Brush(Foreground);
            menu.BorderBrush = Brush(Border);
            menu.FontFamily = UiFont;
            menu.Padding = new Thickness(4);
        }

        public static void ApplyWindow(Window window)
        {
            if (window == null)
                return;
            window.FontFamily = UiFont;
            window.Foreground = Brush(Foreground);
            window.Background = Brush(Background);
            ApplyWindowIcon(window);
        }

        public static void ApplyWindowIcon(Window window)
        {
            if (window == null)
                return;
            try
            {
                var icon = ImageUtil.LoadIconSource();
                if (icon != null)
                    window.Icon = icon;
            }
            catch
            {
                // The icon is cosmetic; never fail window creation because of it.
            }
        }

        public static void SetSelected(Button button, bool selected)
        {
            if (button == null)
                return;
            button.Style = CreateButtonStyle(selected, true);
            button.Background = Brush(selected ? Accent : Panel);
            button.BorderBrush = Brush(selected ? AccentHover : Border);
            button.Foreground = Brush(selected ? PaperText : Foreground);
        }

        private static Style CreateButtonStyle(bool accent, bool tool)
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.FontFamilyProperty, UiFont));
            style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));

            var border = new FrameworkElementFactory(typeof(WpfBorder), "bd");
            border.SetValue(WpfBorder.CornerRadiusProperty, new CornerRadius(tool ? 6 : 7));
            border.SetValue(WpfBorder.SnapsToDevicePixelsProperty, true);
            border.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            border.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            border.SetBinding(WpfBorder.BackgroundProperty, TemplatedBinding("Background"));
            border.SetBinding(WpfBorder.BorderBrushProperty, TemplatedBinding("BorderBrush"));
            border.SetBinding(WpfBorder.BorderThicknessProperty, TemplatedBinding("BorderThickness"));
            border.SetBinding(WpfBorder.PaddingProperty, TemplatedBinding("Padding"));

            var presenter = new FrameworkElementFactory(typeof(WpfContentPresenter), "content");
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(WpfContentPresenter.RecognizesAccessKeyProperty, true);
            presenter.SetBinding(WpfContentPresenter.ContentProperty, TemplatedBinding("Content"));
            presenter.SetBinding(WpfContentPresenter.ContentTemplateProperty, TemplatedBinding("ContentTemplate"));
            border.AppendChild(presenter);

            var template = new ControlTemplate(typeof(WpfButton)) { VisualTree = border };
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(WpfBorder.BackgroundProperty,
                Brush(accent ? AccentHover : PanelHover), "bd"));
            template.Triggers.Add(hover);

            var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(WpfBorder.BackgroundProperty,
                Brush(accent ? Color.FromRgb(126, 49, 30) : Color.FromRgb(226, 211, 192)), "bd"));
            template.Triggers.Add(pressed);

            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.55, "bd"));
            template.Triggers.Add(disabled);

            var focused = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            focused.Setters.Add(new Setter(WpfBorder.BorderBrushProperty, Brush(accent ? Accent : AccentHover), "bd"));
            template.Triggers.Add(focused);

            style.Setters.Add(new Setter(WpfControl.TemplateProperty, template));
            return style;
        }

        private static Binding TemplatedBinding(string path)
        {
            return new Binding(path) { RelativeSource = RelativeSource.TemplatedParent };
        }
    }
}

