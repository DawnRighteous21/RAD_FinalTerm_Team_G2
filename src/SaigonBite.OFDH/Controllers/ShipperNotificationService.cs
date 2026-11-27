using SaigonBite.OFDH.DataAccess;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.Controllers;

public class ShipperNotificationService
{
    private readonly ShipperDAO _shipperDao;

    public ShipperNotificationService(DatabaseConnection db)
    {
        _shipperDao = new ShipperDAO(db);
    }

    public KitchenAlertEntry BroadcastPickupAlert(string orderId, string hubId, string? assignedShipperId = null)
    {
        var targetShipper = !string.IsNullOrWhiteSpace(assignedShipperId)
            ? assignedShipperId
            : _shipperDao.FindAll()
                .FirstOrDefault(s => string.Equals(s.AvailabilityStatus, "Available", StringComparison.OrdinalIgnoreCase))
                ?.ShipperId ?? "Available Pool";

        return new KitchenAlertEntry
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            Headline = $"Alert Sent -> {targetShipper}",
            Detail = $"{orderId} marked ReadyForPickup at {hubId}.",
            AccentHex = "#10B981"
        };
    }
}
