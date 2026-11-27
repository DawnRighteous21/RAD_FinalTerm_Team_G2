using SaigonBite.OFDH.DataAccess;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.Controllers;

public class PlaceOrderController
{
    private readonly CustomerDAO _customerDao;
    private readonly DeliveryAddressDAO _addressDao;
    private readonly HubDAO _hubDao;
    private readonly VoucherDAO _voucherDao;
    private readonly OrderDAO _orderDao;
    private readonly PaymentGatewayAdapter _paymentGateway;

    public Customer? CurrentCustomer { get; private set; }
    public Hub? CurrentHub { get; private set; }
    public DeliveryAddress? CurrentAddress { get; private set; }
    public Order? CurrentOrder { get; private set; }
    public string? LastTransactionRef { get; private set; }

    public PlaceOrderController(DatabaseConnection? db = null)
    {
        var conn = db ?? new DatabaseConnection();
        _customerDao = new CustomerDAO(conn);
        _addressDao = new DeliveryAddressDAO(conn);
        _hubDao = new HubDAO(conn);
        _voucherDao = new VoucherDAO(conn);
        _orderDao = new OrderDAO(conn);
        _paymentGateway = new PaymentGatewayAdapter();
    }

    public List<Customer> GetActiveCustomers() =>
        _customerDao.FindAll()
            .Where(c => string.Equals(c.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .ToList();

    public List<Hub> GetHubs() => _hubDao.FindAll();

    public List<DeliveryAddress> GetCustomerAddresses(string customerId) =>
        _addressDao.FindByCustomerId(customerId)
            .Where(a => string.Equals(a.Status, "Verified", StringComparison.OrdinalIgnoreCase))
            .ToList();

    public List<MenuItem> GetAvailableMenuItems(string hubId) =>
        _hubDao.FindAvailableMenuItemsAtHub(hubId);

    public Hub? FindNearestAvailableHub(string district, string? excludeHubId = null) =>
        _hubDao.FindNearestAvailableHub(district, excludeHubId);

    public bool InitiateCheckout(string customerId, string hubId, string addressId)
    {
        var customer = _customerDao.FindById(customerId)
            ?? throw new InvalidOperationException("Customer not found.");

        if (!string.Equals(customer.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Customer account is not active.");

        var hub = _hubDao.FindById(hubId)
            ?? throw new InvalidOperationException("Selected hub not found.");

        if (!hub.CanAcceptNewOrder())
            throw new InvalidOperationException(
                $"{hub.HubId} is currently {hub.HubStatus} ({hub.CurrentActiveOrders}/{hub.MaxActiveOrders} active orders) and cannot accept new orders.");

        var address = _addressDao.FindById(addressId)
            ?? throw new InvalidOperationException("Delivery address not found.");

        if (!string.Equals(address.CustomerId, customerId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Selected address does not belong to this customer.");

        if (!string.Equals(address.Status, "Verified", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Selected delivery address is not verified.");

        if (!address.VerifyRange(hub.MaxDeliveryDistanceKm))
            throw new InvalidOperationException(
                $"Delivery address ({address.DistanceKmFromHub:0.#} km) exceeds the {hub.MaxDeliveryDistanceKm:0.#} km hub radius.");

        CurrentCustomer = customer;
        CurrentHub = hub;
        CurrentAddress = address;
        LastTransactionRef = null;

        CurrentOrder = new Order
        {
            OrderId = _orderDao.GenerateNextOrderId(),
            CustomerId = customer.CustomerId,
            CustomerName = customer.FullName,
            CustomerType = customer.CustomerType,
            HubId = hub.HubId,
            AddressId = address.AddressId,
            DeliveryAddress = address
        };
        CurrentOrder.CalculateDeliveryFee(address.DistanceKmFromHub, hub.HubStatus);

        return true;
    }

    public decimal AddItemToOrder(string itemId, int quantity, string? note = null)
    {
        EnsureActiveOrder();

        var stockCheck = _hubDao.CheckItemStockAtHub(CurrentHub!.HubId, itemId)
            ?? throw new InvalidOperationException("Selected item is out of stock or unavailable at this hub.");

        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return CurrentOrder!.AddOrderItem(stockCheck.Item, quantity, cleanNote);
    }

    public decimal RemoveItemFromOrder(string orderItemId)
    {
        EnsureActiveOrder();

        CurrentOrder!.Items.RemoveAll(i => i.OrderItemId == orderItemId);
        CurrentOrder.CalculateTotalAmount();

        if (CurrentOrder.AppliedVoucher != null &&
            !CurrentOrder.AppliedVoucher.IsValidForOrder(CurrentOrder.SubtotalAmount, DateTime.Now))
        {
            CurrentOrder.RemoveVoucher();
        }

        return CurrentOrder.SubtotalAmount;
    }

    public Order ApplyVoucher(string voucherCode)
    {
        EnsureActiveOrder();

        if (string.IsNullOrWhiteSpace(voucherCode))
            throw new InvalidOperationException("Please enter a voucher code.");

        var voucher = _voucherDao.FindByCode(voucherCode)
            ?? throw new InvalidOperationException($"Voucher '{voucherCode.Trim()}' does not exist or is inactive.");

        CurrentOrder!.ApplySingleVoucher(voucher, DateTime.Now);
        return CurrentOrder;
    }

    public Order RemoveVoucher()
    {
        EnsureActiveOrder();
        CurrentOrder!.RemoveVoucher();
        return CurrentOrder;
    }

    public Order ConfirmAndPlaceOrder(string paymentMethod, string? paymentDetails = null)
    {
        EnsureActiveOrder();

        if (CurrentOrder!.Items.Count == 0)
            throw new InvalidOperationException("Your cart is empty. Please add at least one menu item.");

        var latestHub = _hubDao.FindById(CurrentHub!.HubId)
            ?? throw new InvalidOperationException("Selected hub not found.");

        if (!latestHub.CanAcceptNewOrder())
            throw new InvalidOperationException(
                $"{latestHub.HubId} reached full capacity ({latestHub.CurrentActiveOrders}/{latestHub.MaxActiveOrders}).");

        if (string.Equals(CurrentCustomer!.CustomerType, "Tourist", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(paymentMethod, "COD", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Cash on Delivery (COD) is disabled for Foreign Tourists. Please pay via MoMo, VNPay, or Card.");
        }

        if (CurrentOrder.AppliedVoucher != null &&
            !CurrentOrder.AppliedVoucher.IsValidForOrder(CurrentOrder.SubtotalAmount, DateTime.Now))
        {
            CurrentOrder.RemoveVoucher();
        }

        CurrentOrder.PaymentMethod = paymentMethod;
        CurrentOrder.CalculateTotalAmount();

        if (string.Equals(paymentMethod, "COD", StringComparison.OrdinalIgnoreCase))
        {
            LastTransactionRef = null;
            CurrentOrder.MarkPlaced("PendingCOD");
        }
        else
        {
            LastTransactionRef = _paymentGateway.AuthorizePayment(
                CurrentOrder.TotalAmount, paymentMethod, paymentDetails);
            CurrentOrder.MarkPlaced("Paid");
        }

        _orderDao.SaveOrderWithItems(CurrentOrder);

        var newActiveCount = latestHub.IncrementActiveOrders();
        _hubDao.UpdateActiveOrderCount(latestHub.HubId, newActiveCount, latestHub.HubStatus);
        CurrentHub = latestHub;

        return CurrentOrder;
    }

    private void EnsureActiveOrder()
    {
        if (CurrentOrder == null || CurrentHub == null || CurrentCustomer == null)
            throw new InvalidOperationException("Checkout session is not initialized. Please select a hub and address first.");
    }
}
