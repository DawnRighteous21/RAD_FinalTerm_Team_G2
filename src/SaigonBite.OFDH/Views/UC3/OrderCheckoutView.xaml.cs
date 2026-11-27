using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SaigonBite.OFDH.Controllers;
using SaigonBite.OFDH.Domain;
using DomainMenuItem = SaigonBite.OFDH.Domain.MenuItem;

namespace SaigonBite.OFDH.Views.UC3;

public partial class OrderCheckoutView : UserControl
{
    private sealed record CustomerOption(Customer Customer, string Label);

    private sealed class MenuCardItem
    {
        public DomainMenuItem Item { get; }
        public string ItemName => Item.ItemName;
        public string CategoryUpper => Item.Category.ToUpperInvariant();
        public string BilingualDescription => string.IsNullOrWhiteSpace(Item.DescriptionVi)
            ? Item.DescriptionEn ?? string.Empty
            : $"{Item.DescriptionEn} / {Item.DescriptionVi}";
        public string PriceText => $"{Item.UnitPrice.ToString("N0", CultureInfo.InvariantCulture)} VND";
        public string QuantityText { get; set; } = "1";
        public string NoteText { get; set; } = string.Empty;

        public MenuCardItem(DomainMenuItem item) => Item = item;
    }

    private sealed class CartRowItem
    {
        public string OrderItemId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public string SubtotalText { get; init; } = string.Empty;
    }

    private readonly PlaceOrderController _controller = new();
    private readonly Action<string>? _onRoleBadgeChanged;
    private bool _isLoading;

    public OrderCheckoutView(Action<string>? onRoleBadgeChanged = null)
    {
        InitializeComponent();
        _onRoleBadgeChanged = onRoleBadgeChanged;
        LoadInitialSelectors();
    }

    private void LoadInitialSelectors(string? preserveCustomerId = null, string? preserveHubId = null, string? preserveAddressId = null)
    {
        _isLoading = true;
        try
        {
            var customers = _controller.GetActiveCustomers()
                .Select(c => new CustomerOption(c, $"{c.CustomerId} — {c.FullName} ({c.CustomerType})"))
                .ToList();
            CmbCustomer.ItemsSource = customers;
            CmbCustomer.SelectedItem = customers.FirstOrDefault(c => c.Customer.CustomerId == preserveCustomerId)
                ?? customers.FirstOrDefault();

            var hubs = _controller.GetHubs();
            CmbHub.ItemsSource = hubs;
            CmbHub.SelectedItem = hubs.FirstOrDefault(h => h.HubId == preserveHubId)
                ?? hubs.FirstOrDefault(h => h.HubId == "HUB-D3" && h.CanAcceptNewOrder())
                ?? hubs.FirstOrDefault(h => h.CanAcceptNewOrder())
                ?? hubs.FirstOrDefault();

            LoadAddressesForSelectedCustomer(preserveAddressId);
        }
        finally
        {
            _isLoading = false;
        }

        StartCheckoutSession();
    }

    private void LoadAddressesForSelectedCustomer(string? preserveAddressId = null)
    {
        if (CmbCustomer.SelectedItem is not CustomerOption opt)
        {
            CmbAddress.ItemsSource = null;
            return;
        }

        var addresses = _controller.GetCustomerAddresses(opt.Customer.CustomerId);
        CmbAddress.ItemsSource = addresses;
        CmbAddress.SelectedItem = addresses.FirstOrDefault(a => a.AddressId == preserveAddressId)
            ?? addresses.FirstOrDefault(a => a.IsDefault)
            ?? addresses.FirstOrDefault();
    }

    private void CmbCustomer_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        _isLoading = true;
        try
        {
            LoadAddressesForSelectedCustomer();
        }
        finally
        {
            _isLoading = false;
        }

