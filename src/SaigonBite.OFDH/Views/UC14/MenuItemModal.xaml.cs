using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SaigonBite.OFDH.Controllers;
using SaigonBite.OFDH.Domain;
using DomainMenuItem = SaigonBite.OFDH.Domain.MenuItem;

namespace SaigonBite.OFDH.Views.UC14;

public partial class MenuItemModal : Window
{
    private readonly CatalogAdminController _controller;
    private readonly DomainMenuItem? _editingItem;
    private readonly List<CheckBox> _hubCheckBoxes = new();

    public DomainMenuItem? SavedItem { get; private set; }

    public MenuItemModal(
        CatalogAdminController controller,
        List<Brand> brands,
        List<Hub> hubs,
        DomainMenuItem? editingItem = null)
    {
        InitializeComponent();
        _controller = controller;
        _editingItem = editingItem;

        var selectableBrands = editingItem == null
            ? brands.Where(b => b.IsActive).ToList()
            : brands;

        CmbBrand.ItemsSource = selectableBrands;

        var assignedHubs = editingItem != null
            ? new HashSet<string>(_controller.GetAssignedHubIds(editingItem.ItemId), StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(hubs.Select(h => h.HubId), StringComparer.OrdinalIgnoreCase);

        foreach (var hub in hubs)
        {
            var cb = new CheckBox
            {
                Content = $"{hub.HubId} ({hub.District})",
                Tag = hub.HubId,
                IsChecked = assignedHubs.Contains(hub.HubId),
                Margin = new Thickness(0, 0, 18, 4),
                FontSize = 12
            };
            _hubCheckBoxes.Add(cb);
            HubCheckBoxPanel.Children.Add(cb);
        }

        if (_editingItem != null)
        {
            TxtModalTitle.Text = $"Edit Bilingual Menu Item ({_editingItem.ItemId})";
            CmbBrand.SelectedItem = selectableBrands.FirstOrDefault(b => b.BrandId == _editingItem.BrandId);
            TxtItemName.Text = _editingItem.ItemName;
            TxtUnitPrice.Text = _editingItem.UnitPrice.ToString("0", CultureInfo.InvariantCulture);
            TxtDescEn.Text = _editingItem.DescriptionEn ?? string.Empty;
            TxtDescVi.Text = _editingItem.DescriptionVi ?? string.Empty;

            foreach (ComboBoxItem item in CmbCategory.Items)
            {
                if (string.Equals(item.Content?.ToString(), _editingItem.Category, StringComparison.OrdinalIgnoreCase))
                {
                    CmbCategory.SelectedItem = item;
                    break;
                }
            }
        }
        else if (selectableBrands.Count > 0)
        {
            CmbBrand.SelectedIndex = 0;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (CmbBrand.SelectedItem is not Brand selectedBrand)
        {
            ShowError("Please select a Virtual Brand.");
            return;
        }

        var rawPrice = TxtUnitPrice.Text.Replace(",", "").Replace(" ", "").Trim();
        if (!decimal.TryParse(rawPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
        {
            ShowError("Unit price must be a valid number in VND.");
            return;
        }

        var category = (CmbCategory.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Main";
        var selectedHubIds = _hubCheckBoxes
            .Where(cb => cb.IsChecked == true)
            .Select(cb => cb.Tag?.ToString() ?? string.Empty)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        try
        {
            SavedItem = _editingItem == null
                ? _controller.CreateMenuItem(selectedBrand.BrandId, TxtItemName.Text, TxtDescEn.Text, TxtDescVi.Text, price, category, selectedHubIds)
                : _controller.UpdateMenuItem(_editingItem.ItemId, selectedBrand.BrandId, TxtItemName.Text, TxtDescEn.Text, TxtDescVi.Text, price, category, selectedHubIds);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        TxtError.Text = message;
        TxtError.Visibility = Visibility.Visible;
    }
}
