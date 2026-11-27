using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SaigonBite.OFDH.Controllers;
using SaigonBite.OFDH.Domain;

namespace SaigonBite.OFDH.Views.UC16;

public partial class ManagementReportView : UserControl
{
    private sealed record FilterOption(string Id, string Label);

    private static readonly string[] BarPalette = { "#E65100", "#1E293B", "#2563EB", "#059669", "#D97706" };

    private readonly ReportingController _controller = new();
    private ManagementReport? _currentReport;

    public ManagementReportView()
    {
        InitializeComponent();
        LoadFilterOptions();
        GenerateCurrentReport(showToast: false);
    }

    private void Root_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var current = e.OriginalSource as DependencyObject;
        while (current != null)
        {
            if (current is DataGridRow or TextBox or ComboBox or ComboBoxItem or Button)
                return;

            var parent = current is Visual
                ? VisualTreeHelper.GetParent(current)
                : null;

            current = parent ?? LogicalTreeHelper.GetParent(current);
        }

        Keyboard.ClearFocus();
    }

    private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DgBrandSales.UnselectAll();
            DgOperationalKpis.UnselectAll();
            Keyboard.ClearFocus();
        }
    }

    private void ChartBarsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_currentReport != null)
            RenderBarChart(_currentReport);
    }

    private void LoadFilterOptions()
    {
        var (reportTypes, hubs, brands) = _controller.GetFilterOptions();

        CmbReportType.ItemsSource = reportTypes;
        if (reportTypes.Count > 0)
            CmbReportType.SelectedIndex = 0;

        var hubOptions = new List<FilterOption> { new("ALL", "All Hubs") };
        hubOptions.AddRange(hubs.Select(h => new FilterOption(h.HubId, $"{h.HubId} ({h.District})")));
        CmbHubFilter.ItemsSource = hubOptions;
        CmbHubFilter.SelectedIndex = 0;

        var brandOptions = new List<FilterOption> { new("ALL", "All Brands") };
        brandOptions.AddRange(brands.Select(b => new FilterOption(b.BrandId, b.DisplayLabel)));
        CmbBrandFilter.ItemsSource = brandOptions;
        CmbBrandFilter.SelectedIndex = 0;

        TxtStartDate.Text = "2026-11-01";
        TxtEndDate.Text = DateTime.Today > new DateTime(2026, 11, 30)
            ? DateTime.Today.ToString("yyyy-MM-dd")
            : "2026-11-30";
    }

    private void BtnGenerateReport_Click(object sender, RoutedEventArgs e)
    {
        GenerateCurrentReport(showToast: true);
    }

    private void GenerateCurrentReport(bool showToast)
    {
        var reportType = CmbReportType.SelectedItem?.ToString() ?? "SalesByBrand";
        var hubId = (CmbHubFilter.SelectedItem as FilterOption)?.Id ?? "ALL";
        var brandId = (CmbBrandFilter.SelectedItem as FilterOption)?.Id ?? "ALL";

        try
        {
            _currentReport = _controller.GenerateReport(
                reportType,
                TxtStartDate.Text,
                TxtEndDate.Text,
                hubId,
                brandId);

            TxtValidationStrip.Foreground = CreateBrush("#15803D");
            TxtValidationStrip.Text =
                "Validation OK: Start Date <= End Date (T-24) | Revenue Rule: Strictly Delivered orders minus approved refunds; Cancelled & DeliveryFailed excluded (T-21).";

            RenderReport(_currentReport);

            if (showToast)
            {
                ShowToast(
                    $"Report {_currentReport.ReportId} ({_currentReport.ReportType}) generated and saved to SQLite.",
                    isError: false);
            }
        }
        catch (Exception ex)
        {
            TxtValidationStrip.Foreground = CreateBrush("#DC2626");
            TxtValidationStrip.Text = $"Validation Error (T-24): {ex.Message}";
            ShowToast($"Report Generation Blocked: {ex.Message}", isError: true);
        }
    }

    private void RenderReport(ManagementReport report)
    {
        TxtKpiRevenue.Text = $"{report.TotalRevenue:N0} VND";
        TxtKpiRevenueSub.Text = report.TotalRefunds > 0
            ? $"{report.TotalOrders} Delivered (-{report.TotalRefunds:N0} refund) | {report.ExcludedOrderCount} Excl."
            : $"{report.TotalOrders} Delivered | {report.ExcludedOrderCount} Cancelled/Failed excl.";

        TxtKpiOrders.Text = report.TotalOrders.ToString();
        TxtKpiOrdersSub.Text = $"Completion Rate: {report.CompletionRatePct:0.0}%";

        TxtKpiAvgTime.Text = $"{report.AvgDeliveryTimeMins:0.0} mins";
        TxtKpiAvgTimeSub.Text = report.AvgDeliveryTimeMins <= 35m
            ? "Target SLA: <= 35 mins (On Target)"
            : "Target SLA: <= 35 mins (Above SLA)";
        TxtKpiAvgTimeSub.Foreground = CreateBrush(report.AvgDeliveryTimeMins <= 35m ? "#15803D" : "#DC2626");

        TxtKpiPeakHub.Text = $"{report.PeakHubUtilizationPct:0.#}%";
        TxtKpiPeakHubSub.Text = report.PeakHubSummaryText;

        TxtBreakdownTitle.Text = $"Report Breakdown — {report.ReportId}";
        TxtReportMeta.Text = $"Report ID: {report.ReportId} | {report.GeneratedAt}";

        DgBrandSales.ItemsSource = report.BrandSalesRows;
        DgOperationalKpis.ItemsSource = report.OperationalKpiRows;

        RenderBarChart(report);
        UpdateStatusBadgesAndButtons(report.Status);
    }

    private void RenderBarChart(ManagementReport report)
    {
        ChartLegendPanel.Children.Clear();
        ChartBarsGrid.Children.Clear();
        ChartBarsGrid.ColumnDefinitions.Clear();

        var dataRows = report.BrandSalesRows.Where(r => !r.IsTotalRow).ToList();
        if (dataRows.Count == 0)
        {
            ChartBarsGrid.Children.Add(new TextBlock
            {
                Text = "No brand revenue data in selected range.",
                Foreground = CreateBrush("#64748B"),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            TxtChartCaption.Text = "Adjust date range or filters and click Generate Report.";
            return;
        }

        var availableHeight = ChartBarsGrid.ActualHeight > 48 ? ChartBarsGrid.ActualHeight : 112.0;
        var maxBarHeight = Math.Max(42.0, availableHeight - 28.0);
        var maxRevenue = Math.Max(1m, dataRows.Max(r => r.NetRevenue));

        for (var i = 0; i < dataRows.Count; i++)
        {
            var row = dataRows[i];
            var barColor = CreateBrush(BarPalette[i % BarPalette.Length]);

            var legendItem = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8, 0, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            legendItem.Children.Add(new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = barColor,
                Margin = new Thickness(0, 0, 6, 0)
            });
            legendItem.Children.Add(new TextBlock
            {
                Text = row.BrandName,
                FontSize = 11.5,
                Foreground = CreateBrush("#0F172A")
            });
            ChartLegendPanel.Children.Add(legendItem);

            ChartBarsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var barRatio = (double)(row.NetRevenue / maxRevenue);
            var barHeight = row.NetRevenue <= 0m
                ? 10.0
                : Math.Max(18.0, Math.Round(barRatio * maxBarHeight));

            var barStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10, 0, 10, 0)
            };

            barStack.Children.Add(new TextBlock
            {
                Text = $"{row.NetRevenue:N0}",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = CreateBrush("#334155"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            });

            barStack.Children.Add(new Border
            {
                Width = 72,
                Height = barHeight,
                Background = barColor,
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                ToolTip = $"{row.BrandName}: {row.NetRevenue:N0} VND ({row.DeliveredCount} delivered)"
            });

            Grid.SetColumn(barStack, i);
            ChartBarsGrid.Children.Add(barStack);
        }

        TxtChartCaption.Text =
            $"Excludes {report.ExcludedOrderValue:N0} VND from {report.ExcludedOrderCount} Cancelled/Rejected/Failed order(s) in selected range.";
    }

    private void UpdateStatusBadgesAndButtons(string status)
    {
        TxtBreakdownStatus.Text = $"Status: {status}";
        TxtLifecycleStatus.Text = status;

        var isArchived = string.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase);
        var isExported = string.Equals(status, "Exported", StringComparison.OrdinalIgnoreCase);

        if (isExported)
        {
            ApplyBadgeColors(BadgeBreakdownStatus, TxtBreakdownStatus, "#DCFCE7", "#15803D");
            ApplyBadgeColors(BadgeLifecycleStatus, TxtLifecycleStatus, "#DCFCE7", "#15803D");
        }
        else if (isArchived)
        {
            ApplyBadgeColors(BadgeBreakdownStatus, TxtBreakdownStatus, "#E2E8F0", "#475569");
            ApplyBadgeColors(BadgeLifecycleStatus, TxtLifecycleStatus, "#E2E8F0", "#475569");
        }
        else
        {
            ApplyBadgeColors(BadgeBreakdownStatus, TxtBreakdownStatus, "#DBEAFE", "#1D4ED8");
            ApplyBadgeColors(BadgeLifecycleStatus, TxtLifecycleStatus, "#DBEAFE", "#1D4ED8");
        }

        BtnExportPdf.IsEnabled = _currentReport != null && !isArchived;
        BtnExportCsv.IsEnabled = _currentReport != null && !isArchived;
        BtnArchiveReport.IsEnabled = _currentReport != null && !isArchived;
    }

    private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
    {
        ExecuteReportLifecycleAction("PDF");
    }

    private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
    {
        ExecuteReportLifecycleAction("CSV");
    }

    private void BtnArchiveReport_Click(object sender, RoutedEventArgs e)
    {
        ExecuteReportLifecycleAction("ARCHIVE");
    }

    private void ExecuteReportLifecycleAction(string actionFormat)
    {
        if (_currentReport == null) return;

        try
        {
            var result = _controller.ExportOrArchiveReport(_currentReport.ReportId, actionFormat);
            var updated = _controller.GetReportById(_currentReport.ReportId);
            if (updated != null)
                _currentReport.Status = updated.Status;

            UpdateStatusBadgesAndButtons(_currentReport.Status);

            if (string.Equals(actionFormat, "ARCHIVE", StringComparison.OrdinalIgnoreCase))
            {
                ShowToast($"Report {_currentReport.ReportId} archived (SQLite status = 'Archived').", isError: false);
            }
            else
            {
                var fileName = System.IO.Path.GetFileName(result);
                ShowToast(
                    $"Report Exported Successfully — Saved {fileName} (SQLite status = 'Exported'). Path: {result}",
                    isError: false);
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Report Action Failed: {ex.Message}", isError: true);
        }
    }

    private static void ApplyBadgeColors(Border border, TextBlock label, string bgHex, string fgHex)
    {
        border.Background = CreateBrush(bgHex);
        label.Foreground = CreateBrush(fgHex);
    }

    private void ShowToast(string message, bool isError)
    {
        TxtToastMessage.Text = message;
        if (isError)
        {
            ToastBorder.Background = CreateBrush("#FEE2E2");
            ToastBorder.BorderBrush = CreateBrush("#FCA5A5");
            TxtToastMessage.Foreground = CreateBrush("#DC2626");
        }
        else
        {
            ToastBorder.Background = CreateBrush("#DCFCE7");
            ToastBorder.BorderBrush = CreateBrush("#86EFAC");
            TxtToastMessage.Foreground = CreateBrush("#15803D");
        }
    }

    private static Brush CreateBrush(string hex)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }
}
