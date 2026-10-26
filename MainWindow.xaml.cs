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
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    public bool _isActuallyExiting = false;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnWindowLoaded;
        InitNotifyIcon();
    }

    private void InitNotifyIcon()
    {
        System.Drawing.Icon trayIcon = System.Drawing.SystemIcons.Application;
        try
        {
            var iconStreamInfo = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/64x64.ico"));
            if (iconStreamInfo != null)
            {
                trayIcon = new System.Drawing.Icon(iconStreamInfo.Stream);
            }
        }
        catch
        {
            // Fallback to default system icon if loading fails
        }

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = trayIcon,
            Text = "代理控制台",
            Visible = true // 始终在系统托盘显示
        };

        _notifyIcon.DoubleClick += (s, args) =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        };

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("打开程序喵~", null, (s, args) =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
        contextMenu.Items.Add("退出程序喵~", null, (s, args) =>
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            _isActuallyExiting = true;
            System.Windows.Application.Current.Shutdown();
        });

        _notifyIcon.ContextMenuStrip = contextMenu;
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
        Hide();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isActuallyExiting)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnClosing(e);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
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

    private void ShowEditSubscriptionWindow(SubscriptionItem item)
    {
        // ...
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
