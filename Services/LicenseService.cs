using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ZenithFiler.Services
{
    /// <summary>
    /// 機能キー定数。有償機能の識別に使用します。
    /// </summary>
    public static class FeatureKeys
    {
        public const string ExcelExport = "ExcelExport";
        public const string CsvExport = "CsvExport";
        public const string PdfConvert = "PdfConvert";
        public const string IndexSearch = "IndexSearch";
        public const string WorkingSet = "WorkingSet";
        public const string ThemeChange = "ThemeChange";
    }

    /// <summary>
    /// シェアウェアライセンス管理サービス。
    /// ロックファイル（.zenith_license）による全機能解除と、SQLite による使用回数カウントを提供します。
    /// </summary>
    public class LicenseService
    {
        private LicenseState _state = LicenseState.Free;

        /// <summary>現在のライセンス状態。</summary>
        public LicenseState State => _state;

        /// <summary>Full ライセンスかどうかのショートカット。</summary>
        public bool IsFullLicense => _state == LicenseState.Full;

        /// <summary>
        /// 無料版での機能別使用回数上限。
        /// 外部設定ファイルに出すと改ざん容易なためハードコードする。
        /// </summary>
        internal static readonly Dictionary<string, int> FreeLimits = new()
        {
            [FeatureKeys.ExcelExport] = 3,
            [FeatureKeys.CsvExport] = 5,
            [FeatureKeys.PdfConvert] = 3,
            [FeatureKeys.IndexSearch] = 20,
            [FeatureKeys.WorkingSet] = 5,
            [FeatureKeys.ThemeChange] = 5,
        };

        // HMAC 検証用の内部シークレット
        private static readonly byte[] _hmacSecret = Encoding.UTF8.GetBytes("ZenithFiler_2026_SharewareLicense_Key");

        /// <summary>
        /// ロックファイルを検証しライセンス状態を確定させ、UsageRecords テーブルを準備します。
        /// App 起動時に一度呼び出してください。
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                // ロックファイル検証
                _state = VerifyLockFile() ? LicenseState.Full : LicenseState.Free;

                // UsageRecords テーブル作成（DatabaseService 側でも行うが念のため）
                var db = App.Database.Connection;
                await db.CreateTableAsync<UsageRecord>().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _state = LicenseState.Free;
                _ = App.FileLogger.LogAsync($"[LicenseService] InitializeAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 指定機能を使用できるかを判定します。
        /// Full ライセンスなら常に true、Free なら使用回数が上限未満の場合に true。
        /// </summary>
        public async Task<bool> CanUseAsync(string featureKey)
        {
            if (_state == LicenseState.Full) return true;

            if (!FreeLimits.TryGetValue(featureKey, out var limit))
                return true; // 上限未定義の機能は制限なし

            var count = await GetUsageCountAsync(featureKey).ConfigureAwait(false);
            return count < limit;
        }

        /// <summary>
        /// 機能の使用をDBに記録します。
        /// </summary>
        public async Task RecordUsageAsync(string featureKey)
        {
            try
            {
                var record = new UsageRecord
                {
                    FeatureKey = featureKey,
                    UsedAt = DateTime.UtcNow.ToString("o")
                };
                await App.Database.Connection.InsertAsync(record).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[LicenseService] RecordUsageAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 指定機能の現在の使用回数を取得します。
        /// </summary>
        public async Task<int> GetUsageCountAsync(string featureKey)
        {
            try
            {
                var db = App.Database.Connection;
                return await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM UsageRecords WHERE FeatureKey = ?", featureKey)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[LicenseService] GetUsageCountAsync failed: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 指定機能の残り使用回数を取得します。Full ライセンスの場合は -1 を返します。
        /// </summary>
        public async Task<int> GetRemainingAsync(string featureKey)
        {
            if (_state == LicenseState.Full) return -1;

            if (!FreeLimits.TryGetValue(featureKey, out var limit))
                return -1; // 上限未定義

            var count = await GetUsageCountAsync(featureKey).ConfigureAwait(false);
            return Math.Max(0, limit - count);
        }

        /// <summary>
        /// 全機能キーとその上限・使用回数の一覧を取得します。設定画面表示用。
        /// </summary>
        public async Task<List<FeatureUsageInfo>> GetAllFeatureUsagesAsync()
        {
            var result = new List<FeatureUsageInfo>();
            foreach (var kv in FreeLimits)
            {
                var count = await GetUsageCountAsync(kv.Key).ConfigureAwait(false);
                result.Add(new FeatureUsageInfo
                {
                    FeatureKey = kv.Key,
                    DisplayName = GetFeatureDisplayName(kv.Key),
                    Limit = kv.Value,
                    UsageCount = count,
                    Remaining = _state == LicenseState.Full ? -1 : Math.Max(0, kv.Value - count),
                });
            }
            return result;
        }

        /// <summary>
        /// ロックファイルの HMAC-SHA256 署名を検証します。
        /// ファイル形式: ZENITH:FULL:{発行日}:{HMAC-SHA256のBase64}
        /// </summary>
        private bool VerifyLockFile()
        {
            try
            {
                var licPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".zenith_license");
                if (!File.Exists(licPath)) return false;

                var content = File.ReadAllText(licPath).Trim();
                var parts = content.Split(':');
                if (parts.Length < 4) return false;

                // 先頭3フィールド: "ZENITH", "FULL", 発行日
                if (parts[0] != "ZENITH" || parts[1] != "FULL") return false;

                // 署名対象は先頭3フィールドをコロン区切りで結合
                var payload = $"{parts[0]}:{parts[1]}:{parts[2]}";
                var expectedSig = parts[3];

                using var hmac = new HMACSHA256(_hmacSecret);
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var actualSig = Convert.ToBase64String(hash);

                return string.Equals(actualSig, expectedSig, StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                _ = App.FileLogger.LogAsync($"[LicenseService] VerifyLockFile failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 機能キーの日本語表示名を返します。
        /// </summary>
        internal static string GetFeatureDisplayName(string featureKey) => featureKey switch
        {
            FeatureKeys.ExcelExport => "Excel エクスポート",
            FeatureKeys.CsvExport => "CSV エクスポート",
            FeatureKeys.PdfConvert => "PDF 変換",
            FeatureKeys.IndexSearch => "インデックス検索",
            FeatureKeys.WorkingSet => "ワーキングセット",
            FeatureKeys.ThemeChange => "テーマ変更",
            _ => featureKey,
        };
    }

    /// <summary>
    /// 機能の使用状況を表示用にまとめた DTO。
    /// </summary>
    public class FeatureUsageInfo
    {
        public string FeatureKey { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int Limit { get; set; }
        public int UsageCount { get; set; }
        /// <summary>残り回数。Full ライセンスの場合は -1。</summary>
        public int Remaining { get; set; }
        /// <summary>使用率 (0.0〜1.0)。ProgressBar 表示用。</summary>
        public double UsageRatio => Limit > 0 ? Math.Min(1.0, (double)UsageCount / Limit) : 0;
        /// <summary>表示テキスト（例: "3 / 5 回使用"）。</summary>
        public string UsageText => $"{UsageCount} / {Limit} 回使用";
        /// <summary>残り回数テキスト（例: "残り 2 回"）。</summary>
        public string RemainingText => Remaining < 0 ? "無制限" : $"残り {Remaining} 回";
    }
}
