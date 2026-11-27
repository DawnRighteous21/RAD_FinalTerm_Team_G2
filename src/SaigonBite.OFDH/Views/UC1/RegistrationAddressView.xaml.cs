// Author: 524H0134 (Ha Du Tuan)
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SaigonBite.OFDH.Controllers;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.Views.UC1;

public partial class RegistrationAddressView : UserControl
{
    private readonly CustomerRegistrationController _controller = new();
    private string _selectedAccountType = "Local";

    public RegistrationAddressView()
    {
        InitializeComponent();
        ApplyAccountTypeMode("Local");
        CmbDistrict.SelectedIndex = 0;
        LoadCustomersAndAddresses();
    }

    private void Root_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var current = e.OriginalSource as DependencyObject;
        while (current != null)
        {
            if (current is DataGridRow or TextBox or ComboBox or ComboBoxItem or Button or CheckBox)
                return;

            var parent = current is Visual
                ? VisualTreeHelper.GetParent(current)
                : null;

            current = parent ?? LogicalTreeHelper.GetParent(current);
        }

        ClearSelectionAndFocus();
    }

    private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            ClearSelectionAndFocus();
    }

    private void ClearSelectionAndFocus()
    {
        DgAddresses.UnselectAll();
        Keyboard.ClearFocus();
    }

    private void BtnTabLocal_Click(object sender, RoutedEventArgs e)
    {
        ApplyAccountTypeMode("Local");
    }

    private void BtnTabTourist_Click(object sender, RoutedEventArgs e)
    {
        ApplyAccountTypeMode("Tourist");
    }

    private void ApplyAccountTypeMode(string accountType)
    {
        _selectedAccountType = accountType;
        _controller.SelectAccountType(accountType);

        var isLocal = string.Equals(accountType, "Local", StringComparison.OrdinalIgnoreCase);

        var activeBg = (Brush)FindResource("PrimaryOrangeBrush");
        var inactiveBg = Brushes.Transparent;
        var inactiveFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"));

        BtnTabLocal.Background = isLocal ? activeBg : inactiveBg;
        BtnTabLocal.Foreground = isLocal ? Brushes.White : inactiveFg;

        BtnTabTourist.Background = isLocal ? inactiveBg : activeBg;
        BtnTabTourist.Foreground = isLocal ? inactiveFg : Brushes.White;

        // Local fields
        TxtVnPhone.IsEnabled = isLocal;
        CmbWallet.IsEnabled = isLocal;
        PhVnPhone.Visibility = isLocal ? Visibility.Collapsed : Visibility.Visible;
        OverlayWalletDisabled.Visibility = isLocal ? Visibility.Collapsed : Visibility.Visible;
        if (!isLocal) TxtVnPhone.Clear();

        // Tourist fields
        TxtPassport.IsEnabled = !isLocal;
        TxtNationality.IsEnabled = !isLocal;
        TxtHotelName.IsEnabled = !isLocal;
        TxtRoomNumber.IsEnabled = !isLocal;

        var touristPlaceholderVisibility = isLocal ? Visibility.Visible : Visibility.Collapsed;
        PhPassport.Visibility = touristPlaceholderVisibility;
        PhNationality.Visibility = touristPlaceholderVisibility;
        PhHotelName.Visibility = touristPlaceholderVisibility;
        PhRoomNumber.Visibility = touristPlaceholderVisibility;

        if (isLocal)
        {
            TxtPassport.Clear();
            TxtNationality.Clear();
            TxtHotelName.Clear();
            TxtRoomNumber.Clear();
        }
    }

    private void LoadCustomersAndAddresses(string? selectCustomerId = null)
    {
        var currentSelectedId = selectCustomerId ?? (CmbActiveCustomer.SelectedItem as Customer)?.CustomerId;
        var customers = _controller.GetAllCustomers();

        CmbActiveCustomer.ItemsSource = customers;
        if (customers.Count > 0)
        {
            var match = customers.FirstOrDefault(c => c.CustomerId == currentSelectedId) ?? customers[0];
            CmbActiveCustomer.SelectedItem = match;
            LoadAddressesForCustomer(match);
        }
        else
        {
            DgAddresses.ItemsSource = null;
        }
    }

    private void LoadAddressesForCustomer(Customer? customer)
    {
        if (customer == null)
        {
            DgAddresses.ItemsSource = null;
            return;
        }

        DgAddresses.ItemsSource = _controller.GetAddressesByCustomer(customer.CustomerId);
        if (string.IsNullOrWhiteSpace(TxtRecipientName.Text))
            TxtRecipientName.Text = customer.FullName;
    }

    private void CmbActiveCustomer_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbActiveCustomer.SelectedItem is Customer customer)
        {
            TxtRecipientName.Text = customer.FullName;
            if (customer is ForeignTourist tourist && !string.IsNullOrWhiteSpace(tourist.HotelName))
                TxtLandmark.Text = string.IsNullOrWhiteSpace(tourist.RoomNumber)
                    ? tourist.HotelName
                    : $"{tourist.HotelName} — Room {tourist.RoomNumber}";
            else
                TxtLandmark.Clear();

            LoadAddressesForCustomer(customer);
        }
    }

    private void CmbDistrict_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TxtHubDistanceHint == null) return;
        var district = (CmbDistrict.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.IsNullOrWhiteSpace(district)) return;

        var hub = _controller.GetNearestHubForDistrict(district);
        TxtHubDistanceHint.Text = hub != null
            ? $"Nearest Hub: {hub.HubId} ({hub.District}) — Distance Check: <= {hub.MaxDeliveryDistanceKm:0.0} km"
            : "Nearest Hub & Distance Check: <= 7.0 km";
    }

    private void BtnRegisterAccount_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var wallet = (CmbWallet.SelectedItem as ComboBoxItem)?.Content?.ToString();
            var created = _controller.RegisterCustomer(
                _selectedAccountType,
                TxtFullName.Text,
                TxtEmail.Text,
                TxtVnPhone.Text,
                wallet,
                TxtPassport.Text,
                TxtNationality.Text,
                TxtHotelName.Text,
                TxtRoomNumber.Text);

            ClearRegistrationInputs();
            LoadCustomersAndAddresses(created.CustomerId);

            ShowToast(
                $"Account Active: {created.CustomerId} — {created.FullName} ({created.CustomerType}) registered successfully.",
                isError: false);
        }
        catch (Exception ex)
        {
            ShowToast($"Registration Error: {ex.Message}", isError: true);
        }
    }

    private void BtnSaveAddress_Click(object sender, RoutedEventArgs e)
    {
        if (CmbActiveCustomer.SelectedItem is not Customer customer)
        {
            ShowToast("Please select an Active Customer first.", isError: true);
            return;
        }

        var district = (CmbDistrict.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;

        try
        {
            var saved = _controller.AddDeliveryAddress(
                customer.CustomerId,
                TxtRecipientName.Text,
                TxtStreetAddress.Text,
                district,
                TxtLandmark.Text,
                ChkIsDefault.IsChecked == true);

            TxtStreetAddress.Clear();
            LoadAddressesForCustomer(customer);

            if (saved.Status == "Verified")
            {
                ShowToast(
                    $"Address Saved ({saved.AddressId}): {saved.DistanceKmFromHub:0.0} km from nearest Hub — Status: Verified.",
                    isError: false);
            }
            else
            {
                ShowToast(
                    $"Out-of-Range Warning ({saved.AddressId}): {saved.DistanceKmFromHub:0.0} km exceeds 7.0 km max radius — Saved as OutOfRange.",
                    isError: true);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Address Save Failed: {ex.Message}", isError: true);
        }
    }

    private void BtnEditAddress_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DeliveryAddress address }) return;
        if (CmbActiveCustomer.SelectedItem is not Customer customer) return;

        var modal = new AddressModal(_controller, customer.CustomerId, address)
        {
            Owner = Window.GetWindow(this)
        };

        if (modal.ShowDialog() == true)
        {
            LoadAddressesForCustomer(customer);
            ShowToast($"Address {address.AddressId} updated and distance re-verified.", isError: false);
        }
    }

    private void BtnArchiveAddress_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DeliveryAddress address }) return;
        if (CmbActiveCustomer.SelectedItem is not Customer customer) return;

        try
        {
            _controller.UpdateOrArchiveAddress(customer.CustomerId, address.AddressId, "ARCHIVE");
            LoadAddressesForCustomer(customer);
            ShowToast($"Address {address.AddressId} archived (soft-deleted).", isError: false);
        }
        catch (Exception ex)
        {
            ShowToast($"Archive Blocked: {ex.Message}", isError: true);
        }
    }

    private void ClearRegistrationInputs()
    {
        TxtFullName.Clear();
        TxtEmail.Clear();
        TxtVnPhone.Clear();
        TxtPassport.Clear();
        TxtNationality.Clear();
        TxtHotelName.Clear();
        TxtRoomNumber.Clear();
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