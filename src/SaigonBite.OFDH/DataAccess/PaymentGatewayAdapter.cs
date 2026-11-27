namespace SaigonBite.OFDH.DataAccess;

public class PaymentGatewayAdapter
{
    private static readonly HashSet<string> SupportedOnlineMethods =
        new(StringComparer.OrdinalIgnoreCase) { "MoMo", "VNPay", "Card" };

    public string AuthorizePayment(decimal totalAmount, string paymentMethod, string? paymentDetails = null)
    {
        if (totalAmount <= 0)
            throw new InvalidOperationException("Payment amount must be greater than 0 VND.");

        if (!SupportedOnlineMethods.Contains(paymentMethod))
            throw new InvalidOperationException($"Unsupported online payment method: {paymentMethod}.");

        var code = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"TXN-{paymentMethod.ToUpperInvariant()}-{code}";
    }
}
