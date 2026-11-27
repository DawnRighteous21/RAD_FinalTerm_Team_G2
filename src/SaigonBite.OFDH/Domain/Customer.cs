using System.Text.RegularExpressions;

namespace SaigonBite.OFDH.Domain;

public abstract class Customer
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string CustomerId { get; set; } = string.Empty;
    public string CustomerType { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string CreatedAt { get; set; } = string.Empty;

    public string DisplayLabel => $"{CustomerId} — {FullName} ({CustomerType})";

    public bool ValidateEmailFormat()
    {
        return !string.IsNullOrWhiteSpace(Email) && EmailRegex.IsMatch(Email.Trim());
    }

    public void ActivateProfile()
    {
        Status = "Active";
        if (string.IsNullOrWhiteSpace(CreatedAt))
            CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public abstract bool ValidateCredentials();
}