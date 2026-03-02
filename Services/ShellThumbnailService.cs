using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.WindowsAPICodePack.Shell;

namespace ZenithFiler.Services
{
    /// <summary>
    /// WindowsAPICodePack-Shell を用いたサムネイル取得サービス。
    /// エクスプローラと同等のサムネイル表示を実現し、STA スレッド要件を満たします。
    /// </summary>
    public sealed class ShellThumbnailService
    {
        private const int MaxCacheSize = 300;
        private const int MaxThumbnailsPerFolder = 200;

        private static readonly Lazy<ShellThumbnailService> _instance = new(() => new ShellThumbnailService());
        public static ShellThumbnailService Instance => _instance.Value;

        private readonly object _cacheLock = new();
        private readonly Dictionary<string, (ImageSource Source, DateTime Added)> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _cacheOrder = new();

        private readonly BlockingCollection<(string Path, int Size, TaskCompletionSource<ImageSource?> Tcs, string? CacheKey)> _queue = new();
        private readonly Thread _staThread;

        private ShellThumbnailService()
        {
            _staThread = new Thread(StaThreadProc)
            {
                IsBackground = true,
                Name = "ShellThumbnail-STA"
            };
            _staThread.SetApartmentState(ApartmentState.STA);
            _staThread.Start();
        }

        private void StaThreadProc()
        {
            foreach (var item in _queue.GetConsumingEnumerable())
            {
                try
                {
                    var result = GetThumbnailOnStaThread(item.Path, item.Size);
                    item.Tcs.TrySetResult(result);
                    if (item.CacheKey != null && result != null)
                    {
                        lock (_cacheLock)
                        {
                            AddToCache(item.CacheKey, result);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _ = App.FileLogger.LogAsync($"ShellThumbnailService.StaThreadProc: {item.Path} - {FileLoggerService.FormatException(ex)}");
                    item.Tcs.TrySetResult(null);
                }
            }
        }

        /// <summary>
        /// サムネイルを同期で取得します。キャッシュにあれば即返し、なければ STA スレッドで取得するまでブロックします。
        /// UIスレッドやバインディングから呼ぶとフリーズの原因になるため、一覧表示などでは必ず <see cref="GetThumbnailAsync"/> を使用してください。
        /// </summary>
        public ImageSource? GetThumbnail(string path, int size = 256)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            if (!ShellIconHelper.IsImageFile(path)) return null;

            if (PathHelper.DetermineSourceType(path) == SourceType.Box) return null;
            try
            {
                var attrs = File.GetAttributes(path);
                if ((attrs & FileAttributes.Offline) != 0 || (attrs & FileAttributes.ReparsePoint) != 0)
                    return null;
            }
            catch
            {
                return null;
            }

            string cacheKey = $"{path}|{size}";
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(cacheKey, out var cached))
                {
                    PromoteInCache(cacheKey);
                    return cached.Source;
                }
            }

            var tcs = new TaskCompletionSource<ImageSource?>();
            _queue.Add((path, size, tcs, CacheKey: (string?)null));

            try
            {
                var result = tcs.Task.GetAwaiter().GetResult();
                if (result != null)
                {
                    lock (_cacheLock)
                    {
                        AddToCache(cacheKey, result);
                    }
                }
                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// サムネイルを非同期で取得します。ブロックせずにキューに積み、完了時に Task が完了します。
        /// 一覧でサムネイルを逐次表示する用途で使用します。
        /// </summary>
        public Task<ImageSource?> GetThumbnailAsync(string path, int size = 256, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(path))
                return Task.FromResult<ImageSource?>(null);
            if (!File.Exists(path))
                return Task.FromResult<ImageSource?>(null);
            if (!ShellIconHelper.IsImageFile(path))
                return Task.FromResult<ImageSource?>(null);
            if (PathHelper.DetermineSourceType(path) == SourceType.Box)
                return Task.FromResult<ImageSource?>(null);
            try
            {
                var attrs = File.GetAttributes(path);
                if ((attrs & FileAttributes.Offline) != 0 || (attrs & FileAttributes.ReparsePoint) != 0)
                    return Task.FromResult<ImageSource?>(null);
            }
            catch
            {
                return Task.FromResult<ImageSource?>(null);
            }

            string cacheKey = $"{path}|{size}";
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(cacheKey, out var cached))
                {
                    PromoteInCache(cacheKey);
                    return Task.FromResult<ImageSource?>(cached.Source);
                }
            }

            var tcs = new TaskCompletionSource<ImageSource?>();
            _queue.Add((path, size, tcs, cacheKey));

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            }

            return tcs.Task;
        }

        private ImageSource? GetThumbnailOnStaThread(string path, int size)
        {
            try
            {
                using var shellFile = ShellFile.FromFilePath(path);
                var thumb = shellFile.Thumbnail;
                if (thumb == null) return null;
                thumb.FormatOption = ShellThumbnailFormatOption.ThumbnailOnly;
                thumb.CurrentSize = new System.Windows.Size(size, size);

                var bmp = thumb.BitmapSource;
                if (bmp == null) return null;

                // 妥当性検証: ピクセルサイズが不正なら破棄
                if (bmp.PixelWidth <= 0 || bmp.PixelHeight <= 0) return null;

                // COM オブジェクト（IShellItemImageFactory）との依存を完全に切るため
                // CopyPixels で明示的にマネージド配列にピクセルデータをコピーし、
                // その配列から新しい WriteableBitmap を構築する。
                // new WriteableBitmap(bmp) はコンストラクタ内部でネイティブアクセスが発生し
                // COM 破棄タイミングによってはクラッシュする可能性があるため、
                // CopyPixels 方式のほうが安全。
                int stride = bmp.PixelWidth * ((bmp.Format.BitsPerPixel + 7) / 8);
                var pixels = new byte[stride * bmp.PixelHeight];
                bmp.CopyPixels(pixels, stride, 0);

                var wb = new WriteableBitmap(bmp.PixelWidth, bmp.PixelHeight, bmp.DpiX, bmp.DpiY, bmp.Format, bmp.Palette);
                wb.WritePixels(new System.Windows.Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight), pixels, stride, 0);
                wb.Freeze();
                return wb;
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"ShellThumbnailService.GetThumbnailOnStaThread: {path} - {FileLoggerService.FormatException(ex)}");
                return null;
            }
        }

        private void AddToCache(string key, ImageSource source)
        {
            while (_cache.Count >= MaxCacheSize && _cacheOrder.Count > 0)
            {
                var oldest = _cacheOrder[0];
                _cacheOrder.RemoveAt(0);
                _cache.Remove(oldest);
            }
            _cache[key] = (source, DateTime.UtcNow);
            _cacheOrder.Add(key);
        }

        private void PromoteInCache(string key)
        {
            _cacheOrder.Remove(key);
            _cacheOrder.Add(key);
        }

        /// <summary>
        /// 1フォルダあたりのサムネイル取得上限。負荷軽減のため。
        /// </summary>
        public static int MaxPerFolder => MaxThumbnailsPerFolder;
    }

}
