using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using GUI.ViewModels;

namespace GUI;

/// <summary>
/// Main page containing the AirPods Monitor UI.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; }

    public MainPage()
    {
        InitializeComponent();

        // Resolve ViewModel with all dependencies from DI container
        ViewModel = App.Current.Services.GetRequiredService<MainPageViewModel>();

        // Initialize when page loads
        Loaded += (_, _) => ViewModel.Initialize();
        Unloaded += (_, _) => ViewModel.Dispose();
    }
}

