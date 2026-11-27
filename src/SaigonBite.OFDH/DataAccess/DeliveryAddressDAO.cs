// Author: 524H0134 (Ha Du Tuan)
using Microsoft.Data.Sqlite;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.DataAccess;

public class DeliveryAddressDAO
{
    private readonly DatabaseConnection _db;

    public DeliveryAddressDAO(DatabaseConnection db)
    {
        _db = db;
    }

    public List<DeliveryAddress> FindByCustomerId(string customerId)
    {
        var addresses = new List<DeliveryAddress>();
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT address_id, customer_id, recipient_name, street_address,
                   district, landmark_or_hotel, distance_km_from_hub, is_default, status
            FROM DeliveryAddresses
            WHERE customer_id = @customerId
            ORDER BY CASE WHEN status = 'Archived' THEN 1 ELSE 0 END,
                     is_default DESC,
                     address_id ASC;
            """;
        cmd.Parameters.AddWithValue("@customerId", customerId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            addresses.Add(MapAddress(reader));
        }
        return addresses;
    }

    public DeliveryAddress? FindById(string addressId)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT address_id, customer_id, recipient_name, street_address,
                   district, landmark_or_hotel, distance_km_from_hub, is_default, status
            FROM DeliveryAddresses
            WHERE address_id = @addressId;
            """;
        cmd.Parameters.AddWithValue("@addressId", addressId);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapAddress(reader) : null;
    }

    public bool HasActiveOrders(string addressId)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*)
            FROM Orders
            WHERE address_id = @addressId
              AND order_status IN ('Placed', 'ConfirmedByKitchen', 'InPreparation', 'ReadyForPickup', 'PickedUp');
            """;
        cmd.Parameters.AddWithValue("@addressId", addressId);

        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    public string GenerateNextAddressId()
    {
        var maxNum = 0;
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT address_id FROM DeliveryAddresses;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            if (id.StartsWith("ADDR-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(id[5..], out var num) &&
                num > maxNum)
            {
                maxNum = num;
            }
        }
        return $"ADDR-{maxNum + 1:D3}";
    }

    public bool InsertAddress(string customerId, DeliveryAddress address)
    {
        using var conn = _db.GetConnection();
        using var tx = conn.BeginTransaction();

        if (address.IsDefault)
        {
            using var resetCmd = conn.CreateCommand();
            resetCmd.Transaction = tx;
            resetCmd.CommandText = "UPDATE DeliveryAddresses SET is_default = 0 WHERE customer_id = @customerId;";
            resetCmd.Parameters.AddWithValue("@customerId", customerId);
            resetCmd.ExecuteNonQuery();
        }

        using var insertCmd = conn.CreateCommand();
        insertCmd.Transaction = tx;
        insertCmd.CommandText = """
            INSERT INTO DeliveryAddresses (
                address_id, customer_id, recipient_name, street_address,
                district, landmark_or_hotel, distance_km_from_hub, is_default, status
            )
            VALUES (
                @addressId, @customerId, @recipientName, @streetAddress,
                @district, @landmark, @distanceKm, @isDefault, @status
            );
            """;
        insertCmd.Parameters.AddWithValue("@addressId", address.AddressId);
        insertCmd.Parameters.AddWithValue("@customerId", customerId);
        insertCmd.Parameters.AddWithValue("@recipientName", address.RecipientName);
        insertCmd.Parameters.AddWithValue("@streetAddress", address.StreetAddress);
        insertCmd.Parameters.AddWithValue("@district", address.District);
        insertCmd.Parameters.AddWithValue("@landmark", (object?)address.LandmarkOrHotel ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@distanceKm", address.DistanceKmFromHub);
        insertCmd.Parameters.AddWithValue("@isDefault", address.IsDefault ? 1 : 0);
        insertCmd.Parameters.AddWithValue("@status", address.Status);

        var rows = insertCmd.ExecuteNonQuery();
        tx.Commit();
        return rows > 0;
    }

    public bool UpdateAddress(DeliveryAddress address)
    {
        using var conn = _db.GetConnection();
        using var tx = conn.BeginTransaction();

        if (address.IsDefault)
        {
            using var resetCmd = conn.CreateCommand();
            resetCmd.Transaction = tx;
            resetCmd.CommandText = """
                UPDATE DeliveryAddresses
                SET is_default = 0
                WHERE customer_id = @customerId AND address_id != @addressId;
                """;
            resetCmd.Parameters.AddWithValue("@customerId", address.CustomerId);
            resetCmd.Parameters.AddWithValue("@addressId", address.AddressId);
            resetCmd.ExecuteNonQuery();
        }

        using var updateCmd = conn.CreateCommand();
        updateCmd.Transaction = tx;
        updateCmd.CommandText = """
            UPDATE DeliveryAddresses
            SET recipient_name = @recipientName,
                street_address = @streetAddress,
                district = @district,
                landmark_or_hotel = @landmark,
                distance_km_from_hub = @distanceKm,
                is_default = @isDefault,
                status = @status
            WHERE address_id = @addressId;
            """;
        updateCmd.Parameters.AddWithValue("@recipientName", address.RecipientName);
        updateCmd.Parameters.AddWithValue("@streetAddress", address.StreetAddress);
        updateCmd.Parameters.AddWithValue("@district", address.District);
        updateCmd.Parameters.AddWithValue("@landmark", (object?)address.LandmarkOrHotel ?? DBNull.Value);
        updateCmd.Parameters.AddWithValue("@distanceKm", address.DistanceKmFromHub);
        updateCmd.Parameters.AddWithValue("@isDefault", address.IsDefault ? 1 : 0);
        updateCmd.Parameters.AddWithValue("@status", address.Status);
        updateCmd.Parameters.AddWithValue("@addressId", address.AddressId);

        var rows = updateCmd.ExecuteNonQuery();
        tx.Commit();
        return rows > 0;
    }

    public bool ArchiveAddress(string addressId)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE DeliveryAddresses
            SET status = 'Archived',
                is_default = 0
            WHERE address_id = @addressId;
            """;
        cmd.Parameters.AddWithValue("@addressId", addressId);

        return cmd.ExecuteNonQuery() > 0;
    }

    private static DeliveryAddress MapAddress(SqliteDataReader reader)
    {
        return new DeliveryAddress
        {
            AddressId = reader.GetString(0),
            CustomerId = reader.GetString(1),
            RecipientName = reader.GetString(2),
            StreetAddress = reader.GetString(3),
            District = reader.GetString(4),
            LandmarkOrHotel = reader.IsDBNull(5) ? null : reader.GetString(5),
            DistanceKmFromHub = Convert.ToDecimal(reader.GetDouble(6)),
            IsDefault = reader.GetInt32(7) == 1,
            Status = reader.GetString(8)
        };
    }
}   