using System.Windows.Controls;

namespace ZenithFiler
{
    public partial class ProjectSetsView : UserControl
    {
        public ProjectSetsView()
        {
            InitializeComponent();
        }

        private void ListViewItem_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is ListViewItem item && item.DataContext is WorkingSetDto set)
            {
                if (DataContext is ProjectSetsViewModel vm)
                    vm.StartPreviewCommand.Execute(set);
            }
        }
    }
}
