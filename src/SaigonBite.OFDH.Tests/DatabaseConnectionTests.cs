using System.IO;
using Microsoft.Data.Sqlite;
using SaigonBite.OFDH.DataAccess;

namespace SaigonBite.OFDH.Tests;

public class DatabaseConnectionTests
{
    [Fact]
    public void InitializeSchema_CreatesTablesAndSeedsInitialData()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"saigonbite_test_{Guid.NewGuid():N}.db");

        try
        {
            var db = new DatabaseConnection(tempDb);
            db.InitializeSchema();

            using var conn = db.GetConnection();
            Assert.Equal(3, CountRows(conn, "Hubs"));
            Assert.Equal(2, CountRows(conn, "Brands"));
            Assert.Equal(3, CountRows(conn, "MenuItems"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb)) File.Delete(tempDb);
        }
    }

    private static long CountRows(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {tableName};";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }
}
