namespace SaigonBite.OFDH.Domain;

public abstract class Customer
{
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerType { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string CreatedAt { get; set; } = string.Empty;

    public abstract bool ValidateCredentials();
}
