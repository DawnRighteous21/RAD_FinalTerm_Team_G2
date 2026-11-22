PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Customers (
    customer_id TEXT PRIMARY KEY,
    customer_type TEXT NOT NULL CHECK (customer_type IN ('Local', 'Tourist')),
    full_name TEXT NOT NULL,
    email TEXT NOT NULL UNIQUE,
    status TEXT NOT NULL DEFAULT 'Active' CHECK (status IN ('Active', 'Inactive', 'Archived')),
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    vn_phone_number TEXT UNIQUE,
    preferred_local_wallet TEXT,
    passport_number TEXT UNIQUE,
    nationality TEXT,
    hotel_name TEXT,
    room_number TEXT,
    CHECK (
        (customer_type = 'Local' AND vn_phone_number IS NOT NULL) OR
        (customer_type = 'Tourist' AND passport_number IS NOT NULL)
    )
);

CREATE TABLE IF NOT EXISTS Hubs (
    hub_id TEXT PRIMARY KEY,
    hub_name TEXT NOT NULL,
    district TEXT NOT NULL,
    max_active_orders INTEGER NOT NULL DEFAULT 15 CHECK (max_active_orders > 0),
    current_active_orders INTEGER NOT NULL DEFAULT 0 CHECK (current_active_orders >= 0),
    max_delivery_distance_km REAL NOT NULL DEFAULT 7.0 CHECK (max_delivery_distance_km > 0),
    hub_status TEXT NOT NULL DEFAULT 'Quiet' CHECK (hub_status IN ('Quiet', 'Normal', 'Busy', 'Closed'))
);

CREATE TABLE IF NOT EXISTS DeliveryAddresses (
    address_id TEXT PRIMARY KEY,
    customer_id TEXT NOT NULL,
    recipient_name TEXT NOT NULL,
    street_address TEXT NOT NULL,
    district TEXT NOT NULL,
    landmark_or_hotel TEXT,
    distance_km_from_hub REAL NOT NULL CHECK (distance_km_from_hub >= 0),
    is_default INTEGER NOT NULL DEFAULT 0 CHECK (is_default IN (0, 1)),
    status TEXT NOT NULL DEFAULT 'Verified' CHECK (status IN ('Verified', 'OutOfRange', 'Archived')),
    FOREIGN KEY (customer_id) REFERENCES Customers(customer_id)
);

CREATE TABLE IF NOT EXISTS Brands (
    brand_id TEXT PRIMARY KEY,
    brand_name TEXT NOT NULL UNIQUE,
    cuisine_type TEXT NOT NULL,
    description TEXT,
    is_active INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1))
);

CREATE TABLE IF NOT EXISTS HubBrands (
    hub_id TEXT NOT NULL,
    brand_id TEXT NOT NULL,
    PRIMARY KEY (hub_id, brand_id),
    FOREIGN KEY (hub_id) REFERENCES Hubs(hub_id),
    FOREIGN KEY (brand_id) REFERENCES Brands(brand_id)
);

CREATE TABLE IF NOT EXISTS MenuItems (
    item_id TEXT PRIMARY KEY,
    brand_id TEXT NOT NULL,
    item_name TEXT NOT NULL,
    description_en TEXT,
    description_vi TEXT,
    unit_price REAL NOT NULL CHECK (unit_price > 0),
    category TEXT NOT NULL,
    is_active INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    FOREIGN KEY (brand_id) REFERENCES Brands(brand_id),
    UNIQUE (brand_id, item_name)
);

CREATE TABLE IF NOT EXISTS HubMenuItems (
    hub_id TEXT NOT NULL,
    item_id TEXT NOT NULL,
    is_available_in_stock INTEGER NOT NULL DEFAULT 1 CHECK (is_available_in_stock IN (0, 1)),
    daily_prep_limit INTEGER NOT NULL DEFAULT 50 CHECK (daily_prep_limit >= 0),
    last_updated TEXT NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY (hub_id, item_id),
    FOREIGN KEY (hub_id) REFERENCES Hubs(hub_id),
    FOREIGN KEY (item_id) REFERENCES MenuItems(item_id)
);

CREATE TABLE IF NOT EXISTS Vouchers (
    voucher_code TEXT PRIMARY KEY,
    discount_percentage REAL NOT NULL CHECK (discount_percentage >= 0 AND discount_percentage <= 100),
    max_discount_amount REAL NOT NULL CHECK (max_discount_amount >= 0),
    min_order_value REAL NOT NULL DEFAULT 0 CHECK (min_order_value >= 0),
    valid_from TEXT NOT NULL,
    valid_until TEXT NOT NULL,
    is_active INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1))
);

