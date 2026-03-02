using System.Windows;

namespace ZenithFiler
{
    public partial class DescriptionEditDialog : Window
    {
        public string DescriptionText
        {
            get => DescriptionTextBox.Text;
            set
            {
                DescriptionTextBox.Text = value ?? "";
                DescriptionTextBox.SelectAll();
            }
        }

        public DescriptionEditDialog(string defaultDescription = "")
        {
            InitializeComponent();
            DescriptionText = defaultDescription;
            DescriptionTextBox.Focus();

            var main = Application.Current.MainWindow;
            if (main != null && main.IsLoaded)
            {
                Owner = main;
                Topmost = main.Topmost;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
