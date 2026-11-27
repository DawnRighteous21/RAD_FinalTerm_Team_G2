using SaigonBite.OFDH.DataAccess;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.Controllers;

public class DeliveryFulfilmentController
{
    public const int MinContactAttemptsForFailure = 3;
    public const int MinWaitMinutesForFailure = 10;

    private readonly OrderDAO _orderDAO;
    private readonly ShipperDAO _shipperDAO;
    private readonly HubDAO _hubDAO;

    public DeliveryFulfilmentController() : this(new DatabaseConnection())
    {
    }

    public DeliveryFulfilmentController(DatabaseConnection db)
    {
        _orderDAO = new OrderDAO(db);
        _shipperDAO = new ShipperDAO(db);
        _hubDAO = new HubDAO(db);
    }

    public List<Shipper> GetAllShippers()
    {
        return _shipperDAO.FindAll();
    }

    public Shipper? GetShipperById(string shipperId)
    {
        if (string.IsNullOrWhiteSpace(shipperId)) return null;
        return _shipperDAO.FindById(shipperId);
    }

    public Hub? GetHubById(string hubId)
    {
        if (string.IsNullOrWhiteSpace(hubId)) return null;
        return _hubDAO.FindById(hubId);
    }

    public List<Order> GetTasksForShipper(string shipperId)
    {
        if (string.IsNullOrWhiteSpace(shipperId))
            return new List<Order>();

        return _orderDAO.FindDeliverableAndActiveOrders(shipperId);
    }

    public Order AcceptDeliveryTask(string shipperId, string orderId)
    {
        var order = _orderDAO.FindById(orderId)
            ?? throw new InvalidOperationException($"Order '{orderId}' was not found.");
        var shipper = _shipperDAO.FindById(shipperId)
            ?? throw new InvalidOperationException($"Shipper '{shipperId}' was not found.");

        if (!string.Equals(order.OrderStatus, "ReadyForPickup", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Order {orderId} is not ready for pickup (current status: {order.OrderStatus}).");

        if (!string.IsNullOrWhiteSpace(order.ShipperId) &&
            !string.Equals(order.ShipperId, shipperId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Order {orderId} is already assigned to shipper {order.ShipperId}.");
        }

        shipper.AcceptTask();
        order.ShipperId = shipper.ShipperId;

        _orderDAO.UpdateDeliveryStatus(order);
        _shipperDAO.UpdateShipperStateAndCash(shipper);

        return order;
    }

    public bool ConfirmOrderPickup(string shipperId, string orderId)
    {
        var order = _orderDAO.FindById(orderId)
            ?? throw new InvalidOperationException($"Order '{orderId}' was not found.");
        var shipper = _shipperDAO.FindById(shipperId)
            ?? throw new InvalidOperationException($"Shipper '{shipperId}' was not found.");

        order.MarkPickedUp(shipper.ShipperId);
        shipper.AssignToOrder();

        var orderUpdated = _orderDAO.UpdateDeliveryStatus(order);
        var shipperUpdated = _shipperDAO.UpdateShipperStateAndCash(shipper);
        return orderUpdated && shipperUpdated;
    }

    public decimal ConfirmCashCollected(string shipperId, string orderId, decimal cashAmount)
    {
        var order = _orderDAO.FindById(orderId)
            ?? throw new InvalidOperationException($"Order '{orderId}' was not found.");
        var shipper = _shipperDAO.FindById(shipperId)
            ?? throw new InvalidOperationException($"Shipper '{shipperId}' was not found.");

        if (!string.Equals(order.ShipperId, shipperId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Order {orderId} is not assigned to shipper {shipperId}.");

        order.RecordCODPayment(cashAmount);
        var updatedCash = shipper.AddCollectedCash(cashAmount);

        _orderDAO.UpdateDeliveryStatus(order);
        _shipperDAO.UpdateShipperStateAndCash(shipper);

        return updatedCash;
    }

    public Order ConfirmDeliverySuccess(string shipperId, string orderId, decimal? codAmount = null)
    {
        var order = _orderDAO.FindById(orderId)
            ?? throw new InvalidOperationException($"Order '{orderId}' was not found.");
        var shipper = _shipperDAO.FindById(shipperId)
            ?? throw new InvalidOperationException($"Shipper '{shipperId}' was not found.");

        if (!string.Equals(order.ShipperId, shipperId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Order {orderId} is not assigned to shipper {shipperId}.");

        if (order.IsCodOrder && !string.Equals(order.PaymentStatus, "PaidCOD", StringComparison.OrdinalIgnoreCase))
        {
            if (codAmount.HasValue)
            {
                order.RecordCODPayment(codAmount.Value);
                shipper.AddCollectedCash(codAmount.Value);
            }
            else
            {
                throw new InvalidOperationException(
                    $"COD cash ({order.TotalAmount:N0} VND) must be confirmed before marking delivery successful.");
            }
        }

        order.MarkDelivered();
        shipper.MarkAvailable();

        _orderDAO.UpdateDeliveryStatus(order);
        _shipperDAO.UpdateShipperStateAndCash(shipper);
        DecrementHubWorkload(order.HubId);

        return order;
    }

    public Order ReportDeliveryFailure(
        string shipperId,
        string orderId,
        string failureReason,
        int contactAttempts,
        int waitMinutes)
    {
        if (contactAttempts < MinContactAttemptsForFailure || waitMinutes < MinWaitMinutesForFailure)
        {
            throw new InvalidOperationException(
                $"Delivery failure guard rule not met: requires at least {MinContactAttemptsForFailure} contact attempts AND {MinWaitMinutesForFailure} minutes wait (provided: {contactAttempts} attempts, {waitMinutes} mins).");
        }

        var order = _orderDAO.FindById(orderId)
            ?? throw new InvalidOperationException($"Order '{orderId}' was not found.");
        var shipper = _shipperDAO.FindById(shipperId)
            ?? throw new InvalidOperationException($"Shipper '{shipperId}' was not found.");

        if (!string.Equals(order.ShipperId, shipperId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Order {orderId} is not assigned to shipper {shipperId}.");

        order.MarkDeliveryFailed(failureReason);
        shipper.MarkAvailable();

        _orderDAO.UpdateDeliveryStatus(order);
        _shipperDAO.UpdateShipperStateAndCash(shipper);
        DecrementHubWorkload(order.HubId);

        return order;
    }

    private void DecrementHubWorkload(string hubId)
    {
        var hub = _hubDAO.FindById(hubId);
        if (hub == null) return;

        var newCount = hub.DecrementActiveOrders();
        _hubDAO.UpdateActiveOrderCount(hub.HubId, newCount, hub.HubStatus);
    }
}
