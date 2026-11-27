// Author: 524H0015 (Ly Tin Hoa)
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SaigonBite.OFDH.Controllers;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.Views.UC8;

public partial class KitchenDisplayView : UserControl
{
    private const string DefaultStaffId = "STF-K01";

    private sealed record AlertDisplayItem(string HeaderText, string Detail, Brush AccentBrush);

    private readonly KitchenPrepController _controller = new();
    private readonly ObservableCollection<AlertDisplayItem> _alerts = new();

    private Hub? _currentHub;
    private Order? _selectedOrder;
    private bool _isLoadingHubs;

    public KitchenDisplayView()
    {
        InitializeComponent();
        IcAlerts.ItemsSource = _alerts;
        LoadHubs("HUB-D1");
    }

    private void Root_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var current = e.OriginalSource as DependencyObject;
        while (current != null)
        {
            if (current is DataGridRow or TextBox or ComboBox or ComboBoxItem or Button)
                return;

            var parent = current is Visual
                ? VisualTreeHelper.GetParent(current)
                : null;

            current = parent ?? LogicalTreeHelper.GetParent(current);
        }

        Keyboard.ClearFocus();
    }

    private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DgKitchenQueue.UnselectAll();
            Keyboard.ClearFocus();
        }
    }

    private void LoadHubs(string? preferredHubId = null, string? preferredOrderId = null)
    {
        _isLoadingHubs = true;
        try
        {
            var targetHubId = preferredHubId ?? _currentHub?.HubId ?? "HUB-D1";
            var hubs = _controller.GetAllHubs();

            CmbHub.ItemsSource = hubs;
            if (hubs.Count == 0)
            {
                _currentHub = null;
                DgKitchenQueue.ItemsSource = null;
                IcHubStock.ItemsSource = null;
                UpdateSelectedOrderPanel();
                ShowToast("No cloud hubs found in SQLite table 'Hubs'.", isError: true);
                return;
            }

            var selected = hubs.FirstOrDefault(h =>
                string.Equals(h.HubId, targetHubId, StringComparison.OrdinalIgnoreCase)) ?? hubs[0];

            CmbHub.SelectedItem = selected;
            _currentHub = selected;
        }
        finally
        {
            _isLoadingHubs = false;
        }

        RefreshHubWorkspace(preferredOrderId);
    }

    private void RefreshHubWorkspace(string? preferredOrderId = null)
    {
        if (_currentHub == null) return;

        _currentHub = _controller.GetHubById(_currentHub.HubId) ?? _currentHub;
        UpdateHubStrip();

        var queue = _controller.GetKitchenQueue(_currentHub.HubId);
        DgKitchenQueue.ItemsSource = queue;

        var incoming = queue.Count(o => string.Equals(o.OrderStatus, "Placed", StringComparison.OrdinalIgnoreCase));
        var confirmed = queue.Count(o => string.Equals(o.OrderStatus, "ConfirmedByKitchen", StringComparison.OrdinalIgnoreCase));
        var preparing = queue.Count(o => string.Equals(o.OrderStatus, "InPreparation", StringComparison.OrdinalIgnoreCase));
        var ready = queue.Count(o => string.Equals(o.OrderStatus, "ReadyForPickup", StringComparison.OrdinalIgnoreCase));
        TxtQueueCounts.Text = $"Incoming: {incoming} | Confirmed: {confirmed} | Preparing: {preparing} | Ready: {ready}";

        var stock = _controller.GetHubMenuStock(_currentHub.HubId);
        TxtStockHeader.Text = $"{_currentHub.HubId} Live Menu Stock (US-11)";
        IcHubStock.ItemsSource = stock;

        if (_alerts.Count == 0)
            SeedInitialAlertsFromQueue(queue);

        Order? match = null;
        if (!string.IsNullOrWhiteSpace(preferredOrderId))
        {
            match = queue.FirstOrDefault(o =>
                string.Equals(o.OrderId, preferredOrderId, StringComparison.OrdinalIgnoreCase));
        }

        match ??= queue.FirstOrDefault(o =>
                      o.OrderStatus is "InPreparation" or "ConfirmedByKitchen" or "Placed")
                  ?? queue.FirstOrDefault();

        DgKitchenQueue.SelectedItem = match;
        _selectedOrder = match;
        UpdateSelectedOrderPanel();
    }

    private void SeedInitialAlertsFromQueue(List<Order> queue)
    {
        foreach (var ord in queue.OrderByDescending(o => o.OrderDateTime).Take(3))
        {
            if (string.Equals(ord.OrderStatus, "ReadyForPickup", StringComparison.OrdinalIgnoreCase))
            {
                AddAlert(new KitchenAlertEntry
                {
                    Timestamp = ExtractClockTime(ord.ReadyAt ?? ord.OrderDateTime),
                    Headline = $"Alert Sent -> {(string.IsNullOrWhiteSpace(ord.ShipperId) ? "Available Pool" : ord.ShipperId)}",
                    Detail = $"{ord.OrderId} marked ReadyForPickup at {ord.HubId}.",
                    AccentHex = "#10B981"
                }, insertAtTop: false);
            }
            else if (string.Equals(ord.OrderStatus, "Placed", StringComparison.OrdinalIgnoreCase))
            {
                AddAlert(new KitchenAlertEntry
                {
                    Timestamp = ExtractClockTime(ord.OrderDateTime),
                    Headline = "New Order Received",
                    Detail = $"{ord.OrderId} placed. 5-min confirmation timer active.",
                    AccentHex = "#EA580C"
                }, insertAtTop: false);
            }
            else
            {
                AddAlert(new KitchenAlertEntry
                {
                    Timestamp = ExtractClockTime(ord.PrepStartedAt ?? ord.OrderDateTime),
                    Headline = $"Order In Queue ({DefaultStaffId})",
                    Detail = $"{ord.OrderId} is currently {ord.OrderStatus}.",
                    AccentHex = "#3B82F6"
                }, insertAtTop: false);
            }
        }
    }

    private void UpdateHubStrip()
    {
        if (_currentHub == null) return;

        TxtHubCapacity.Text = $"Active: {_currentHub.CurrentActiveOrders} / {_currentHub.MaxActiveOrders} | {_currentHub.HubStatus}";
        switch (_currentHub.HubStatus)
        {
            case "Quiet":
                BadgeHubCapacity.Background = CreateBrush("#DCFCE7");
                TxtHubCapacity.Foreground = CreateBrush("#15803D");
                break;
            case "Normal":
                BadgeHubCapacity.Background = CreateBrush("#DBEAFE");
                TxtHubCapacity.Foreground = CreateBrush("#1D4ED8");
                break;
            case "Busy":
                BadgeHubCapacity.Background = CreateBrush("#FEE2E2");
                TxtHubCapacity.Foreground = CreateBrush("#B91C1C");
                break;
            default:
                BadgeHubCapacity.Background = CreateBrush("#E2E8F0");
                TxtHubCapacity.Foreground = CreateBrush("#475569");
                break;
        }
    }

    private void UpdateSelectedOrderPanel()
    {
        if (_selectedOrder == null)
        {
            TxtActiveOrderHeader.Text = "Active Order Control — No Order Selected";
            TxtActiveOrderMeta.Text = "Select an order from the FIFO queue above to manage preparation or reject incoming orders.";
            TxtActiveOrderChecklist.Text = "No items selected";
            TxtRejectHeading.Text = "Reject Unconfirmed Order (Select a 'Placed' order)";

            BtnConfirmOrder.IsEnabled = false;
            BtnStartPrep.IsEnabled = false;
            BtnMarkReady.IsEnabled = false;
            BtnRejectOrder.IsEnabled = false;
            CmbRejectReason.IsEnabled = false;
            TxtRejectDetails.IsEnabled = false;

            BadgeCancelLock.Background = CreateBrush("#F1F5F9");
            TxtCancelLock.Foreground = CreateBrush("#64748B");
            TxtCancelLock.Text = "Customer Cancel: —";
            return;
        }

        var prepTimeText = string.IsNullOrWhiteSpace(_selectedOrder.PrepStartedAt)
            ? "Not started"
            : _selectedOrder.PrepStartedAt;

        TxtActiveOrderHeader.Text = $"Active Order Control — {_selectedOrder.OrderId}";
        TxtActiveOrderMeta.Text = $"Customer: {_selectedOrder.CustomerDisplayText}  |  Payment: {_selectedOrder.PaymentSummaryText}  |  Prep Started: {prepTimeText}";
        TxtActiveOrderChecklist.Text = $"{_selectedOrder.ItemsSummary}   |   Status: {_selectedOrder.OrderStatus}";

        var isPlaced = string.Equals(_selectedOrder.OrderStatus, "Placed", StringComparison.OrdinalIgnoreCase);
        var isConfirmed = string.Equals(_selectedOrder.OrderStatus, "ConfirmedByKitchen", StringComparison.OrdinalIgnoreCase);
        var isPreparing = string.Equals(_selectedOrder.OrderStatus, "InPreparation", StringComparison.OrdinalIgnoreCase);

        BtnConfirmOrder.IsEnabled = isPlaced;
        BtnStartPrep.IsEnabled = isConfirmed;
        BtnMarkReady.IsEnabled = isPreparing;

        BtnRejectOrder.IsEnabled = isPlaced;
        CmbRejectReason.IsEnabled = isPlaced;
        TxtRejectDetails.IsEnabled = isPlaced;

        TxtRejectHeading.Text = isPlaced
            ? $"Reject Unconfirmed Order (Target: {_selectedOrder.OrderId} — 5m confirm window)"
            : $"Reject Unconfirmed Order ({_selectedOrder.OrderId} is already '{_selectedOrder.OrderStatus}')";

        if (_selectedOrder.IsCancellationLocked)
        {
            BadgeCancelLock.Background = CreateBrush("#FFF1F2");
            TxtCancelLock.Foreground = CreateBrush("#BE123C");
            TxtCancelLock.Text = "Customer Cancel: LOCKED (A-03)";
        }
        else
        {
            BadgeCancelLock.Background = CreateBrush("#DBEAFE");
            TxtCancelLock.Foreground = CreateBrush("#1D4ED8");
            TxtCancelLock.Text = "Customer Cancel: UNLOCKED (Awaiting Confirm)";
        }
    }

    private void CmbHub_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingHubs) return;
        if (CmbHub.SelectedItem is not Hub hub) return;

        _currentHub = hub;
        _alerts.Clear();
        RefreshHubWorkspace();
    }

    private void BtnRefreshQueue_Click(object sender, RoutedEventArgs e)
    {
        var currentOrderId = _selectedOrder?.OrderId;
        LoadHubs(_currentHub?.HubId, currentOrderId);
        ShowToast($"Kitchen queue and live menu stock refreshed for {_currentHub?.HubId}.", isError: false);
    }

    private void DgKitchenQueue_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedOrder = DgKitchenQueue.SelectedItem as Order;
        UpdateSelectedOrderPanel();
    }

    private void TxtRejectDetails_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (PhRejectDetails != null)
            PhRejectDetails.Visibility = string.IsNullOrEmpty(TxtRejectDetails.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void BtnConfirmOrder_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOrder == null) return;

        try
        {
            var (updated, alert) = _controller.ConfirmOrder(_selectedOrder.OrderId, DefaultStaffId);
            AddAlert(alert, insertAtTop: true);
            LoadHubs(_currentHub?.HubId, updated.OrderId);
            ShowToast($"Order {updated.OrderId} confirmed -> ConfirmedByKitchen. Customer cancellation is now locked (A-03).", isError: false);
        }
        catch (Exception ex)
        {
            ShowToast($"Confirm Failed: {ex.Message}", isError: true);
        }
    }

    private void BtnStartPrep_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOrder == null) return;

        try
        {
            var (updated, alert) = _controller.StartPreparation(_selectedOrder.OrderId, DefaultStaffId);
            AddAlert(alert, insertAtTop: true);
            LoadHubs(_currentHub?.HubId, updated.OrderId);
            ShowToast($"Preparation started for {updated.OrderId} -> InPreparation (prep_started_at: {updated.PrepStartedAt}).", isError: false);
        }
        catch (Exception ex)
        {
            ShowToast($"Start Preparation Failed: {ex.Message}", isError: true);
        }
    }

    private void BtnMarkReady_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOrder == null) return;

        try
        {
            var (updated, alert) = _controller.MarkOrderReady(_selectedOrder.OrderId, DefaultStaffId);
            AddAlert(alert, insertAtTop: true);
            LoadHubs(_currentHub?.HubId, updated.OrderId);
            ShowToast($"✓ Order {updated.OrderId} marked ReadyForPickup (ready_at: {updated.ReadyAt}) — Shipper pickup alert dispatched.", isError: false);
        }
        catch (Exception ex)
        {
            ShowToast($"Mark Ready Failed: {ex.Message}", isError: true);
        }
    }

    private void BtnRejectOrder_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOrder == null) return;

        var reasonCode = (CmbRejectReason.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
        try
        {
            var (rejected, alert) = _controller.RejectOrder(
                _selectedOrder.OrderId,
                reasonCode,
                TxtRejectDetails.Text,
                DefaultStaffId);

            TxtRejectDetails.Clear();
            AddAlert(alert, insertAtTop: true);
            LoadHubs(_currentHub?.HubId);
            ShowToast($"Order {rejected.OrderId} rejected ({rejected.FailureReason}). Hub active orders decremented.", isError: false);
        }
        catch (Exception ex)
        {
            ShowToast($"Reject Blocked (T-10): {ex.Message}", isError: true);
        }
    }

    private void BtnToggleStock_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HubMenuItem item }) return;

        try
        {
            var (updated, alert) = item.IsAvailableInStock
                ? _controller.MarkHubItemUnavailable(item.HubId, item.ItemId)
                : _controller.RestockHubItem(item.HubId, item.ItemId);

            AddAlert(alert, insertAtTop: true);
            RefreshHubWorkspace(_selectedOrder?.OrderId);

            var statusMsg = updated.IsAvailableInStock
                ? $"{updated.DisplayTitle} restocked -> In Stock at {updated.HubId}."
                : $"{updated.DisplayTitle} marked Out-of-Stock -> Unavailable at {updated.HubId} (US-11).";
            ShowToast(statusMsg, isError: false);
        }
        catch (Exception ex)
        {
            ShowToast($"Stock Toggle Failed: {ex.Message}", isError: true);
        }
    }

    private void BtnDismissToast_Click(object sender, RoutedEventArgs e)
    {
        ToastBorder.Visibility = Visibility.Collapsed;
    }

    private void AddAlert(KitchenAlertEntry entry, bool insertAtTop)
    {
        var display = new AlertDisplayItem(entry.HeaderText, entry.Detail, CreateBrush(entry.AccentHex));
        if (insertAtTop)
            _alerts.Insert(0, display);
        else
            _alerts.Add(display);

        while (_alerts.Count > 12)
            _alerts.RemoveAt(_alerts.Count - 1);
    }

    private void ShowToast(string message, bool isError)
    {
        ToastBorder.Visibility = Visibility.Visible;
        TxtToastMessage.Text = message;
        if (isError)
        {
            ToastBorder.Background = CreateBrush("#FEE2E2");
            ToastBorder.BorderBrush = CreateBrush("#FCA5A5");
            TxtToastMessage.Foreground = CreateBrush("#DC2626");
        }
        else
        {
            ToastBorder.Background = CreateBrush("#DCFCE7");
            ToastBorder.BorderBrush = CreateBrush("#86EFAC");
            TxtToastMessage.Foreground = CreateBrush("#15803D");
        }
    }

    private static Brush CreateBrush(string hex)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    private static string ExtractClockTime(string? timestamp)
    {
        if (string.IsNullOrWhiteSpace(timestamp)) return DateTime.Now.ToString("HH:mm:ss");
        return DateTime.TryParse(timestamp, out var parsed)
            ? parsed.ToString("HH:mm:ss")
            : timestamp;
    }
}
