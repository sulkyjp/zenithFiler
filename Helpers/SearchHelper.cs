using System;

namespace ZenithFiler.Helpers
{
    public static class SearchHelper
    {
        /// <summary>
        /// Fuzzy Search (Levenshtein Distance) を用いたマッチング判定を行います。
        /// 文字数に応じて許容する距離を変えます。
        /// 3文字以下: 完全一致/部分一致のみ
        /// 4-7文字: 距離1まで
        /// 8文字以上: 距離2まで
        /// </summary>
        public static bool IsMatch(string text, string searchText)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchText)) return false;

            string target = text.ToLower();
            string search = searchText.Trim().ToLower();

            // 完全一致・部分一致（最優先）
            if (target.Contains(search))
            {
                return true;
            }

            // Fuzzy Search
            int threshold = search.Length <= 3 ? 0 : search.Length <= 7 ? 1 : 2;
            
            if (threshold > 0)
            {
                int distance = StringHelper.ComputeLevenshteinDistance(search, target);
                if (distance <= threshold) return true;
            }

            return false;
        }
    }
}
