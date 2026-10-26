using System.Windows;
using System.Windows.Input;
using chrelyonly_xray_win.Models;
using chrelyonly_xray_win.ViewModels;
using chrelyonly_xray_win.Views;

namespace chrelyonly_xray_win;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.RequestAddSubscriptionAction = ShowAddSubscriptionDialog;
        ViewModel.RequestEditSubscriptionAction = ShowEditSubscriptionDialog;
        ViewModel.RequestDeleteSubscriptionAction = ShowDeleteSubscriptionConfirmation;
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ShowAddSubscriptionDialog()
    {
        ViewModel.IsModalOpen = true;
        try
        {
            var dlg = new EditSubscriptionWindow("添加订阅源", "", "") { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                var newItem = new SubscriptionItem
                {
                    Name = string.IsNullOrWhiteSpace(dlg.SubscriptionName) ? "新订阅源" : dlg.SubscriptionName,
                    Url = dlg.SubscriptionUrl,
                    NodeCount = 0
                };
                ViewModel.Subscriptions.Add(newItem);
                ViewModel.SelectedSubscription = newItem;

                // Trigger node fetch for new subscription
                _ = ViewModel.UpdateSubscriptionItemAsync(newItem);
            }
        }
        finally
        {
            ViewModel.IsModalOpen = false;
        }
    }

    private void ShowEditSubscriptionDialog(SubscriptionItem item)
    {
        ViewModel.IsModalOpen = true;
        try
        {
            var dlg = new EditSubscriptionWindow("编辑订阅源", item.Name, item.Url) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                item.Name = string.IsNullOrWhiteSpace(dlg.SubscriptionName) ? "订阅源" : dlg.SubscriptionName;
                item.Url = dlg.SubscriptionUrl;

                // Re-fetch nodes with updated URL
                _ = ViewModel.UpdateSubscriptionItemAsync(item);
            }
        }
        finally
        {
            ViewModel.IsModalOpen = false;
        }
    }

    private void ShowDeleteSubscriptionConfirmation(SubscriptionItem item)
    {
        ViewModel.IsModalOpen = true;
        try
        {
            var confirmDlg = new ConfirmWindow("确认删除订阅", $"确定要删除订阅源“{item.Name}”吗？此操作无法撤销。") { Owner = this };
            if (confirmDlg.ShowDialog() == true)
            {
                ViewModel.Subscriptions.Remove(item);
                if (ViewModel.SelectedSubscription == item)
                {
                    ViewModel.SelectedSubscription = ViewModel.Subscriptions.FirstOrDefault();
                }
            }
        }
        finally
        {
            ViewModel.IsModalOpen = false;
        }
    }
}
