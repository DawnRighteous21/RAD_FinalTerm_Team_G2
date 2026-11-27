using System.IO;
using Microsoft.Data.Sqlite;
using SaigonBite.OFDH.Controllers;
using SaigonBite.OFDH.DataAccess;

namespace SaigonBite.OFDH.Tests;

public class PlaceOrderTests
{
    private static (DatabaseConnection Db, string Path) CreateTempDb()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"saigonbite_uc3_{Guid.NewGuid():N}.db");
        var db = new DatabaseConnection(tempPath);
        db.InitializeSchema();
        return (db, tempPath);
    }

    private static void Cleanup(string path)
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(path)) File.Delete(path);
    }

    [Fact]
    public void InitiateCheckout_BusyHub_ThrowsInvalidOperationException()
    {
        var (db, path) = CreateTempDb();
        try
        {
            var ctrl = new PlaceOrderController(db);
            Assert.Throws<InvalidOperationException>(() =>
                ctrl.InitiateCheckout("CUST-001", "HUB-D7", "ADDR-001"));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void InitiateCheckout_QuietHub_AppliesTenThousandVndDeliveryDiscount()
    {
        var (db, path) = CreateTempDb();
        try
        {
            var ctrl = new PlaceOrderController(db);
            Assert.True(ctrl.InitiateCheckout("CUST-001", "HUB-D1", "ADDR-001"));
            Assert.Equal(5000m, ctrl.CurrentOrder!.DeliveryFee);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void InitiateCheckout_AddressExceedsSevenKmRadius_ThrowsInvalidOperationException()
    {
        var (db, path) = CreateTempDb();
        try
        {
            using (var conn = db.GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO DeliveryAddresses
                    (address_id, customer_id, recipient_name, street_address, district, distance_km_from_hub, is_default, status)
                    VALUES ('ADDR-999', 'CUST-001', 'Nguyen Van An', 'Far Street', 'District 9', 9.5, 0, 'Verified');
                    """;
                cmd.ExecuteNonQuery();
            }

            var ctrl = new PlaceOrderController(db);
            Assert.Throws<InvalidOperationException>(() =>
                ctrl.InitiateCheckout("CUST-001", "HUB-D3", "ADDR-999"));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void ConfirmAndPlaceOrder_LocalCodWithVoucher_PersistsOrderAndIncrementsHubCount()
    {
        var (db, path) = CreateTempDb();
        try
        {
            var ctrl = new PlaceOrderController(db);
            ctrl.InitiateCheckout("CUST-001", "HUB-D3", "ADDR-002");
            ctrl.AddItemToOrder("ITEM-01", 2, "Extra pickled veggies");
            ctrl.ApplyVoucher("VOU20");

            var placed = ctrl.ConfirmAndPlaceOrder("COD");

            Assert.Equal("ORD-1004", placed.OrderId);
            Assert.Equal("Placed", placed.OrderStatus);
            Assert.Equal("PendingCOD", placed.PaymentStatus);
            Assert.Equal(120000m, placed.SubtotalAmount);
            Assert.Equal(25000m, placed.DeliveryFee);
            Assert.Equal(20000m, placed.DiscountAmount);
            Assert.Equal(125000m, placed.TotalAmount);

            var saved = new OrderDAO(db).FindByIdWithItems(placed.OrderId);
            Assert.NotNull(saved);
            Assert.Single(saved!.Items);
            Assert.Equal("Extra pickled veggies", saved.Items[0].SpecialInstructions);

            var updatedHub = new HubDAO(db).FindById("HUB-D3");
            Assert.Equal(7, updatedHub!.CurrentActiveOrders);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void ConfirmAndPlaceOrder_ForeignTouristWithCod_ThrowsInvalidOperationException()
    {
        var (db, path) = CreateTempDb();
        try
        {
            var ctrl = new PlaceOrderController(db);
            ctrl.InitiateCheckout("CUST-002", "HUB-D1", "ADDR-003");
            ctrl.AddItemToOrder("ITEM-02", 1, "No cilantro");

            Assert.Throws<InvalidOperationException>(() => ctrl.ConfirmAndPlaceOrder("COD"));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void ConfirmAndPlaceOrder_ForeignTouristWithCard_MarksPaidAndIncrementsHubCount()
    {
        var (db, path) = CreateTempDb();
        try
        {
            var ctrl = new PlaceOrderController(db);
            ctrl.InitiateCheckout("CUST-002", "HUB-D1", "ADDR-003");
            ctrl.AddItemToOrder("ITEM-02", 1, "No cilantro");

            var placed = ctrl.ConfirmAndPlaceOrder("Card", "VISA-4242");

            Assert.Equal("Placed", placed.OrderStatus);
            Assert.Equal("Paid", placed.PaymentStatus);
            Assert.Equal(80000m, placed.TotalAmount);
            Assert.NotNull(ctrl.LastTransactionRef);
            Assert.StartsWith("TXN-CARD-", ctrl.LastTransactionRef);

            var updatedHub = new HubDAO(db).FindById("HUB-D1");
            Assert.Equal(3, updatedHub!.CurrentActiveOrders);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void ApplyVoucher_ExpiredOrBelowMinimumSubtotal_ThrowsInvalidOperationException()
    {
        var (db, path) = CreateTempDb();
        try
        {
            var ctrl = new PlaceOrderController(db);
            ctrl.InitiateCheckout("CUST-001", "HUB-D3", "ADDR-002");
            ctrl.AddItemToOrder("ITEM-03", 1);

            Assert.Throws<InvalidOperationException>(() => ctrl.ApplyVoucher("EXPIRED50"));
            Assert.Throws<InvalidOperationException>(() => ctrl.ApplyVoucher("VOU20"));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void AddItemToOrder_OutOfStockItemAtHub_ThrowsInvalidOperationException()
    {
        var (db, path) = CreateTempDb();
        try
        {
            var hmiDao = new HubMenuItemDAO(db);
            var stock = hmiDao.FindByHubAndItem("HUB-D1", "ITEM-02")!;
            stock.IsAvailableInStock = false;
            hmiDao.UpdateStockAvailability(stock);

            var ctrl = new PlaceOrderController(db);
            ctrl.InitiateCheckout("CUST-001", "HUB-D1", "ADDR-001");

            Assert.Throws<InvalidOperationException>(() => ctrl.AddItemToOrder("ITEM-02", 1));
        }
        finally { Cleanup(path); }
    }
}
