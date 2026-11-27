// Author: 524H0015 (Ly Tin Hoa)
using Microsoft.Data.Sqlite;
using SaigonBite.OFDH.Controllers;
using SaigonBite.OFDH.DataAccess;

namespace SaigonBite.OFDH.Tests;

public class KitchenPrepTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly DatabaseConnection _db;
    private readonly KitchenPrepController _controller;
    private readonly OrderDAO _orderDao;
    private readonly HubDAO _hubDao;

    public KitchenPrepTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"saigonbite_uc8_test_{Guid.NewGuid():N}.db");
        _db = new DatabaseConnection(_tempDbPath);
        _db.InitializeSchema();
        _controller = new KitchenPrepController(_db);
        _orderDao = new OrderDAO(_db);
        _hubDao = new HubDAO(_db);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_tempDbPath))
            File.Delete(_tempDbPath);
    }

    private void InsertPlacedOrder(string orderId, string hubId, string orderDateTime, string paymentMethod = "Card", string paymentStatus = "Paid")
    {
        using var conn = _db.GetConnection();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO Orders (
                    order_id, customer_id, hub_id, address_id, voucher_code, shipper_id,
                    order_date_time, order_status, subtotal_amount, delivery_fee,
                    discount_amount, total_amount, payment_method, payment_status
                )
                VALUES (
                    @orderId, 'CUST-001', @hubId, 'ADDR-001', NULL, NULL,
                    @dt, 'Placed', 120000, 5000, 0, 125000, @payMethod, @payStatus
                );
                """;
            cmd.Parameters.AddWithValue("@orderId", orderId);
            cmd.Parameters.AddWithValue("@hubId", hubId);
            cmd.Parameters.AddWithValue("@dt", orderDateTime);
            cmd.Parameters.AddWithValue("@payMethod", paymentMethod);
            cmd.Parameters.AddWithValue("@payStatus", paymentStatus);
            cmd.ExecuteNonQuery();
        }

        using var itemCmd = conn.CreateCommand();
        itemCmd.CommandText = """
            INSERT INTO OrderItems (
                order_item_id, order_id, item_id, quantity, unit_price_at_order, line_total, special_instructions
            )
            VALUES (@oiId, @orderId, 'ITEM-01', 2, 60000, 120000, 'Less rice');
            """;
        itemCmd.Parameters.AddWithValue("@oiId", $"OI-{orderId}");
        itemCmd.Parameters.AddWithValue("@orderId", orderId);
        itemCmd.ExecuteNonQuery();
    }

    [Fact]
    public void T09_GetKitchenQueue_ReturnsFifoOrder_AndConfirmLocksCustomerCancellation()
    {
        InsertPlacedOrder("ORD-1004", "HUB-D1", "2026-11-21 19:18:00");

        var queue = _controller.GetKitchenQueue("HUB-D1");
        Assert.True(queue.Count >= 3);
        Assert.Equal("ORD-1002", queue[0].OrderId);
        Assert.Equal("ORD-1003", queue[1].OrderId);
        Assert.Equal("ORD-1004", queue[2].OrderId);

        var (confirmed, alert) = _controller.ConfirmOrder("ORD-1004", "STF-K01");
        Assert.Equal("ConfirmedByKitchen", confirmed.OrderStatus);
        Assert.True(confirmed.IsCancellationLocked);
        Assert.Contains("ORD-1004", alert.Detail);

        var reloaded = _orderDao.FindById("ORD-1004");
        Assert.NotNull(reloaded);
        Assert.Equal("ConfirmedByKitchen", reloaded.OrderStatus);
    }

    [Fact]
    public void T10_RejectOrder_BlocksEmptyOtherReason_AndPersistsValidRejectionWithRefund()
    {
        InsertPlacedOrder("ORD-1005", "HUB-D1", "2026-11-21 19:20:00", paymentMethod: "Card", paymentStatus: "Paid");
        var initialHubOrders = _hubDao.FindById("HUB-D1")!.CurrentActiveOrders;

        Assert.Throws<ArgumentException>(() =>
            _controller.RejectOrder("ORD-1005", "Other", "", "STF-K01"));

        Assert.Throws<ArgumentException>(() =>
            _controller.RejectOrder("ORD-1005", "Other", "   ", "STF-K01"));

        Assert.Equal("Placed", _orderDao.FindById("ORD-1005")!.OrderStatus);

        var (rejected, alert) = _controller.RejectOrder(
            "ORD-1005",
            "Other",
            "Out of pork ribs for evening shift",
            "STF-K01");

        Assert.Equal("Rejected", rejected.OrderStatus);
        Assert.Equal("Refunded", rejected.PaymentStatus);
        Assert.Equal("Other: Out of pork ribs for evening shift", rejected.FailureReason);
        Assert.Contains("ORD-1005", alert.Detail);

        var persisted = _orderDao.FindById("ORD-1005")!;
        Assert.Equal("Rejected", persisted.OrderStatus);
        Assert.Equal("Refunded", persisted.PaymentStatus);
        Assert.Equal(Math.Max(0, initialHubOrders - 1), _hubDao.FindById("HUB-D1")!.CurrentActiveOrders);
    }

    [Fact]
    public void T11_ToggleHubMenuStock_UpdatesSqliteAvailabilityImmediately()
    {
        var (outOfStock, alertOut) = _controller.MarkHubItemUnavailable("HUB-D1", "ITEM-03");
        Assert.False(outOfStock.IsAvailableInStock);
        Assert.Contains("ITEM-03", alertOut.Detail);

        var stockList = _controller.GetHubMenuStock("HUB-D1");
        Assert.False(stockList.First(i => i.ItemId == "ITEM-03").IsAvailableInStock);

        var (restocked, alertIn) = _controller.RestockHubItem("HUB-D1", "ITEM-03");
        Assert.True(restocked.IsAvailableInStock);
        Assert.Contains("ITEM-03", alertIn.Detail);

        var refreshed = _controller.GetHubMenuStock("HUB-D1");
        Assert.True(refreshed.First(i => i.ItemId == "ITEM-03").IsAvailableInStock);
    }

    [Fact]
    public void T12_StartPreparationAndMarkReady_PersistsTimestampsAndAlertsShipper()
    {
        var (inPrep, prepAlert) = _controller.StartPreparation("ORD-1002", "STF-K01");
        Assert.Equal("InPreparation", inPrep.OrderStatus);
        Assert.False(string.IsNullOrWhiteSpace(inPrep.PrepStartedAt));
        Assert.Contains("ORD-1002", prepAlert.Detail);

        var (ready, readyAlert) = _controller.MarkOrderReady("ORD-1002", "STF-K01");
        Assert.Equal("ReadyForPickup", ready.OrderStatus);
        Assert.False(string.IsNullOrWhiteSpace(ready.ReadyAt));
        Assert.Contains("SHP-02", readyAlert.Headline);
        Assert.Contains("ORD-1002", readyAlert.Detail);

        var persisted = _orderDao.FindById("ORD-1002")!;
        Assert.Equal("ReadyForPickup", persisted.OrderStatus);
        Assert.False(string.IsNullOrWhiteSpace(persisted.PrepStartedAt));
        Assert.False(string.IsNullOrWhiteSpace(persisted.ReadyAt));
    }

    [Fact]
    public void StateGuards_BlockSkippingStepsOrRejectingConfirmedOrder()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _controller.MarkOrderReady("ORD-1002", "STF-K01"));

        Assert.Throws<InvalidOperationException>(() =>
            _controller.RejectOrder("ORD-1002", "KitchenOverloaded", null, "STF-K01"));
    }
}
