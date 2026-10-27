using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FreePin.Native;

namespace FreePin.Imaging
{
    public static class ImageUtil
    {
        public static BitmapSource LoadBitmapSource(string path, int decodePixelWidth = 0)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                image.StreamSource = stream;
                if (decodePixelWidth > 0)
                    image.DecodePixelWidth = decodePixelWidth;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }

        public static BitmapSource ToBitmapSource(Bitmap bitmap)
        {
            return FreePin.Services.ScreenCapture.ToBitmapSource(bitmap);
        }

        public static Bitmap ToBitmap(BitmapSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            BitmapSource converted = source;
            if (source.Format != PixelFormats.Bgra32 && source.Format != PixelFormats.Bgr32)
            {
                converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
            }

            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                int stride = width * 4;
                byte[] pixels = new byte[stride * height];
                converted.CopyPixels(pixels, stride, 0);
                for (int y = 0; y < height; y++)
                {
                    IntPtr destination = IntPtr.Add(data.Scan0, y * data.Stride);
                    System.Runtime.InteropServices.Marshal.Copy(pixels, y * stride, destination, stride);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bitmap;
        }

        public static BitmapSource ScaleDown(BitmapSource source, int maxDimension)
        {
            if (source == null || source.PixelWidth <= maxDimension && source.PixelHeight <= maxDimension)
                return source;

            double scale = Math.Min((double)maxDimension / source.PixelWidth,
                (double)maxDimension / source.PixelHeight);
            var transformed = new TransformedBitmap(source, new ScaleTransform(scale, scale));
            transformed.Freeze();
            return transformed;
        }

        public static void SavePng(BitmapSource source, string path)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                encoder.Save(stream);
            }
        }

        public static void SavePng(Bitmap bitmap, string path)
        {
            bitmap.Save(path, ImageFormat.Png);
        }

        public static void CopyToClipboard(BitmapSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            Exception last = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    Clipboard.SetImage(source);
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    System.Threading.Thread.Sleep(60);
                }
            }

            if (last != null)
                throw new InvalidOperationException("复制到剪贴板失败，请稍后重试。", last);
        }

        public static BitmapSource LoadIconSource()
        {
            using (Stream stream = typeof(ImageUtil).Assembly.GetManifestResourceStream("FreePin.freepin.ico"))
            {
                if (stream == null)
                    return null;
                BitmapFrame frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                frame.Freeze();
                return frame;
            }
        }

        public static Icon LoadTrayIcon()
        {
            using (Stream stream = typeof(ImageUtil).Assembly.GetManifestResourceStream("FreePin.freepin.ico"))
            {
                if (stream == null)
                    return SystemIcons.Application;
                using (var icon = new Icon(stream))
                {
                    return (Icon)icon.Clone();
                }
            }
        }

        public static string HumanFileSize(long bytes)
        {
            if (bytes < 1024)
                return bytes + " B";
            if (bytes < 1024 * 1024)
                return (bytes / 1024.0).ToString("0.0") + " KB";
            return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
        }

        public static Bitmap CreateBitmapFromPixels(int width, int height, System.Drawing.Imaging.PixelFormat format)
        {
            return new Bitmap(width, height, format);
        }

        public static void DeleteHBitmap(IntPtr hBitmap)
        {
            if (hBitmap != IntPtr.Zero)
                NativeMethods.DeleteObject(hBitmap);
        }
    }
}
