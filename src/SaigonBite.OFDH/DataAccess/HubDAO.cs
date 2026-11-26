using Microsoft.Data.Sqlite;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.DataAccess;

public class HubDAO
{
    private readonly DatabaseConnection _db;

    public HubDAO(DatabaseConnection db)
    {
        _db = db;
    }

    public List<Hub> FindAll()
    {
        var hubs = new List<Hub>();
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT hub_id, hub_name, district, max_active_orders,
                   current_active_orders, max_delivery_distance_km, hub_status
            FROM Hubs
            ORDER BY hub_id;
            """;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            hubs.Add(MapHub(reader));
        }
        return hubs;
    }

    public Hub? FindById(string hubId)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT hub_id, hub_name, district, max_active_orders,
                   current_active_orders, max_delivery_distance_km, hub_status
            FROM Hubs
            WHERE hub_id = @hubId;
            """;
        cmd.Parameters.AddWithValue("@hubId", hubId);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapHub(reader) : null;
    }

    private static Hub MapHub(SqliteDataReader reader)
    {
        return new Hub
        {
            HubId = reader.GetString(0),
            HubName = reader.GetString(1),
            District = reader.GetString(2),
            MaxActiveOrders = reader.GetInt32(3),
            CurrentActiveOrders = reader.GetInt32(4),
            MaxDeliveryDistanceKm = Convert.ToDecimal(reader.GetDouble(5)),
            HubStatus = reader.GetString(6)
        };
    }
}
