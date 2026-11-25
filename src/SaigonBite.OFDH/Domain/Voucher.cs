namespace SaigonBite.OFDH.Domain;

public class Voucher
{
    public string VoucherCode { get; set; } = string.Empty;
    public decimal DiscountPercentage { get; set; }
    public decimal MaxDiscountAmount { get; set; }
    public decimal MinOrderValue { get; set; }
    public string ValidFrom { get; set; } = string.Empty;
    public string ValidUntil { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public bool IsValidForOrder(decimal subtotal, DateTime currentDate)
    {
        if (!IsActive || subtotal < MinOrderValue) return false;
        if (DateTime.TryParse(ValidFrom, out var from) && currentDate.Date < from.Date) return false;
        if (DateTime.TryParse(ValidUntil, out var until) && currentDate.Date > until.Date) return false;
        return true;
    }

    public decimal CalculateDiscount(decimal subtotal)
    {
        var raw = Math.Round(subtotal * (DiscountPercentage / 100m), 0);
        return Math.Min(raw, MaxDiscountAmount);
    }
}
