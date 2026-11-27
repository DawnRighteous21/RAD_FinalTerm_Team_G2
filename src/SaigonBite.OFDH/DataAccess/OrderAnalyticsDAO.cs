using Microsoft.Data.Sqlite;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.DataAccess;

public class OrderAnalyticsDAO
{
    private readonly DatabaseConnection _db;

    public OrderAnalyticsDAO(DatabaseConnection db)
    {
        _db = db;
    }

    public List<Order> FindOrdersByCriteria(
        string startDate,
        string endDate,
        string? hubId = null,
        string? brandId = null)
    {
        var orders = new List<Order>();
        var orderMap = new Dictionary<string, Order>(StringComparer.OrdinalIgnoreCase);

        using var conn = _db.GetConnection();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT DISTINCT o.order_id, o.customer_id, o.hub_id, o.address_id,
                       o.voucher_code, o.shipper_id, o.order_date_time, o.order_status,
                       o.subtotal_amount, o.delivery_fee, o.discount_amount, o.total_amount,
                       o.payment_method, o.payment_status, o.prep_started_at, o.ready_at,
                       o.delivered_at, o.failure_reason
                FROM Orders o
                LEFT JOIN OrderItems oi ON o.order_id = oi.order_id
                LEFT JOIN MenuItems m ON oi.item_id = m.item_id
                WHERE date(o.order_date_time) BETWEEN @startDate AND @endDate
                  AND (@hubId IS NULL OR o.hub_id = @hubId)
                  AND (@brandId IS NULL OR m.brand_id = @brandId)
                ORDER BY o.order_date_time ASC;
                """;
            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", endDate);
            cmd.Parameters.AddWithValue("@hubId", string.IsNullOrWhiteSpace(hubId) ? DBNull.Value : hubId);
            cmd.Parameters.AddWithValue("@brandId", string.IsNullOrWhiteSpace(brandId) ? DBNull.Value : brandId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var order = MapOrder(reader);
                orders.Add(order);
                orderMap[order.OrderId] = order;
            }
        }

        if (orders.Count == 0)
            return orders;

        using (var itemCmd = conn.CreateCommand())
        {
            itemCmd.CommandText = """
                SELECT oi.order_item_id, oi.order_id, oi.item_id, m.item_name,
                       m.brand_id, b.brand_name, oi.quantity, oi.unit_price_at_order,
                       oi.line_total, oi.special_instructions
                FROM OrderItems oi
                JOIN MenuItems m ON oi.item_id = m.item_id
                LEFT JOIN Brands b ON m.brand_id = b.brand_id
                ORDER BY oi.order_item_id ASC;
                """;

            using var reader = itemCmd.ExecuteReader();
            while (reader.Read())
            {
                var orderId = reader.GetString(1);
                if (!orderMap.TryGetValue(orderId, out var order))
                    continue;

                order.Items.Add(new OrderItem
                {
                    OrderItemId = reader.GetString(0),
                    OrderId = orderId,
                    ItemId = reader.GetString(2),
                    ItemName = reader.GetString(3),
                    BrandId = reader.GetString(4),
                    BrandName = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Quantity = reader.GetInt32(6),
                    UnitPriceAtOrder = Convert.ToDecimal(reader.GetDouble(7)),
                    LineTotal = Convert.ToDecimal(reader.GetDouble(8)),
                    SpecialInstructions = reader.IsDBNull(9) ? null : reader.GetString(9)
                });
            }
        }

        return orders;
    }

    private static Order MapOrder(SqliteDataReader reader)
    {
        return new Order
        {
            OrderId = reader.GetString(0),
            CustomerId = reader.GetString(1),
            HubId = reader.GetString(2),
            AddressId = reader.GetString(3),
            VoucherCode = reader.IsDBNull(4) ? null : reader.GetString(4),
            ShipperId = reader.IsDBNull(5) ? null : reader.GetString(5),
            OrderDateTime = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            OrderStatus = reader.GetString(7),
            SubtotalAmount = Convert.ToDecimal(reader.GetDouble(8)),
            DeliveryFee = Convert.ToDecimal(reader.GetDouble(9)),
            DiscountAmount = Convert.ToDecimal(reader.GetDouble(10)),
            TotalAmount = Convert.ToDecimal(reader.GetDouble(11)),
            PaymentMethod = reader.GetString(12),
            PaymentStatus = reader.GetString(13),
            PrepStartedAt = reader.IsDBNull(14) ? null : reader.GetString(14),
            ReadyAt = reader.IsDBNull(15) ? null : reader.GetString(15),
            DeliveredAt = reader.IsDBNull(16) ? null : reader.GetString(16),
            FailureReason = reader.IsDBNull(17) ? null : reader.GetString(17)
        };
    }
}
