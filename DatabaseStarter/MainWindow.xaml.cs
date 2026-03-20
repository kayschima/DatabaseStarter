using System.ComponentModel;
using System.Windows;
using DatabaseStarter.ViewModels;

namespace DatabaseStarter;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        Closing += MainWindow_Closing;
        _viewModel.LanguageChanged += OnLanguageChanged;
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Stop all running databases on exit
        await _viewModel.StopAllAsync();
    }

    private void InfoButton_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow { Owner = this };
        aboutWindow.ShowDialog();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Recreate the window so all x:Static bindings are re-evaluated
        var newWindow = new MainWindow
        {
            Left = Left,
            Top = Top,
            Width = Width,
            Height = Height,
            WindowState = WindowState
        };

        // Detach events so the old window doesn't stop databases on close
        _viewModel.LanguageChanged -= OnLanguageChanged;
        Closing -= MainWindow_Closing;

        Application.Current.MainWindow = newWindow;
        newWindow.Show();
        Close();
    }
}