using System.Windows;
using chrelyonly_xray_win.ViewModels;

namespace chrelyonly_xray_win.Views;

public partial class AddServerWindow : Window
{
    public AddServerViewModel ViewModel { get; }

    public AddServerWindow(AddServerViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
