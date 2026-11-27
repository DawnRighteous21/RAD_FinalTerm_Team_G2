using Microsoft.Data.Sqlite;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.DataAccess;

public class ShipperDAO
{
    private readonly DatabaseConnection _db;

    public ShipperDAO(DatabaseConnection db)
    {
        _db = db;
    }

    public List<Shipper> FindAll()
    {
        var shippers = new List<Shipper>();
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT shipper_id, full_name, phone_number, vehicle_plate,
                   availability_status, cash_on_hand
            FROM Shippers
            ORDER BY shipper_id;
            """;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            shippers.Add(MapShipper(reader));
        }
        return shippers;
    }

    public Shipper? FindById(string shipperId)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT shipper_id, full_name, phone_number, vehicle_plate,
                   availability_status, cash_on_hand
            FROM Shippers
            WHERE shipper_id = @shipperId;
            """;
        cmd.Parameters.AddWithValue("@shipperId", shipperId);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapShipper(reader) : null;
    }

    public bool UpdateShipperStateAndCash(Shipper shipper)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Shippers
            SET availability_status = @status,
                cash_on_hand = @cash
            WHERE shipper_id = @shipperId;
            """;
        cmd.Parameters.AddWithValue("@status", shipper.AvailabilityStatus);
        cmd.Parameters.AddWithValue("@cash", shipper.CashOnHand);
        cmd.Parameters.AddWithValue("@shipperId", shipper.ShipperId);

        return cmd.ExecuteNonQuery() > 0;
    }

    private static Shipper MapShipper(SqliteDataReader reader)
    {
        return new Shipper
        {
            ShipperId = reader.GetString(0),
            FullName = reader.GetString(1),
            PhoneNumber = reader.GetString(2),
            VehiclePlate = reader.GetString(3),
            AvailabilityStatus = reader.GetString(4),
            CashOnHand = Convert.ToDecimal(reader.GetDouble(5))
        };
    }
}
