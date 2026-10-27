using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FreePin.Imaging;
using FreePin.Services;

namespace FreePin.Annotations
{
    public static class AnnotationRenderer
    {
        public static BitmapSource Render(System.Drawing.Bitmap baseImage, IList<Annotation> annotations)
        {
            if (baseImage == null)
                throw new ArgumentNullException(nameof(baseImage));
            if (annotations == null)
                annotations = new List<Annotation>();

            int width = baseImage.Width;
            int height = baseImage.Height;
            BitmapSource source = ScreenCapture.ToBitmapSource(baseImage);

            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawImage(source, new Rect(0, 0, width, height));
                foreach (Annotation annotation in annotations)
                {
                    var mosaic = annotation as MosaicAnnotation;
                    if (mosaic != null)
                        mosaic.EnsureMosaic(baseImage);
                    annotation.Draw(dc, source);
                }
            }

            var bitmap = new RenderTargetBitmap(Math.Max(1, width), Math.Max(1, height), 96, 96,
                PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        public static BitmapSource Render(System.Drawing.Bitmap baseImage, IList<Annotation> annotations, bool includeSelection)
        {
            if (!includeSelection)
                return Render(baseImage, annotations);

            BitmapSource rendered = Render(baseImage, annotations);
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawImage(rendered, new Rect(0, 0, rendered.PixelWidth, rendered.PixelHeight));
                foreach (Annotation annotation in annotations)
                {
                    if (annotation.IsSelected)
                        DrawSelection(dc, annotation.Bounds);
                }
            }
            var bitmap = new RenderTargetBitmap(rendered.PixelWidth, rendered.PixelHeight, 96, 96,
                PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static void DrawSelection(DrawingContext dc, Rect bounds)
        {
            if (bounds.IsEmpty)
                return;
            var brush = new SolidColorBrush(Color.FromArgb(255, 0, 120, 215));
            brush.Freeze();
            var pen = new Pen(brush, 1.5)
            {
                DashStyle = new DashStyle(new double[] { 4, 3 }, 0)
            };
            pen.Freeze();
            Rect selection = bounds;
            selection.Inflate(5, 5);
            dc.DrawRectangle(null, pen, selection);
        }

        public static BitmapSource EncodePngSource(BitmapSource source)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (var stream = new System.IO.MemoryStream())
            {
                encoder.Save(stream);
                stream.Position = 0;
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }
    }
}
