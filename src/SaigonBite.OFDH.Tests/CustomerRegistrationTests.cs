// Author: 524H0134 (Ha Du Tuan)
using Microsoft.Data.Sqlite;
using SaigonBite.OFDH.Controllers;
using SaigonBite.OFDH.DataAccess;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.Tests;

public class CustomerRegistrationTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly DatabaseConnection _db;
    private readonly CustomerRegistrationController _controller;

    public CustomerRegistrationTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"saigonbite_uc1_test_{Guid.NewGuid():N}.db");
        _db = new DatabaseConnection(_tempDbPath);
        _db.InitializeSchema();
        _controller = new CustomerRegistrationController(_db);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_tempDbPath))
            File.Delete(_tempDbPath);
    }

    [Fact]
    public void T01_RegisterLocalCustomer_PersistsActiveProfileWithNullPassport()
    {
        var created = _controller.RegisterCustomer(
            type: "Local",
            fullName: "Tran Minh Khoa",
            email: "khoa.local.test@saigonbite.vn",
            vnPhone: "0909888777",
            wallet: "MoMo",
            passport: null,
            nationality: null,
            hotelName: null,
            roomNumber: null);

        var local = Assert.IsType<LocalCustomer>(created);
        Assert.StartsWith("CUST-", local.CustomerId);
        Assert.Equal("Active", local.Status);
        Assert.Equal("0909888777", local.VnPhoneNumber);
        Assert.Equal("MoMo", local.PreferredLocalWallet);

        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT customer_type, status, vn_phone_number, preferred_local_wallet, passport_number
            FROM Customers
            WHERE customer_id = @id;
            """;
        cmd.Parameters.AddWithValue("@id", local.CustomerId);

        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("Local", reader.GetString(0));
        Assert.Equal("Active", reader.GetString(1));
        Assert.Equal("0909888777", reader.GetString(2));
        Assert.Equal("MoMo", reader.GetString(3));
        Assert.True(reader.IsDBNull(4));
    }

    [Fact]
    public void T02_RegisterForeignTourist_PersistsActiveProfileWithNullVnPhone()
    {
        var created = _controller.RegisterCustomer(
            type: "Tourist",
            fullName: "Emily Carter",
            email: "emily.tourist.test@example.com",
            vnPhone: null,
            wallet: null,
            passport: "P9876543",
            nationality: "USA",
            hotelName: "Rex Hotel Saigon",
            roomNumber: "1204");

        var tourist = Assert.IsType<ForeignTourist>(created);
        Assert.Equal("Active", tourist.Status);
        Assert.Equal("P9876543", tourist.PassportNumber);
        Assert.Equal("USA", tourist.Nationality);
        Assert.Equal("Rex Hotel Saigon", tourist.HotelName);
        Assert.Equal("1204", tourist.RoomNumber);

        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT customer_type, status, vn_phone_number, preferred_local_wallet,
                   passport_number, hotel_name, room_number
            FROM Customers
            WHERE customer_id = @id;
            """;
        cmd.Parameters.AddWithValue("@id", tourist.CustomerId);

        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("Tourist", reader.GetString(0));
        Assert.Equal("Active", reader.GetString(1));
        Assert.True(reader.IsDBNull(2));
        Assert.True(reader.IsDBNull(3));
        Assert.Equal("P9876543", reader.GetString(4));
        Assert.Equal("Rex Hotel Saigon", reader.GetString(5));
        Assert.Equal("1204", reader.GetString(6));
    }

    [Fact]
    public void RegisterCustomer_RejectsDuplicateEmailPhoneOrPassport()
    {
        _controller.RegisterCustomer(
            "Local", "Nguyen Van Mot", "dup.local@test.vn",
            "0909111222", "VNPay", null, null, null, null);

        _controller.RegisterCustomer(
            "Tourist", "John Doe", "dup.tourist@test.com",
            null, null, "AB123456", "UK", "Caravelle", "502");

        // Duplicate Email
        Assert.Throws<InvalidOperationException>(() =>
            _controller.RegisterCustomer(
                "Local", "Other User", "dup.local@test.vn",
                "0909333444", "MoMo", null, null, null, null));

        // Duplicate VN Phone
        Assert.Throws<InvalidOperationException>(() =>
            _controller.RegisterCustomer(
                "Local", "Other Local", "unique.local@test.vn",
                "0909111222", "ZaloPay", null, null, null, null));

        // Duplicate Passport
        Assert.Throws<InvalidOperationException>(() =>
            _controller.RegisterCustomer(
                "Tourist", "Other Tourist", "unique.tourist@test.com",
                null, null, "AB123456", "Canada", "Park Hyatt", "808"));
    }

    [Fact]
    public void T03_AddDeliveryAddress_Within7KmIsVerified_AndBeyond7KmIsOutOfRange()
    {
        var customer = _controller.RegisterCustomer(
            "Local", "Le Thi Hoa", "hoa.addr@test.vn",
            "0912345678", "MoMo", null, null, null, null);

        var verifiedAddr = _controller.AddDeliveryAddress(
            customer.CustomerId,
            "Le Thi Hoa",
            "123 Nguyen Hue",
            "District 1",
            "Bitexco Tower",
            isDefault: true);

        Assert.True(verifiedAddr.DistanceKmFromHub <= 7.0m);
        Assert.Equal("Verified", verifiedAddr.Status);

        var outOfRangeAddr = _controller.AddDeliveryAddress(
            customer.CustomerId,
            "Le Thi Hoa",
            "99 Tinh Lo 8",
            "Cu Chi",
            "Cu Chi Town",
            isDefault: false);

        Assert.True(outOfRangeAddr.DistanceKmFromHub > 7.0m);
        Assert.Equal("OutOfRange", outOfRangeAddr.Status);

        var savedList = _controller.GetAddressesByCustomer(customer.CustomerId);
        Assert.Equal(2, savedList.Count);
        Assert.Contains(savedList, a => a.AddressId == verifiedAddr.AddressId && a.Status == "Verified");
        Assert.Contains(savedList, a => a.AddressId == outOfRangeAddr.AddressId && a.Status == "OutOfRange");
    }

    [Fact]
    public void T04_UpdateAddressRecalculatesStatus_AndArchiveSoftDeletesAddress()
    {
        var customer = _controller.RegisterCustomer(
            "Local", "Pham Gia Huy", "huy.addr@test.vn",
            "0918777666", "MoMo", null, null, null, null);

        var address = _controller.AddDeliveryAddress(
            customer.CustomerId,
            "Pham Gia Huy",
            "88 Quoc Lo 22",
            "Cu Chi",
            null,
            isDefault: true);

        Assert.Equal("OutOfRange", address.Status);

        var updated = _controller.UpdateOrArchiveAddress(
            customer.CustomerId,
            address.AddressId,
            "UPDATE",
            recipientName: "Pham Gia Huy",
            street: "45 Le Loi",
            district: "District 1",
            landmarkOrHotel: "Saigon Centre",
            isDefault: true);

        Assert.True(updated);

        var addressDAO = new DeliveryAddressDAO(_db);
        var reloadedAfterUpdate = addressDAO.FindById(address.AddressId);
        Assert.NotNull(reloadedAfterUpdate);
        Assert.True(reloadedAfterUpdate.DistanceKmFromHub <= 7.0m);
        Assert.Equal("Verified", reloadedAfterUpdate.Status);
        Assert.Equal("District 1", reloadedAfterUpdate.District);

        var archived = _controller.UpdateOrArchiveAddress(
            customer.CustomerId,
            address.AddressId,
            "ARCHIVE");

        Assert.True(archived);

        var reloadedAfterArchive = addressDAO.FindById(address.AddressId);
        Assert.NotNull(reloadedAfterArchive);
        Assert.Equal("Archived", reloadedAfterArchive.Status);
        Assert.False(reloadedAfterArchive.IsDefault);
    }

    [Fact]
    public void T04_ArchiveAddress_BlockedWhenAttachedToActiveOrder()
    {
        var customer = _controller.RegisterCustomer(
            "Local", "Vo Minh Quan", "quan.activeorder@test.vn",
            "0933555444", "VNPay", null, null, null, null);

        var address = _controller.AddDeliveryAddress(
            customer.CustomerId,
            "Vo Minh Quan",
            "200 Vo Van Tan",
            "District 3",
            null,
            isDefault: true);

        var hub = new HubDAO(_db).FindAll().First();

        using (var conn = _db.GetConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO Orders (
                    order_id, customer_id, hub_id, address_id, voucher_code, shipper_id,
                    order_date_time, order_status, subtotal_amount, delivery_fee,
                    discount_amount, total_amount, payment_method, payment_status
                )
                VALUES (
                    'ORD-ACT-01', @custId, @hubId, @addrId, NULL, NULL,
                    '2026-11-27 12:00:00', 'InPreparation', 120000, 15000,
                    0, 135000, 'COD', 'PendingCOD'
                );
                """;
            cmd.Parameters.AddWithValue("@custId", customer.CustomerId);
            cmd.Parameters.AddWithValue("@hubId", hub.HubId);
            cmd.Parameters.AddWithValue("@addrId", address.AddressId);
            cmd.ExecuteNonQuery();
        }

        Assert.Throws<InvalidOperationException>(() =>
            _controller.UpdateOrArchiveAddress(customer.CustomerId, address.AddressId, "ARCHIVE"));

        var stillVerified = new DeliveryAddressDAO(_db).FindById(address.AddressId);
        Assert.NotNull(stillVerified);
        Assert.Equal("Verified", stillVerified.Status);
    }
}