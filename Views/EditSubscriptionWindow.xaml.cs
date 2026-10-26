using System.Windows;

namespace chrelyonly_xray_win.Views;

public partial class EditSubscriptionWindow : Window
{
    public string SubscriptionName => TxtName.Text.Trim();
    public string SubscriptionUrl => TxtUrl.Text.Trim();

    public EditSubscriptionWindow(string title = "添加订阅源", string name = "", string url = "")
    {
        InitializeComponent();
        TxtHeader.Text = title;
        TxtName.Text = name;
        TxtUrl.Text = url;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtUrl.Text))
        {
            MessageBox.Show("请输入有效的订阅地址 URL", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
