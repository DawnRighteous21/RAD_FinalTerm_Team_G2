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

    public Hub? FindNearestAvailableHub(string district, string? excludeHubId = null)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT hub_id, hub_name, district, max_active_orders,
                   current_active_orders, max_delivery_distance_km, hub_status
            FROM Hubs
            WHERE hub_status NOT IN ('Busy', 'Closed')
              AND current_active_orders < max_active_orders
              AND (@excludeId IS NULL OR hub_id != @excludeId)
            ORDER BY CASE WHEN LOWER(district) = LOWER(@district) THEN 0 ELSE 1 END,
                     current_active_orders ASC,
                     hub_id ASC
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@district", district.Trim());
        cmd.Parameters.AddWithValue("@excludeId", (object?)excludeHubId ?? DBNull.Value);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapHub(reader) : null;
    }

    public List<MenuItem> FindAvailableMenuItemsAtHub(string hubId)
    {
        var items = new List<MenuItem>();
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT m.item_id, m.brand_id, m.item_name, m.description_en,
                   m.description_vi, m.unit_price, m.category, m.is_active
            FROM HubMenuItems hmi
            JOIN MenuItems m ON hmi.item_id = m.item_id
            JOIN Brands b ON m.brand_id = b.brand_id
            WHERE hmi.hub_id = @hubId
              AND hmi.is_available_in_stock = 1
              AND m.is_active = 1
              AND b.is_active = 1
            ORDER BY m.item_id;
            """;
        cmd.Parameters.AddWithValue("@hubId", hubId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new MenuItem
            {
                ItemId = reader.GetString(0),
                BrandId = reader.GetString(1),
                ItemName = reader.GetString(2),
                DescriptionEn = reader.IsDBNull(3) ? null : reader.GetString(3),
                DescriptionVi = reader.IsDBNull(4) ? null : reader.GetString(4),
                UnitPrice = Convert.ToDecimal(reader.GetDouble(5)),
                Category = reader.GetString(6),
                IsActive = reader.GetInt32(7) == 1
            });
        }
        return items;
    }

    public (HubMenuItem Stock, MenuItem Item)? CheckItemStockAtHub(string hubId, string itemId)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT hmi.hub_id, hmi.item_id, hmi.is_available_in_stock, hmi.daily_prep_limit, hmi.last_updated,
                   m.brand_id, m.item_name, m.description_en, m.description_vi, m.unit_price, m.category, m.is_active
            FROM HubMenuItems hmi
            JOIN MenuItems m ON hmi.item_id = m.item_id
            JOIN Brands b ON m.brand_id = b.brand_id
            WHERE hmi.hub_id = @hubId
              AND hmi.item_id = @itemId
              AND hmi.is_available_in_stock = 1
              AND m.is_active = 1
              AND b.is_active = 1;
            """;
        cmd.Parameters.AddWithValue("@hubId", hubId);
        cmd.Parameters.AddWithValue("@itemId", itemId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var stock = new HubMenuItem
        {
            HubId = reader.GetString(0),
            ItemId = reader.GetString(1),
            IsAvailableInStock = reader.GetInt32(2) == 1,
            DailyPrepLimit = reader.GetInt32(3),
            LastUpdated = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            BrandId = reader.GetString(5),
            ItemName = reader.GetString(6)
        };

        var item = new MenuItem
        {
            ItemId = reader.GetString(1),
            BrandId = reader.GetString(5),
            ItemName = reader.GetString(6),
            DescriptionEn = reader.IsDBNull(7) ? null : reader.GetString(7),
            DescriptionVi = reader.IsDBNull(8) ? null : reader.GetString(8),
            UnitPrice = Convert.ToDecimal(reader.GetDouble(9)),
            Category = reader.GetString(10),
            IsActive = reader.GetInt32(11) == 1
        };

        return (stock, item);
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
