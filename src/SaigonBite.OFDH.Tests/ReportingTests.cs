// Author: 524H0015 (Ly Tin Hoa)
using Microsoft.Data.Sqlite;
using SaigonBite.OFDH.Controllers;
using SaigonBite.OFDH.DataAccess;

namespace SaigonBite.OFDH.Tests;

public class ReportingTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempExportDir;
    private readonly DatabaseConnection _db;
    private readonly ReportingController _controller;

    public ReportingTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"saigonbite_uc16_test_{Guid.NewGuid():N}.db");
        _tempExportDir = Path.Combine(Path.GetTempPath(), $"saigonbite_exports_{Guid.NewGuid():N}");
        _db = new DatabaseConnection(_tempDbPath);
        _db.InitializeSchema();
        _controller = new ReportingController(_db);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_tempDbPath))
            File.Delete(_tempDbPath);
        if (Directory.Exists(_tempExportDir))
            Directory.Delete(_tempExportDir, recursive: true);
    }

    private void InsertOrderForAnalytics(
        string orderId,
        string hubId,
        string shipperId,
        string itemId,
        string orderDateTime,
        string? deliveredAt,
        string orderStatus,
        decimal totalAmount,
        string paymentMethod,
        string paymentStatus)
    {
        using var conn = _db.GetConnection();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO Orders (
                    order_id, customer_id, hub_id, address_id, voucher_code, shipper_id,
                    order_date_time, order_status, subtotal_amount, delivery_fee,
                    discount_amount, total_amount, payment_method, payment_status, delivered_at
                )
                VALUES (
                    @orderId, 'CUST-001', @hubId, 'ADDR-001', NULL, @shipperId,
                    @orderDt, @status, @total, 0, 0, @total, @payMethod, @payStatus, @deliveredAt
                );
                """;
            cmd.Parameters.AddWithValue("@orderId", orderId);
            cmd.Parameters.AddWithValue("@hubId", hubId);
            cmd.Parameters.AddWithValue("@shipperId", shipperId);
            cmd.Parameters.AddWithValue("@orderDt", orderDateTime);
            cmd.Parameters.AddWithValue("@status", orderStatus);
            cmd.Parameters.AddWithValue("@total", totalAmount);
            cmd.Parameters.AddWithValue("@payMethod", paymentMethod);
            cmd.Parameters.AddWithValue("@payStatus", paymentStatus);
            cmd.Parameters.AddWithValue("@deliveredAt", (object?)deliveredAt ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        using var itemCmd = conn.CreateCommand();
        itemCmd.CommandText = """
            INSERT INTO OrderItems (order_item_id, order_id, item_id, quantity, unit_price_at_order, line_total)
            VALUES (@oiId, @orderId, @itemId, 1, @total, @total);
            """;
        itemCmd.Parameters.AddWithValue("@oiId", $"OI-{orderId}");
        itemCmd.Parameters.AddWithValue("@orderId", orderId);
        itemCmd.Parameters.AddWithValue("@itemId", itemId);
        itemCmd.Parameters.AddWithValue("@total", totalAmount);
        itemCmd.ExecuteNonQuery();
    }

    [Fact]
    public void T21_SalesByBrand_IncludesDeliveredMinusRefunds_ExcludesCancelledAndFailed()
    {
        InsertOrderForAnalytics("ORD-R01", "HUB-D1", "SHP-01", "ITEM-01", "2026-11-22 10:00:00", "2026-11-22 10:25:00", "Delivered", 200000m, "MoMo", "Paid");
        InsertOrderForAnalytics("ORD-R02", "HUB-D1", "SHP-01", "ITEM-02", "2026-11-22 11:00:00", "2026-11-22 11:35:00", "Delivered", 150000m, "Card", "Paid");
        InsertOrderForAnalytics("ORD-R03", "HUB-D1", "SHP-01", "ITEM-01", "2026-11-22 12:00:00", "2026-11-22 12:30:00", "Delivered", 50000m, "Card", "Refunded");
        InsertOrderForAnalytics("ORD-R04", "HUB-D1", "SHP-01", "ITEM-01", "2026-11-22 13:00:00", null, "Cancelled", 180000m, "COD", "PendingCOD");
        InsertOrderForAnalytics("ORD-R05", "HUB-D1", "SHP-01", "ITEM-02", "2026-11-22 14:00:00", null, "DeliveryFailed", 120000m, "COD", "PendingCOD");

        var report = _controller.GenerateReport("SalesByBrand", "2026-11-22", "2026-11-22", "HUB-D1", "ALL");

        Assert.Equal(3, report.TotalOrders);
        Assert.Equal(50000m, report.TotalRefunds);
        Assert.Equal(350000m, report.TotalRevenue);
        Assert.Equal(2, report.ExcludedOrderCount);
        Assert.Equal(300000m, report.ExcludedOrderValue);
        Assert.Equal(30.0m, report.AvgDeliveryTimeMins);

        var persisted = _controller.GetReportById(report.ReportId);
        Assert.NotNull(persisted);
        Assert.Equal("Generated", persisted.Status);
        Assert.Equal(350000m, persisted.TotalRevenue);
        Assert.Contains("HUB-D1", persisted.IncludedHubIds);
    }

    [Fact]
    public void T22_HubWorkload_ComputesPeakHubUtilizationAcrossHubs()
    {
        var report = _controller.GenerateReport("HubWorkload", "2026-11-01", "2026-11-30", "ALL", "ALL");

        Assert.Equal(100.0m, report.PeakHubUtilizationPct);
        Assert.Contains("HUB-D7", report.PeakHubSummaryText);
        Assert.Equal(3, report.IncludedHubIds.Count);
        Assert.Contains(report.OperationalKpiRows, r => r.EntityLabel.Contains("HUB-D7") && r.HasWarning);
    }

    [Fact]
    public void T23_DeliveryPerformance_ExcludesUncollectedFailedCodFromCollectedCash()
    {
        InsertOrderForAnalytics("ORD-C01", "HUB-D1", "SHP-02", "ITEM-01", "2026-11-23 12:00:00", "2026-11-23 12:20:00", "Delivered", 140000m, "COD", "PaidCOD");
        InsertOrderForAnalytics("ORD-C02", "HUB-D1", "SHP-02", "ITEM-02", "2026-11-23 13:00:00", null, "DeliveryFailed", 95000m, "COD", "PendingCOD");

        var report = _controller.GenerateReport("DeliveryPerformance", "2026-11-23", "2026-11-23", "HUB-D1", "ALL");

        Assert.Equal(50.0m, report.CompletionRatePct);

        var shp02Row = report.OperationalKpiRows.First(r => r.EntityLabel.StartsWith("SHP-02"));
        Assert.Equal("140,000 VND", shp02Row.CollectedCodText);
        Assert.Contains("1 Failed", shp02Row.RerouteOrFailedText);
        Assert.Contains("Uncollected COD", shp02Row.RerouteOrFailedText);
    }

    [Fact]
    public void T24_GenerateReport_StartDateAfterEndDate_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _controller.GenerateReport("SalesByBrand", "2026-11-25", "2026-11-20", "ALL", "ALL"));

        Assert.Empty(_controller.GetReportHistory());
    }

    [Fact]
    public void ReportLifecycle_ExportPdfCsvAndArchive_PersistsStatusAndBlocksExportWhenArchived()
    {
        var report = _controller.GenerateReport("SalesByBrand", "2026-11-01", "2026-11-30", "ALL", "ALL");

        var pdfPath = _controller.ExportOrArchiveReport(report.ReportId, "PDF", _tempExportDir);
        Assert.True(File.Exists(pdfPath));
        Assert.Equal("Exported", _controller.GetReportById(report.ReportId)!.Status);

        var csvPath = _controller.ExportOrArchiveReport(report.ReportId, "CSV", _tempExportDir);
        Assert.True(File.Exists(csvPath));
        Assert.Contains(report.ReportId, File.ReadAllText(csvPath));

        var archiveResult = _controller.ExportOrArchiveReport(report.ReportId, "ARCHIVE", _tempExportDir);
        Assert.Equal("Archived", archiveResult);
        Assert.Equal("Archived", _controller.GetReportById(report.ReportId)!.Status);

        Assert.Throws<InvalidOperationException>(() =>
            _controller.ExportOrArchiveReport(report.ReportId, "PDF", _tempExportDir));
    }
}
