using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.DataAccess;

public class VoucherDAO
{
    private readonly DatabaseConnection _db;

    public VoucherDAO(DatabaseConnection db)
    {
        _db = db;
    }

    public Voucher? FindByCode(string voucherCode)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT voucher_code, discount_percentage, max_discount_amount,
                   min_order_value, valid_from, valid_until, is_active
            FROM Vouchers
            WHERE UPPER(voucher_code) = UPPER(@code)
              AND is_active = 1;
            """;
        cmd.Parameters.AddWithValue("@code", voucherCode.Trim());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new Voucher
        {
            VoucherCode = reader.GetString(0),
            DiscountPercentage = Convert.ToDecimal(reader.GetDouble(1)),
            MaxDiscountAmount = Convert.ToDecimal(reader.GetDouble(2)),
            MinOrderValue = Convert.ToDecimal(reader.GetDouble(3)),
            ValidFrom = reader.GetString(4),
            ValidUntil = reader.GetString(5),
            IsActive = reader.GetInt32(6) == 1
        };
    }
}
