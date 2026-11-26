namespace SaigonBite.OFDH.Domain;

public class MenuItem
{
    public string ItemId { get; set; } = string.Empty;
    public string BrandId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }
    public string? DescriptionVi { get; set; }
    public decimal UnitPrice { get; set; }
    public string Category { get; set; } = "Main";
    public bool IsActive { get; set; } = true;
    public string AssignedHubsText { get; set; } = string.Empty;

    public string StatusText => IsActive ? "Active" : "Archived";

    public void ValidateItemData()
    {
        if (string.IsNullOrWhiteSpace(BrandId))
            throw new ArgumentException("Brand is required.");
        if (string.IsNullOrWhiteSpace(ItemName) || !ItemName.Any(char.IsLetter))
            throw new ArgumentException("Item name is required and must contain letters.");
        if (string.IsNullOrWhiteSpace(DescriptionEn) || !DescriptionEn.Any(char.IsLetter))
            throw new ArgumentException("English description is required and must contain letters.");
        if (string.IsNullOrWhiteSpace(DescriptionVi) || !DescriptionVi.Any(char.IsLetter))
            throw new ArgumentException("Vietnamese description is required and must contain letters.");
        if (UnitPrice <= 0)
            throw new ArgumentException("Unit price must be greater than 0 VND.");
    }

    public void UpdateDetails(string name, string? descEn, string? descVi, decimal newPrice, string category)
    {
        ItemName = name.Trim();
        DescriptionEn = descEn?.Trim();
        DescriptionVi = descVi?.Trim();
        UnitPrice = newPrice;
        Category = category;
        ValidateItemData();
    }

    public void ArchiveItem()
    {
        IsActive = false;
    }

    public void ReactivateItem()
    {
        IsActive = true;
    }
}
