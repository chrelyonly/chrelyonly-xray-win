using System.Windows;

namespace chrelyonly_xray_win.Views;

public partial class AlertWindow : Window
{
    public AlertWindow(string title, string message, bool isSuccess = true)
    {
        InitializeComponent();
        TxtTitle.Text = title;
        TxtMessage.Text = message;

        if (!isSuccess)
        {
            IconBorder.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEE2E2"));
            IconPath.Fill = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444"));
            IconPath.Data = System.Windows.Media.Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z");
        }

        Loaded += (s, e) =>
        {
            Activate();
            Topmost = true;
            Focus();
        };
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
