using System.Windows;

namespace chrelyonly_xray_win.Views;

public partial class EditSubscriptionWindow : Window
{
    public string SubscriptionName => TxtName.Text.Trim();
    public string SubscriptionUrl => TxtUrl.Text.Trim();

    public string SubscriptionTypeTag
    {
        get
        {
            if (CmbTypeTag.SelectedIndex == 1) return "YML";
            if (CmbTypeTag.SelectedIndex == 2) return "通用";
            return "Auto";
        }
    }

    public EditSubscriptionWindow(string title = "添加订阅源", string name = "", string url = "", string typeTag = "Auto")
    {
        InitializeComponent();
        TxtHeader.Text = title;
        TxtName.Text = name;
        TxtUrl.Text = url;

        if (typeTag == "YML" || typeTag == "YAML") CmbTypeTag.SelectedIndex = 1;
        else if (typeTag == "通用") CmbTypeTag.SelectedIndex = 2;
        else CmbTypeTag.SelectedIndex = 0;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtUrl.Text))
        {
            System.Windows.MessageBox.Show("请输入有效的订阅地址 URL", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
