PRAGMA foreign_keys = ON;

INSERT INTO Hubs (hub_id, hub_name, district, max_active_orders, current_active_orders, max_delivery_distance_km, hub_status) VALUES
('HUB-D1', 'SaigonBite Hub District 1', 'District 1', 15, 2, 7.0, 'Quiet'),
('HUB-D3', 'SaigonBite Hub District 3', 'District 3', 15, 6, 7.0, 'Normal'),
('HUB-D7', 'SaigonBite Hub District 7', 'District 7', 15, 15, 7.0, 'Busy');

INSERT INTO Customers (customer_id, customer_type, full_name, email, status, vn_phone_number, preferred_local_wallet, passport_number, nationality, hotel_name, room_number) VALUES
('CUST-001', 'Local', 'Nguyen Van An', 'an.nguyen@gmail.com', 'Active', '0901234567', 'MoMo', NULL, NULL, NULL, NULL),
('CUST-002', 'Tourist', 'John Smith', 'john.smith@uk.org', 'Active', NULL, NULL, 'C12345678', 'United Kingdom', 'Rex Hotel Saigon', '504');

INSERT INTO DeliveryAddresses (address_id, customer_id, recipient_name, street_address, district, landmark_or_hotel, distance_km_from_hub, is_default, status) VALUES
('ADDR-001', 'CUST-001', 'Nguyen Van An', '19 Nguyen Huu Tho', 'District 7', 'TDTU Gate A', 2.4, 1, 'Verified'),
('ADDR-002', 'CUST-001', 'Nguyen Van An', '120 Vo Van Tan', 'District 3', 'Office Tower', 4.2, 0, 'Verified'),
('ADDR-003', 'CUST-002', 'John Smith', '141 Nguyen Hue', 'District 1', 'Rex Hotel Lobby', 1.8, 1, 'Verified');

INSERT INTO Brands (brand_id, brand_name, cuisine_type, description, is_active) VALUES
('BR-01', 'Com Tam Saigon', 'Vietnamese Rice', 'Charcoal grilled pork broken rice', 1),
('BR-02', 'Pho Hoa Express', 'Vietnamese Noodle', 'Traditional beef pho cooked at cloud hubs', 1);

INSERT INTO HubBrands (hub_id, brand_id) VALUES
('HUB-D1', 'BR-01'), ('HUB-D1', 'BR-02'),
('HUB-D3', 'BR-01'), ('HUB-D3', 'BR-02'),
('HUB-D7', 'BR-01');

INSERT INTO MenuItems (item_id, brand_id, item_name, description_en, description_vi, unit_price, category, is_active) VALUES
('ITEM-01', 'BR-01', 'Com Tam Suon Bi Cha', 'Broken rice with grilled pork chop and egg meatloaf', 'Com tam suon nuong bi cha trung', 60000, 'Main', 1),
('ITEM-02', 'BR-02', 'Pho Tai Lan', 'Stir-fried rare beef pho noodle soup', 'Pho bo tai lan truyen thong', 75000, 'Main', 1),
('ITEM-03', 'BR-01', 'Tra Da Chanh', 'Iced lime tea', 'Tra chanh tuoi mat lanh', 15000, 'Drink', 1);

INSERT INTO HubMenuItems (hub_id, item_id, is_available_in_stock, daily_prep_limit) VALUES
('HUB-D1', 'ITEM-01', 1, 80), ('HUB-D1', 'ITEM-02', 1, 60), ('HUB-D1', 'ITEM-03', 1, 100),
('HUB-D3', 'ITEM-01', 1, 80), ('HUB-D3', 'ITEM-02', 1, 60), ('HUB-D3', 'ITEM-03', 1, 100),
('HUB-D7', 'ITEM-01', 1, 50), ('HUB-D7', 'ITEM-03', 1, 100);

INSERT INTO Vouchers (voucher_code, discount_percentage, max_discount_amount, min_order_value, valid_from, valid_until, is_active) VALUES
('VOU20', 20.0, 20000, 100000, '2026-11-01', '2026-12-31', 1),
('WELCOME10', 10.0, 15000, 50000, '2026-11-01', '2026-12-31', 1),
('EXPIRED50', 50.0, 50000, 50000, '2026-01-01', '2026-06-30', 0);

INSERT INTO Shippers (shipper_id, full_name, phone_number, vehicle_plate, availability_status, cash_on_hand) VALUES
('SHP-01', 'Tran Minh Quan', '0912345678', '59-X3 123.45', 'Available', 0),
('SHP-02', 'Le Hoang Nam', '0987654321', '59-C2 678.90', 'Available', 145000);

INSERT INTO Orders (order_id, customer_id, hub_id, address_id, voucher_code, shipper_id, order_date_time, order_status, subtotal_amount, delivery_fee, discount_amount, total_amount, payment_method, payment_status, prep_started_at, ready_at, delivered_at, failure_reason) VALUES
('ORD-1001', 'CUST-001', 'HUB-D3', 'ADDR-002', 'VOU20', 'SHP-01', '2026-11-20 11:30:00', 'Delivered', 120000, 25000, 20000, 125000, 'COD', 'PaidCOD', '2026-11-20 11:33:00', '2026-11-20 11:48:00', '2026-11-20 12:05:00', NULL),
('ORD-1002', 'CUST-002', 'HUB-D1', 'ADDR-003', NULL, 'SHP-02', '2026-11-21 18:15:00', 'ConfirmedByKitchen', 75000, 5000, 0, 80000, 'Card', 'Paid', NULL, NULL, NULL, NULL),
('ORD-1003', 'CUST-001', 'HUB-D1', 'ADDR-001', NULL, 'SHP-01', '2026-11-21 19:00:00', 'ReadyForPickup', 150000, 5000, 0, 155000, 'COD', 'PendingCOD', '2026-11-21 19:05:00', '2026-11-21 19:20:00', NULL, NULL);

INSERT INTO OrderItems (order_item_id, order_id, item_id, quantity, unit_price_at_order, line_total, special_instructions) VALUES
('OI-001', 'ORD-1001', 'ITEM-01', 2, 60000, 120000, 'Extra pickled veggies'),
('OI-002', 'ORD-1002', 'ITEM-02', 1, 75000, 75000, 'No cilantro'),
('OI-003', 'ORD-1003', 'ITEM-02', 2, 75000, 150000, 'Separate broth');
