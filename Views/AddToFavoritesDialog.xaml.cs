using System.Windows;

namespace ZenithFiler
{
    public partial class AddToFavoritesDialog : Window
    {
        public string NameText
        {
            get => NameTextBox.Text;
            set
            {
                NameTextBox.Text = value;
                NameTextBox.SelectAll();
            }
        }

        public string? DescriptionText
        {
            get => DescriptionTextBox.Text;
            set => DescriptionTextBox.Text = value ?? "";
        }

        public AddToFavoritesDialog(string defaultName, string defaultDescription = "")
        {
            InitializeComponent();
            NameText = defaultName;
            DescriptionText = defaultDescription;
            NameTextBox.Focus();

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
