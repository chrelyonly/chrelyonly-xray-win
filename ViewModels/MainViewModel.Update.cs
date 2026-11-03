using chrelyonly_xray_win.Services;

namespace chrelyonly_xray_win.ViewModels;

public partial class MainViewModel
{
    private bool _isCheckingUpdate = false;
    public bool IsCheckingUpdate
    {
        get => _isCheckingUpdate;
        set => SetProperty(ref _isCheckingUpdate, value);
    }

    public async Task CheckForUpdateAsync(bool manual = false)
    {
        if (IsCheckingUpdate) return;
        IsCheckingUpdate = true;
        AppLogger.LogSystem($"[版本检测] 触发更新检测 (手动模式={manual})...");
        try
        {
            var (hasUpdate, info, error) = await UpdateCheckService.CheckForUpdateAsync();
            if (hasUpdate && info != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ShowModalDialog(new Views.UpdateWindow(info));
                });
            }
            else if (manual)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        ShowModalDialog(new Views.AlertWindow("检测失败", $"检查版本发生错误: {error}", false));
                    }
                    else
                    {
                        ShowModalDialog(new Views.AlertWindow("已是最新版本", $"当前版本 (v{UpdateCheckService.CurrentVersionCode}) 已是最新版本，无需更新！", true));
                    }
                });
            }
        }
        catch (Exception ex)
        {
            if (manual)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ShowModalDialog(new Views.AlertWindow("检测失败", $"检查更新发生异常: {ex.Message}", false));
                });
            }
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }
}
