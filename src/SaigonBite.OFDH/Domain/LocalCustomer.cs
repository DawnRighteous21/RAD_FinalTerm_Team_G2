namespace SaigonBite.OFDH.Domain;

public class LocalCustomer : Customer
{
    public string VnPhoneNumber { get; set; } = string.Empty;
    public string? PreferredLocalWallet { get; set; }

    public LocalCustomer()
    {
        CustomerType = "Local";
    }

    public override bool ValidateCredentials()
    {
        return !string.IsNullOrWhiteSpace(FullName)
            && !string.IsNullOrWhiteSpace(Email)
            && !string.IsNullOrWhiteSpace(VnPhoneNumber);
    }
}
