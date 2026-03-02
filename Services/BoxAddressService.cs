using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ZenithFiler
{
    /// <summary>
    /// Box Drive 内のフォルダ／ファイルについて、連携用パス（Box\～）を取得する。
    /// 実アドレス（URL）は Shift+右クリック後のクリップボード監視で取得する。
    /// </summary>
    public static class BoxAddressService
    {
        /// <summary>
        /// Box 連携用パスを取得する。URL は取得しない（Shift+右クリック＋「共有リンクをコピー」で監視により取得）。
        /// </summary>
        /// <param name="physicalPath">物理パス（フォルダまたはファイル）</param>
        /// <param name="cancellationToken">キャンセル（未使用・将来用）</param>
        /// <returns>(boxPath, null) のタプル。Box 外のパスは (null, null)。</returns>
        public static Task<(string? BoxPath, string? Url)> TryGetBoxPathAndUrlAsync(string physicalPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(physicalPath)) return Task.FromResult<(string?, string?)>((null, null));
            if (PathHelper.DetermineSourceType(physicalPath) != SourceType.Box) return Task.FromResult<(string?, string?)>((null, null));
            if (!PathHelper.TryGetBoxSharePath(physicalPath, out var boxPath) || string.IsNullOrEmpty(boxPath))
                return Task.FromResult<(string?, string?)>((null, null));

            return Task.FromResult<(string?, string?)>((boxPath, null));
        }
    }
}
