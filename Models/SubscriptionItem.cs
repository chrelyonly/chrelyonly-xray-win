using System.Collections.ObjectModel;
using chrelyonly_xray_win.ViewModels;

namespace chrelyonly_xray_win.Models;

public class SubscriptionItem : ViewModelBase
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "新建订阅";
    private string _url = "";
    private int _nodeCount = 0;
    private DateTime? _lastUpdated;
    private bool _autoUpdate = true;
    private bool _isUpdating = false;
    private bool _isFixed = false;
    private string _typeTag = "通用"; // "通用", "YML", "Auto"

    public ObservableCollection<ServerNode> Nodes { get; } = new();

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    public string TypeTag
    {
        get => _typeTag;
        set
        {
            if (SetProperty(ref _typeTag, value))
            {
                OnPropertyChanged(nameof(TypeTagDisplay));
                OnPropertyChanged(nameof(TypeTagBackground));
                OnPropertyChanged(nameof(TypeTagForeground));
            }
        }
    }

    public string TypeTagDisplay
    {
        get
        {
            if (IsFixed) return "🔒 内置固定";
            if (_typeTag == "YML" || _typeTag == "YAML") return "📄 YML订阅";
            return "🔗 通用订阅";
        }
    }

    public string TypeTagBackground
    {
        get
        {
            if (IsFixed) return "#DBEAFE";
            if (_typeTag == "YML" || _typeTag == "YAML") return "#F3E8FF";
            return "#DCFCE7";
        }
    }

    public string TypeTagForeground
    {
        get
        {
            if (IsFixed) return "#1E40AF";
            if (_typeTag == "YML" || _typeTag == "YAML") return "#7E22CE";
            return "#15803D";
        }
    }

    public int NodeCount
    {
        get => _nodeCount;
        set => SetProperty(ref _nodeCount, value);
    }

    public bool IsFixed
    {
        get => _isFixed;
        set
        {
            if (SetProperty(ref _isFixed, value))
            {
                OnPropertyChanged(nameof(TypeTagDisplay));
                OnPropertyChanged(nameof(TypeTagBackground));
                OnPropertyChanged(nameof(TypeTagForeground));
            }
        }
    }

    public DateTime? LastUpdated
    {
        get => _lastUpdated;
        set
        {
            if (SetProperty(ref _lastUpdated, value))
            {
                OnPropertyChanged(nameof(LastUpdatedDisplay));
            }
        }
    }

    public bool AutoUpdate
    {
        get => _autoUpdate;
        set => SetProperty(ref _autoUpdate, value);
    }

    public bool IsUpdating
    {
        get => _isUpdating;
        set => SetProperty(ref _isUpdating, value);
    }

    public string LastUpdatedDisplay => LastUpdated.HasValue
        ? LastUpdated.Value.ToString("yyyy-MM-dd HH:mm")
        : "未更新";
}
