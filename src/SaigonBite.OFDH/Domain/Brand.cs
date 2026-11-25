namespace SaigonBite.OFDH.Domain;

public class Brand
{
    public string BrandId { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string CuisineType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public string StatusText => IsActive ? "Active" : "Archived";
    public string DisplayLabel => $"{BrandId} — {BrandName}";
}
