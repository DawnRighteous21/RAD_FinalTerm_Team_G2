namespace SaigonBite.OFDH.Domain;

public class Hub
{
    public string HubId { get; set; } = string.Empty;
    public string HubName { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public int MaxActiveOrders { get; set; } = 15;
    public int CurrentActiveOrders { get; set; }
    public decimal MaxDeliveryDistanceKm { get; set; } = 7.0m;
    public string HubStatus { get; set; } = "Quiet";

    public bool CanAcceptNewOrder()
    {
        return HubStatus != "Closed"
            && HubStatus != "Busy"
            && CurrentActiveOrders < MaxActiveOrders;
    }

    public decimal GetUtilizationPercentage()
    {
        if (MaxActiveOrders <= 0) return 0m;
        return Math.Round((decimal)CurrentActiveOrders / MaxActiveOrders * 100m, 1);
    }

    public int IncrementActiveOrders()
    {
        CurrentActiveOrders++;
        RefreshStatus();
        return CurrentActiveOrders;
    }

    public int DecrementActiveOrders()
    {
        CurrentActiveOrders = Math.Max(0, CurrentActiveOrders - 1);
        RefreshStatus();
        return CurrentActiveOrders;
    }

    public decimal CalculateDistanceTo(string street, string district)
    {
        var normalizedTarget = NormalizeDistrict(district);
        var normalizedHub = NormalizeDistrict(District);

        // Deterministic offset (0.0 .. 0.8 km) based on street name so same address always gets same distance
        var streetOffset = string.IsNullOrWhiteSpace(street)
            ? 0.2m
            : Math.Abs(street.Trim().ToLowerInvariant().GetHashCode() % 9) * 0.1m;

        if (normalizedTarget == normalizedHub)
            return Math.Round(1.8m + streetOffset, 1);

        var baseDistance = normalizedTarget switch
        {
            "district 1" or "district 3" or "district 4" or "district 5" or "district 10" => 3.6m,
            "binh thanh" or "phu nhuan" or "tan binh" or "district 7" => 4.8m,
            "tan phu" or "go vap" or "district 2" or "district 6" or "district 8" or "district 11" => 6.0m,
            "thu duc" => 9.8m,
            "nha be" => 11.4m,
            "binh chanh" => 14.2m,
            "hoc mon" => 16.5m,
            "cu chi" => 28.4m,
            "can gio" => 36.0m,
            _ => 8.5m
        };

        return Math.Round(baseDistance + streetOffset, 1);
    }

    private static string NormalizeDistrict(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        return text switch
        {
            "d1" or "q1" or "quan 1" or "quận 1" => "district 1",
            "d3" or "q3" or "quan 3" or "quận 3" => "district 3",
            "d7" or "q7" or "quan 7" or "quận 7" => "district 7",
            "bình thạnh" => "binh thanh",
            "phú nhuận" => "phu nhuan",
            "tân phú" => "tan phu",
            "thủ đức" or "tp thu duc" => "thu duc",
            "củ chi" => "cu chi",
            "nhà bè" => "nha be",
            "bình chánh" => "binh chanh",
            "hóc môn" => "hoc mon",
            "cần giờ" => "can gio",
            _ => text
        };
    }

    public void RefreshStatus()
    {
        if (HubStatus == "Closed") return;
        if (CurrentActiveOrders >= MaxActiveOrders)
            HubStatus = "Busy";
        else if (CurrentActiveOrders <= 3)
            HubStatus = "Quiet";
        else
            HubStatus = "Normal";
    }
    
    public string DisplayLabel => $"{HubId} — {HubName}";
}