        StartCheckoutSession();
    }

    private void CmbHub_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        StartCheckoutSession();
    }

    private void CmbAddress_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        StartCheckoutSession();
    }

    private void StartCheckoutSession()
    {
        HideError();
        HideVoucherFeedback();
        RedirectNoticeBanner.Visibility = Visibility.Collapsed;

        if (CmbCustomer.SelectedItem is not CustomerOption customerOpt ||
            CmbHub.SelectedItem is not Hub selectedHub ||
            CmbAddress.SelectedItem is not DeliveryAddress selectedAddress)
        {
            MenuItemsControl.ItemsSource = null;
            DgCart.ItemsSource = null;
            return;
        }

        var customer = customerOpt.Customer;
        _onRoleBadgeChanged?.Invoke($"Role: Customer | {customer.CustomerId} ({customer.CustomerType})");
        ConfigurePaymentForCustomer(customer);

        var activeHub = selectedHub;
        if (!activeHub.CanAcceptNewOrder())
        {
            var fallback = _controller.FindNearestAvailableHub(selectedAddress.District, activeHub.HubId);
            if (fallback != null)
            {
                TxtRedirectNotice.Text =
                    $"{activeHub.HubId} is {activeHub.HubStatus} ({activeHub.CurrentActiveOrders}/{activeHub.MaxActiveOrders}) — auto-redirected to {fallback.HubId} ({fallback.HubStatus}, {fallback.CurrentActiveOrders}/{fallback.MaxActiveOrders}).";
                RedirectNoticeBanner.Visibility = Visibility.Visible;

                _isLoading = true;
                try
                {
                    var hubs = CmbHub.ItemsSource as List<Hub>;
                    activeHub = hubs?.FirstOrDefault(h => h.HubId == fallback.HubId) ?? fallback;
                    CmbHub.SelectedItem = activeHub;
                }
                finally
                {
                    _isLoading = false;
                }
            }
        }

        try
        {
            _controller.InitiateCheckout(customer.CustomerId, activeHub.HubId, selectedAddress.AddressId);
            TxtVoucherCode.Text = string.Empty;
            BtnVoucherAction.Content = "Apply";

            var menuItems = _controller.GetAvailableMenuItems(activeHub.HubId)
                .Select(m => new MenuCardItem(m))
                .ToList();
            MenuItemsControl.ItemsSource = menuItems;

            UpdateHubStatusBadge(activeHub, _controller.CurrentOrder!.DeliveryFee);
            RefreshCartAndSummary();
        }
        catch (Exception ex)
        {
            MenuItemsControl.ItemsSource = null;
            DgCart.ItemsSource = null;
            UpdateHubStatusBadge(activeHub, 0m, isBlocked: true);
            ShowError(ex.Message);
        }
    }

    private void ConfigurePaymentForCustomer(Customer customer)
    {
        var isTourist = string.Equals(customer.CustomerType, "Tourist", StringComparison.OrdinalIgnoreCase);
        RbCod.IsEnabled = !isTourist;

        if (isTourist && RbCod.IsChecked == true)
            RbCard.IsChecked = true;

        TxtCodNote.Foreground = isTourist
            ? (Brush)FindResource("DangerRedBrush")
            : (Brush)FindResource("TextMutedBrush");

        UpdatePaymentInputState();
    }

    private void UpdateHubStatusBadge(Hub hub, decimal deliveryFee, bool isBlocked = false)
    {
        var feeText = $"{deliveryFee.ToString("N0", CultureInfo.InvariantCulture)} VND";

        if (isBlocked || !hub.CanAcceptNewOrder())
        {
            HubStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
            HubStatusBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCA5A5"));
            TxtHubStatusBadge.Foreground = (Brush)FindResource("DangerRedBrush");
            TxtHubStatusBadge.Text = $"{hub.HubStatus} ({hub.CurrentActiveOrders}/{hub.MaxActiveOrders}) • Unavailable";
        }
        else if (hub.HubStatus == "Quiet")
        {
            HubStatusBadge.Background = (Brush)FindResource("SuccessGreenBgBrush");
            HubStatusBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#86EFAC"));
            TxtHubStatusBadge.Foreground = (Brush)FindResource("SuccessGreenBrush");
            TxtHubStatusBadge.Text = $"Quiet ({hub.CurrentActiveOrders}/{hub.MaxActiveOrders}) • Fee: {feeText} (-10k)";
        }
        else
        {
            HubStatusBadge.Background = (Brush)FindResource("SuccessGreenBgBrush");
            HubStatusBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#86EFAC"));
            TxtHubStatusBadge.Foreground = (Brush)FindResource("SuccessGreenBrush");
            TxtHubStatusBadge.Text = $"{hub.HubStatus} ({hub.CurrentActiveOrders}/{hub.MaxActiveOrders}) • Fee: {feeText}";
        }
    }

    private void BtnAddMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MenuCardItem card }) return;
        HideError();

        if (!int.TryParse(card.QuantityText.Trim(), out var qty) || qty <= 0)
        {
            ShowError($"Quantity for '{card.ItemName}' must be a positive whole number.");
            return;
        }

        try
        {
            _controller.AddItemToOrder(card.Item.ItemId, qty, card.NoteText);
            RefreshCartAndSummary();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void BtnRemoveCartItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string orderItemId }) return;
        HideError();

        try
        {
            _controller.RemoveItemFromOrder(orderItemId);
            if (_controller.CurrentOrder?.AppliedVoucher == null)
            {
                BtnVoucherAction.Content = "Apply";
                HideVoucherFeedback();
            }
            RefreshCartAndSummary();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void TxtVoucherCode_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TxtVoucherPlaceholder != null)
            TxtVoucherPlaceholder.Visibility = string.IsNullOrEmpty(TxtVoucherCode.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void BtnVoucherAction_Click(object sender, RoutedEventArgs e)
    {
        HideError();
        if (_controller.CurrentOrder == null) return;

        if (BtnVoucherAction.Content?.ToString() == "Remove")
        {
            _controller.RemoveVoucher();
            TxtVoucherCode.Text = string.Empty;
            BtnVoucherAction.Content = "Apply";
            HideVoucherFeedback();
            RefreshCartAndSummary();
            return;
        }

        try
        {
            var updated = _controller.ApplyVoucher(TxtVoucherCode.Text);
            BtnVoucherAction.Content = "Remove";
            TxtVoucherFeedback.Foreground = (Brush)FindResource("SuccessGreenBrush");
            TxtVoucherFeedback.Text = $"✓ Applied: -{updated.DiscountAmount.ToString("N0", CultureInfo.InvariantCulture)} VND (Max 1 voucher per order)";
            TxtVoucherFeedback.Visibility = Visibility.Visible;
            RefreshCartAndSummary();
        }
        catch (Exception ex)
        {
            TxtVoucherFeedback.Foreground = (Brush)FindResource("DangerRedBrush");
            TxtVoucherFeedback.Text = ex.Message;
            TxtVoucherFeedback.Visibility = Visibility.Visible;
        }
    }

    private void PaymentMethod_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdatePaymentInputState();
    }

    private void TxtPaymentDetails_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TxtPaymentPlaceholder != null)
            TxtPaymentPlaceholder.Visibility = string.IsNullOrEmpty(TxtPaymentDetails.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void UpdatePaymentInputState()
    {
        if (TxtPaymentDetails == null || TxtPaymentPlaceholder == null) return;

        var isCod = RbCod.IsChecked == true;
        TxtPaymentDetails.IsEnabled = !isCod;
        if (isCod)
        {
            TxtPaymentDetails.Text = string.Empty;
            TxtPaymentPlaceholder.Text = "Not required for COD payment";
        }
        else
        {
            TxtPaymentPlaceholder.Text = $"Enter {GetSelectedPaymentMethod()} reference (optional in demo)";
        }
        TxtPaymentPlaceholder.Visibility = string.IsNullOrEmpty(TxtPaymentDetails.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private string GetSelectedPaymentMethod()
    {
        if (RbMoMo.IsChecked == true) return "MoMo";
        if (RbVNPay.IsChecked == true) return "VNPay";
        if (RbCard.IsChecked == true) return "Card";
        return "COD";
    }

    private void BtnConfirmOrder_Click(object sender, RoutedEventArgs e)
    {
        HideError();

        try
        {
            var method = GetSelectedPaymentMethod();
            var placedOrder = _controller.ConfirmAndPlaceOrder(method, TxtPaymentDetails.Text);
            var updatedHub = _controller.CurrentHub!;

            var dialog = new OrderConfirmDialog(placedOrder, updatedHub, _controller.LastTransactionRef)
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();

            LoadInitialSelectors(
                placedOrder.CustomerId,
                updatedHub.CanAcceptNewOrder() ? updatedHub.HubId : null,
                placedOrder.AddressId);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void RefreshCartAndSummary()
    {
        var order = _controller.CurrentOrder;
        if (order == null) return;

        DgCart.ItemsSource = order.Items.Select(i => new CartRowItem
        {
            OrderItemId = i.OrderItemId,
            DisplayName = string.IsNullOrWhiteSpace(i.SpecialInstructions)
                ? i.ItemName
                : $"{i.ItemName} ({i.SpecialInstructions})",
            Quantity = i.Quantity,
            SubtotalText = $"{i.LineTotal.ToString("N0", CultureInfo.InvariantCulture)} VND"
        }).ToList();

        TxtSubtotal.Text = $"{order.SubtotalAmount.ToString("N0", CultureInfo.InvariantCulture)} VND";
        TxtDeliveryFee.Text = $"{order.DeliveryFee.ToString("N0", CultureInfo.InvariantCulture)} VND";
        TxtDiscount.Text = order.DiscountAmount > 0
            ? $"-{order.DiscountAmount.ToString("N0", CultureInfo.InvariantCulture)} VND"
            : "0 VND";
        TxtTotalAmount.Text = $"{order.TotalAmount.ToString("N0", CultureInfo.InvariantCulture)} VND";

        if (order.AppliedVoucher != null)
        {
            TxtVoucherFeedback.Foreground = (Brush)FindResource("SuccessGreenBrush");
            TxtVoucherFeedback.Text = $"✓ Applied: -{order.DiscountAmount.ToString("N0", CultureInfo.InvariantCulture)} VND (Max 1 voucher per order)";
            TxtVoucherFeedback.Visibility = Visibility.Visible;
        }
    }

    private void ShowError(string message)
    {
        TxtCheckoutError.Text = message;
        TxtCheckoutError.Visibility = Visibility.Visible;
    }

    private void HideError() => TxtCheckoutError.Visibility = Visibility.Collapsed;

    private void HideVoucherFeedback() => TxtVoucherFeedback.Visibility = Visibility.Collapsed;
}
