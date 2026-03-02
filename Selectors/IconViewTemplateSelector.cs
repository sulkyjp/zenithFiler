using System.Windows;
using System.Windows.Controls;

namespace ZenithFiler
{
    /// <summary>
    /// アイコンビュー用のDataTemplateSelector。
    /// 画像ファイルと非画像ファイルで表示を切り替える。
    /// </summary>
    public class IconViewTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? ImageTemplate { get; set; }
        public DataTemplate? NonImageTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is FileItem fileItem)
                return fileItem.IsImageFile ? ImageTemplate : NonImageTemplate;
            return NonImageTemplate;
        }
    }
}
