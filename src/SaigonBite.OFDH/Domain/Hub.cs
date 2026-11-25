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
