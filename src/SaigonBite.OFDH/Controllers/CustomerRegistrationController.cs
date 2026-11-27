using SaigonBite.OFDH.DataAccess;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.Controllers;

public class CustomerRegistrationController
{
    private readonly CustomerDAO _customerDAO;
    private readonly DeliveryAddressDAO _addressDAO;
    private readonly HubDAO _hubDAO;

    public CustomerRegistrationController() : this(new DatabaseConnection())
    {
    }

    public CustomerRegistrationController(DatabaseConnection db)
    {
        _customerDAO = new CustomerDAO(db);
        _addressDAO = new DeliveryAddressDAO(db);
        _hubDAO = new HubDAO(db);
    }

    public List<string> SelectAccountType(string accountType)
    {
        return string.Equals(accountType, "Tourist", StringComparison.OrdinalIgnoreCase)
            ? new List<string> { "FullName", "Email", "PassportNumber", "Nationality", "HotelName", "RoomNumber" }
            : new List<string> { "FullName", "Email", "VnPhoneNumber", "PreferredLocalWallet" };
    }

    public List<Customer> GetAllCustomers()
    {
        return _customerDAO.FindAll();
    }

    public List<DeliveryAddress> GetAddressesByCustomer(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            return new List<DeliveryAddress>();

        return _addressDAO.FindByCustomerId(customerId);
    }

    public Hub? GetNearestHubForDistrict(string district)
    {
        if (string.IsNullOrWhiteSpace(district)) return null;
        return _hubDAO.FindNearestHub(district);
    }

    public Customer RegisterCustomer(
        string type,
        string fullName,
        string email,
        string? vnPhone,
        string? wallet,
        string? passport,
        string? nationality,
        string? hotelName,
        string? roomNumber)
    {
        var cleanName = (fullName ?? string.Empty).Trim();
        var cleanEmail = (email ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(cleanName))
            throw new ArgumentException("Full name is required.");

        if (string.IsNullOrWhiteSpace(cleanEmail))
            throw new ArgumentException("Email address is required.");

        if (_customerDAO.ExistsByEmail(cleanEmail))
            throw new InvalidOperationException($"Email '{cleanEmail}' is already registered.");

        Customer customer;
        if (string.Equals(type, "Tourist", StringComparison.OrdinalIgnoreCase))
        {
            var cleanPassport = (passport ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(cleanPassport))
                throw new ArgumentException("Passport number is required for Foreign Tourists.");

            if (_customerDAO.ExistsByPhoneOrPassport(cleanPassport))
                throw new InvalidOperationException($"Passport '{cleanPassport}' is already registered.");

            customer = new ForeignTourist
            {
                CustomerId = _customerDAO.GenerateNextCustomerId(),
                FullName = cleanName,
                Email = cleanEmail,
                PassportNumber = cleanPassport,
                Nationality = string.IsNullOrWhiteSpace(nationality) ? null : nationality.Trim(),
                HotelName = string.IsNullOrWhiteSpace(hotelName) ? null : hotelName.Trim(),
                RoomNumber = string.IsNullOrWhiteSpace(roomNumber) ? null : roomNumber.Trim()
            };

            if (!customer.ValidateEmailFormat())
                throw new ArgumentException("Invalid email format.");

            if (!customer.ValidateCredentials())
                throw new ArgumentException("Passport number must be 6–12 alphanumeric characters.");
        }
        else
        {
            var cleanPhone = (vnPhone ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(cleanPhone))
                throw new ArgumentException("VN phone number is required for Local Customers.");

            if (_customerDAO.ExistsByPhoneOrPassport(cleanPhone))
                throw new InvalidOperationException($"VN phone number '{cleanPhone}' is already registered.");

            customer = new LocalCustomer
            {
                CustomerId = _customerDAO.GenerateNextCustomerId(),
                FullName = cleanName,
                Email = cleanEmail,
                VnPhoneNumber = cleanPhone,
                PreferredLocalWallet = string.IsNullOrWhiteSpace(wallet) ? "MoMo" : wallet.Trim()
            };

            if (!customer.ValidateEmailFormat())
                throw new ArgumentException("Invalid email format.");

            if (!customer.ValidateCredentials())
                throw new ArgumentException("VN phone number must be 10 digits starting with 0 (e.g., 0901234567).");
        }

        customer.ActivateProfile();

        if (!_customerDAO.InsertCustomer(customer))
            throw new InvalidOperationException("Failed to persist customer record.");

        return customer;
    }

