namespace SaigonBite.OFDH.Domain;

public class DeliveryAddress
{
    public string AddressId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string StreetAddress { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string? LandmarkOrHotel { get; set; }
    public decimal DistanceKmFromHub { get; set; }
    public bool IsDefault { get; set; }
    public string Status { get; set; } = "Verified";

    public bool VerifyRange(decimal maxDistanceKm = 7.0m)
    {
        return DistanceKmFromHub >= 0 && DistanceKmFromHub <= maxDistanceKm;
    }

    public string DisplayLabel => $"{AddressId} — {StreetAddress}, {District} ({DistanceKmFromHub:0.#} km)";
}
