using Microsoft.Data.Sqlite;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.DataAccess;

public class ReportDAO
{
    private readonly DatabaseConnection _db;

    public ReportDAO(DatabaseConnection db)
    {
        _db = db;
    }

    public string GenerateNextReportId()
    {
        var prefix = $"RPT-{DateTime.Now:yyyyMMdd}-";
        var maxSeq = 0;

        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT report_id FROM ManagementReports WHERE report_id LIKE @prefix;";
        cmd.Parameters.AddWithValue("@prefix", prefix + "%");

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            if (id.Length > prefix.Length &&
                int.TryParse(id[prefix.Length..], out var seq) &&
                seq > maxSeq)
            {
                maxSeq = seq;
            }
        }

        return $"{prefix}{maxSeq + 1:D2}";
    }

    public bool SaveReportSnapshot(ManagementReport report)
    {
        using var conn = _db.GetConnection();
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO ManagementReports (
                    report_id, report_type, start_date, end_date, generated_at,
                    total_revenue, total_orders, avg_delivery_time_mins,
                    peak_hub_utilization_pct, status
                )
                VALUES (
                    @reportId, @reportType, @startDate, @endDate, @generatedAt,
                    @totalRevenue, @totalOrders, @avgDeliveryTimeMins,
                    @peakHubUtilizationPct, @status
                );
                """;
            cmd.Parameters.AddWithValue("@reportId", report.ReportId);
            cmd.Parameters.AddWithValue("@reportType", report.ReportType);
            cmd.Parameters.AddWithValue("@startDate", report.StartDate);
            cmd.Parameters.AddWithValue("@endDate", report.EndDate);
            cmd.Parameters.AddWithValue("@generatedAt", report.GeneratedAt);
            cmd.Parameters.AddWithValue("@totalRevenue", report.TotalRevenue);
            cmd.Parameters.AddWithValue("@totalOrders", report.TotalOrders);
            cmd.Parameters.AddWithValue("@avgDeliveryTimeMins", report.AvgDeliveryTimeMins);
            cmd.Parameters.AddWithValue("@peakHubUtilizationPct", report.PeakHubUtilizationPct);
            cmd.Parameters.AddWithValue("@status", report.Status);
            cmd.ExecuteNonQuery();
        }

        foreach (var hubId in report.IncludedHubIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            using var hubCmd = conn.CreateCommand();
            hubCmd.Transaction = tx;
            hubCmd.CommandText = """
                INSERT OR IGNORE INTO ReportHubs (report_id, hub_id)
                VALUES (@reportId, @hubId);
                """;
            hubCmd.Parameters.AddWithValue("@reportId", report.ReportId);
            hubCmd.Parameters.AddWithValue("@hubId", hubId);
            hubCmd.ExecuteNonQuery();
        }

        tx.Commit();
        return true;
    }

    public ManagementReport? FindById(string reportId)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT report_id, report_type, start_date, end_date, generated_at,
                   total_revenue, total_orders, avg_delivery_time_mins,
                   peak_hub_utilization_pct, status
            FROM ManagementReports
            WHERE report_id = @reportId;
            """;
        cmd.Parameters.AddWithValue("@reportId", reportId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var report = MapReport(reader);
        report.IncludedHubIds = FindHubIdsForReport(conn, report.ReportId);
        return report;
    }

    public List<ManagementReport> FindAll()
    {
        var list = new List<ManagementReport>();
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT report_id, report_type, start_date, end_date, generated_at,
                   total_revenue, total_orders, avg_delivery_time_mins,
                   peak_hub_utilization_pct, status
            FROM ManagementReports
            ORDER BY generated_at DESC, report_id DESC;
            """;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapReport(reader));
        }

        foreach (var report in list)
        {
            report.IncludedHubIds = FindHubIdsForReport(conn, report.ReportId);
        }

        return list;
    }

    public bool UpdateReportStatus(string reportId, string status)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE ManagementReports
            SET status = @status
            WHERE report_id = @reportId;
            """;
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@reportId", reportId);

        return cmd.ExecuteNonQuery() > 0;
    }

    private static List<string> FindHubIdsForReport(SqliteConnection conn, string reportId)
    {
        var hubIds = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT hub_id FROM ReportHubs WHERE report_id = @reportId ORDER BY hub_id;";
        cmd.Parameters.AddWithValue("@reportId", reportId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            hubIds.Add(reader.GetString(0));
        }
        return hubIds;
    }

    private static ManagementReport MapReport(SqliteDataReader reader)
    {
        return new ManagementReport
        {
            ReportId = reader.GetString(0),
            ReportType = reader.GetString(1),
            StartDate = reader.GetString(2),
            EndDate = reader.GetString(3),
            GeneratedAt = reader.GetString(4),
            TotalRevenue = Convert.ToDecimal(reader.GetDouble(5)),
            TotalOrders = reader.GetInt32(6),
            AvgDeliveryTimeMins = Convert.ToDecimal(reader.GetDouble(7)),
            PeakHubUtilizationPct = Convert.ToDecimal(reader.GetDouble(8)),
            Status = reader.GetString(9)
        };
    }
}
