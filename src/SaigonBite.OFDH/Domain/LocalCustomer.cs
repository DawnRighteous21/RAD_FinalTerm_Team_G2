using System.Text.RegularExpressions;

namespace SaigonBite.OFDH.Domain;

public class LocalCustomer : Customer
{
    private static readonly Regex VnPhoneRegex = new(@"^0\d{9}$", RegexOptions.Compiled);

    public string VnPhoneNumber { get; set; } = string.Empty;
    public string? PreferredLocalWallet { get; set; }

    public LocalCustomer()
    {
        CustomerType = "Local";
    }

    public bool ValidateVnPhone()
    {
        return !string.IsNullOrWhiteSpace(VnPhoneNumber) && VnPhoneRegex.IsMatch(VnPhoneNumber.Trim());
    }

    public override bool ValidateCredentials()
    {
        return !string.IsNullOrWhiteSpace(FullName)
            && ValidateEmailFormat()
            && ValidateVnPhone();
    }
}