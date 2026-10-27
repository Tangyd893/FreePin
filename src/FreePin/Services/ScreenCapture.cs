using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using FreePin.Native;

namespace FreePin.Services
{
    public sealed class ScreenFrame : IDisposable
    {
        public Bitmap Bitmap { get; private set; }
        public Rectangle Bounds { get; private set; }
        public double Scale { get; private set; }
        public BitmapSource ImageSource { get; private set; }

        public ScreenFrame(Bitmap bitmap, Rectangle bounds, double scale)
        {
            Bitmap = bitmap;
            Bounds = bounds;
            Scale = scale <= 0 ? 1.0 : scale;
            ImageSource = ScreenCapture.ToBitmapSource(bitmap);
        }

        public void Dispose()
        {
            if (Bitmap != null)
            {
                Bitmap.Dispose();
                Bitmap = null;
            }
        }
    }

    public static class ScreenCapture
    {
        public static double PrimaryScale
        {
            get { return NativeMethods.GetSystemDpiScale(); }
        }

        public static Rectangle VirtualBounds
        {
            get { return NativeMethods.GetVirtualScreenBounds(); }
        }

        public static ScreenFrame CaptureVirtualScreen()
        {
            Rectangle bounds = VirtualBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                throw new InvalidOperationException("无法获取屏幕尺寸。");

            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }

            return new ScreenFrame(bitmap, bounds, PrimaryScale);
        }

        public static Bitmap CaptureRegion(Rectangle physicalBounds)
        {
            if (physicalBounds.Width <= 0 || physicalBounds.Height <= 0)
                throw new ArgumentException("截图区域无效。", nameof(physicalBounds));

            Bitmap bitmap = new Bitmap(physicalBounds.Width, physicalBounds.Height, PixelFormat.Format32bppArgb);
            try
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(physicalBounds.Left, physicalBounds.Top, 0, 0, physicalBounds.Size,
                        CopyPixelOperation.SourceCopy);
                }
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }

        public static Bitmap Crop(Bitmap source, Rectangle pixelRect)
        {
            Rectangle bounds = new Rectangle(0, 0, source.Width, source.Height);
            Rectangle rect = Rectangle.Intersect(bounds, pixelRect);
            if (rect.Width <= 0 || rect.Height <= 0)
                throw new InvalidOperationException("截图区域无效。");

            Bitmap result = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.DrawImage(source, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
            }
            return result;
        }

        public static BitmapSource ToBitmapSource(Bitmap bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));

            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                hBitmap = bitmap.GetHbitmap();
                BitmapSource source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                if (hBitmap != IntPtr.Zero)
                    NativeMethods.DeleteObject(hBitmap);
            }
        }

        public static System.Windows.Point GetCursorPosition()
        {
            NativeMethods.POINT point;
            NativeMethods.GetCursorPos(out point);
            return new System.Windows.Point(point.X, point.Y);
        }
    }
}
