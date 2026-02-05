using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using PFD.Shared.Models;
using PFD.Wpf.ViewModels;

namespace PFD.Wpf;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item && item.Tag is string themeName)
        {
            ApplyTheme(themeName);
        }
    }

    private void ApplyTheme(string themeName)
    {
        var themeUri = new Uri($"Themes/{themeName}Theme.xaml", UriKind.Relative);

        // Find and remove the current custom theme
        ResourceDictionary? themeToRemove = null;
        foreach (var dict in Application.Current.Resources.MergedDictionaries)
        {
            if (dict.Source != null && dict.Source.OriginalString.Contains("Themes/")
                && dict.Source.OriginalString.EndsWith("Theme.xaml"))
            {
                themeToRemove = dict;
                break;
            }
        }

        if (themeToRemove != null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(themeToRemove);
        }

        // Add the new theme
        var newTheme = new ResourceDictionary { Source = themeUri };
        Application.Current.Resources.MergedDictionaries.Add(newTheme);

        // Switch Material Design base theme for dark/light themes
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();

        if (themeName == "Blackish")
        {
            theme.SetBaseTheme(BaseTheme.Dark);
        }
        else
        {
            theme.SetBaseTheme(BaseTheme.Light);
        }

        paletteHelper.SetTheme(theme);
    }

    private void TaskTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is DailyTask task && DataContext is MainViewModel vm)
        {
            vm.UpdateTaskTitleCommand.Execute(task);
            Keyboard.ClearFocus();
        }
    }

    private void WeeklyTaskTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is WeeklyTaskViewModel weeklyTask && DataContext is MainViewModel vm)
        {
            vm.UpdateWeeklyTaskTitleCommand.Execute(weeklyTask);
            Keyboard.ClearFocus();
        }
    }

    private void LoginPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox && DataContext is MainViewModel vm)
        {
            vm.LoginPassword = passwordBox.Password;
        }
    }

    private void RegisterPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox && DataContext is MainViewModel vm)
        {
            vm.LoginPassword = passwordBox.Password;
        }
    }
}
