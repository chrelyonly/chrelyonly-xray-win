using chrelyonly_xray_win.ViewModels;

namespace chrelyonly_xray_win.Models;

public enum UnlockStatus
{
    Untested,
    Testing,
    Passed, // 通过 (绿色)
    Failed  // 未通过 (红色)
}

public class AiUnlockItem : ViewModelBase
{
    private string _key = "";
    private string _name = "";
    private string _testUrl = "";
    private UnlockStatus _status = UnlockStatus.Untested;
    private int _latencyMs = -1;
    private bool _isTesting = false;

    public string Key
    {
        get => _key;
        set => SetProperty(ref _key, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string TestUrl
    {
        get => _testUrl;
        set => SetProperty(ref _testUrl, value);
    }

    public UnlockStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBadgeBackground));
                OnPropertyChanged(nameof(StatusBorderBrush));
                OnPropertyChanged(nameof(LatencyText));
            }
        }
    }

    public int LatencyMs
    {
        get => _latencyMs;
        set
        {
            if (SetProperty(ref _latencyMs, value))
            {
                OnPropertyChanged(nameof(LatencyText));
            }
        }
    }

    public bool IsTesting
    {
        get => _isTesting;
        set
        {
            if (SetProperty(ref _isTesting, value))
            {
                OnPropertyChanged(nameof(LatencyText));
            }
        }
    }

    public string LatencyText
    {
        get
        {
            if (IsTesting) return "测速中...";
            if (_latencyMs < 0) return "-- ms";
            if (_latencyMs == 0 || _latencyMs > 9999) return "超时 / 不可用";
            return $"{_latencyMs} ms";
        }
    }

    public string StatusColor => Status switch
    {
        UnlockStatus.Passed => "#10B981",  // 绿色 (通过)
        UnlockStatus.Failed => "#EF4444",  // 红色 (未通过)
        UnlockStatus.Testing => "#F59E0B", // 黄色 (检测中)
        _ => "#9CA3AF"                      // 灰色 (未检测)
    };

    public string StatusText => Status switch
    {
        UnlockStatus.Passed => "已解锁",
        UnlockStatus.Failed => "未解锁",
        UnlockStatus.Testing => "检测中",
        _ => "未检测"
    };

    public string StatusBadgeBackground => Status switch
    {
        UnlockStatus.Passed => "#DCFCE7", // 浅绿
        UnlockStatus.Failed => "#FEE2E2", // 浅红
        UnlockStatus.Testing => "#FEF3C7", // 浅黄
        _ => "#F3F4F6"                     // 浅灰
    };

    public string StatusBorderBrush => Status switch
    {
        UnlockStatus.Passed => "#86EFAC",
        UnlockStatus.Failed => "#FCA5A5",
        UnlockStatus.Testing => "#FCD34D",
        _ => "#E5E7EB"
    };
}
