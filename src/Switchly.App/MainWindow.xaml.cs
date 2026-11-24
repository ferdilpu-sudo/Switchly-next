using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Switchly.App.ViewModels;

namespace Switchly.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _viewModel;

        _viewModel.NavigateRequested += url => Browser.Load(url);
        Browser.AddressChanged += (_, e) => _viewModel.Address = (e.NewValue as string) ?? string.Empty;
    }
}
