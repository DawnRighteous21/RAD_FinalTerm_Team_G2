using SaigonBite.OFDH.DataAccess;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.Controllers;

public class CatalogAdminController
{
    private readonly BrandDAO _brandDao;
    private readonly MenuItemDAO _menuItemDao;
    private readonly HubMenuItemDAO _hubMenuItemDao;
    private readonly HubDAO _hubDao;

    public CatalogAdminController(DatabaseConnection? db = null)
    {
        var conn = db ?? new DatabaseConnection();
        _brandDao = new BrandDAO(conn);
        _menuItemDao = new MenuItemDAO(conn);
        _hubMenuItemDao = new HubMenuItemDAO(conn);
        _hubDao = new HubDAO(conn);
    }

    public (List<Brand> Brands, List<MenuItem> Items) LoadCatalog()
    {
        var brands = _brandDao.FindAllBrands();
        var items = _menuItemDao.FindAllMenuItems();
        var hubMap = _hubMenuItemDao.FindAllAssignedHubsByItem();

        foreach (var item in items)
        {
            item.AssignedHubsText = hubMap.TryGetValue(item.ItemId, out var hubs)
                ? string.Join(", ", hubs)
                : "-";
        }

        return (brands, items);
    }

    public List<Hub> GetAllHubs() => _hubDao.FindAll();

    public List<string> GetAssignedHubIds(string itemId) => _hubMenuItemDao.FindHubIdsByItemId(itemId);

    public Brand CreateBrand(string brandName, string cuisineType, string? description)
    {
        if (string.IsNullOrWhiteSpace(brandName))
            throw new ArgumentException("Brand name is required.");
        if (string.IsNullOrWhiteSpace(cuisineType))
            throw new ArgumentException("Cuisine type is required.");
        if (_brandDao.ExistsByName(brandName))
            throw new InvalidOperationException($"Brand '{brandName.Trim()}' already exists.");

        var brand = new Brand
        {
            BrandId = _brandDao.GenerateNextBrandId(),
            BrandName = brandName.Trim(),
            CuisineType = cuisineType.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsActive = true
        };

        _brandDao.InsertBrand(brand);
        return brand;
    }

    public Brand UpdateBrand(string brandId, string brandName, string cuisineType, string? description)
    {
        var brand = _brandDao.FindById(brandId)
            ?? throw new InvalidOperationException($"Brand '{brandId}' not found.");

        if (string.IsNullOrWhiteSpace(brandName))
            throw new ArgumentException("Brand name is required.");
        if (string.IsNullOrWhiteSpace(cuisineType))
            throw new ArgumentException("Cuisine type is required.");
        if (_brandDao.ExistsByName(brandName, brandId))
            throw new InvalidOperationException($"Brand '{brandName.Trim()}' already exists.");

        brand.BrandName = brandName.Trim();
        brand.CuisineType = cuisineType.Trim();
        brand.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        _brandDao.UpdateBrand(brand);
        return brand;
    }

    public MenuItem CreateMenuItem(
        string brandId,
        string name,
        string? descEn,
        string? descVi,
        decimal price,
        string category,
        IEnumerable<string> hubIds)
    {
        var brand = _brandDao.FindById(brandId)
            ?? throw new InvalidOperationException($"Brand '{brandId}' not found.");

        if (!brand.IsActive)
            throw new InvalidOperationException("Cannot add menu items to an archived brand.");

        if (_menuItemDao.ExistsByNameInBrand(brandId, name))
            throw new InvalidOperationException($"Item '{name.Trim()}' already exists in brand '{brand.BrandName}'.");

        var hubList = hubIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (hubList.Count == 0)
            throw new ArgumentException("Please assign at least one Cloud Hub.");

        var item = new MenuItem
        {
            ItemId = _menuItemDao.GenerateNextItemId(),
            BrandId = brandId,
            ItemName = name.Trim(),
            DescriptionEn = string.IsNullOrWhiteSpace(descEn) ? null : descEn.Trim(),
            DescriptionVi = string.IsNullOrWhiteSpace(descVi) ? null : descVi.Trim(),
            UnitPrice = price,
            Category = string.IsNullOrWhiteSpace(category) ? "Main" : category.Trim(),
            IsActive = true
        };
        item.ValidateItemData();

        _menuItemDao.InsertMenuItem(item);
        _hubMenuItemDao.AssignItemToHubs(item.ItemId, brandId, hubList);
        item.AssignedHubsText = string.Join(", ", hubList.OrderBy(h => h));

        return item;
    }

    public MenuItem UpdateMenuItem(
        string itemId,
        string brandId,
        string name,
        string? descEn,
        string? descVi,
        decimal newPrice,
        string category,
        IEnumerable<string>? hubIds = null)
    {
        var item = _menuItemDao.FindById(itemId)
            ?? throw new InvalidOperationException($"Menu item '{itemId}' not found.");

        if (_menuItemDao.ExistsByNameInBrand(brandId, name, itemId))
            throw new InvalidOperationException($"Item '{name.Trim()}' already exists in this brand.");

        item.BrandId = brandId;
        item.UpdateDetails(name, descEn, descVi, newPrice, string.IsNullOrWhiteSpace(category) ? "Main" : category.Trim());

        _menuItemDao.UpdateMenuItem(item);

        if (hubIds != null)
        {
            var hubList = hubIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (hubList.Count == 0)
                throw new ArgumentException("Please assign at least one Cloud Hub.");

            _hubMenuItemDao.AssignItemToHubs(item.ItemId, brandId, hubList);
            item.AssignedHubsText = string.Join(", ", hubList.OrderBy(h => h));
        }

        return item;
    }

    public bool ArchiveCatalogEntity(string entityId, string entityType)
    {
        if (entityType.Equals("Brand", StringComparison.OrdinalIgnoreCase))
        {
            var brand = _brandDao.FindById(entityId)
                ?? throw new InvalidOperationException($"Brand '{entityId}' not found.");
            brand.IsActive = false;
            return _brandDao.SetBrandActive(entityId, false);
        }

        if (entityType.Equals("MenuItem", StringComparison.OrdinalIgnoreCase))
        {
            var item = _menuItemDao.FindById(entityId)
                ?? throw new InvalidOperationException($"Menu item '{entityId}' not found.");
            item.ArchiveItem();
            return _menuItemDao.ArchiveMenuItem(entityId);
        }

        throw new ArgumentException($"Unsupported entity type: {entityType}");
    }

    public bool ReactivateCatalogEntity(string entityId, string entityType)
    {
        if (entityType.Equals("Brand", StringComparison.OrdinalIgnoreCase))
        {
            var brand = _brandDao.FindById(entityId)
                ?? throw new InvalidOperationException($"Brand '{entityId}' not found.");
            brand.IsActive = true;
            return _brandDao.SetBrandActive(entityId, true);
        }

        if (entityType.Equals("MenuItem", StringComparison.OrdinalIgnoreCase))
        {
            var item = _menuItemDao.FindById(entityId)
                ?? throw new InvalidOperationException($"Menu item '{entityId}' not found.");
            item.ReactivateItem();
            return _menuItemDao.ReactivateMenuItem(entityId);
        }

        throw new ArgumentException($"Unsupported entity type: {entityType}");
    }
}
