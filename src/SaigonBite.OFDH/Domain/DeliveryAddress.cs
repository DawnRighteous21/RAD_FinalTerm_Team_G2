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

    public void MarkArchived()
    {
        Status = "Archived";
        IsDefault = false;
    }

    public void UpdateDetails(
        string recipientName,
        string streetAddress,
        string district,
        string? landmarkOrHotel,
        decimal distanceKmFromHub,
        bool isDefault,
        decimal maxDistanceKm = 7.0m)
    {
        RecipientName = recipientName.Trim();
        StreetAddress = streetAddress.Trim();
        District = district.Trim();
        LandmarkOrHotel = string.IsNullOrWhiteSpace(landmarkOrHotel) ? null : landmarkOrHotel.Trim();
        DistanceKmFromHub = distanceKmFromHub;
        IsDefault = isDefault;
        Status = VerifyRange(maxDistanceKm) ? "Verified" : "OutOfRange";
    }

    public string DisplayLabel => $"{AddressId} — {StreetAddress}, {District} ({DistanceKmFromHub:0.#} km)";
    public string StreetAndDistrict => IsDefault ? $"{StreetAddress}, {District} ★" : $"{StreetAddress}, {District}";
    public string LandmarkDisplay => string.IsNullOrWhiteSpace(LandmarkOrHotel) ? "—" : LandmarkOrHotel;
    public bool CanModify => !string.Equals(Status, "Archived", StringComparison.OrdinalIgnoreCase);
}