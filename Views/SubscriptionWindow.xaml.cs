using System.Windows;
using chrelyonly_xray_win.ViewModels;

namespace chrelyonly_xray_win.Views;

public partial class SubscriptionWindow : Window
{
    public SubscriptionViewModel ViewModel { get; }

    public SubscriptionWindow(SubscriptionViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
