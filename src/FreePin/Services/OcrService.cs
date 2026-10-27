using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using FreePin.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using WinBitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace FreePin.Services
{
    public sealed class OcrService
    {
        private static readonly Regex ChineseSpace = new Regex(
            @"(?<=[\u3400-\u9fff])\s+(?=[\u3400-\u9fff])", RegexOptions.Compiled);

        private readonly OcrEngine _engine;

        public OcrService()
        {
            try
            {
                Language language = OcrEngine.AvailableRecognizerLanguages
                    .FirstOrDefault(l => l.LanguageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase));
                _engine = language != null
                    ? OcrEngine.TryCreateFromLanguage(language)
                    : OcrEngine.TryCreateFromUserProfileLanguages();
            }
            catch (Exception ex)
            {
                Log.Error("初始化 Windows OCR 失败", ex);
            }
        }

        public bool IsAvailable
        {
            get { return _engine != null; }
        }

        public string LanguageName
        {
            get
            {
                if (_engine == null)
                    return "不可用";
                return _engine.RecognizerLanguage.DisplayName + " (" +
                       _engine.RecognizerLanguage.LanguageTag + ")";
            }
        }

        public async Task<string> RecognizeAsync(BitmapSource source)
        {
            if (_engine == null)
                throw new InvalidOperationException("系统 OCR 不可用。请确认 Windows 已安装中文语言包。");
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            BitmapSource prepared = ImageUtil.ScaleDown(source, (int)OcrEngine.MaxImageDimension);
            byte[] png = EncodePng(prepared);
            string text = await RecognizePngAsync(png);
            return Normalize(text);
        }

        private async Task<string> RecognizePngAsync(byte[] png)
        {
            using (var stream = new InMemoryRandomAccessStream())
            {
                DataWriter writer = new DataWriter(stream.GetOutputStreamAt(0));
                writer.WriteBytes(png);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
                stream.Seek(0);

                WinBitmapDecoder decoder = await WinBitmapDecoder.CreateAsync(stream);
                using (SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync(
                           BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                {
                    OcrResult result = await _engine.RecognizeAsync(bitmap);
                    return FormatLines(result);
                }
            }
        }

        /// <summary>
        /// Windows OCR's OcrResult.Text joins every recognized line into one
        /// string. Use OcrResult.Lines so the result keeps the visual line breaks.
        /// </summary>
        private static string FormatLines(OcrResult result)
        {
            if (result == null)
                return string.Empty;

            var lines = new List<string>();
            if (result.Lines != null)
            {
                foreach (OcrLine line in result.Lines)
                {
                    string normalized = Normalize(line.Text);
                    if (normalized.Length > 0)
                        lines.Add(normalized);
                }
            }

            if (lines.Count == 0)
                return Normalize(result.Text);
            return string.Join(Environment.NewLine, lines);
        }

        private static byte[] EncodePng(BitmapSource source)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var lines = new List<string>();
            foreach (string rawLine in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = ChineseSpace.Replace(rawLine.Trim(), string.Empty);
                if (line.Length > 0)
                    lines.Add(line);
            }
            return string.Join(Environment.NewLine, lines);
        }
    }
}
