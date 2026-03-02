using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using MahApps.Metro.IconPacks;
using ZenithFiler.Helpers;

namespace ZenithFiler
{
    // ソートの向きとプロパティを判定してVisibilityに変えるコンバーター
    public class SortInfoToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0]: SortProperty (string) - 例: "Name"
            // values[1]: SortDirection (ListSortDirection)
            // values[2]: ColumnHeader (object) - 例: "名前"
            if (values.Length >= 3 && 
                values[0] is string sortProp && 
                values[1] is ListSortDirection dir && 
                values[2] != null && 
                parameter is string targetDir)
            {
                string headerText = values[2]?.ToString() ?? string.Empty;
                string mappedProp = "";
                switch (headerText)
                {
                    case "名前": mappedProp = "Name"; break;
                    case "更新日時": mappedProp = "LastModified"; break;
                    case "種類": mappedProp = "TypeName"; break;
                    case "サイズ": mappedProp = "Size"; break;
                    case "場所": mappedProp = "LocationType"; break;
                    case "パス": mappedProp = "FullPath"; break;
                }

                if (sortProp == mappedProp)
                {
                    if (targetDir == "Ascending" && dir == ListSortDirection.Ascending) return Visibility.Visible;
                    if (targetDir == "Descending" && dir == ListSortDirection.Descending) return Visibility.Visible;
                }
                else
                {
                    // 非アクティブな列の場合、Ascending（上向き）アイコンのみを常に表示する
                    if (targetDir == "Ascending") return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // 現在アクティブなソート列かどうかを判定するコンバーター
    public class IsColumnActiveConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is string sortProp && values[1] != null)
            {
                string headerText = values[1]?.ToString() ?? string.Empty;
                string mappedProp = "";
                switch (headerText)
                {
                    case "名前": mappedProp = "Name"; break;
                    case "更新日時": mappedProp = "LastModified"; break;
                    case "種類": mappedProp = "TypeName"; break;
                    case "サイズ": mappedProp = "Size"; break;
                    case "場所": mappedProp = "LocationType"; break;
                    case "パス": mappedProp = "FullPath"; break;
                }
                return sortProp == mappedProp;
            }
            return false;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// IsBusy=true かつ IsIndexing=false のとき Visible を返す。
    /// インデックス作成中は専用インジケータがあるため、IsBusy インジケータを非表示にする。
    /// </summary>
    public class BusyNotIndexingConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is bool isBusy && values[1] is bool isIndexing)
            {
                return isBusy && !isIndexing ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int val && parameter is string param && int.TryParse(param, out int threshold))
            {
                return val >= threshold ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class EqualityToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() == parameter?.ToString();
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>enum と ConverterParameter の一致を bool に変換。ConvertBack で bool true のとき parameter の enum を返す（RadioButton の TwoWay 用）。</summary>
    public class EnumEqualityToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() == parameter?.ToString();
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? parameter : Binding.DoNothing;
        }
    }

    /// <summary>int と ConverterParameter（文字列の数値）の一致を bool に変換。ConvertBack で bool true のとき parameter の int を返す（RadioButton の TwoWay 用）。</summary>
    public class IntEqualityToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i && parameter != null && int.TryParse(parameter.ToString(), out var p))
                return i == p;
            return false;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter != null && int.TryParse(parameter.ToString(), out var p))
                return p;
            return Binding.DoNothing;
        }
    }

    /// <summary>enum と ConverterParameter の一致を Visibility に変換。</summary>
    public class EnumEqualityToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>enum と ConverterParameter が一致しないときに Visible、一致するときに Collapsed を返す。</summary>
    public class EnumEqualityToVisibilityInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() == parameter?.ToString() ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>Thumbnail ?? Icon を返す MultiValueConverter。values[0]=Thumbnail, values[1]=Icon。
    /// Thumbnail/Icon プロパティのみを監視するため、FileItem の他のプロパティ変更では再評価されない。
    /// 不正な BitmapSource は除外する。</summary>
    public class ThumbnailOrIconConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var thumbnail = values.Length > 0 ? values[0] as ImageSource : null;
                var icon = values.Length > 1 ? values[1] as ImageSource : null;
                var source = thumbnail ?? icon;
                if (source == null) return null;
                // 不正な BitmapSource（ピクセルサイズ 0 以下）は WPF レンダリングスレッドでクラッシュするため除外
                if (source is System.Windows.Media.Imaging.BitmapSource bmp && (bmp.PixelWidth <= 0 || bmp.PixelHeight <= 0))
                    return null;
                // Frozen でない BitmapSource はレンダリングスレッドからアクセスされるとクラッシュする
                if (source is System.Windows.Media.Imaging.BitmapSource bmp2 && !bmp2.IsFrozen)
                    return null;
                return source;
            }
            catch (Exception)
            {
                // コンバーター例外が WPF レンダリングパイプラインに波及してネイティブクラッシュを
                // 引き起こすことを防止する
                return null;
            }
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>enum と ConverterParameter が一致しないときに true を返す（アイコンモード時にアクセント色表示など）。</summary>
    public class EnumEqualityToBooleanInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() != parameter?.ToString();
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (parameter?.ToString() == "Inverse") boolValue = !boolValue;
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>bool が false のときに Visible、true のときに Collapsed を返す。</summary>
    public class BooleanToVisibilityInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>bool を反転して返す。例: GridSplitter の IsEnabled を「ロック時は false」にするため。</summary>
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : false;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : false;
        }
    }

    /// <summary>FavoritesSearchMode を ToggleButton の bool に変換。PathAndDescription → true、NameAndDescription → false。</summary>
    public class FavoritesSearchModeToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is FavoritesSearchMode mode && mode == FavoritesSearchMode.PathAndDescription;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? FavoritesSearchMode.PathAndDescription : FavoritesSearchMode.NameAndDescription;
        }
    }

    /// <summary>お気に入り検索モードトグルの ToolTip（アクション指向・短い文言）。</summary>
    public class FavoritesSearchModeToTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FavoritesSearchMode mode)
            {
                if (mode == FavoritesSearchMode.NameAndDescription)
                    return "フルパスで検索";
                if (mode == FavoritesSearchMode.PathAndDescription)
                    return "名前で検索";
            }
            return "検索対象を切替";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>ロック/固定系 Toggle の ToolTip。bool が true＝ロック中 → 解除の文言、false → ロック/固定の文言。parameter で種類を指定。</summary>
    public class BooleanToLockTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isLocked = value is bool b && b;
            string? key = parameter?.ToString();

            if (key == "TreeLock")
                return isLocked ? "ツリーロックを解除" : "ツリービューをロック";
            if (key == "FavoritesLock")
                return isLocked ? "お気に入りロックを解除" : "お気に入りをロック";
            if (key == "NavWidthLock")
                return isLocked ? "ナビ幅固定を解除" : "ナビ幅を固定";
            if (key == "DeleteConfirm")
                return isLocked ? "削除確認を無効化" : "削除確認を有効化";
            if (key == "SidebarVisibility")
                return isLocked ? "ナビを非表示" : "ナビを表示";
            if (key == "AlwaysOnTop")
                return isLocked ? "最前面固定を解除" : "最前面に固定";

            return isLocked ? "解除" : "ロック";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>インデックス検索モードの ToolTip。true＝通常モードへ戻す、false＝インデックスモードに入る。</summary>
    public class IndexSearchModeTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isIndexMode = value is bool b && b;
            return isIndexMode ? "通常検索モードに戻す (Ctrl+F)" : "インデックス検索モードに入る (Ctrl+Shift+F)";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>検索履歴のアイコン種別。通常検索＝虫眼鏡(Search)、インデックス検索＝雷(Zap)。</summary>
    public class SearchHistoryIconKindConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isIndexSearch = value is bool b && b;
            return isIndexSearch ? PackIconLucideKind.Zap : PackIconLucideKind.Search;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>非空文字列のとき Visible、それ以外は Collapsed。</summary>
    public class NonEmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class IsTodayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dt)
            {
                return dt.Date == DateTime.Today;
            }
            return false;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>double を GridLength に変換。負の値は Auto、0以上はピクセル幅。</summary>
    public class DoubleToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
                return d < 0 ? GridLength.Auto : new GridLength(d);
            return GridLength.Auto;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // ListViewの幅から他の列の幅を引いた残りを計算するコンバーター
    public class GridViewStarWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0]: ListView
            // values[1...N]: 他の列の Width (DateCol, TypeCol, SizeCol)
            // values[N+1...]: トリガー用 (ActualWidth, ActualHeight, Items.Count, WindowState など)
            if (values.Length > 0 && values[0] is ListView lv)
            {
                // ScrollViewerを取得して ViewportWidth を使う（スクロールバー除外後の幅）
                var scrollViewer = VisualTreeHelperExtensions.FindVisualChild<ScrollViewer>(lv);
                double totalWidth = scrollViewer != null && scrollViewer.ViewportWidth > 0 
                    ? scrollViewer.ViewportWidth 
                    : lv.ActualWidth;
                
                if (totalWidth <= 0) return 100.0;

                double otherWidths = 0;
                
                // パラメータで固定減算値を指定可能にする（検索ビュー用）
                if (parameter is string paramStr && double.TryParse(paramStr, out double fixedSubtraction))
                {
                    otherWidths += fixedSubtraction;
                }

                // インデックス 1, 2, 3, 4, 5 が LocationCol, DateCol, TypeCol, SizeCol の Width であると想定
                for (int i = 1; i <= 5; i++)
                {
                    if (i < values.Length && values[i] is double w)
                    {
                        otherWidths += w;
                    }
                }
                
                // 余裕を持たせて少し引く（境界線の太さなど）。ぴったり埋めるには 4-6px 程度引くのが安全
                double result = totalWidth - otherWidths - 6.0; 
                return result > 100 ? result : 100.0;
            }
            return 100.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>FavoriteItem の ToolTip テキスト（Path と Description を結合）。</summary>
    public class FavoriteToolTipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FavoriteItem item)
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(item.Path))
                    parts.Add(item.Path);
                if (!string.IsNullOrWhiteSpace(item.Description))
                    parts.Add($"概要: {item.Description}");
                return parts.Count > 0 ? string.Join("\n", parts) : item.Name ?? "";
            }
            return "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // 列の表示・非表示を幅に応じて切り替えるコンバーター
    public class AdaptiveColumnWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0]: ListView
            // values[1]: ListView.ActualWidth (トリガー用)
            // values[2]: IsAdaptiveColumnsEnabled (bool)
            // values[3...]: トリガー用 (Items.Count, ActualHeight など)
            // parameter: 列の種類 (string: "Date", "Type", "Size")
            
            if (values.Length >= 3 && values[0] is ListView lv && values[2] is bool enabled && parameter is string colType)
            {
                if (!enabled)
                {
                    // 機能OFF時はデフォルト幅を返す
                    return colType switch
                    {
                        "Location" => 40.0,
                        "Date" => 140.0,
                        "Type" => 120.0,
                        "Size" => 105.0, // スクロールバー分を考慮しない
                        _ => 100.0
                    };
                }

                double availableWidth = lv.ActualWidth;

                // 優先順位（残る順）: 名前 > 場所 > パス > 更新日時 > サイズ
                // 消える順: サイズ -> 更新日時 -> パス(可変だがここでは扱わない) -> 場所 -> 名前(消えない)
                // ※パスは可変幅でGridViewStarWidthConverterで制御されるため、ここでは「場所」が消えるかどうかの閾値を決める
                // Typeは要件にないが、サイズと同様に低優先度で消す

                // 閾値計算: [必須の幅] + [自分より優先度の高い列の幅]
                // 名前の最低幅: 150
                // 場所: 40
                // パス最低幅: 100 (概算)
                // 更新日時: 140
                // サイズ: 105
                // 種類: 120

                // サイズが表示される条件: 全て入る
                double allColumns = 150 + 40 + 100 + 140 + 105 + 120;
                // 種類が表示される条件
                double typeLimit = 150 + 40 + 100 + 140 + 120; 
                // 更新日時が表示される条件
                double dateLimit = 150 + 40 + 100 + 140;
                // 場所が表示される条件
                double locationLimit = 150 + 40 + 100;

                return colType switch
                {
                    "Type" => availableWidth > typeLimit ? 120.0 : 0.0, // 更新日時より先に消すか後に消すか不明だが、サイズと同程度とする
                    "Size" => availableWidth > allColumns ? 105.0 : 0.0,
                    "Date" => availableWidth > dateLimit ? 140.0 : 0.0,
                    "Location" => availableWidth > locationLimit ? 40.0 : 0.0, // パスが極限まで縮んだ後に消える
                    _ => 0.0
                };
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// ActualWidth と ActualHeight から角丸矩形の Clip 用 Geometry を生成する（アイコンビュー角丸用）。
    /// </summary>
    public class SizeToRoundedRectGeometryConverter : IMultiValueConverter
    {
        private const double Radius = 4.0;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2 ||
                values[0] is not double width ||
                values[1] is not double height ||
                width <= 0 || height <= 0)
            {
                return null!;
            }
            return new RectangleGeometry(new Rect(0, 0, width, height), Radius, Radius);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// double 値を半分にして返す（末尾子の縦線高さ計算用）。
    /// </summary>
    public class HalfValueConverter : IValueConverter
    {
        public static readonly HalfValueConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && d > 0)
                return d / 2.0;
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// ツリー接続線の表示制御コンバーター。ConverterParameter で動作を切替:
    /// "vert-full" = 非末尾の子 → Visible, "vert-last" = 末尾の子 → Visible, "horz" = 子なら常に Visible。
    /// ルート直下 (Level 0) では常に Collapsed。
    /// </summary>
    public class TreeLineVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not TreeViewItem item || parameter is not string mode)
                return Visibility.Collapsed;

            var parent = ItemsControl.ItemsControlFromItemContainer(item);
            if (parent == null || parent is TreeView)
                return Visibility.Collapsed;

            bool isLast = false;
            if (parent is TreeViewItem parentItem)
            {
                var generator = parentItem.ItemContainerGenerator;
                int index = generator.IndexFromContainer(item);
                isLast = index == parentItem.Items.Count - 1;
            }

            return mode switch
            {
                "vert-full" => isLast ? Visibility.Collapsed : Visibility.Visible,
                "vert-last" => isLast ? Visibility.Visible : Visibility.Collapsed,
                "horz" => Visibility.Visible,
                _ => Visibility.Collapsed,
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>long (bytes) ↔ double (MB) の双方向変換。検索フィルタのカスタムサイズ入力に使用。</summary>
    public class BytesToMBConverter : IValueConverter
    {
        private const double MB = 1024.0 * 1024.0;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
                return bytes <= 0 ? "" : (bytes / MB).ToString("0.##");
            return "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && double.TryParse(s, out var mb) && mb >= 0)
                return (long)(mb * MB);
            return 0L;
        }
    }
}
