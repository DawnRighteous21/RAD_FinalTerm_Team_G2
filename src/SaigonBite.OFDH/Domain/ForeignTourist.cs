using System.Text.RegularExpressions;

namespace SaigonBite.OFDH.Domain;

public class ForeignTourist : Customer
{
    private static readonly Regex PassportRegex = new(@"^[A-Za-z0-9]{6,12}$", RegexOptions.Compiled);

    public string PassportNumber { get; set; } = string.Empty;
    public string? Nationality { get; set; }
    public string? HotelName { get; set; }
    public string? RoomNumber { get; set; }

    public ForeignTourist()
    {
        CustomerType = "Tourist";
    }

    public bool ValidatePassport()
    {
        return !string.IsNullOrWhiteSpace(PassportNumber) && PassportRegex.IsMatch(PassportNumber.Trim());
    }

    public override bool ValidateCredentials()
    {
        return !string.IsNullOrWhiteSpace(FullName)
            && ValidateEmailFormat()
            && ValidatePassport();
    }
}