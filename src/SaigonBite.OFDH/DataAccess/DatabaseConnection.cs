using System.IO;
using Microsoft.Data.Sqlite;

namespace SaigonBite.OFDH.DataAccess;

public class DatabaseConnection
{
    public static string DefaultDbPath { get; } =
        Path.Combine(AppContext.BaseDirectory, "saigonbite.db");

    private readonly string _connectionString;

    public DatabaseConnection(string? dbPath = null)
    {
        var path = string.IsNullOrWhiteSpace(dbPath) ? DefaultDbPath : dbPath;
        _connectionString = $"Data Source={path}";
    }

    public SqliteConnection GetConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        return conn;
    }

    public void InitializeSchema()
    {
        using var conn = GetConnection();

        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Brands';";
        var exists = Convert.ToInt64(checkCmd.ExecuteScalar()) > 0;
        if (exists) return;

        var sqlDir = Path.Combine(AppContext.BaseDirectory, "SQL");
        var createSql = File.ReadAllText(Path.Combine(sqlDir, "01_create_tables.sql"));
        var seedSql = File.ReadAllText(Path.Combine(sqlDir, "02_seed_data.sql"));

        using var tx = conn.BeginTransaction();

        using (var createCmd = conn.CreateCommand())
        {
            createCmd.Transaction = tx;
            createCmd.CommandText = createSql;
            createCmd.ExecuteNonQuery();
        }

        using (var seedCmd = conn.CreateCommand())
        {
            seedCmd.Transaction = tx;
            seedCmd.CommandText = seedSql;
            seedCmd.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
