using System.Windows;
using System.Windows.Controls;
using SaigonBite.OFDH.Controllers;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.Views.UC1;

public partial class AddressModal : Window
{
    private readonly CustomerRegistrationController _controller;
    private readonly string _customerId;
    private readonly DeliveryAddress _editingAddress;

    public AddressModal(
        CustomerRegistrationController controller,
        string customerId,
        DeliveryAddress editingAddress)
    {
        InitializeComponent();
        _controller = controller;
        _customerId = customerId;
        _editingAddress = editingAddress;

        TxtModalTitle.Text = $"Edit Delivery Address ({_editingAddress.AddressId})";
        TxtRecipientName.Text = _editingAddress.RecipientName;
        TxtStreetAddress.Text = _editingAddress.StreetAddress;
        TxtLandmark.Text = _editingAddress.LandmarkOrHotel ?? string.Empty;
        ChkIsDefault.IsChecked = _editingAddress.IsDefault;

        var matched = false;
        foreach (ComboBoxItem item in CmbDistrict.Items)
        {
            if (string.Equals(item.Content?.ToString(), _editingAddress.District, StringComparison.OrdinalIgnoreCase))
            {
                CmbDistrict.SelectedItem = item;
                matched = true;
                break;
            }
        }
        if (!matched && CmbDistrict.Items.Count > 0)
            CmbDistrict.SelectedIndex = 0;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;
        var district = (CmbDistrict.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;

        try
        {
            _controller.UpdateOrArchiveAddress(
                _customerId,
                _editingAddress.AddressId,
                "UPDATE",
                TxtRecipientName.Text,
                TxtStreetAddress.Text,
                district,
                TxtLandmark.Text,
                ChkIsDefault.IsChecked == true);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            TxtError.Text = ex.Message;
            TxtError.Visibility = Visibility.Visible;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
