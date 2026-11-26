using Microsoft.Data.Sqlite;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.DataAccess;

public class BrandDAO
{
    private readonly DatabaseConnection _db;

    public BrandDAO(DatabaseConnection db)
    {
        _db = db;
    }

    public List<Brand> FindAllBrands()
    {
        var brands = new List<Brand>();
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT brand_id, brand_name, cuisine_type, description, is_active
            FROM Brands
            ORDER BY brand_id;
            """;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            brands.Add(MapBrand(reader));
        }
        return brands;
    }

    public Brand? FindById(string brandId)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT brand_id, brand_name, cuisine_type, description, is_active
            FROM Brands
            WHERE brand_id = @brandId;
            """;
        cmd.Parameters.AddWithValue("@brandId", brandId);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapBrand(reader) : null;
    }

    public bool ExistsByName(string brandName, string? excludeBrandId = null)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*)
            FROM Brands
            WHERE LOWER(brand_name) = LOWER(@name)
              AND (@excludeId IS NULL OR brand_id != @excludeId);
            """;
        cmd.Parameters.AddWithValue("@name", brandName.Trim());
        cmd.Parameters.AddWithValue("@excludeId", (object?)excludeBrandId ?? DBNull.Value);

        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    public string GenerateNextBrandId()
    {
        var maxNum = 0;
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT brand_id FROM Brands;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            if (id.StartsWith("BR-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(id[3..], out var num) &&
                num > maxNum)
            {
                maxNum = num;
            }
        }
        return $"BR-{maxNum + 1:D2}";
    }

    public bool InsertBrand(Brand brand)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Brands (brand_id, brand_name, cuisine_type, description, is_active)
            VALUES (@brandId, @brandName, @cuisineType, @description, @isActive);
            """;
        cmd.Parameters.AddWithValue("@brandId", brand.BrandId);
        cmd.Parameters.AddWithValue("@brandName", brand.BrandName);
        cmd.Parameters.AddWithValue("@cuisineType", brand.CuisineType);
        cmd.Parameters.AddWithValue("@description", (object?)brand.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isActive", brand.IsActive ? 1 : 0);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool UpdateBrand(Brand brand)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Brands
            SET brand_name = @brandName,
                cuisine_type = @cuisineType,
                description = @description
            WHERE brand_id = @brandId;
            """;
        cmd.Parameters.AddWithValue("@brandName", brand.BrandName);
        cmd.Parameters.AddWithValue("@cuisineType", brand.CuisineType);
        cmd.Parameters.AddWithValue("@description", (object?)brand.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@brandId", brand.BrandId);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool SetBrandActive(string brandId, bool isActive)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Brands SET is_active = @isActive WHERE brand_id = @brandId;";
        cmd.Parameters.AddWithValue("@isActive", isActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@brandId", brandId);

        return cmd.ExecuteNonQuery() > 0;
    }

    private static Brand MapBrand(SqliteDataReader reader)
    {
        return new Brand
        {
            BrandId = reader.GetString(0),
            BrandName = reader.GetString(1),
            CuisineType = reader.GetString(2),
            Description = reader.IsDBNull(3) ? null : reader.GetString(3),
            IsActive = reader.GetInt32(4) == 1
        };
    }
}
