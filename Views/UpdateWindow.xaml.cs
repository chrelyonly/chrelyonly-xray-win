using System.Diagnostics;
using System.Windows;
using chrelyonly_xray_win.Models;

namespace chrelyonly_xray_win.Views;

public partial class UpdateWindow : Window
{
    public UpdateWindow(UpdateInfo info)
    {
        InitializeComponent();
        Owner = System.Windows.Application.Current.MainWindow;

        TxtTitle.Text = string.IsNullOrWhiteSpace(info.Title) ? "更新提示" : info.Title;
        TxtVersionTag.Text = $"最新版本: v{info.Version}";
        TxtDescription.Text = string.IsNullOrWhiteSpace(info.Des) ? "暂无更新描述" : info.Des;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://www.chrelyonly.cn/")
            {
                UseShellExecute = true
            });
        }
        catch { }

        DialogResult = true;
        Close();
    }
}
