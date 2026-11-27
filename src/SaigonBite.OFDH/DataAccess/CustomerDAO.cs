using Microsoft.Data.Sqlite;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.DataAccess;

public class CustomerDAO
{
    private readonly DatabaseConnection _db;

    public CustomerDAO(DatabaseConnection db)
    {
        _db = db;
    }

    public List<Customer> FindAll()
    {
        var customers = new List<Customer>();
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT customer_id, customer_type, full_name, email, status, created_at,
                   vn_phone_number, preferred_local_wallet,
                   passport_number, nationality, hotel_name, room_number
            FROM Customers
            ORDER BY customer_id;
            """;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            customers.Add(MapCustomer(reader));
        }
        return customers;
    }

    public Customer? FindById(string customerId)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT customer_id, customer_type, full_name, email, status, created_at,
                   vn_phone_number, preferred_local_wallet,
                   passport_number, nationality, hotel_name, room_number
            FROM Customers
            WHERE customer_id = @customerId;
            """;
        cmd.Parameters.AddWithValue("@customerId", customerId);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapCustomer(reader) : null;
    }

    public bool ExistsByPhoneOrPassport(string identifier)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*)
            FROM Customers
            WHERE LOWER(vn_phone_number) = LOWER(@id)
               OR LOWER(passport_number) = LOWER(@id);
            """;
        cmd.Parameters.AddWithValue("@id", identifier.Trim());

        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    public bool ExistsByEmail(string email)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*)
            FROM Customers
            WHERE LOWER(email) = LOWER(@email);
            """;
        cmd.Parameters.AddWithValue("@email", email.Trim());

        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    public string GenerateNextCustomerId()
    {
        var maxNum = 0;
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT customer_id FROM Customers;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            if (id.StartsWith("CUST-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(id[5..], out var num) &&
                num > maxNum)
            {
                maxNum = num;
            }
        }
        return $"CUST-{maxNum + 1:D3}";
    }

    public bool InsertCustomer(Customer customer)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Customers (
                customer_id, customer_type, full_name, email, status, created_at,
                vn_phone_number, preferred_local_wallet,
                passport_number, nationality, hotel_name, room_number
            )
            VALUES (
                @customerId, @customerType, @fullName, @email, @status, @createdAt,
                @vnPhone, @wallet,
                @passport, @nationality, @hotelName, @roomNumber
            );
            """;

        cmd.Parameters.AddWithValue("@customerId", customer.CustomerId);
        cmd.Parameters.AddWithValue("@customerType", customer.CustomerType);
        cmd.Parameters.AddWithValue("@fullName", customer.FullName);
        cmd.Parameters.AddWithValue("@email", customer.Email);
        cmd.Parameters.AddWithValue("@status", customer.Status);
        cmd.Parameters.AddWithValue("@createdAt", customer.CreatedAt);

        if (customer is LocalCustomer local)
        {
            cmd.Parameters.AddWithValue("@vnPhone", local.VnPhoneNumber);
            cmd.Parameters.AddWithValue("@wallet", (object?)local.PreferredLocalWallet ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@passport", DBNull.Value);
            cmd.Parameters.AddWithValue("@nationality", DBNull.Value);
            cmd.Parameters.AddWithValue("@hotelName", DBNull.Value);
            cmd.Parameters.AddWithValue("@roomNumber", DBNull.Value);
        }
        else if (customer is ForeignTourist tourist)
        {
            cmd.Parameters.AddWithValue("@vnPhone", DBNull.Value);
            cmd.Parameters.AddWithValue("@wallet", DBNull.Value);
            cmd.Parameters.AddWithValue("@passport", tourist.PassportNumber);
            cmd.Parameters.AddWithValue("@nationality", (object?)tourist.Nationality ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@hotelName", (object?)tourist.HotelName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@roomNumber", (object?)tourist.RoomNumber ?? DBNull.Value);
        }
        else
        {
            throw new InvalidOperationException("Unsupported customer type.");
        }

        return cmd.ExecuteNonQuery() > 0;
    }

    private static Customer MapCustomer(SqliteDataReader reader)
    {
        var type = reader.GetString(1);
        Customer customer = string.Equals(type, "Tourist", StringComparison.OrdinalIgnoreCase)
            ? new ForeignTourist
            {
                PassportNumber = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                Nationality = reader.IsDBNull(9) ? null : reader.GetString(9),
                HotelName = reader.IsDBNull(10) ? null : reader.GetString(10),
                RoomNumber = reader.IsDBNull(11) ? null : reader.GetString(11)
            }
            : new LocalCustomer
            {
                VnPhoneNumber = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                PreferredLocalWallet = reader.IsDBNull(7) ? null : reader.GetString(7)
            };

        customer.CustomerId = reader.GetString(0);
        customer.CustomerType = type;
        customer.FullName = reader.GetString(2);
        customer.Email = reader.GetString(3);
        customer.Status = reader.GetString(4);
        customer.CreatedAt = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
        return customer;
    }
}
