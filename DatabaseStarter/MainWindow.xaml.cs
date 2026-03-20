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
}