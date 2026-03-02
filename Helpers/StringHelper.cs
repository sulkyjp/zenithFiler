using System;

namespace ZenithFiler
{
    public static class StringHelper
    {
        /// <summary>
        /// 編集距離（Levenshtein距離）を計算します。
        /// メモリを O(min(n,m)) に抑えつつ、履歴検索のフィルタで使用する短い文字列向けに最適化しています。
        /// </summary>
        public static int ComputeLevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
            if (string.IsNullOrEmpty(t)) return s.Length;

            // 長さの差が大きすぎる場合は計算しない（高速化）
            if (Math.Abs(s.Length - t.Length) > 5) return int.MaxValue;

            int n = s.Length;
            int m = t.Length;
            if (n < m) return ComputeLevenshteinDistance(t, s);

            // 2行のみ保持してメモリ使用量を O(m) に削減
            var prev = new int[m + 1];
            var curr = new int[m + 1];

            for (int j = 0; j <= m; j++) prev[j] = j;

            for (int i = 1; i <= n; i++)
            {
                curr[0] = i;
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    curr[j] = Math.Min(
                        Math.Min(prev[j] + 1, curr[j - 1] + 1),
                        prev[j - 1] + cost);
                }
                (prev, curr) = (curr, prev);
            }
            return prev[m];
        }
    }
}
