using SaigonBite.OFDH.DataAccess;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.Controllers;

public class KitchenPrepController
{
    private readonly OrderDAO _orderDao;
    private readonly HubDAO _hubDao;
    private readonly HubMenuItemDAO _hubMenuItemDao;
    private readonly ShipperNotificationService _notificationService;

    public KitchenPrepController() : this(new DatabaseConnection())
    {
    }

    public KitchenPrepController(DatabaseConnection db)
    {
        _orderDao = new OrderDAO(db);
        _hubDao = new HubDAO(db);
        _hubMenuItemDao = new HubMenuItemDAO(db);
        _notificationService = new ShipperNotificationService(db);
    }

    public List<Hub> GetAllHubs() => _hubDao.FindAll();

    public Hub? GetHubById(string hubId) => _hubDao.FindById(hubId);

    public List<Order> GetKitchenQueue(string hubId)
    {
        if (string.IsNullOrWhiteSpace(hubId))
            return new List<Order>();

        return _orderDao.FindKitchenQueueByHub(hubId, confirmedOnly: false);
    }

    public List<Order> GetConfirmedOrderQueue(string hubId)
    {
        if (string.IsNullOrWhiteSpace(hubId))
            return new List<Order>();

        return _orderDao.FindKitchenQueueByHub(hubId, confirmedOnly: true);
    }

    public Order? GetOrderDetails(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId)) return null;
        return _orderDao.FindByIdWithItems(orderId);
    }

    public (Order Order, KitchenAlertEntry Alert) ConfirmOrder(string orderId, string staffId)
    {
        var order = _orderDao.FindByIdWithItems(orderId)
            ?? throw new InvalidOperationException($"Order '{orderId}' was not found.");

        order.ConfirmByKitchen();
        if (!_orderDao.UpdateOrderPrepState(order))
            throw new InvalidOperationException($"Failed to confirm order '{orderId}'.");

        var alert = new KitchenAlertEntry
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            Headline = $"Order Confirmed ({staffId})",
            Detail = $"{order.OrderId} confirmed; customer cancellation locked (A-03).",
            AccentHex = "#3B82F6"
        };

        return (order, alert);
    }

    public (Order Order, KitchenAlertEntry Alert) RejectOrder(
        string orderId,
        string reasonCode,
        string? customDetails,
        string staffId)
    {
        var order = _orderDao.FindByIdWithItems(orderId)
            ?? throw new InvalidOperationException($"Order '{orderId}' was not found.");

        order.RejectByKitchen(reasonCode, customDetails);
        if (!_orderDao.UpdateOrderPrepState(order))
            throw new InvalidOperationException($"Failed to reject order '{orderId}'.");

        var hub = _hubDao.FindById(order.HubId);
        if (hub != null)
        {
            var newCount = hub.DecrementActiveOrders();
            _hubDao.UpdateActiveOrderCount(hub.HubId, newCount, hub.HubStatus);
        }

        var alert = new KitchenAlertEntry
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            Headline = $"Order Rejected ({staffId})",
            Detail = $"{order.OrderId} rejected — {order.FailureReason}.",
            AccentHex = "#EA580C"
        };

        return (order, alert);
    }

    public (Order Order, KitchenAlertEntry Alert) StartPreparation(string orderId, string staffId)
    {
        var order = _orderDao.FindByIdWithItems(orderId)
            ?? throw new InvalidOperationException($"Order '{orderId}' was not found.");

        order.StartPreparation();
        if (!_orderDao.UpdateOrderPrepState(order))
            throw new InvalidOperationException($"Failed to start preparation for '{orderId}'.");

        var alert = new KitchenAlertEntry
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            Headline = $"Preparation Started ({staffId})",
            Detail = $"{order.OrderId}: ConfirmedByKitchen -> InPreparation.",
            AccentHex = "#3B82F6"
        };

        return (order, alert);
    }

    public (Order Order, KitchenAlertEntry Alert) MarkOrderReady(string orderId, string staffId)
    {
        var order = _orderDao.FindByIdWithItems(orderId)
            ?? throw new InvalidOperationException($"Order '{orderId}' was not found.");

        order.MarkOrderReady();
        if (!_orderDao.UpdateOrderPrepState(order))
            throw new InvalidOperationException($"Failed to mark order '{orderId}' ready.");

        var alert = _notificationService.BroadcastPickupAlert(order.OrderId, order.HubId, order.ShipperId);
        return (order, alert);
    }

    public List<HubMenuItem> GetHubMenuStock(string hubId)
    {
        if (string.IsNullOrWhiteSpace(hubId))
            return new List<HubMenuItem>();

        return _hubMenuItemDao.FindByHubWithItemDetails(hubId);
    }

    public (HubMenuItem Item, KitchenAlertEntry Alert) MarkHubItemUnavailable(string hubId, string itemId)
    {
        var hmi = _hubMenuItemDao.FindByHubAndItem(hubId, itemId)
            ?? throw new InvalidOperationException($"Menu item '{itemId}' is not assigned to hub '{hubId}'.");

        hmi.MarkOutOfStock();
        if (!_hubMenuItemDao.UpdateStockAvailability(hmi))
            throw new InvalidOperationException($"Failed to mark '{itemId}' out of stock.");

        var alert = new KitchenAlertEntry
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            Headline = $"Stock Depleted ({hubId})",
            Detail = $"{hmi.ItemId} ({hmi.ItemName}) marked Unavailable (US-11).",
            AccentHex = "#EA580C"
        };

        return (hmi, alert);
    }

    public (HubMenuItem Item, KitchenAlertEntry Alert) RestockHubItem(string hubId, string itemId)
    {
        var hmi = _hubMenuItemDao.FindByHubAndItem(hubId, itemId)
            ?? throw new InvalidOperationException($"Menu item '{itemId}' is not assigned to hub '{hubId}'.");

        hmi.Restock();
        if (!_hubMenuItemDao.UpdateStockAvailability(hmi))
            throw new InvalidOperationException($"Failed to restock '{itemId}'.");

        var alert = new KitchenAlertEntry
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            Headline = $"Item Restocked ({hubId})",
            Detail = $"{hmi.ItemId} ({hmi.ItemName}) restored to In Stock.",
            AccentHex = "#10B981"
        };

        return (hmi, alert);
    }
}
