namespace SaigonBite.OFDH.Domain;

public class KitchenAlertEntry
{
    public string Timestamp { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string AccentHex { get; set; } = "#3B82F6";

    public string HeaderText => $"[{Timestamp}] {Headline}";
}
