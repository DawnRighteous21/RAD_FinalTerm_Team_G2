using System.Globalization;

namespace SaigonBite.OFDH.Domain;

public class ManagementReport
{
    private static readonly string[] AcceptedDateFormats = { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy" };

    public string ReportId { get; set; } = string.Empty;
    public string ReportType { get; set; } = "SalesByBrand";
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string GeneratedAt { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public decimal AvgDeliveryTimeMins { get; set; }
    public decimal PeakHubUtilizationPct { get; set; }
    public string Status { get; set; } = "Generated";

    public int ExcludedOrderCount { get; set; }
    public decimal ExcludedOrderValue { get; set; }
    public decimal TotalRefunds { get; set; }
    public decimal CompletionRatePct { get; set; }
    public string PeakHubSummaryText { get; set; } = "No hub data";

    public List<string> IncludedHubIds { get; set; } = new();
    public List<BrandSalesRow> BrandSalesRows { get; set; } = new();
    public List<OperationalKpiRow> OperationalKpiRows { get; set; } = new();

    public bool ValidateDateRange()
    {
        if (!DateTime.TryParseExact(StartDate, AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            throw new ArgumentException("Start date must be in YYYY-MM-DD or DD/MM/YYYY format.");

        if (!DateTime.TryParseExact(EndDate, AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            throw new ArgumentException("End date must be in YYYY-MM-DD or DD/MM/YYYY format.");

        StartDate = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        EndDate = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (start.Date > end.Date)
            throw new ArgumentException($"Invalid date range: Start Date ({StartDate}) cannot be later than End Date ({EndDate}).");

        return true;
    }

    public void ComputeKPIs(
        List<Order> matchingOrders,
        List<Hub> scopedHubs,
        List<Brand> scopedBrands,
        List<Shipper> shippers,
        string? brandFilterId = null)
    {
        var deliveredOrders = matchingOrders
            .Where(o => string.Equals(o.OrderStatus, "Delivered", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var excludedOrders = matchingOrders
            .Where(o => o.OrderStatus is "Cancelled" or "Rejected" or "DeliveryFailed")
            .ToList();

        var failedDeliveryOrders = matchingOrders
            .Where(o => string.Equals(o.OrderStatus, "DeliveryFailed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        TotalOrders = deliveredOrders.Count;
        ExcludedOrderCount = excludedOrders.Count;
        ExcludedOrderValue = excludedOrders.Sum(o => o.TotalAmount);

        TotalRefunds = deliveredOrders
            .Where(o => string.Equals(o.PaymentStatus, "Refunded", StringComparison.OrdinalIgnoreCase))
            .Sum(o => o.TotalAmount);

        TotalRevenue = Math.Max(0m, deliveredOrders.Sum(o => o.TotalAmount) - TotalRefunds);

        var terminalDeliveryCount = deliveredOrders.Count + failedDeliveryOrders.Count;
        CompletionRatePct = terminalDeliveryCount == 0
            ? 100m
            : Math.Round((decimal)deliveredOrders.Count / terminalDeliveryCount * 100m, 1);

        var deliveryDurations = new List<double>();
        foreach (var order in deliveredOrders)
        {
            if (DateTime.TryParse(order.OrderDateTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var placedAt) &&
                DateTime.TryParse(order.DeliveredAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var deliveredAt) &&
                deliveredAt >= placedAt)
            {
                deliveryDurations.Add((deliveredAt - placedAt).TotalMinutes);
            }
        }

        AvgDeliveryTimeMins = deliveryDurations.Count == 0
            ? 0m
            : Math.Round((decimal)deliveryDurations.Average(), 1);

        IncludedHubIds = scopedHubs.Select(h => h.HubId).ToList();

        if (scopedHubs.Count > 0)
        {
            var busiestHub = scopedHubs
                .OrderByDescending(h => h.GetUtilizationPercentage())
                .ThenByDescending(h => h.CurrentActiveOrders)
                .First();

            PeakHubUtilizationPct = busiestHub.GetUtilizationPercentage();
            PeakHubSummaryText = string.Create(
                CultureInfo.InvariantCulture,
                $"{busiestHub.HubId} Peak: {busiestHub.CurrentActiveOrders}/{busiestHub.MaxActiveOrders} ({busiestHub.HubStatus})");
        }
        else
        {
            PeakHubUtilizationPct = 0m;
            PeakHubSummaryText = "No active hubs";
        }

        BuildBrandSalesRows(matchingOrders, scopedBrands, brandFilterId);
        BuildOperationalKpiRows(matchingOrders, scopedHubs, shippers);

        if (string.IsNullOrWhiteSpace(GeneratedAt))
            GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private void BuildBrandSalesRows(List<Order> matchingOrders, List<Brand> scopedBrands, string? brandFilterId)
    {
        var brandMap = scopedBrands.ToDictionary(b => b.BrandId, b => b.BrandName, StringComparer.OrdinalIgnoreCase);
        var buckets = new Dictionary<string, BrandSalesRow>(StringComparer.OrdinalIgnoreCase);

        foreach (var brand in scopedBrands)
        {
            if (!string.IsNullOrWhiteSpace(brandFilterId) &&
                !string.Equals(brand.BrandId, brandFilterId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            buckets[brand.BrandId] = new BrandSalesRow
            {
                BrandId = brand.BrandId,
                BrandName = brand.BrandName
            };
        }

        foreach (var order in matchingOrders)
        {
            var orderItems = order.Items;
            if (orderItems.Count == 0) continue;

            var totalItemLines = orderItems.Sum(i => i.LineTotal);
            var distinctBrandsInOrder = orderItems
                .Where(i => !string.IsNullOrWhiteSpace(i.BrandId))
                .GroupBy(i => i.BrandId, StringComparer.OrdinalIgnoreCase);

            foreach (var group in distinctBrandsInOrder)
            {
                var bId = group.Key;
                if (!string.IsNullOrWhiteSpace(brandFilterId) &&
                    !string.Equals(bId, brandFilterId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!buckets.TryGetValue(bId, out var row))
                {
                    var bName = group.First().BrandName;
                    if (string.IsNullOrWhiteSpace(bName))
                        brandMap.TryGetValue(bId, out bName);

                    row = new BrandSalesRow
                    {
                        BrandId = bId,
                        BrandName = string.IsNullOrWhiteSpace(bName) ? bId : bName
                    };
                    buckets[bId] = row;
                }

                var share = totalItemLines > 0m
                    ? group.Sum(i => i.LineTotal) / totalItemLines
                    : 1m;
                var allocatedAmount = Math.Round(order.TotalAmount * share, 0);

                if (string.Equals(order.OrderStatus, "Delivered", StringComparison.OrdinalIgnoreCase))
                {
                    row.DeliveredCount++;
                    if (string.Equals(order.PaymentStatus, "Refunded", StringComparison.OrdinalIgnoreCase))
                        row.RefundsAmount += allocatedAmount;
                    else
                        row.NetRevenue += allocatedAmount;
                }
                else if (order.OrderStatus is "Cancelled" or "Rejected" or "DeliveryFailed")
                {
                    row.ExcludedCount++;
                }
            }
        }

        var rows = buckets.Values.OrderBy(r => r.BrandId).ToList();
        var totalRow = new BrandSalesRow
        {
            BrandId = "TOTAL",
            BrandName = $"{rows.Count} Brand(s)",
            DeliveredCount = rows.Sum(r => r.DeliveredCount),
            ExcludedCount = rows.Sum(r => r.ExcludedCount),
            RefundsAmount = rows.Sum(r => r.RefundsAmount),
            NetRevenue = rows.Sum(r => r.NetRevenue),
            IsTotalRow = true
        };

        rows.Add(totalRow);
        BrandSalesRows = rows;
    }

    private void BuildOperationalKpiRows(List<Order> matchingOrders, List<Hub> scopedHubs, List<Shipper> shippers)
    {
        var kpiRows = new List<OperationalKpiRow>();

        foreach (var hub in scopedHubs)
        {
            var hubOrders = matchingOrders
                .Where(o => string.Equals(o.HubId, hub.HubId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var hubDelivered = hubOrders.Count(o => string.Equals(o.OrderStatus, "Delivered", StringComparison.OrdinalIgnoreCase));
            var hubRejectedOrCancelled = hubOrders.Count(o => o.OrderStatus is "Cancelled" or "Rejected");
            var isBusy = string.Equals(hub.HubStatus, "Busy", StringComparison.OrdinalIgnoreCase)
                || hub.CurrentActiveOrders >= hub.MaxActiveOrders;

            kpiRows.Add(new OperationalKpiRow
            {
                EntityLabel = $"{hub.HubId} (Workload)",
                PeakOrCompletedText = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Active {hub.CurrentActiveOrders}/{hub.MaxActiveOrders} ({hub.HubStatus}) | {hubDelivered} Delivered"),
                RerouteOrFailedText = isBusy
                    ? "Busy threshold reached (>=15)"
                    : $"{hubRejectedOrCancelled} Cancelled/Rejected",
                CollectedCodText = "—",
                HasWarning = isBusy || hubRejectedOrCancelled > 0
            });
        }

        foreach (var shipper in shippers)
        {
            var shipperOrders = matchingOrders
                .Where(o => string.Equals(o.ShipperId, shipper.ShipperId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var deliveredCount = shipperOrders.Count(o => string.Equals(o.OrderStatus, "Delivered", StringComparison.OrdinalIgnoreCase));
            var failedCount = shipperOrders.Count(o => string.Equals(o.OrderStatus, "DeliveryFailed", StringComparison.OrdinalIgnoreCase));
            var totalHandled = deliveredCount + failedCount;
            var rate = totalHandled == 0 ? 100m : Math.Round((decimal)deliveredCount / totalHandled * 100m, 1);

            var collectedCod = shipperOrders
                .Where(o => o.IsCodOrder
                    && string.Equals(o.OrderStatus, "Delivered", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(o.PaymentStatus, "PaidCOD", StringComparison.OrdinalIgnoreCase))
                .Sum(o => o.TotalAmount);

            var uncollectedFailedCodCount = shipperOrders.Count(o =>
                o.IsCodOrder && string.Equals(o.OrderStatus, "DeliveryFailed", StringComparison.OrdinalIgnoreCase));

            kpiRows.Add(new OperationalKpiRow
            {
                EntityLabel = $"{shipper.ShipperId} ({shipper.FullName})",
                PeakOrCompletedText = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{deliveredCount} Delivered ({rate:0.0}%)"),
                RerouteOrFailedText = failedCount == 0
                    ? "0 Failed"
                    : uncollectedFailedCodCount > 0
                        ? $"{failedCount} Failed ({uncollectedFailedCodCount} Uncollected COD excl.)"
                        : $"{failedCount} Failed",
                CollectedCodText = $"{collectedCod.ToString("N0", CultureInfo.InvariantCulture)} VND",
                HasWarning = failedCount > 0
            });
        }

        OperationalKpiRows = kpiRows;
    }

    public string MarkExported(string format)
    {
        if (string.Equals(Status, "Archived", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Report {ReportId} is archived and cannot be exported.");

        var normalizedFormat = string.Equals(format, "CSV", StringComparison.OrdinalIgnoreCase) ? "csv" : "pdf";
        Status = "Exported";
        return $"SaigonBite_{ReportType}_{ReportId}.{normalizedFormat}";
    }

    public void MarkArchived()
    {
        if (string.Equals(Status, "Archived", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Report {ReportId} is already archived.");

        Status = "Archived";
    }
}
