namespace SaigonBite.OFDH.Domain;

public class HubMenuItem
{
    public string HubId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string BrandId { get; set; } = string.Empty;
    public bool IsAvailableInStock { get; set; } = true;
    public int DailyPrepLimit { get; set; } = 50;
    public string LastUpdated { get; set; } = string.Empty;

    public string DisplayTitle => string.IsNullOrWhiteSpace(ItemName) ? ItemId : $"{ItemId}: {ItemName}";
    public string DailyLimitText => $"Daily Prep Limit: {DailyPrepLimit}";
    public string StockStatusText => IsAvailableInStock ? "In Stock" : "Unavailable";
    public string ToggleActionText => IsAvailableInStock ? "Mark Out of Stock" : "Restock Item";

    public void MarkOutOfStock()
    {
        IsAvailableInStock = false;
        LastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public void Restock()
    {
        IsAvailableInStock = true;
        LastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
