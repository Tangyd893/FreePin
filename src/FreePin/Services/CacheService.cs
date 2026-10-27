using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using FreePin.Imaging;
using FreePin.Models;

namespace FreePin.Services
{
    public sealed class CacheEntry
    {
        public string Path { get; set; }
        public string FileName { get; set; }
        public DateTime Created { get; set; }
        public long Size { get; set; }

        public string DisplayTime
        {
            get { return Created.ToString("MM-dd HH:mm:ss"); }
        }
    }

    public sealed class CacheService
    {
        private const string FilePattern = "FreePin_*.png";
        private AppSettings _settings;

        public CacheService(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void UpdateSettings(AppSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            _settings = settings;
            Cleanup();
        }

        public AppSettings Settings
        {
            get { return _settings; }
        }

        public bool Enabled
        {
            get { return _settings.CacheEnabled; }
        }

        public string DirectoryPath
        {
            get
            {
                string path = _settings.CachePath;
                if (string.IsNullOrWhiteSpace(path))
                    path = SettingsService.DefaultCachePath;
                return Environment.ExpandEnvironmentVariables(path);
            }
        }

        public string Save(Bitmap bitmap)
        {
            if (bitmap == null || !Enabled)
                return null;

            try
            {
                string directory = DirectoryPath;
                Directory.CreateDirectory(directory);

                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                string path = Path.Combine(directory, "FreePin_" + stamp + ".png");
                int suffix = 1;
                while (File.Exists(path))
                {
                    path = Path.Combine(directory, "FreePin_" + stamp + "_" + suffix + ".png");
                    suffix++;
                }

                bitmap.Save(path, ImageFormat.Png);
                Log.Info("截图已加入最近缓存: " + path);
                Cleanup();
                return path;
            }
            catch (Exception ex)
            {
                Log.Error("写入最近截图缓存失败", ex);
                return null;
            }
        }

        public string Save(BitmapSource source)
        {
            if (source == null)
                return null;
            using (Bitmap bitmap = ImageUtil.ToBitmap(source))
            {
                return Save(bitmap);
            }
        }

        public List<CacheEntry> List()
        {
            var result = new List<CacheEntry>();
            try
            {
                string directory = DirectoryPath;
                if (!Directory.Exists(directory))
                    return result;

                foreach (string file in Directory.GetFiles(directory, FilePattern, SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        result.Add(new CacheEntry
                        {
                            Path = file,
                            FileName = Path.GetFileName(file),
                            Created = info.LastWriteTime,
                            Size = info.Length
                        });
                    }
                    catch
                    {
                        // Skip files that disappear while listing.
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("读取最近截图缓存失败", ex);
            }

            result.Sort((a, b) => b.Created.CompareTo(a.Created));
            return result;
        }

        public bool Delete(CacheEntry entry)
        {
            if (entry == null)
                return false;
            try
            {
                if (File.Exists(entry.Path))
                    File.Delete(entry.Path);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("删除缓存图片失败", ex);
                return false;
            }
        }

        public BitmapSource LoadThumbnail(CacheEntry entry, int decodeWidth)
        {
            if (entry == null)
                return null;
            try
            {
                return ImageUtil.LoadBitmapSource(entry.Path, decodeWidth);
            }
            catch (Exception ex)
            {
                Log.Error("读取缓存缩略图失败: " + entry.Path, ex);
                return null;
            }
        }

        public void Cleanup()
        {
            if (!Enabled)
                return;

            try
            {
                string directory = DirectoryPath;
                if (!Directory.Exists(directory))
                    return;

                DateTime cutoff = DateTime.Now.AddDays(-Math.Max(1, _settings.CacheRetentionDays));
                var files = new DirectoryInfo(directory).GetFiles(FilePattern, SearchOption.TopDirectoryOnly)
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();

                int keep = Math.Max(1, _settings.CacheMaxCount);
                for (int i = 0; i < files.Count; i++)
                {
                    bool expired = files[i].LastWriteTime < cutoff;
                    bool overLimit = i >= keep;
                    if (expired || overLimit)
                    {
                        try
                        {
                            files[i].Delete();
                        }
                        catch (Exception ex)
                        {
                            Log.Error("清理缓存文件失败: " + files[i].FullName, ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("清理最近截图缓存失败", ex);
            }
        }
    }
}
