using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using chrelyonly_xray_win.Models;
using chrelyonly_xray_win.ViewModels;
using chrelyonly_xray_win.Views;
using MessageBox = System.Windows.MessageBox;

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
        InitLogAutoScroll();
    }

    private void InitLogAutoScroll()
    {
        Services.AppLogger.SystemLogLines.CollectionChanged += (s, e) =>
        {
            if (ViewModel?.IsAutoScrollEnabled == true && ViewModel.IsLogsTabSelected)
            {
                SystemLogScrollViewer?.ScrollToEnd();
            }
        };

        Services.AppLogger.CoreLogLines.CollectionChanged += (s, e) =>
        {
            if (ViewModel?.IsAutoScrollEnabled == true && ViewModel.IsLogsTabSelected)
            {
                CoreLogScrollViewer?.ScrollToEnd();
            }
        };
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
            Text = "莓莓修仙版",
            Visible = true // 始终在系统托盘显示
        };

        _notifyIcon.DoubleClick += (s, args) =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        };

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("打开面板喵~", null, (s, args) =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
        contextMenu.Items.Add("退出程序喵~", null, (s, args) =>
        {
            _isActuallyExiting = true;

            try
            {
                Services.AppLogger.LogSystem("[程序退出] 正在停止 Xray 内核...");
                ViewModel.StopXrayCore();
            }
            catch (Exception ex)
            {
                Services.AppLogger.LogSystem(
                    $"[程序退出] 停止 Xray 内核失败：{ex.Message}");
            }
            finally
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }

                System.Windows.Application.Current.Shutdown();
            }
        });

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.RequestAddSubscriptionAction = ShowAddSubscriptionDialog;
        ViewModel.RequestEditSubscriptionAction = ShowEditSubscriptionDialog;
        ViewModel.RequestDeleteSubscriptionAction = ShowDeleteSubscriptionConfirmation;

        ViewModel.ResetDefaultSizeAction = () =>
        {
            Width = 440;
            Height = 720;
        };

        if (ViewModel.SavedWindowWidth > 300 && ViewModel.SavedWindowHeight > 300)
        {
            Width = ViewModel.SavedWindowWidth;
            Height = ViewModel.SavedWindowHeight;
        }
        else
        {
            Width = 440;
            Height = 720;
        }

        SizeChanged += (s, args) =>
        {
            if (IsLoaded && WindowState == WindowState.Normal)
            {
                ViewModel.SavedWindowWidth = ActualWidth;
                ViewModel.SavedWindowHeight = ActualHeight;
                ViewModel.SaveState();
            }
        };

        ViewModel.PropertyChanged += (s, args) =>
        {
            if (args.PropertyName == nameof(ViewModel.SelectedTabIndex))
            {
                AnimateTabTransition(ViewModel.SelectedTabIndex);
            }
            else if (args.PropertyName == nameof(ViewModel.IsXrayRunning))
            {
                string msg = ViewModel.IsXrayRunning ? "节点变化中~🐱" : "再见了喵~🐱";
                ShowTrayNotification("莓莓修仙提醒喵", msg);
            }
        };
    }

    public void ShowTrayNotification(string title, string message, System.Windows.Forms.ToolTipIcon icon = System.Windows.Forms.ToolTipIcon.Info)
    {
        if (ViewModel?.IsNotificationsEnabled != true) return;
        try
        {
            _notifyIcon?.ShowBalloonTip(2500, title, message, icon);
        }
        catch { }
    }

    private int _previousTabIndex = 0;

    private void AnimateTabTransition(int newTabIndex)
    {
        if (newTabIndex == _previousTabIndex) return;

        FrameworkElement? targetElement = newTabIndex switch
        {
            0 => HomeTabGrid,
            1 => SpeedStatsTabGrid,
            2 => NodesTabGrid,
            3 => SubscriptionsTabGrid,
            4 => LogsTabGrid,
            _ => null
        };

        TranslateTransform? targetTransform = newTabIndex switch
        {
            0 => HomeTabTransform,
            1 => SpeedStatsTabTransform,
            2 => NodesTabTransform,
            3 => SubscriptionsTabTransform,
            4 => LogsTabTransform,
            _ => null
        };

        if (targetElement != null && targetTransform != null)
        {
            // 如果新 Tab 索引大（往右切换），则新 Tab 从右侧 (+80) 向左平滑切入
            // 如果新 Tab 索引小（往左切换），则新 Tab 从左侧 (-80) 向右平滑切入
            double startX = (newTabIndex > _previousTabIndex) ? 80.0 : -80.0;

            var translateXAnimation = new DoubleAnimation
            {
                From = startX,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };

            var opacityAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };

            targetTransform.BeginAnimation(TranslateTransform.XProperty, translateXAnimation);
            targetElement.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
        }

        _previousTabIndex = newTabIndex;
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
        try
        {
            ViewModel.StopXrayCore();
        }
        catch (Exception ex)
        {
            Services.AppLogger.LogSystem(
                $"[程序退出] 关闭窗口时停止 Xray 失败：{ex.Message}");
        }
        finally
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            base.OnClosed(e);
        }
    }

    private void ShowAddSubscriptionDialog()
    {
        var dlg = new EditSubscriptionWindow("添加订阅源", "", "", "Auto") { Owner = this };
        if (ViewModel.ShowModalDialog(dlg) == true)
        {
            var newItem = new SubscriptionItem
            {
                Name = string.IsNullOrWhiteSpace(dlg.SubscriptionName) ? "新订阅源" : dlg.SubscriptionName,
                Url = dlg.SubscriptionUrl,
                TypeTag = dlg.SubscriptionTypeTag,
                NodeCount = 0
            };
            ViewModel.Subscriptions.Add(newItem);
            ViewModel.SelectedSubscription = newItem;

            _ = ViewModel.UpdateSubscriptionItemAsync(newItem);
        }
    }

    private void ShowEditSubscriptionDialog(SubscriptionItem item)
    {
        if (item.IsFixed || item.Id == "fixed-general-subscription")
        {
            var alertDlg = new AlertWindow("无法编辑", $"“{item.Name}” 是系统内置的固定通用订阅列表，不能被编辑！", false) { Owner = this };
            ViewModel.ShowModalDialog(alertDlg);
            return;
        }

        var dlg = new EditSubscriptionWindow("编辑订阅源", item.Name, item.Url, item.TypeTag) { Owner = this };
        if (ViewModel.ShowModalDialog(dlg) == true)
        {
            item.Name = string.IsNullOrWhiteSpace(dlg.SubscriptionName) ? "订阅源" : dlg.SubscriptionName;
            item.Url = dlg.SubscriptionUrl;
            item.TypeTag = dlg.SubscriptionTypeTag;

            _ = ViewModel.UpdateSubscriptionItemAsync(item);
        }
    }

    private void ShowDeleteSubscriptionConfirmation(SubscriptionItem item)
    {
        if (item.IsFixed || item.Id == "fixed-general-subscription")
        {
            var alertDlg = new AlertWindow("无法删除", $"“{item.Name}” 是系统内置的固定通用订阅列表，不能被删除！", false) { Owner = this };
            ViewModel.ShowModalDialog(alertDlg);
            return;
        }

        var confirmDlg = new ConfirmWindow("确认删除订阅", $"确定要删除订阅源“{item.Name}”吗？此操作无法撤销。") { Owner = this };
        if (ViewModel.ShowModalDialog(confirmDlg) == true)
        {
            ViewModel.Subscriptions.Remove(item);
            if (ViewModel.SelectedSubscription == item)
            {
                ViewModel.SelectedSubscription = ViewModel.Subscriptions.FirstOrDefault();
            }
        }
    }

    private void OnWindowPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (e.Delta > 0)
            {
                ViewModel.UiScale = Math.Min(2.0, ViewModel.UiScale + 0.05);
            }
            else if (e.Delta < 0)
            {
                ViewModel.UiScale = Math.Max(0.75, ViewModel.UiScale - 0.05);
            }
            e.Handled = true;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private const int WM_SYSCOMMAND = 0x112;
    private const int SC_SIZE = 0xF000;

    public enum ResizeDirection
    {
        Left = 1,
        Right = 2,
        Top = 3,
        TopLeft = 4,
        TopRight = 5,
        Bottom = 6,
        BottomLeft = 7,
        BottomRight = 8
    }

    private void ResizeWindow(ResizeDirection direction)
    {
        System.Windows.Input.Mouse.Capture(null);
        IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        SendMessage(hwnd, WM_SYSCOMMAND, (IntPtr)(SC_SIZE + (int)direction), IntPtr.Zero);
    }

    private void OnResizeTop(object sender, MouseButtonEventArgs e) => ResizeWindow(ResizeDirection.Top);
    private void OnResizeBottom(object sender, MouseButtonEventArgs e) => ResizeWindow(ResizeDirection.Bottom);
    private void OnResizeLeft(object sender, MouseButtonEventArgs e) => ResizeWindow(ResizeDirection.Left);
    private void OnResizeRight(object sender, MouseButtonEventArgs e) => ResizeWindow(ResizeDirection.Right);
    private void OnResizeTopLeft(object sender, MouseButtonEventArgs e) => ResizeWindow(ResizeDirection.TopLeft);
    private void OnResizeTopRight(object sender, MouseButtonEventArgs e) => ResizeWindow(ResizeDirection.TopRight);
    private void OnResizeBottomLeft(object sender, MouseButtonEventArgs e) => ResizeWindow(ResizeDirection.BottomLeft);
    private void OnResizeBottomRight(object sender, MouseButtonEventArgs e) => ResizeWindow(ResizeDirection.BottomRight);
}
