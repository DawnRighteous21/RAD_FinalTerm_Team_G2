using System.IO;
using System.Text;
using SaigonBite.OFDH.DataAccess;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.Controllers;

public class ReportingController
{
    private readonly HubDAO _hubDao;
    private readonly BrandDAO _brandDao;
    private readonly ShipperDAO _shipperDao;
    private readonly OrderAnalyticsDAO _orderAnalyticsDao;
    private readonly ReportDAO _reportDao;

    public ReportingController() : this(new DatabaseConnection())
    {
    }

    public ReportingController(DatabaseConnection db)
    {
        _hubDao = new HubDAO(db);
        _brandDao = new BrandDAO(db);
        _shipperDao = new ShipperDAO(db);
        _orderAnalyticsDao = new OrderAnalyticsDAO(db);
        _reportDao = new ReportDAO(db);
    }

    public (List<string> ReportTypes, List<Hub> Hubs, List<Brand> Brands) GetFilterOptions()
    {
        var types = new List<string> { "SalesByBrand", "HubWorkload", "DeliveryPerformance" };
        var hubs = _hubDao.FindAll();
        var brands = _brandDao.FindAllBrands();
        return (types, hubs, brands);
    }

    public ManagementReport GenerateReport(
        string reportType,
        string startDate,
        string endDate,
        string? hubId = null,
        string? brandId = null)
    {
        var normalizedType = reportType is "SalesByBrand" or "HubWorkload" or "DeliveryPerformance"
            ? reportType
            : throw new ArgumentException($"Unsupported report type '{reportType}'.");

        var cleanHubId = string.IsNullOrWhiteSpace(hubId) || string.Equals(hubId, "ALL", StringComparison.OrdinalIgnoreCase)
            ? null
            : hubId.Trim();

        var cleanBrandId = string.IsNullOrWhiteSpace(brandId) || string.Equals(brandId, "ALL", StringComparison.OrdinalIgnoreCase)
            ? null
            : brandId.Trim();

        var report = new ManagementReport
        {
            ReportId = _reportDao.GenerateNextReportId(),
            ReportType = normalizedType,
            StartDate = (startDate ?? string.Empty).Trim(),
            EndDate = (endDate ?? string.Empty).Trim(),
            GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Status = "Generated"
        };

        report.ValidateDateRange();

        var allHubs = _hubDao.FindAll();
        var scopedHubs = cleanHubId == null
            ? allHubs
            : allHubs.Where(h => string.Equals(h.HubId, cleanHubId, StringComparison.OrdinalIgnoreCase)).ToList();

        var allBrands = _brandDao.FindAllBrands();
        var shippers = _shipperDao.FindAll();
        var matchingOrders = _orderAnalyticsDao.FindOrdersByCriteria(
            report.StartDate,
            report.EndDate,
            cleanHubId,
            cleanBrandId);

        report.ComputeKPIs(matchingOrders, scopedHubs, allBrands, shippers, cleanBrandId);
        _reportDao.SaveReportSnapshot(report);

        return report;
    }

    public string ExportOrArchiveReport(string reportId, string actionFormat, string? exportDirectory = null)
    {
        var report = _reportDao.FindById(reportId)
            ?? throw new InvalidOperationException($"Report '{reportId}' was not found.");

        if (string.Equals(actionFormat, "ARCHIVE", StringComparison.OrdinalIgnoreCase))
        {
            report.MarkArchived();
            _reportDao.UpdateReportStatus(report.ReportId, "Archived");
            return "Archived";
        }

        if (string.Equals(actionFormat, "PDF", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(actionFormat, "CSV", StringComparison.OrdinalIgnoreCase))
        {
            var fileName = report.MarkExported(actionFormat);
            _reportDao.UpdateReportStatus(report.ReportId, "Exported");

            var dir = string.IsNullOrWhiteSpace(exportDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "Exports")
                : exportDirectory;
            Directory.CreateDirectory(dir);

            var fullPath = Path.Combine(dir, fileName);
            if (string.Equals(actionFormat, "CSV", StringComparison.OrdinalIgnoreCase))
                WriteCsvSummary(fullPath, report);
            else
                WriteMinimalPdfSummary(fullPath, report);

            return fullPath;
        }

        throw new ArgumentException($"Unsupported action format '{actionFormat}'. Use PDF, CSV, or ARCHIVE.");
    }

    public ManagementReport? GetReportById(string reportId) => _reportDao.FindById(reportId);

    public List<ManagementReport> GetReportHistory() => _reportDao.FindAll();

    private static void WriteCsvSummary(string path, ManagementReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ReportId,ReportType,StartDate,EndDate,GeneratedAt,TotalRevenueVND,TotalDeliveredOrders,AvgDeliveryTimeMins,PeakHubUtilizationPct,Status");
        sb.AppendLine($"{report.ReportId},{report.ReportType},{report.StartDate},{report.EndDate},{report.GeneratedAt},{report.TotalRevenue},{report.TotalOrders},{report.AvgDeliveryTimeMins},{report.PeakHubUtilizationPct},{report.Status}");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteMinimalPdfSummary(string path, ManagementReport report)
    {
        var lines = new[]
        {
            "SaigonBite OFDH - Management Analytics Report",
            $"Report ID: {report.ReportId} | Type: {report.ReportType}",
            $"Date Range: {report.StartDate} to {report.EndDate}",
            $"Generated At: {report.GeneratedAt} | Status: {report.Status}",
            $"Total Net Revenue: {report.TotalRevenue:N0} VND",
            $"Total Delivered Orders: {report.TotalOrders}",
            $"Avg Delivery Time: {report.AvgDeliveryTimeMins:0.0} mins",
            $"Peak Hub Utilization: {report.PeakHubUtilizationPct:0.0}%"
        };

        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("BT");
        contentBuilder.AppendLine("/F1 12 Tf");
        contentBuilder.AppendLine("50 760 Td");
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) contentBuilder.AppendLine("0 -22 Td");
            var escaped = lines[i].Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
            contentBuilder.AppendLine($"({escaped}) Tj");
        }
        contentBuilder.AppendLine("ET");

        var streamText = contentBuilder.ToString();
        var streamBytes = Encoding.ASCII.GetByteCount(streamText);

        var pdf = new StringBuilder();
        pdf.AppendLine("%PDF-1.4");
        var offsets = new List<int>();

        offsets.Add(pdf.Length);
        pdf.AppendLine("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj");

        offsets.Add(pdf.Length);
        pdf.AppendLine("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj");

        offsets.Add(pdf.Length);
        pdf.AppendLine("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >> endobj");

        offsets.Add(pdf.Length);
        pdf.AppendLine($"4 0 obj << /Length {streamBytes} >> stream");
        pdf.Append(streamText);
        pdf.AppendLine("endstream endobj");

        offsets.Add(pdf.Length);
        pdf.AppendLine("5 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj");

        var xrefOffset = pdf.Length;
        pdf.AppendLine("xref");
        pdf.AppendLine($"0 {offsets.Count + 1}");
        pdf.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets)
        {
            pdf.AppendLine($"{offset:D10} 00000 n ");
        }

        pdf.AppendLine("trailer");
        pdf.AppendLine($"<< /Size {offsets.Count + 1} /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefOffset.ToString());
        pdf.AppendLine("%%EOF");

        File.WriteAllText(path, pdf.ToString(), Encoding.ASCII);
    }
}
