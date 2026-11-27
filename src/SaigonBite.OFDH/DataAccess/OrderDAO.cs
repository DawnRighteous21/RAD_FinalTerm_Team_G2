// Author: 524H0134 (Ha Du Tuan)
using Microsoft.Data.Sqlite;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.DataAccess;

public class OrderDAO
{
    private readonly DatabaseConnection _db;

    public OrderDAO(DatabaseConnection db)
    {
        _db = db;
    }

    public Order? FindById(string orderId)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT o.order_id, o.customer_id, o.hub_id, o.address_id,
                   o.voucher_code, o.shipper_id, o.order_date_time, o.order_status,
                   o.subtotal_amount, o.delivery_fee, o.discount_amount, o.total_amount,
                   o.payment_method, o.payment_status, o.prep_started_at, o.ready_at,
                   o.delivered_at, o.failure_reason,
                   a.address_id, a.customer_id, a.recipient_name, a.street_address,
                   a.district, a.landmark_or_hotel, a.distance_km_from_hub, a.is_default, a.status
            FROM Orders o
            LEFT JOIN DeliveryAddresses a ON o.address_id = a.address_id
            WHERE o.order_id = @orderId;
            """;
        cmd.Parameters.AddWithValue("@orderId", orderId);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapOrderWithAddress(reader) : null;
    }

    public List<Order> FindDeliverableAndActiveOrders(string shipperId)
    {
        var orders = new List<Order>();
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT o.order_id, o.customer_id, o.hub_id, o.address_id,
                   o.voucher_code, o.shipper_id, o.order_date_time, o.order_status,
                   o.subtotal_amount, o.delivery_fee, o.discount_amount, o.total_amount,
                   o.payment_method, o.payment_status, o.prep_started_at, o.ready_at,
                   o.delivered_at, o.failure_reason,
                   a.address_id, a.customer_id, a.recipient_name, a.street_address,
                   a.district, a.landmark_or_hotel, a.distance_km_from_hub, a.is_default, a.status
            FROM Orders o
            LEFT JOIN DeliveryAddresses a ON o.address_id = a.address_id
            WHERE (o.order_status = 'PickedUp' AND o.shipper_id = @shipperId)
               OR (o.order_status = 'ReadyForPickup' AND (o.shipper_id IS NULL OR o.shipper_id = '' OR o.shipper_id = @shipperId))
            ORDER BY CASE
                        WHEN o.order_status = 'PickedUp' THEN 0
                        WHEN o.shipper_id = @shipperId THEN 1
                        ELSE 2
                     END,
                     o.order_date_time ASC;
            """;
        cmd.Parameters.AddWithValue("@shipperId", shipperId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            orders.Add(MapOrderWithAddress(reader));
        }
        return orders;
    }

    public bool UpdateDeliveryStatus(Order order)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Orders
            SET order_status = @orderStatus,
                shipper_id = @shipperId,
                payment_status = @paymentStatus,
                delivered_at = @deliveredAt,
                failure_reason = @failureReason
            WHERE order_id = @orderId;
            """;
        cmd.Parameters.AddWithValue("@orderStatus", order.OrderStatus);
        cmd.Parameters.AddWithValue("@shipperId", (object?)order.ShipperId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@paymentStatus", order.PaymentStatus);
        cmd.Parameters.AddWithValue("@deliveredAt", (object?)order.DeliveredAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@failureReason", (object?)order.FailureReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@orderId", order.OrderId);

        return cmd.ExecuteNonQuery() > 0;
    }

    private static Order MapOrderWithAddress(SqliteDataReader reader)
    {
        var order = new Order
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

        if (!reader.IsDBNull(18))
        {
            order.DeliveryAddress = new DeliveryAddress
            {
                AddressId = reader.GetString(18),
                CustomerId = reader.GetString(19),
                RecipientName = reader.GetString(20),
                StreetAddress = reader.GetString(21),
                District = reader.GetString(22),
                LandmarkOrHotel = reader.IsDBNull(23) ? null : reader.GetString(23),
                DistanceKmFromHub = Convert.ToDecimal(reader.GetDouble(24)),
                IsDefault = reader.GetInt32(25) == 1,
                Status = reader.GetString(26)
            };
        }

        return order;
    }
}