using Microsoft.Data.Sqlite;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.DataAccess;

public class MenuItemDAO
{
    private readonly DatabaseConnection _db;

    public MenuItemDAO(DatabaseConnection db)
    {
        _db = db;
    }

    public List<MenuItem> FindAllMenuItems()
    {
        var items = new List<MenuItem>();
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT item_id, brand_id, item_name, description_en,
                   description_vi, unit_price, category, is_active
            FROM MenuItems
            ORDER BY item_id;
            """;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(MapMenuItem(reader));
        }
        return items;
    }

    public MenuItem? FindById(string itemId)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT item_id, brand_id, item_name, description_en,
                   description_vi, unit_price, category, is_active
            FROM MenuItems
            WHERE item_id = @itemId;
            """;
        cmd.Parameters.AddWithValue("@itemId", itemId);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapMenuItem(reader) : null;
    }

    public bool ExistsByNameInBrand(string brandId, string name, string? excludeItemId = null)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*)
            FROM MenuItems
            WHERE brand_id = @brandId
              AND LOWER(item_name) = LOWER(@name)
              AND (@excludeId IS NULL OR item_id != @excludeId);
            """;
        cmd.Parameters.AddWithValue("@brandId", brandId);
        cmd.Parameters.AddWithValue("@name", name.Trim());
        cmd.Parameters.AddWithValue("@excludeId", (object?)excludeItemId ?? DBNull.Value);

        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    public string GenerateNextItemId()
    {
        var maxNum = 0;
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT item_id FROM MenuItems;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            if (id.StartsWith("ITEM-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(id[5..], out var num) &&
                num > maxNum)
            {
                maxNum = num;
            }
        }
        return $"ITEM-{maxNum + 1:D2}";
    }

    public bool InsertMenuItem(MenuItem item)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO MenuItems (
                item_id, brand_id, item_name, description_en,
                description_vi, unit_price, category, is_active
            )
            VALUES (
                @itemId, @brandId, @itemName, @descEn,
                @descVi, @unitPrice, @category, @isActive
            );
            """;
        cmd.Parameters.AddWithValue("@itemId", item.ItemId);
        cmd.Parameters.AddWithValue("@brandId", item.BrandId);
        cmd.Parameters.AddWithValue("@itemName", item.ItemName);
        cmd.Parameters.AddWithValue("@descEn", (object?)item.DescriptionEn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@descVi", (object?)item.DescriptionVi ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@unitPrice", item.UnitPrice);
        cmd.Parameters.AddWithValue("@category", item.Category);
        cmd.Parameters.AddWithValue("@isActive", item.IsActive ? 1 : 0);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool UpdateMenuItem(MenuItem item)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE MenuItems
            SET brand_id = @brandId,
                item_name = @itemName,
                description_en = @descEn,
                description_vi = @descVi,
                unit_price = @unitPrice,
                category = @category
            WHERE item_id = @itemId;
            """;
        cmd.Parameters.AddWithValue("@brandId", item.BrandId);
        cmd.Parameters.AddWithValue("@itemName", item.ItemName);
        cmd.Parameters.AddWithValue("@descEn", (object?)item.DescriptionEn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@descVi", (object?)item.DescriptionVi ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@unitPrice", item.UnitPrice);
        cmd.Parameters.AddWithValue("@category", item.Category);
        cmd.Parameters.AddWithValue("@itemId", item.ItemId);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool ArchiveMenuItem(string itemId)
    {
        return SetItemActive(itemId, false);
    }

    public bool ReactivateMenuItem(string itemId)
    {
        return SetItemActive(itemId, true);
    }

    private bool SetItemActive(string itemId, bool isActive)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE MenuItems SET is_active = @isActive WHERE item_id = @itemId;";
        cmd.Parameters.AddWithValue("@isActive", isActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@itemId", itemId);

        return cmd.ExecuteNonQuery() > 0;
    }

    private static MenuItem MapMenuItem(SqliteDataReader reader)
    {
        return new MenuItem
        {
            ItemId = reader.GetString(0),
            BrandId = reader.GetString(1),
            ItemName = reader.GetString(2),
            DescriptionEn = reader.IsDBNull(3) ? null : reader.GetString(3),
            DescriptionVi = reader.IsDBNull(4) ? null : reader.GetString(4),
            UnitPrice = Convert.ToDecimal(reader.GetDouble(5)),
            Category = reader.GetString(6),
            IsActive = reader.GetInt32(7) == 1
        };
    }
}