CREATE TABLE IF NOT EXISTS Shippers (
    shipper_id TEXT PRIMARY KEY,
    full_name TEXT NOT NULL,
    phone_number TEXT NOT NULL UNIQUE,
    vehicle_plate TEXT NOT NULL,
    availability_status TEXT NOT NULL DEFAULT 'Available' CHECK (availability_status IN ('Offline', 'Available', 'AssignedToOrder', 'OnDelivery')),
    cash_on_hand REAL NOT NULL DEFAULT 0 CHECK (cash_on_hand >= 0)
);

CREATE TABLE IF NOT EXISTS Orders (
    order_id TEXT PRIMARY KEY,
    customer_id TEXT NOT NULL,
    hub_id TEXT NOT NULL,
    address_id TEXT NOT NULL,
    voucher_code TEXT,
    shipper_id TEXT,
    order_date_time TEXT NOT NULL DEFAULT (datetime('now')),
    order_status TEXT NOT NULL DEFAULT 'Placed' CHECK (order_status IN (
        'Placed', 'ConfirmedByKitchen', 'InPreparation', 'ReadyForPickup',
        'PickedUp', 'Delivered', 'Cancelled', 'Rejected', 'DeliveryFailed'
    )),
    subtotal_amount REAL NOT NULL DEFAULT 0 CHECK (subtotal_amount >= 0),
    delivery_fee REAL NOT NULL DEFAULT 0 CHECK (delivery_fee >= 0),
    discount_amount REAL NOT NULL DEFAULT 0 CHECK (discount_amount >= 0),
    total_amount REAL NOT NULL DEFAULT 0 CHECK (total_amount >= 0),
    payment_method TEXT NOT NULL CHECK (payment_method IN ('COD', 'MoMo', 'VNPay', 'Card')),
    payment_status TEXT NOT NULL DEFAULT 'PendingCOD' CHECK (payment_status IN ('Paid', 'PendingCOD', 'PaidCOD', 'Refunded')),
    prep_started_at TEXT,
    ready_at TEXT,
    delivered_at TEXT,
    failure_reason TEXT,
    FOREIGN KEY (customer_id) REFERENCES Customers(customer_id),
    FOREIGN KEY (hub_id) REFERENCES Hubs(hub_id),
    FOREIGN KEY (address_id) REFERENCES DeliveryAddresses(address_id),
    FOREIGN KEY (voucher_code) REFERENCES Vouchers(voucher_code),
    FOREIGN KEY (shipper_id) REFERENCES Shippers(shipper_id)
);

CREATE TABLE IF NOT EXISTS OrderItems (
    order_item_id TEXT PRIMARY KEY,
    order_id TEXT NOT NULL,
    item_id TEXT NOT NULL,
    quantity INTEGER NOT NULL CHECK (quantity > 0),
    unit_price_at_order REAL NOT NULL CHECK (unit_price_at_order > 0),
    line_total REAL NOT NULL CHECK (line_total > 0),
    special_instructions TEXT,
    FOREIGN KEY (order_id) REFERENCES Orders(order_id),
    FOREIGN KEY (item_id) REFERENCES MenuItems(item_id)
);

CREATE TABLE IF NOT EXISTS ManagementReports (
    report_id TEXT PRIMARY KEY,
    report_type TEXT NOT NULL CHECK (report_type IN ('SalesByBrand', 'HubWorkload', 'DeliveryPerformance')),
    start_date TEXT NOT NULL,
    end_date TEXT NOT NULL,
    generated_at TEXT NOT NULL DEFAULT (datetime('now')),
    total_revenue REAL NOT NULL DEFAULT 0,
    total_orders INTEGER NOT NULL DEFAULT 0,
    avg_delivery_time_mins REAL NOT NULL DEFAULT 0,
    peak_hub_utilization_pct REAL NOT NULL DEFAULT 0,
    status TEXT NOT NULL DEFAULT 'Generated' CHECK (status IN ('Generated', 'Exported', 'Archived')),
    CHECK (start_date <= end_date)
);

CREATE TABLE IF NOT EXISTS ReportHubs (
    report_id TEXT NOT NULL,
    hub_id TEXT NOT NULL,
    PRIMARY KEY (report_id, hub_id),
    FOREIGN KEY (report_id) REFERENCES ManagementReports(report_id),
    FOREIGN KEY (hub_id) REFERENCES Hubs(hub_id)
);
