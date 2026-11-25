namespace SaigonBite.OFDH.Domain;

public class OrderItem
{
    public string OrderItemId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPriceAtOrder { get; set; }
    public decimal LineTotal { get; set; }
    public string? SpecialInstructions { get; set; }
}
