using System.Windows;
using SaigonBite.OFDH.Controllers;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.Views.UC14;

public partial class BrandModal : Window
{
    private readonly CatalogAdminController _controller;
    private readonly Brand? _editingBrand;

    public Brand? SavedBrand { get; private set; }

    public BrandModal(CatalogAdminController controller, Brand? editingBrand = null)
    {
        InitializeComponent();
        _controller = controller;
        _editingBrand = editingBrand;

        if (_editingBrand != null)
        {
            TxtModalTitle.Text = $"Edit Virtual Brand ({_editingBrand.BrandId})";
            TxtBrandName.Text = _editingBrand.BrandName;
            TxtCuisineType.Text = _editingBrand.CuisineType;
            TxtDescription.Text = _editingBrand.Description ?? string.Empty;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;
        try
        {
            SavedBrand = _editingBrand == null
                ? _controller.CreateBrand(TxtBrandName.Text, TxtCuisineType.Text, TxtDescription.Text)
                : _controller.UpdateBrand(_editingBrand.BrandId, TxtBrandName.Text, TxtCuisineType.Text, TxtDescription.Text);

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
