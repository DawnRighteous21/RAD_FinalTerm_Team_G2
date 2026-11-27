namespace SaigonBite.OFDH.Domain;

public class Shipper
{
    public string ShipperId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string VehiclePlate { get; set; } = string.Empty;
    public string AvailabilityStatus { get; set; } = "Available";
    public decimal CashOnHand { get; set; }

    public string DisplayLabel => $"{ShipperId} — {FullName} ({VehiclePlate})";

    public void AcceptTask()
    {
        if (!string.Equals(AvailabilityStatus, "Available", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Shipper {ShipperId} cannot accept a new task while status is '{AvailabilityStatus}'.");

        AvailabilityStatus = "AssignedToOrder";
    }

    public void AssignToOrder()
    {
        if (!string.Equals(AvailabilityStatus, "Available", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(AvailabilityStatus, "AssignedToOrder", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Shipper {ShipperId} cannot confirm pickup while status is '{AvailabilityStatus}'.");
        }

        AvailabilityStatus = "OnDelivery";
    }

    public decimal AddCollectedCash(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Collected COD cash amount must be greater than 0 VND.");

        CashOnHand += amount;
        return CashOnHand;
    }

    public void MarkAvailable()
    {
        AvailabilityStatus = "Available";
    }
}
