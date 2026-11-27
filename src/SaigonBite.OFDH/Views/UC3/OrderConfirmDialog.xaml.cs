using System.Globalization;
using System.Windows;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.Views.UC3;

public partial class OrderConfirmDialog : Window
{
    public OrderConfirmDialog(Order order, Hub updatedHub, string? transactionRef)
    {
        InitializeComponent();

        TxtOrderId.Text = $"{order.OrderId} ({order.OrderStatus})";
        TxtAssignedHub.Text = $"{updatedHub.HubId} ({updatedHub.CurrentActiveOrders} / {updatedHub.MaxActiveOrders} active)";
        TxtPaymentStatus.Text = $"{order.PaymentMethod} — {order.PaymentStatus}";
        TxtTotalPayable.Text = $"{order.TotalAmount.ToString("N0", CultureInfo.InvariantCulture)} VND";

        if (!string.IsNullOrWhiteSpace(transactionRef))
        {
            LblTxRef.Visibility = Visibility.Visible;
            TxtTxRef.Visibility = Visibility.Visible;
            TxtTxRef.Text = transactionRef;
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
