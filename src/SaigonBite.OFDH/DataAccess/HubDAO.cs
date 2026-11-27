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

    public Hub? FindNearestHub(string district)
    {
        using var conn = _db.GetConnection();

        using (var exactCmd = conn.CreateCommand())
        {
            exactCmd.CommandText = """
                SELECT hub_id, hub_name, district, max_active_orders,
                       current_active_orders, max_delivery_distance_km, hub_status
                FROM Hubs
                WHERE LOWER(district) = LOWER(@district)
                  AND hub_status != 'Closed'
                ORDER BY current_active_orders ASC, hub_id ASC
                LIMIT 1;
                """;
            exactCmd.Parameters.AddWithValue("@district", district.Trim());

            using var reader = exactCmd.ExecuteReader();
            if (reader.Read())
                return MapHub(reader);
        }

        using var fallbackCmd = conn.CreateCommand();
        fallbackCmd.CommandText = """
            SELECT hub_id, hub_name, district, max_active_orders,
                   current_active_orders, max_delivery_distance_km, hub_status
            FROM Hubs
            WHERE hub_status != 'Closed'
            ORDER BY current_active_orders ASC, hub_id ASC
            LIMIT 1;
            """;

        using var fallbackReader = fallbackCmd.ExecuteReader();
        return fallbackReader.Read() ? MapHub(fallbackReader) : null;
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
    
    public bool UpdateActiveOrderCount(string hubId, int newCount, string hubStatus)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Hubs
            SET current_active_orders = @count,
                hub_status = @status
            WHERE hub_id = @hubId;
            """;
        cmd.Parameters.AddWithValue("@count", Math.Max(0, newCount));
        cmd.Parameters.AddWithValue("@status", hubStatus);
        cmd.Parameters.AddWithValue("@hubId", hubId);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool UpdateActiveOrderCount(string hubId, int newCount)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Hubs
            SET current_active_orders = @count
            WHERE hub_id = @hubId;
            """;
        cmd.Parameters.AddWithValue("@count", Math.Max(0, newCount));
        cmd.Parameters.AddWithValue("@hubId", hubId);

        return cmd.ExecuteNonQuery() > 0;
    }
}