namespace SaigonBite.OFDH.Domain;

public class BrandSalesRow
{
    public string BrandId { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public int DeliveredCount { get; set; }
    public int ExcludedCount { get; set; }
    public decimal RefundsAmount { get; set; }
    public decimal NetRevenue { get; set; }
    public bool IsTotalRow { get; set; }

    public string ExcludedDisplay => ExcludedCount == 0 ? "0" : $"{ExcludedCount} (Excl.)";
    public string RefundsDisplay => $"{RefundsAmount:N0}";
    public string NetRevenueDisplay => $"{NetRevenue:N0}";
}
