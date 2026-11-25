namespace SaigonBite.OFDH.Domain;

public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string HubId { get; set; } = string.Empty;
    public string AddressId { get; set; } = string.Empty;
    public string? VoucherCode { get; set; }
    public string? ShipperId { get; set; }
    public string OrderDateTime { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = "Placed";
    public decimal SubtotalAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = "COD";
    public string PaymentStatus { get; set; } = "PendingCOD";
    public string? PrepStartedAt { get; set; }
    public string? ReadyAt { get; set; }
    public string? DeliveredAt { get; set; }
    public string? FailureReason { get; set; }

    public List<OrderItem> Items { get; set; } = new();
    public Voucher? AppliedVoucher { get; set; }

    public decimal CalculateDeliveryFee(decimal distanceKm, string hubStatus)
    {
        var baseFee = distanceKm <= 3.0m ? 15000m : 25000m;
        if (hubStatus == "Quiet")
            baseFee = Math.Max(0m, baseFee - 10000m);

        DeliveryFee = baseFee;
        CalculateTotalAmount();
        return DeliveryFee;
    }

    public decimal AddOrderItem(MenuItem menuItem, int quantity, string? note = null)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be at least 1.");

        var existing = Items.FirstOrDefault(x => x.ItemId == menuItem.ItemId && x.SpecialInstructions == note);
        if (existing != null)
        {
            existing.Quantity += quantity;
            existing.LineTotal = existing.Quantity * existing.UnitPriceAtOrder;
        }
        else
        {
            Items.Add(new OrderItem
            {
                OrderItemId = $"OI-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                OrderId = OrderId,
                ItemId = menuItem.ItemId,
                ItemName = menuItem.ItemName,
                Quantity = quantity,
                UnitPriceAtOrder = menuItem.UnitPrice,
                LineTotal = menuItem.UnitPrice * quantity,
                SpecialInstructions = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
            });
        }

        CalculateTotalAmount();
        return SubtotalAmount;
    }

    public void ApplySingleVoucher(Voucher voucher, DateTime now)
    {
        if (!voucher.IsValidForOrder(SubtotalAmount, now))
            throw new InvalidOperationException("Voucher is invalid, expired, or order subtotal is below minimum.");

        AppliedVoucher = voucher;
        VoucherCode = voucher.VoucherCode;
        CalculateTotalAmount();
    }

    public void RemoveVoucher()
    {
        AppliedVoucher = null;
        VoucherCode = null;
        DiscountAmount = 0m;
        CalculateTotalAmount();
    }

    public decimal CalculateTotalAmount()
    {
        SubtotalAmount = Items.Sum(i => i.LineTotal);
        DiscountAmount = AppliedVoucher != null && AppliedVoucher.IsValidForOrder(SubtotalAmount, DateTime.Now)
            ? AppliedVoucher.CalculateDiscount(SubtotalAmount)
            : 0m;

        TotalAmount = Math.Max(0m, SubtotalAmount + DeliveryFee - DiscountAmount);
        return TotalAmount;
    }

    public void MarkPlaced(string paymentStatus)
    {
        OrderStatus = "Placed";
        PaymentStatus = paymentStatus;
        OrderDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