    public DeliveryAddress AddDeliveryAddress(
        string customerId,
        string recipientName,
        string street,
        string district,
        string? landmarkOrHotel,
        bool isDefault)
    {
        if (string.IsNullOrWhiteSpace(customerId) || _customerDAO.FindById(customerId) == null)
            throw new ArgumentException("Selected customer does not exist.");

        if (string.IsNullOrWhiteSpace(recipientName))
            throw new ArgumentException("Recipient name is required.");

        if (string.IsNullOrWhiteSpace(street))
            throw new ArgumentException("Street address is required.");

        if (string.IsNullOrWhiteSpace(district))
            throw new ArgumentException("District is required.");

        var nearestHub = _hubDAO.FindNearestHub(district)
            ?? throw new InvalidOperationException("No active delivery hub found in the system.");

        var distanceKm = nearestHub.CalculateDistanceTo(street, district);
        var address = new DeliveryAddress
        {
            AddressId = _addressDAO.GenerateNextAddressId(),
            CustomerId = customerId,
            RecipientName = recipientName.Trim(),
            StreetAddress = street.Trim(),
            District = district.Trim(),
            LandmarkOrHotel = string.IsNullOrWhiteSpace(landmarkOrHotel) ? null : landmarkOrHotel.Trim(),
            DistanceKmFromHub = distanceKm,
            IsDefault = isDefault
        };

        address.Status = address.VerifyRange(nearestHub.MaxDeliveryDistanceKm)
            ? "Verified"
            : "OutOfRange";

        if (!_addressDAO.InsertAddress(customerId, address))
            throw new InvalidOperationException("Failed to save delivery address.");

        return address;
    }

    public bool UpdateOrArchiveAddress(
        string customerId,
        string addressId,
        string action,
        string? recipientName = null,
        string? street = null,
        string? district = null,
        string? landmarkOrHotel = null,
        bool? isDefault = null)
    {
        var existing = _addressDAO.FindById(addressId)
            ?? throw new InvalidOperationException($"Delivery address '{addressId}' was not found.");

        if (!string.Equals(existing.CustomerId, customerId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Address does not belong to the selected customer.");

        if (string.Equals(action, "ARCHIVE", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(existing.Status, "Archived", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Address is already archived.");

            if (_addressDAO.HasActiveOrders(addressId))
                throw new InvalidOperationException(
                    $"Cannot archive {addressId}: it is attached to an active order in progress.");

            existing.MarkArchived();
            return _addressDAO.ArchiveAddress(addressId);
        }

        if (string.Equals(action, "UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(existing.Status, "Archived", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Archived addresses cannot be edited.");

            if (string.IsNullOrWhiteSpace(recipientName))
                throw new ArgumentException("Recipient name is required.");
            if (string.IsNullOrWhiteSpace(street))
                throw new ArgumentException("Street address is required.");
            if (string.IsNullOrWhiteSpace(district))
                throw new ArgumentException("District is required.");

            var nearestHub = _hubDAO.FindNearestHub(district)
                ?? throw new InvalidOperationException("No active delivery hub found in the system.");

            var newDistance = nearestHub.CalculateDistanceTo(street, district);
            existing.UpdateDetails(
                recipientName,
                street,
                district,
                landmarkOrHotel,
                newDistance,
                isDefault ?? existing.IsDefault,
                nearestHub.MaxDeliveryDistanceKm);

            return _addressDAO.UpdateAddress(existing);
        }

        throw new ArgumentException($"Unsupported address action '{action}'. Use UPDATE or ARCHIVE.");
    }
}
