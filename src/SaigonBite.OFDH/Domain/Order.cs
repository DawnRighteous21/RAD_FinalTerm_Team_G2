namespace SaigonBite.OFDH.Domain;

public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? CustomerType { get; set; }
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
    public DeliveryAddress? DeliveryAddress { get; set; }

    public bool IsCodOrder => string.Equals(PaymentMethod, "COD", StringComparison.OrdinalIgnoreCase);
    public string PaymentSummaryText => $"{PaymentMethod} ({PaymentStatus})";

    public bool IsCancellationLocked => OrderStatus is
        "ConfirmedByKitchen" or "InPreparation" or "ReadyForPickup" or "PickedUp" or "Delivered";

    public string ItemsSummary => Items.Count == 0
        ? "No items loaded"
        : string.Join(", ", Items.Select(i =>
            string.IsNullOrWhiteSpace(i.SpecialInstructions)
                ? $"{i.Quantity}x {i.ItemName}"
                : $"{i.Quantity}x {i.ItemName} ({i.SpecialInstructions})"));

    public string KitchenTimeSummary
    {
        get
        {
            var shortOrderTime = ExtractClockTime(OrderDateTime);
            return OrderStatus switch
            {
                "Placed" => $"{shortOrderTime}\n(5m confirm window)",
                "ConfirmedByKitchen" => $"{shortOrderTime}\n(Confirmed)",
                "InPreparation" => $"{shortOrderTime}\n(Prep {ExtractClockTime(PrepStartedAt)})",
                "ReadyForPickup" => $"{shortOrderTime}\n(Ready {ExtractClockTime(ReadyAt)})",
                _ => shortOrderTime
            };
        }
    }

    public string KitchenActionHint => OrderStatus switch
    {
        "Placed" => "[Confirm] [Reject]",
        "ConfirmedByKitchen" => "[Start Prep]",
        "InPreparation" => "[Mark Ready]",
        "ReadyForPickup" => "[Awaiting Shipper]",
        _ => OrderStatus
    };

    public string CustomerDisplayText => string.IsNullOrWhiteSpace(CustomerName)
        ? CustomerId
        : string.IsNullOrWhiteSpace(CustomerType)
            ? $"{CustomerId} ({CustomerName})"
            : $"{CustomerId} ({CustomerName} — {CustomerType})";

    private static string ExtractClockTime(string? timestamp)
    {
        if (string.IsNullOrWhiteSpace(timestamp)) return "--:--";
        return DateTime.TryParse(timestamp, out var parsed)
            ? parsed.ToString("HH:mm")
            : timestamp;
    }

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
                BrandId = menuItem.BrandId,
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

    public void ConfirmByKitchen()
    {
        if (!string.Equals(OrderStatus, "Placed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Order {OrderId} cannot be confirmed because current status is '{OrderStatus}' (requires 'Placed').");

        OrderStatus = "ConfirmedByKitchen";
        FailureReason = null;
    }

    public void RejectByKitchen(string reasonCode, string? customDetails)
    {
        if (!string.Equals(OrderStatus, "Placed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Order {OrderId} cannot be rejected because current status is '{OrderStatus}' (requires 'Placed').");

        if (string.IsNullOrWhiteSpace(reasonCode))
            throw new ArgumentException("A rejection reason code is required.");

        var cleanCode = reasonCode.Trim();
        var cleanDetails = (customDetails ?? string.Empty).Trim();

        if (string.Equals(cleanCode, "Other", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(cleanDetails))
        {
            throw new ArgumentException("Detailed explanation is required when rejection reason is 'Other'.");
        }

        OrderStatus = "Rejected";
        FailureReason = string.IsNullOrWhiteSpace(cleanDetails)
            ? cleanCode
            : $"{cleanCode}: {cleanDetails}";

        if (string.Equals(PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            PaymentStatus = "Refunded";
    }

    public void StartPreparation()
    {
        if (!string.Equals(OrderStatus, "ConfirmedByKitchen", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Order {OrderId} cannot start preparation because current status is '{OrderStatus}' (requires 'ConfirmedByKitchen').");

        OrderStatus = "InPreparation";
        PrepStartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public void MarkOrderReady()
    {
        if (!string.Equals(OrderStatus, "InPreparation", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Order {OrderId} cannot be marked ready because current status is '{OrderStatus}' (requires 'InPreparation').");

        OrderStatus = "ReadyForPickup";
        ReadyAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public void MarkPickedUp(string shipperId)
    {
        if (!string.Equals(OrderStatus, "ReadyForPickup", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Order {OrderId} cannot be picked up because current status is '{OrderStatus}' (requires 'ReadyForPickup').");

        if (string.IsNullOrWhiteSpace(shipperId))
            throw new ArgumentException("Shipper ID is required to pick up an order.");

        if (!string.IsNullOrWhiteSpace(ShipperId) &&
            !string.Equals(ShipperId, shipperId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Order {OrderId} is already assigned to another shipper ({ShipperId}).");
        }

        ShipperId = shipperId.Trim();
        OrderStatus = "PickedUp";
    }

    public bool RecordCODPayment(decimal amount)
    {
        if (!IsCodOrder)
            throw new InvalidOperationException($"Order {OrderId} is prepaid ({PaymentMethod}), not COD.");

        if (!string.Equals(OrderStatus, "PickedUp", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Cannot record COD payment while order status is '{OrderStatus}' (requires 'PickedUp').");

        if (string.Equals(PaymentStatus, "PaidCOD", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"COD payment for order {OrderId} has already been confirmed.");

        if (amount != TotalAmount)
            throw new InvalidOperationException(
                $"COD mismatch: exact amount required is {TotalAmount:N0} VND, but entered {amount:N0} VND.");

        PaymentStatus = "PaidCOD";
        return true;
    }

    public void MarkDelivered()
    {
        if (!string.Equals(OrderStatus, "PickedUp", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Order {OrderId} cannot be marked Delivered from status '{OrderStatus}' (requires 'PickedUp').");

        if (IsCodOrder && !string.Equals(PaymentStatus, "PaidCOD", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"COD cash ({TotalAmount:N0} VND) must be collected and confirmed (PaidCOD) before completing delivery.");

        OrderStatus = "Delivered";
        DeliveredAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        FailureReason = null;
    }

    public void MarkDeliveryFailed(string reason)
    {
        if (!string.Equals(OrderStatus, "PickedUp", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Cannot report delivery failure while order status is '{OrderStatus}' (requires 'PickedUp').");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A valid failure reason must be provided.");

        OrderStatus = "DeliveryFailed";
        FailureReason = reason.Trim();
    }
}
