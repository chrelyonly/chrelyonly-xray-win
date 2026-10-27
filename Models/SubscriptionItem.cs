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

    public int NodeCount
    {
        get => _nodeCount;
        set => SetProperty(ref _nodeCount, value);
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
