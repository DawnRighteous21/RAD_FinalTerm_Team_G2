using System.Windows;
using System.Windows.Controls;
using SaigonBite.OFDH.Controllers;
using SaigonBite.OFDH.Domain;
using System.Windows.Input;
using System.Windows.Media;
using DomainMenuItem = SaigonBite.OFDH.Domain.MenuItem;

namespace SaigonBite.OFDH.Views.UC14;

public partial class BrandMenuAdminView : UserControl
{
    private record BrandFilterOption(string BrandId, string Label);

    private readonly CatalogAdminController _controller = new();
    private List<Brand> _allBrands = new();
    private List<DomainMenuItem> _allItems = new();
    private List<Hub> _allHubs = new();

    public BrandMenuAdminView()
    {
        InitializeComponent();
        LoadCatalogData();
    }

    private void LoadCatalogData(string? statusMessage = null)
    {
        var previousFilterId = (CmbBrandFilter.SelectedItem as BrandFilterOption)?.BrandId ?? "ALL";

        var (brands, items) = _controller.LoadCatalog();
        _allBrands = brands;
        _allItems = items;
        _allHubs = _controller.GetAllHubs();

        var filterOptions = new List<BrandFilterOption> { new("ALL", "All Brands") };
        filterOptions.AddRange(_allBrands.Select(b => new BrandFilterOption(b.BrandId, b.DisplayLabel)));

        CmbBrandFilter.ItemsSource = filterOptions;
        CmbBrandFilter.SelectedItem = filterOptions.FirstOrDefault(o => o.BrandId == previousFilterId) ?? filterOptions[0];

        ApplyFilters();

        if (!string.IsNullOrWhiteSpace(statusMessage))
            TxtStatusMessage.Text = statusMessage;
    }

    private void Filter_Changed(object sender, EventArgs e)
    {
        if (TxtSearchPlaceholder != null)
            TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (DgBrands == null || DgMenuItems == null) return;
        ApplyFilters();
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

        ClearSelectionAndFocus();
    }

    private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            ClearSelectionAndFocus();
    }

    private void ClearSelectionAndFocus()
    {
        DgBrands.UnselectAll();
        DgMenuItems.UnselectAll();
        Keyboard.ClearFocus();
    }

    private void ApplyFilters()
    {
        var keyword = TxtSearch.Text.Trim();
        var selectedBrandId = (CmbBrandFilter.SelectedItem as BrandFilterOption)?.BrandId ?? "ALL";

        var filteredBrands = _allBrands.Where(b =>
            (selectedBrandId == "ALL" || b.BrandId == selectedBrandId) &&
            (string.IsNullOrEmpty(keyword) ||
             b.BrandName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
             b.CuisineType.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
             b.BrandId.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        var filteredItems = _allItems.Where(i =>
            (selectedBrandId == "ALL" || i.BrandId == selectedBrandId) &&
            (string.IsNullOrEmpty(keyword) ||
             i.ItemName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
             (i.DescriptionEn?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
             (i.DescriptionVi?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
             i.ItemId.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        DgBrands.ItemsSource = filteredBrands;
        DgMenuItems.ItemsSource = filteredItems;
    }

    private void BtnNewBrand_Click(object sender, RoutedEventArgs e)
    {
        var modal = new BrandModal(_controller) { Owner = Window.GetWindow(this) };
        if (modal.ShowDialog() == true && modal.SavedBrand != null)
            LoadCatalogData($"Virtual Brand Created: {modal.SavedBrand.BrandId} — {modal.SavedBrand.BrandName}");
    }

    private void BtnEditBrand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Brand brand }) return;
        var modal = new BrandModal(_controller, brand) { Owner = Window.GetWindow(this) };
        if (modal.ShowDialog() == true && modal.SavedBrand != null)
            LoadCatalogData($"Virtual Brand Updated: {modal.SavedBrand.BrandId}");
    }

    private void BtnToggleBrandStatus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Brand brand }) return;
        try
        {
            if (brand.IsActive)
            {
                _controller.ArchiveCatalogEntity(brand.BrandId, "Brand");
                LoadCatalogData($"Brand Archived (Hidden from Customer Menu): {brand.BrandId}");
            }
            else
            {
                _controller.ReactivateCatalogEntity(brand.BrandId, "Brand");
                LoadCatalogData($"Brand Reactivated: {brand.BrandId}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Brand Action Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnNewMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var modal = new MenuItemModal(_controller, _allBrands, _allHubs) { Owner = Window.GetWindow(this) };
        if (modal.ShowDialog() == true && modal.SavedItem != null)
            LoadCatalogData($"Menu Item Created: {modal.SavedItem.ItemId} — {modal.SavedItem.ItemName}");
    }

    private void BtnEditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DomainMenuItem item }) return;
        var modal = new MenuItemModal(_controller, _allBrands, _allHubs, item) { Owner = Window.GetWindow(this) };
        if (modal.ShowDialog() == true && modal.SavedItem != null)
            LoadCatalogData($"Menu Item Updated: {modal.SavedItem.ItemId}");
    }

    private void BtnToggleItemStatus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DomainMenuItem item }) return;
        try
        {
            if (item.IsActive)
            {
                _controller.ArchiveCatalogEntity(item.ItemId, "MenuItem");
                LoadCatalogData($"Item Archived (Hidden from Customer Menu): {item.ItemId}");
            }
            else
            {
                _controller.ReactivateCatalogEntity(item.ItemId, "MenuItem");
                LoadCatalogData($"Item Reactivated: {item.ItemId}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Menu Item Action Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
