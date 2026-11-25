namespace SaigonBite.OFDH.Domain;

public class HubMenuItem
{
    public string HubId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public bool IsAvailableInStock { get; set; } = true;
    public int DailyPrepLimit { get; set; } = 50;
    public string LastUpdated { get; set; } = string.Empty;
}
