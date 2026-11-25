namespace SaigonBite.OFDH.Domain;

public class ForeignTourist : Customer
{
    public string PassportNumber { get; set; } = string.Empty;
    public string? Nationality { get; set; }
    public string? HotelName { get; set; }
    public string? RoomNumber { get; set; }

    public ForeignTourist()
    {
        CustomerType = "Tourist";
    }

    public override bool ValidateCredentials()
    {
        return !string.IsNullOrWhiteSpace(FullName)
            && !string.IsNullOrWhiteSpace(Email)
            && !string.IsNullOrWhiteSpace(PassportNumber);
    }
}
