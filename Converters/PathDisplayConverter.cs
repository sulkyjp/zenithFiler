using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace ZenithFiler
{
    public class PathDisplayConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not string fullPath || values[1] is not double width)
            {
                return Binding.DoNothing;
            }

            // デフォルトの閾値は250pxとするが、パラメータで変更可能にする
            double threshold = 250;
            if (parameter is string paramStr && double.TryParse(paramStr, out double p))
            {
                threshold = p;
            }

            // 幅が狭い場合は親フォルダ名のみ表示
            if (width < threshold)
            {
                try
                {
                    // ファイルまたはフォルダの親ディレクトリを取得
                    string? parentDir = Path.GetDirectoryName(fullPath);
                    
                    // ルートディレクトリの場合などは親がnullになる
                    if (string.IsNullOrEmpty(parentDir))
                    {
                        return fullPath;
                    }

                    // 親ディレクトリの名前部分だけを取得（例: "C:\A\B" -> "B"）
                    string parentName = Path.GetFileName(parentDir);
                    
                    // ドライブ直下（"C:\"）の場合など GetFileName が空になる場合はそのまま返す
                    if (string.IsNullOrEmpty(parentName))
                    {
                         // "C:\" -> "C:\" のようにする、あるいは parentDir を使う
                         return parentDir;
                    }

                    return parentName;
                }
                catch
                {
                    return fullPath;
                }
            }

            // 十分な幅がある場合はフルパス
            return fullPath;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
