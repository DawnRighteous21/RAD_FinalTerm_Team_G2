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

    public string GenerateNextOrderId()
    {
        var maxNum = 1000;
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT order_id FROM Orders;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            if (id.StartsWith("ORD-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(id[4..], out var num) &&
                num > maxNum)
            {
                maxNum = num;
            }
        }
        return $"ORD-{maxNum + 1:D4}";
    }

    public bool SaveOrderWithItems(Order order)
    {
        using var conn = _db.GetConnection();
        using var tx = conn.BeginTransaction();

        using (var orderCmd = conn.CreateCommand())
        {
            orderCmd.Transaction = tx;
            orderCmd.CommandText = """
                INSERT INTO Orders (
                    order_id, customer_id, hub_id, address_id, voucher_code,
                    order_date_time, order_status, subtotal_amount, delivery_fee,
                    discount_amount, total_amount, payment_method, payment_status
                )
                VALUES (
                    @orderId, @customerId, @hubId, @addressId, @voucherCode,
                    @orderDateTime, @orderStatus, @subtotal, @deliveryFee,
                    @discount, @total, @paymentMethod, @paymentStatus
                );
                """;
            orderCmd.Parameters.AddWithValue("@orderId", order.OrderId);
            orderCmd.Parameters.AddWithValue("@customerId", order.CustomerId);
            orderCmd.Parameters.AddWithValue("@hubId", order.HubId);
            orderCmd.Parameters.AddWithValue("@addressId", order.AddressId);
            orderCmd.Parameters.AddWithValue("@voucherCode", (object?)order.VoucherCode ?? DBNull.Value);
            orderCmd.Parameters.AddWithValue("@orderDateTime", order.OrderDateTime);
            orderCmd.Parameters.AddWithValue("@orderStatus", order.OrderStatus);
            orderCmd.Parameters.AddWithValue("@subtotal", order.SubtotalAmount);
            orderCmd.Parameters.AddWithValue("@deliveryFee", order.DeliveryFee);
            orderCmd.Parameters.AddWithValue("@discount", order.DiscountAmount);
            orderCmd.Parameters.AddWithValue("@total", order.TotalAmount);
            orderCmd.Parameters.AddWithValue("@paymentMethod", order.PaymentMethod);
            orderCmd.Parameters.AddWithValue("@paymentStatus", order.PaymentStatus);
            orderCmd.ExecuteNonQuery();
        }

        foreach (var item in order.Items)
        {
            item.OrderId = order.OrderId;
            using var itemCmd = conn.CreateCommand();
            itemCmd.Transaction = tx;
            itemCmd.CommandText = """
                INSERT INTO OrderItems (
                    order_item_id, order_id, item_id, quantity,
                    unit_price_at_order, line_total, special_instructions
                )
                VALUES (
                    @orderItemId, @orderId, @itemId, @quantity,
                    @unitPrice, @lineTotal, @instructions
                );
                """;
            itemCmd.Parameters.AddWithValue("@orderItemId", item.OrderItemId);
            itemCmd.Parameters.AddWithValue("@orderId", order.OrderId);
            itemCmd.Parameters.AddWithValue("@itemId", item.ItemId);
            itemCmd.Parameters.AddWithValue("@quantity", item.Quantity);
            itemCmd.Parameters.AddWithValue("@unitPrice", item.UnitPriceAtOrder);
            itemCmd.Parameters.AddWithValue("@lineTotal", item.LineTotal);
            itemCmd.Parameters.AddWithValue("@instructions", (object?)item.SpecialInstructions ?? DBNull.Value);
            itemCmd.ExecuteNonQuery();
        }

        tx.Commit();
        return true;
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
                   a.district, a.landmark_or_hotel, a.distance_km_from_hub, a.is_default, a.status,
                   c.full_name, c.customer_type
            FROM Orders o
            LEFT JOIN DeliveryAddresses a ON o.address_id = a.address_id
            LEFT JOIN Customers c ON o.customer_id = c.customer_id
            WHERE o.order_id = @orderId;
            """;
        cmd.Parameters.AddWithValue("@orderId", orderId);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapOrderWithAddress(reader) : null;
    }

    public Order? FindByIdWithItems(string orderId)
    {
        var order = FindById(orderId);
        if (order == null) return null;

        order.Items = FindOrderItems(order.OrderId);
        return order;
    }

    public List<Order> FindKitchenQueueByHub(string hubId, bool confirmedOnly = false)
    {
        var orders = new List<Order>();
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = confirmedOnly
            ? """
                SELECT o.order_id, o.customer_id, o.hub_id, o.address_id,
                       o.voucher_code, o.shipper_id, o.order_date_time, o.order_status,
                       o.subtotal_amount, o.delivery_fee, o.discount_amount, o.total_amount,
                       o.payment_method, o.payment_status, o.prep_started_at, o.ready_at,
                       o.delivered_at, o.failure_reason,
                       a.address_id, a.customer_id, a.recipient_name, a.street_address,
                       a.district, a.landmark_or_hotel, a.distance_km_from_hub, a.is_default, a.status,
                       c.full_name, c.customer_type
                FROM Orders o
                LEFT JOIN DeliveryAddresses a ON o.address_id = a.address_id
                LEFT JOIN Customers c ON o.customer_id = c.customer_id
                WHERE o.hub_id = @hubId
                  AND o.order_status IN ('ConfirmedByKitchen', 'InPreparation')
                ORDER BY o.order_date_time ASC;
                """
            : """
                SELECT o.order_id, o.customer_id, o.hub_id, o.address_id,
                       o.voucher_code, o.shipper_id, o.order_date_time, o.order_status,
                       o.subtotal_amount, o.delivery_fee, o.discount_amount, o.total_amount,
                       o.payment_method, o.payment_status, o.prep_started_at, o.ready_at,
                       o.delivered_at, o.failure_reason,
                       a.address_id, a.customer_id, a.recipient_name, a.street_address,
                       a.district, a.landmark_or_hotel, a.distance_km_from_hub, a.is_default, a.status,
                       c.full_name, c.customer_type
                FROM Orders o
                LEFT JOIN DeliveryAddresses a ON o.address_id = a.address_id
                LEFT JOIN Customers c ON o.customer_id = c.customer_id
                WHERE o.hub_id = @hubId
                  AND o.order_status IN ('Placed', 'ConfirmedByKitchen', 'InPreparation', 'ReadyForPickup')
                ORDER BY o.order_date_time ASC;
                """;
        cmd.Parameters.AddWithValue("@hubId", hubId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            orders.Add(MapOrderWithAddress(reader));
        }

        foreach (var order in orders)
        {
            order.Items = FindOrderItems(order.OrderId);
        }

        return orders;
    }

    public bool UpdateOrderPrepState(Order order)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Orders
            SET order_status = @orderStatus,
                payment_status = @paymentStatus,
                prep_started_at = @prepStartedAt,
                ready_at = @readyAt,
                failure_reason = @failureReason
            WHERE order_id = @orderId;
            """;
        cmd.Parameters.AddWithValue("@orderStatus", order.OrderStatus);
        cmd.Parameters.AddWithValue("@paymentStatus", order.PaymentStatus);
        cmd.Parameters.AddWithValue("@prepStartedAt", (object?)order.PrepStartedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@readyAt", (object?)order.ReadyAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@failureReason", (object?)order.FailureReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@orderId", order.OrderId);

        return cmd.ExecuteNonQuery() > 0;
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
                   a.district, a.landmark_or_hotel, a.distance_km_from_hub, a.is_default, a.status,
                   c.full_name, c.customer_type
            FROM Orders o
            LEFT JOIN DeliveryAddresses a ON o.address_id = a.address_id
            LEFT JOIN Customers c ON o.customer_id = c.customer_id
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

    private List<OrderItem> FindOrderItems(string orderId)
    {
        var items = new List<OrderItem>();
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT oi.order_item_id, oi.order_id, oi.item_id, m.item_name,
                   m.brand_id, b.brand_name, oi.quantity, oi.unit_price_at_order,
                   oi.line_total, oi.special_instructions
            FROM OrderItems oi
            JOIN MenuItems m ON oi.item_id = m.item_id
            LEFT JOIN Brands b ON m.brand_id = b.brand_id
            WHERE oi.order_id = @orderId
            ORDER BY oi.order_item_id ASC;
            """;
        cmd.Parameters.AddWithValue("@orderId", orderId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new OrderItem
            {
                OrderItemId = reader.GetString(0),
                OrderId = reader.GetString(1),
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
        return items;
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
            FailureReason = reader.IsDBNull(17) ? null : reader.GetString(17),
            CustomerName = reader.IsDBNull(27) ? null : reader.GetString(27),
            CustomerType = reader.IsDBNull(28) ? null : reader.GetString(28)
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
