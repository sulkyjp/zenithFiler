using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ZenithFiler.Models;

namespace ZenithFiler.Views;

public partial class WelcomeWindow : Window
{
    private const int TotalPages = 9;
    private int _currentPage;
    private UIElement[] _pages = null!;
    private Ellipse[] _dots = null!;
    private string _selectedThemeName = "standard";
    private bool _themesLoaded;

    public WelcomeWindow()
    {
        InitializeComponent();
        Loaded += WelcomeWindow_Loaded;
    }

    private void WelcomeWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _pages = new UIElement[] { Page0, Page1, Page2, Page3, Page4, Page5, Page6, Page7, Page8 };
        _dots = new[] { Dot0, Dot1, Dot2, Dot3, Dot4, Dot5, Dot6, Dot7, Dot8 };
        UpdatePageVisibility();
    }

    private void UpdatePageVisibility()
    {
        for (int i = 0; i < TotalPages; i++)
            _pages[i].Visibility = i == _currentPage ? Visibility.Visible : Visibility.Collapsed;

        // Back button: hidden on page 0
        BackButton.Visibility = _currentPage > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Next button: visible on pages 0–7, hidden on page 8
        NextButton.Visibility = _currentPage < TotalPages - 1 ? Visibility.Visible : Visibility.Collapsed;

        // Update page 8 selected theme text
        if (_currentPage == TotalPages - 1)
            SelectedThemeText.Text = $"テーマ: {_selectedThemeName}";

        UpdateIndicators();
    }

    private void UpdateIndicators()
    {
        for (int i = 0; i < TotalPages; i++)
        {
            _dots[i].Fill = i == _currentPage
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("SubTextBrush");
        }
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage >= TotalPages - 1) return;
        _currentPage++;

        // Load themes when arriving at page 7
        if (_currentPage == 7 && !_themesLoaded)
        {
            _themesLoaded = true;
            LoadAllThemes();
        }

        UpdatePageVisibility();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage <= 0) return;
        _currentPage--;
        UpdatePageVisibility();
    }

    private void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = WindowSettings.CreateDefault();
        settings.ThemeName = _selectedThemeName;
        settings.Save();
        Close();
    }

    private void LoadAllThemes()
    {
        var themes = App.ThemeService.ScanThemes();
        ThemeCardsHost.ItemsSource = themes;

        // Pre-select "standard"
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var container = ThemeCardsHost.ItemContainerGenerator.ContainerFromIndex(0) as ContentPresenter;
            if (container != null)
            {
                container.ApplyTemplate();
                var radio = FindRadioButton(container);
                if (radio != null)
                    radio.IsChecked = true;
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static RadioButton? FindRadioButton(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is RadioButton rb) return rb;
            var found = FindRadioButton(child);
            if (found != null) return found;
        }
        return null;
    }

    private void ThemeCard_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.DataContext is ThemeInfo theme)
        {
            _selectedThemeName = theme.Name;
            App.ThemeService.ApplyThemeLive(theme.Name, Application.Current.Resources);
        }
    }

    private void ManualButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new ManualWindow { Owner = this };
        window.ShowDialog();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}
