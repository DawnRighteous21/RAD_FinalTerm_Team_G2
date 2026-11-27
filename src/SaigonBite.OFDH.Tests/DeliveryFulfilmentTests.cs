// Author: 524H0134 (Ha Du Tuan)
using Microsoft.Data.Sqlite;
using SaigonBite.OFDH.Controllers;
using SaigonBite.OFDH.DataAccess;

namespace SaigonBite.OFDH.Tests;

public class DeliveryFulfilmentTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly DatabaseConnection _db;
    private readonly DeliveryFulfilmentController _controller;
    private readonly OrderDAO _orderDAO;
    private readonly ShipperDAO _shipperDAO;
    private readonly HubDAO _hubDAO;

    public DeliveryFulfilmentTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"saigonbite_uc10_test_{Guid.NewGuid():N}.db");
        _db = new DatabaseConnection(_tempDbPath);
        _db.InitializeSchema();

        _controller = new DeliveryFulfilmentController(_db);
        _orderDAO = new OrderDAO(_db);
        _shipperDAO = new ShipperDAO(_db);
        _hubDAO = new HubDAO(_db);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_tempDbPath))
            File.Delete(_tempDbPath);
    }

    private (string ShipperId, string OrderId, string HubId) SeedDeliveryScenario(
        string orderStatus = "ReadyForPickup",
        decimal totalAmount = 155000m,
        string paymentMethod = "COD",
        string paymentStatus = "PendingCOD",
        int initialHubActiveOrders = 5)
    {
        var regController = new CustomerRegistrationController(_db);
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var customer = regController.RegisterCustomer(
            "Local",
            $"Customer {suffix}",
            $"cust.{suffix.ToLowerInvariant()}@test.vn",
            $"09{Random.Shared.Next(10000000, 99999999)}",
            "MoMo",
            null, null, null, null);

        var address = regController.AddDeliveryAddress(
            customer.CustomerId,
            customer.FullName,
            "100 Pasteur",
            "District 1",
            "Lobby A",
            isDefault: true);

        var hub = _hubDAO.FindAll().First();
        _hubDAO.UpdateActiveOrderCount(hub.HubId, initialHubActiveOrders, "Normal");

        var shipperId = $"SHP-T{suffix[..3]}";
        var orderId = $"ORD-T{suffix[..4]}";

        using var conn = _db.GetConnection();

        using (var shipCmd = conn.CreateCommand())
        {
            shipCmd.CommandText = """
                INSERT INTO Shippers (
                    shipper_id, full_name, phone_number, vehicle_plate,
                    availability_status, cash_on_hand
                )
                VALUES (
                    @shipperId, 'Nguyen Van Shipper', @phone, '59X1-999.99',
                    'Available', 0
                );
                """;
            shipCmd.Parameters.AddWithValue("@shipperId", shipperId);
            shipCmd.Parameters.AddWithValue("@phone", $"08{Random.Shared.Next(10000000, 99999999)}");
            shipCmd.ExecuteNonQuery();
        }

        using (var ordCmd = conn.CreateCommand())
        {
            ordCmd.CommandText = """
                INSERT INTO Orders (
                    order_id, customer_id, hub_id, address_id, voucher_code, shipper_id,
                    order_date_time, order_status, subtotal_amount, delivery_fee,
                    discount_amount, total_amount, payment_method, payment_status
                )
                VALUES (
                    @orderId, @custId, @hubId, @addrId, NULL, NULL,
                    '2026-11-27 12:15:00', @status, @total, 0,
                    0, @total, @payMethod, @payStatus
                );
                """;
            ordCmd.Parameters.AddWithValue("@orderId", orderId);
            ordCmd.Parameters.AddWithValue("@custId", customer.CustomerId);
            ordCmd.Parameters.AddWithValue("@hubId", hub.HubId);
            ordCmd.Parameters.AddWithValue("@addrId", address.AddressId);
            ordCmd.Parameters.AddWithValue("@status", orderStatus);
            ordCmd.Parameters.AddWithValue("@total", totalAmount);
            ordCmd.Parameters.AddWithValue("@payMethod", paymentMethod);
            ordCmd.Parameters.AddWithValue("@payStatus", paymentStatus);
            ordCmd.ExecuteNonQuery();
        }

        return (shipperId, orderId, hub.HubId);
    }

    [Fact]
    public void T13_AcceptAndConfirmPickup_UpdatesOrderToPickedUpAndShipperToOnDelivery()
    {
        var (shipperId, orderId, _) = SeedDeliveryScenario(orderStatus: "ReadyForPickup");

        var accepted = _controller.AcceptDeliveryTask(shipperId, orderId);
        Assert.Equal(shipperId, accepted.ShipperId);
        Assert.Equal("AssignedToOrder", _shipperDAO.FindById(shipperId)!.AvailabilityStatus);

        var pickedUp = _controller.ConfirmOrderPickup(shipperId, orderId);
        Assert.True(pickedUp);

        var orderInDb = _orderDAO.FindById(orderId);
        var shipperInDb = _shipperDAO.FindById(shipperId);

        Assert.NotNull(orderInDb);
        Assert.NotNull(shipperInDb);
        Assert.Equal("PickedUp", orderInDb.OrderStatus);
        Assert.Equal(shipperId, orderInDb.ShipperId);
        Assert.Equal("OnDelivery", shipperInDb.AvailabilityStatus);
    }

    [Fact]
    public void T14_ConfirmCashCollected_BlocksMismatchAndUpdatesCashLedgerOnExactAmount()
    {
        var (shipperId, orderId, _) = SeedDeliveryScenario(
            orderStatus: "ReadyForPickup",
            totalAmount: 155000m,
            paymentMethod: "COD",
            paymentStatus: "PendingCOD");

        _controller.AcceptDeliveryTask(shipperId, orderId);
        _controller.ConfirmOrderPickup(shipperId, orderId);

        // Wrong COD amount (140,000 vs 155,000) must be rejected
        Assert.Throws<InvalidOperationException>(() =>
            _controller.ConfirmCashCollected(shipperId, orderId, 140000m));

        Assert.Equal("PendingCOD", _orderDAO.FindById(orderId)!.PaymentStatus);
        Assert.Equal(0m, _shipperDAO.FindById(shipperId)!.CashOnHand);

        // Exact COD amount (155,000) succeeds
        var updatedCash = _controller.ConfirmCashCollected(shipperId, orderId, 155000m);

        Assert.Equal(155000m, updatedCash);
        Assert.Equal("PaidCOD", _orderDAO.FindById(orderId)!.PaymentStatus);
        Assert.Equal(155000m, _shipperDAO.FindById(shipperId)!.CashOnHand);
    }

    [Fact]
    public void DeliveryGuards_BlockUnpaidCodCompletionAndPickupOfNonReadyOrder()
    {
        // Guard 1: Cannot pick up an order that is not ReadyForPickup
        var (shipper1, notReadyOrderId, _) = SeedDeliveryScenario(orderStatus: "InPreparation");
        Assert.Throws<InvalidOperationException>(() =>
            _controller.ConfirmOrderPickup(shipper1, notReadyOrderId));

        // Guard 2: Cannot complete COD delivery before paymentStatus is PaidCOD
        var (shipper2, codOrderId, _) = SeedDeliveryScenario(
            orderStatus: "ReadyForPickup",
            totalAmount: 155000m,
            paymentMethod: "COD",
            paymentStatus: "PendingCOD");

        _controller.AcceptDeliveryTask(shipper2, codOrderId);
        _controller.ConfirmOrderPickup(shipper2, codOrderId);

        Assert.Throws<InvalidOperationException>(() =>
            _controller.ConfirmDeliverySuccess(shipper2, codOrderId));

        Assert.Equal("PickedUp", _orderDAO.FindById(codOrderId)!.OrderStatus);
    }

    [Fact]
    public void ConfirmDeliverySuccess_MarksDeliveredReleasesShipperAndDecrementsHubWorkload()
    {
        var (shipperId, orderId, hubId) = SeedDeliveryScenario(
            orderStatus: "ReadyForPickup",
            totalAmount: 155000m,
            paymentMethod: "COD",
            paymentStatus: "PendingCOD",
            initialHubActiveOrders: 6);

        _controller.AcceptDeliveryTask(shipperId, orderId);
        _controller.ConfirmOrderPickup(shipperId, orderId);
        _controller.ConfirmCashCollected(shipperId, orderId, 155000m);

        var completed = _controller.ConfirmDeliverySuccess(shipperId, orderId);

        var orderInDb = _orderDAO.FindById(orderId)!;
        var shipperInDb = _shipperDAO.FindById(shipperId)!;
        var hubInDb = _hubDAO.FindById(hubId)!;

        Assert.Equal("Delivered", completed.OrderStatus);
        Assert.Equal("Delivered", orderInDb.OrderStatus);
        Assert.False(string.IsNullOrWhiteSpace(orderInDb.DeliveredAt));
        Assert.Equal("Available", shipperInDb.AvailabilityStatus);
        Assert.Equal(155000m, shipperInDb.CashOnHand);
        Assert.Equal(5, hubInDb.CurrentActiveOrders);
    }

    [Fact]
    public void T15_ReportDeliveryFailure_EnforcesGuardRulesAndUpdatesOrderShipperAndHub()
    {
        var (shipperId, orderId, hubId) = SeedDeliveryScenario(
            orderStatus: "ReadyForPickup",
            totalAmount: 155000m,
            paymentMethod: "COD",
            paymentStatus: "PendingCOD",
            initialHubActiveOrders: 4);

        _controller.AcceptDeliveryTask(shipperId, orderId);
        _controller.ConfirmOrderPickup(shipperId, orderId);

        // Blocked: 3 calls but only 4 minutes wait (< 10 mins)
        Assert.Throws<InvalidOperationException>(() =>
            _controller.ReportDeliveryFailure(shipperId, orderId, "CustomerNoAnswer", contactAttempts: 3, waitMinutes: 4));

        // Blocked: 2 calls (< 3 attempts) even with 15 minutes wait
        Assert.Throws<InvalidOperationException>(() =>
            _controller.ReportDeliveryFailure(shipperId, orderId, "CustomerNoAnswer", contactAttempts: 2, waitMinutes: 15));

        Assert.Equal("PickedUp", _orderDAO.FindById(orderId)!.OrderStatus);
        Assert.Equal(4, _hubDAO.FindById(hubId)!.CurrentActiveOrders);

        // Allowed: >= 3 attempts AND >= 10 mins wait
        var failed = _controller.ReportDeliveryFailure(
            shipperId, orderId, "CustomerNoAnswer", contactAttempts: 3, waitMinutes: 10);

        var orderInDb = _orderDAO.FindById(orderId)!;
        var shipperInDb = _shipperDAO.FindById(shipperId)!;
        var hubInDb = _hubDAO.FindById(hubId)!;

        Assert.Equal("DeliveryFailed", failed.OrderStatus);
        Assert.Equal("DeliveryFailed", orderInDb.OrderStatus);
        Assert.Equal("CustomerNoAnswer", orderInDb.FailureReason);
        Assert.Equal("Available", shipperInDb.AvailabilityStatus);
        Assert.Equal(3, hubInDb.CurrentActiveOrders);
    }
}