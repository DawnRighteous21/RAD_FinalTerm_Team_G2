using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SaigonBite.OFDH.Controllers;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.Views.UC10;

public partial class ShipperDeliveryView : UserControl
{
    private readonly DeliveryFulfilmentController _controller = new();
    private Shipper? _currentShipper;
    private Order? _selectedOrder;
    private bool _isLoadingShippers;

    public ShipperDeliveryView()
    {
        InitializeComponent();
        LoadShippers("SHP-01");
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
            DgOrders.UnselectAll();
            Keyboard.ClearFocus();
        }
    }

    private void LoadShippers(string? preferredShipperId = null, string? preferredOrderId = null)
    {
        _isLoadingShippers = true;
        try
        {
            var targetId = preferredShipperId ?? _currentShipper?.ShipperId ?? "SHP-01";
            var shippers = _controller.GetAllShippers();

            CmbShipper.ItemsSource = shippers;
            if (shippers.Count == 0)
            {
                _currentShipper = null;
                DgOrders.ItemsSource = null;
                UpdateShipperKpiStrip();
                UpdateSelectedOrderPanels();
                ShowToast("No shippers found in SQLite table 'Shippers'.", isError: true);
                return;
            }

            var selected = shippers.FirstOrDefault(s =>
                string.Equals(s.ShipperId, targetId, StringComparison.OrdinalIgnoreCase)) ?? shippers[0];

            CmbShipper.SelectedItem = selected;
            _currentShipper = selected;
        }
        finally
        {
            _isLoadingShippers = false;
        }

        UpdateShipperKpiStrip();
        LoadOrderQueue(preferredOrderId);
    }

    private void LoadOrderQueue(string? preferredOrderId = null)
    {
        if (_currentShipper == null)
        {
            DgOrders.ItemsSource = null;
            _selectedOrder = null;
            UpdateSelectedOrderPanels();
            return;
        }

        var orders = _controller.GetTasksForShipper(_currentShipper.ShipperId);
        if (orders.Count == 0)
        {
            ShowToast(
                $"No 'ReadyForPickup' or 'PickedUp' orders found for {_currentShipper.ShipperId}. Check 02_seed_data.sql or switch shipper.",
                isError: true);
        }
        DgOrders.ItemsSource = orders;

        Order? match = null;
        if (!string.IsNullOrWhiteSpace(preferredOrderId))
        {
            match = orders.FirstOrDefault(o =>
                string.Equals(o.OrderId, preferredOrderId, StringComparison.OrdinalIgnoreCase));
        }

        match ??= orders.FirstOrDefault(o =>
                      string.Equals(o.OrderStatus, "PickedUp", StringComparison.OrdinalIgnoreCase))
                  ?? orders.FirstOrDefault();

        DgOrders.SelectedItem = match;
        _selectedOrder = match;
        UpdateSelectedOrderPanels();
    }

    private void CmbShipper_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingShippers) return;
        if (CmbShipper.SelectedItem is not Shipper shipper) return;

        _currentShipper = _controller.GetShipperById(shipper.ShipperId) ?? shipper;
        UpdateShipperKpiStrip();
        LoadOrderQueue();
    }

    private void BtnRefreshQueue_Click(object sender, RoutedEventArgs e)
    {
        var currentOrderId = _selectedOrder?.OrderId;
        LoadShippers(_currentShipper?.ShipperId, currentOrderId);
        ShowToast("Delivery queue and shipper KPI ledger refreshed.", isError: false);
    }

    private void DgOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedOrder = DgOrders.SelectedItem as Order;
        TxtCashCollected.Clear();
        UpdateSelectedOrderPanels();
    }

    private void GuardInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TxtGuardStatus == null || BtnReportFailure == null) return;
        UpdateGuardRuleState();
    }

    private void BtnAcceptTask_Click(object sender, RoutedEventArgs e)
    {
        if (_currentShipper == null || _selectedOrder == null) return;

        try
        {
            var orderId = _selectedOrder.OrderId;
            _controller.AcceptDeliveryTask(_currentShipper.ShipperId, orderId);
            LoadShippers(_currentShipper.ShipperId, orderId);

            ShowToast(
                $"Task Accepted: {orderId} assigned to {_currentShipper.ShipperId} (Status: AssignedToOrder). Proceed to Hub to confirm pickup.",
                isError: false);
        }
        catch (Exception ex)
        {
            ShowToast($"Accept Task Failed: {ex.Message}", isError: true);
        }
    }

    private void BtnConfirmPickup_Click(object sender, RoutedEventArgs e)
    {
        if (_currentShipper == null || _selectedOrder == null) return;

        try
        {
            var orderId = _selectedOrder.OrderId;
            _controller.ConfirmOrderPickup(_currentShipper.ShipperId, orderId);
            LoadShippers(_currentShipper.ShipperId, orderId);

            ShowToast(
                $"Pickup Confirmed: {orderId} -> PickedUp | Shipper {_currentShipper.ShipperId} -> OnDelivery.",
                isError: false);
        }
        catch (Exception ex)
        {
            ShowToast($"Confirm Pickup Failed: {ex.Message}", isError: true);
        }
    }

    private void BtnConfirmCash_Click(object sender, RoutedEventArgs e)
    {
        if (_currentShipper == null || _selectedOrder == null) return;

        var rawAmount = TxtCashCollected.Text.Replace(",", "").Replace(" ", "").Trim();
        if (!decimal.TryParse(rawAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var cashAmount))
        {
            ShowToast("Please enter a valid numeric COD cash amount in VND.", isError: true);
            return;
        }

        try
        {
            var orderId = _selectedOrder.OrderId;
            var updatedCash = _controller.ConfirmCashCollected(_currentShipper.ShipperId, orderId, cashAmount);
            TxtCashCollected.Clear();
            LoadShippers(_currentShipper.ShipperId, orderId);

            ShowToast(
                $"COD Confirmed ({orderId}): {cashAmount:N0} VND logged as PaidCOD | Shipper Cash on Hand: {updatedCash:N0} VND.",
                isError: false);
        }
        catch (Exception ex)
        {
            ShowToast($"COD Settlement Blocked: {ex.Message}", isError: true);
        }
    }

    private void BtnConfirmDelivery_Click(object sender, RoutedEventArgs e)
    {
        if (_currentShipper == null || _selectedOrder == null) return;

        try
        {
            var orderId = _selectedOrder.OrderId;
            var hubId = _selectedOrder.HubId;
            var completed = _controller.ConfirmDeliverySuccess(_currentShipper.ShipperId, orderId);

            TxtCashCollected.Clear();
            TxtContactAttempts.Text = "0";
            TxtWaitMinutes.Text = "0";
            LoadShippers(_currentShipper.ShipperId);

            ShowToast(
                $"Delivery Completed: {orderId} -> Delivered ({completed.DeliveredAt}) | Shipper -> Available | {hubId} active orders -1.",
                isError: false);
        }
        catch (Exception ex)
        {
            ShowToast($"Delivery Completion Blocked: {ex.Message}", isError: true);
        }
    }

    private void BtnReportFailure_Click(object sender, RoutedEventArgs e)
    {
        if (_currentShipper == null || _selectedOrder == null) return;

        if (!int.TryParse(TxtContactAttempts.Text.Trim(), out var attempts)) attempts = 0;
        if (!int.TryParse(TxtWaitMinutes.Text.Trim(), out var waitMins)) waitMins = 0;
        var reason = (CmbFailureReason.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "CustomerNoAnswer";

        try
        {
            var orderId = _selectedOrder.OrderId;
            var hubId = _selectedOrder.HubId;
            _controller.ReportDeliveryFailure(_currentShipper.ShipperId, orderId, reason, attempts, waitMins);

            TxtCashCollected.Clear();
            TxtContactAttempts.Text = "0";
            TxtWaitMinutes.Text = "0";
            LoadShippers(_currentShipper.ShipperId);

            ShowToast(
                $"Delivery Failure Logged: {orderId} -> DeliveryFailed ({reason}) | Shipper -> Available | {hubId} active orders -1.",
                isError: true);
        }
        catch (Exception ex)
        {
            ShowToast($"Failure Report Blocked: {ex.Message}", isError: true);
        }
    }

    private void UpdateShipperKpiStrip()
    {
        if (_currentShipper == null)
        {
            TxtShipperStatus.Text = "Offline";
            TxtCashOnHand.Text = "0";
            return;
        }

        TxtShipperStatus.Text = _currentShipper.AvailabilityStatus;
        TxtCashOnHand.Text = _currentShipper.CashOnHand.ToString("N0", CultureInfo.InvariantCulture);

        switch (_currentShipper.AvailabilityStatus)
        {
            case "Available":
                BadgeShipperStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"));
                TxtShipperStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
                break;
            case "AssignedToOrder":
                BadgeShipperStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF7ED"));
                TxtShipperStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100"));
                break;
            case "OnDelivery":
                BadgeShipperStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DBEAFE"));
                TxtShipperStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
                break;
            default:
                BadgeShipperStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
                TxtShipperStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"));
                break;
        }
    }

    private void UpdateSelectedOrderPanels()
    {
        if (_selectedOrder == null || _currentShipper == null)
        {
            TxtDetailPickupHub.Text = "Pickup Hub: —";
            TxtDetailDeliverTo.Text = "Deliver to: Select an order from the queue above.";
            TxtDetailPayment.Text = "Payment & Total: —";

            TxtActiveOrderId.Text = "Order: None selected";
            TxtActiveOrderStatus.Text = "—";
            TxtActiveDestination.Text = "Destination: —";
            TxtPaymentRule.Text = "Select a picked-up order to view payment settlement instructions.";
            SetPaymentBannerStyle("#F1F5F9", "#CBD5E1", "#64748B");

            BtnAcceptTask.IsEnabled = false;
            BtnConfirmPickup.IsEnabled = false;
            TxtCashCollected.IsEnabled = false;
            BtnConfirmCash.IsEnabled = false;
            BtnConfirmDelivery.IsEnabled = false;
            UpdateGuardRuleState();
            return;
        }

        var hub = _controller.GetHubById(_selectedOrder.HubId);
        var hubText = hub != null
            ? $"{hub.HubId} — {hub.HubName} ({hub.District}) | Active: {hub.CurrentActiveOrders}/{hub.MaxActiveOrders}"
            : _selectedOrder.HubId;

        var addr = _selectedOrder.DeliveryAddress;
        var destText = addr != null
            ? $"{addr.RecipientName} — {addr.StreetAddress}, {addr.District}" +
              (string.IsNullOrWhiteSpace(addr.LandmarkOrHotel) ? string.Empty : $" ({addr.LandmarkOrHotel})")
            : $"Address ID: {_selectedOrder.AddressId}";

        TxtDetailPickupHub.Text = $"Pickup Hub: {hubText}";
        TxtDetailDeliverTo.Text = $"Deliver to: {destText}";
        TxtDetailPayment.Text = $"Payment & Total: {_selectedOrder.PaymentMethod} ({_selectedOrder.PaymentStatus}) — {_selectedOrder.TotalAmount:N0} VND";

        TxtActiveOrderId.Text = $"Order: {_selectedOrder.OrderId} ({_selectedOrder.HubId})";
        TxtActiveOrderStatus.Text = _selectedOrder.OrderStatus;
        TxtActiveDestination.Text = $"Destination: {destText}";

        var isReady = string.Equals(_selectedOrder.OrderStatus, "ReadyForPickup", StringComparison.OrdinalIgnoreCase);
        var isPickedUp = string.Equals(_selectedOrder.OrderStatus, "PickedUp", StringComparison.OrdinalIgnoreCase);
        var isShipperAvailable = string.Equals(_currentShipper.AvailabilityStatus, "Available", StringComparison.OrdinalIgnoreCase);
        var isShipperAssigned = string.Equals(_currentShipper.AvailabilityStatus, "AssignedToOrder", StringComparison.OrdinalIgnoreCase);
        var isAssignedToMe = string.Equals(_selectedOrder.ShipperId, _currentShipper.ShipperId, StringComparison.OrdinalIgnoreCase);
        var isUnassigned = string.IsNullOrWhiteSpace(_selectedOrder.ShipperId);

        BtnAcceptTask.IsEnabled = isReady && isShipperAvailable && (isUnassigned || isAssignedToMe);
        BtnConfirmPickup.IsEnabled = isReady && (isAssignedToMe || (isUnassigned && isShipperAvailable));

        var isCodPaid = string.Equals(_selectedOrder.PaymentStatus, "PaidCOD", StringComparison.OrdinalIgnoreCase);
        if (_selectedOrder.IsCodOrder)
        {
            if (isCodPaid)
            {
                TxtPaymentRule.Text = $"COD Paid — {_selectedOrder.TotalAmount:N0} VND collected & logged (PaidCOD). Ready to complete handover.";
                SetPaymentBannerStyle("#DCFCE7", "#86EFAC", "#16A34A");
            }
            else
            {
                TxtPaymentRule.Text = $"COD — Exact Cash Required: {_selectedOrder.TotalAmount:N0} VND";
                SetPaymentBannerStyle("#FFF7ED", "#FDBA74", "#E65100");
            }
        }
        else
        {
            TxtPaymentRule.Text = $"Prepaid Online ({_selectedOrder.PaymentMethod}) — No Cash Collection";
            SetPaymentBannerStyle("#DBEAFE", "#93C5FD", "#2563EB");
        }

        var canCollectCod = isPickedUp && isAssignedToMe && _selectedOrder.IsCodOrder && !isCodPaid;
        TxtCashCollected.IsEnabled = canCollectCod;
        BtnConfirmCash.IsEnabled = canCollectCod;

        BtnConfirmDelivery.IsEnabled = isPickedUp && isAssignedToMe && (!_selectedOrder.IsCodOrder || isCodPaid);

        UpdateGuardRuleState();
    }

    private void UpdateGuardRuleState()
    {
        var hasAttempts = int.TryParse(TxtContactAttempts.Text.Trim(), out var attempts);
        var hasWait = int.TryParse(TxtWaitMinutes.Text.Trim(), out var waitMins);
        var guardMet = hasAttempts && hasWait
            && attempts >= DeliveryFulfilmentController.MinContactAttemptsForFailure
            && waitMins >= DeliveryFulfilmentController.MinWaitMinutesForFailure;

        var isPickedUpByMe = _selectedOrder != null
            && _currentShipper != null
            && string.Equals(_selectedOrder.OrderStatus, "PickedUp", StringComparison.OrdinalIgnoreCase)
            && string.Equals(_selectedOrder.ShipperId, _currentShipper.ShipperId, StringComparison.OrdinalIgnoreCase);

        if (guardMet)
        {
            TxtGuardStatus.Text = isPickedUpByMe
                ? "Guard rules satisfied — Failure reporting unlocked"
                : "Guard rules satisfied — Select a PickedUp order to report failure";
            GuardStatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"));
            GuardStatusBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#86EFAC"));
            TxtGuardStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
        }
        else
        {
            TxtGuardStatus.Text = "Locked: Requires >= 3 contact attempts AND >= 10 mins wait";
            GuardStatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
            GuardStatusBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FECACA"));
            TxtGuardStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
        }

        BtnReportFailure.IsEnabled = guardMet && isPickedUpByMe;
    }

    private void SetPaymentBannerStyle(string bgHex, string borderHex, string fgHex)
    {
        PaymentRuleBanner.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex));
        PaymentRuleBanner.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderHex));
        TxtPaymentRule.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fgHex));
    }

    private void ShowToast(string message, bool isError)
    {
        TxtToastMessage.Text = message;
        if (isError)
        {
            ToastBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
            ToastBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCA5A5"));
            TxtToastMessage.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
        }
        else
        {
            ToastBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"));
            ToastBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#86EFAC"));
            TxtToastMessage.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
        }
    }
}
