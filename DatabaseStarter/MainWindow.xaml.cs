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

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Stop all running databases on exit
        await _viewModel.StopAllAsync();
    }
}