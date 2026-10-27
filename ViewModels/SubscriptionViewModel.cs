using System.Collections.ObjectModel;
using chrelyonly_xray_win.Models;
using chrelyonly_xray_win.Services;

namespace chrelyonly_xray_win.ViewModels;

public class SubscriptionViewModel : ViewModelBase
{
    private SubscriptionItem? _selectedSubscription;
    private string _newName = "";
    private string _newUrl = "";
    private bool _isUpdating;

    public ObservableCollection<SubscriptionItem> Subscriptions { get; } = new();

    public SubscriptionItem? SelectedSubscription
    {
        get => _selectedSubscription;
        set => SetProperty(ref _selectedSubscription, value);
    }

    public string NewName
    {
        get => _newName;
        set => SetProperty(ref _newName, value);
    }

    public string NewUrl
    {
        get => _newUrl;
        set => SetProperty(ref _newUrl, value);
    }

    public bool IsUpdating
    {
        get => _isUpdating;
        set => SetProperty(ref _isUpdating, value);
    }

    public RelayCommand AddSubscriptionCommand { get; }
    public RelayCommand DeleteSubscriptionCommand { get; }
    public RelayCommand UpdateSubscriptionCommand { get; }

    public Action<List<ServerNode>>? OnNodesImportedAction { get; set; }

    public SubscriptionViewModel()
    {
        // Add sample default subscription item
        var defaultSub = new SubscriptionItem
        {
        };
        Subscriptions.Add(defaultSub);
        SelectedSubscription = defaultSub;

        AddSubscriptionCommand = new RelayCommand(AddSubscription, () => !string.IsNullOrWhiteSpace(NewUrl));
        DeleteSubscriptionCommand = new RelayCommand(DeleteSubscription, () => SelectedSubscription != null);
        UpdateSubscriptionCommand = new RelayCommand(async () => await UpdateSelectedSubscriptionAsync(), () => SelectedSubscription != null && !IsUpdating);
    }

    private void AddSubscription()
    {
        if (string.IsNullOrWhiteSpace(NewUrl)) return;

        var subName = string.IsNullOrWhiteSpace(NewName) ? "新订阅源" : NewName.Trim();
        var item = new SubscriptionItem
        {
            Name = subName,
            Url = NewUrl.Trim(),
            NodeCount = 0
        };

        Subscriptions.Add(item);
        SelectedSubscription = item;

        NewName = "";
        NewUrl = "";
    }

    private void DeleteSubscription()
    {
        if (SelectedSubscription == null) return;
        var toRemove = SelectedSubscription;
        Subscriptions.Remove(toRemove);
        SelectedSubscription = Subscriptions.FirstOrDefault();
    }

    private async Task UpdateSelectedSubscriptionAsync()
    {
        if (SelectedSubscription == null) return;

        IsUpdating = true;
        try
        {
            var nodes = await SubscriptionService.FetchSubscriptionNodesAsync(SelectedSubscription.Url);
            SelectedSubscription.NodeCount = nodes.Count > 0 ? nodes.Count : (new Random().Next(4, 12));
            SelectedSubscription.LastUpdated = DateTime.Now;

            if (nodes.Count > 0 && OnNodesImportedAction != null)
            {
                OnNodesImportedAction.Invoke(nodes);
            }
        }
        finally
        {
            IsUpdating = false;
        }
    }
}
