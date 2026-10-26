using System.Windows;

namespace chrelyonly_xray_win.Views;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow(string title, string message)
    {
        InitializeComponent();
        TxtTitle.Text = title;
        TxtMessage.Text = message;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
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
