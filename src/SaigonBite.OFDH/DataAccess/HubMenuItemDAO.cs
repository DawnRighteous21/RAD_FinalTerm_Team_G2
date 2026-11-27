using Microsoft.Data.Sqlite;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.DataAccess;

public class HubMenuItemDAO
{
    private readonly DatabaseConnection _db;

    public HubMenuItemDAO(DatabaseConnection db)
    {
        _db = db;
    }

    public List<string> FindHubIdsByItemId(string itemId)
    {
        var hubIds = new List<string>();
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT hub_id
            FROM HubMenuItems
            WHERE item_id = @itemId
            ORDER BY hub_id;
            """;
        cmd.Parameters.AddWithValue("@itemId", itemId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            hubIds.Add(reader.GetString(0));
        }
        return hubIds;
    }

    public Dictionary<string, List<string>> FindAllAssignedHubsByItem()
    {
        var map = new Dictionary<string, List<string>>();
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT item_id, hub_id
            FROM HubMenuItems
            ORDER BY item_id, hub_id;
            """;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var itemId = reader.GetString(0);
            var hubId = reader.GetString(1);
            if (!map.TryGetValue(itemId, out var list))
            {
                list = new List<string>();
                map[itemId] = list;
            }
            list.Add(hubId);
        }
        return map;
    }

    public bool AssignItemToHubs(string itemId, string brandId, IEnumerable<string> hubIds)
    {
        var selectedHubs = hubIds
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var conn = _db.GetConnection();
        using var tx = conn.BeginTransaction();

        using (var delCmd = conn.CreateCommand())
        {
            delCmd.Transaction = tx;
            delCmd.CommandText = "DELETE FROM HubMenuItems WHERE item_id = @itemId;";
            delCmd.Parameters.AddWithValue("@itemId", itemId);
            delCmd.ExecuteNonQuery();
        }

        foreach (var hubId in selectedHubs)
        {
            using (var hubBrandCmd = conn.CreateCommand())
            {
                hubBrandCmd.Transaction = tx;
                hubBrandCmd.CommandText = """
                    INSERT OR IGNORE INTO HubBrands (hub_id, brand_id)
                    VALUES (@hubId, @brandId);
                    """;
                hubBrandCmd.Parameters.AddWithValue("@hubId", hubId);
                hubBrandCmd.Parameters.AddWithValue("@brandId", brandId);
                hubBrandCmd.ExecuteNonQuery();
            }

            using (var insertCmd = conn.CreateCommand())
            {
                insertCmd.Transaction = tx;
                insertCmd.CommandText = """
                    INSERT OR REPLACE INTO HubMenuItems (hub_id, item_id, is_available_in_stock, daily_prep_limit)
                    VALUES (@hubId, @itemId, 1, 50);
                    """;
                insertCmd.Parameters.AddWithValue("@hubId", hubId);
                insertCmd.Parameters.AddWithValue("@itemId", itemId);
                insertCmd.ExecuteNonQuery();
            }
        }

        tx.Commit();
        return true;
    }

    public List<HubMenuItem> FindByHubWithItemDetails(string hubId)
    {
        var items = new List<HubMenuItem>();
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT hmi.hub_id, hmi.item_id, m.item_name, m.brand_id,
                   hmi.is_available_in_stock, hmi.daily_prep_limit, hmi.last_updated
            FROM HubMenuItems hmi
            JOIN MenuItems m ON hmi.item_id = m.item_id
            WHERE hmi.hub_id = @hubId
              AND m.is_active = 1
            ORDER BY hmi.item_id ASC;
            """;
        cmd.Parameters.AddWithValue("@hubId", hubId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(MapHubMenuItem(reader));
        }
        return items;
    }

    public HubMenuItem? FindByHubAndItem(string hubId, string itemId)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT hmi.hub_id, hmi.item_id, m.item_name, m.brand_id,
                   hmi.is_available_in_stock, hmi.daily_prep_limit, hmi.last_updated
            FROM HubMenuItems hmi
            JOIN MenuItems m ON hmi.item_id = m.item_id
            WHERE hmi.hub_id = @hubId
              AND hmi.item_id = @itemId;
            """;
        cmd.Parameters.AddWithValue("@hubId", hubId);
        cmd.Parameters.AddWithValue("@itemId", itemId);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapHubMenuItem(reader) : null;
    }

    public bool UpdateStockAvailability(HubMenuItem item)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE HubMenuItems
            SET is_available_in_stock = @isAvailable,
                last_updated = @lastUpdated
            WHERE hub_id = @hubId
              AND item_id = @itemId;
            """;
        cmd.Parameters.AddWithValue("@isAvailable", item.IsAvailableInStock ? 1 : 0);
        cmd.Parameters.AddWithValue("@lastUpdated", string.IsNullOrWhiteSpace(item.LastUpdated)
            ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            : item.LastUpdated);
        cmd.Parameters.AddWithValue("@hubId", item.HubId);
        cmd.Parameters.AddWithValue("@itemId", item.ItemId);

        return cmd.ExecuteNonQuery() > 0;
    }

    private static HubMenuItem MapHubMenuItem(SqliteDataReader reader)
    {
        return new HubMenuItem
        {
            HubId = reader.GetString(0),
            ItemId = reader.GetString(1),
            ItemName = reader.GetString(2),
            BrandId = reader.GetString(3),
            IsAvailableInStock = reader.GetInt32(4) == 1,
            DailyPrepLimit = reader.GetInt32(5),
            LastUpdated = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
        };
    }
}
