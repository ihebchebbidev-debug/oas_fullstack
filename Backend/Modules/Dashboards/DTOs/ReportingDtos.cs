using System.Text.Json.Serialization;

namespace MyApi.Modules.Dashboards.DTOs
{
    public class ReportKpiDto
    {
        public string Title { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string FormattedValue { get; set; } = string.Empty;
        public string Trend { get; set; } = string.Empty; // e.g. "+5%", "-2%"
        public string RagStatus { get; set; } = "neutral"; // "green", "yellow", "red", "neutral"
    }

    public class ChartDataPointDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Value { get; set; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? Target { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Color { get; set; }
    }

    public class MultiSeriesChartPointDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Series1 { get; set; }
        public decimal Series2 { get; set; }
        public decimal Series3 { get; set; }
    }

    public class RagTableItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string RagDot { get; set; } = "neutral";
        public DateTime Date { get; set; }
    }

    public class SalesReportDto
    {
        public List<ChartDataPointDto> OffersByStatus { get; set; } = new();
        public List<ChartDataPointDto> SalesByStatus { get; set; } = new();
        public List<ChartDataPointDto> ConversionTrend { get; set; } = new();
        public List<MultiSeriesChartPointDto> YoyComparison { get; set; } = new();
        public List<ChartDataPointDto> OrdersByType { get; set; } = new();
        public List<RagTableItemDto> TopCustomers { get; set; } = new();
    }

    public class ServiceReportDto
    {
        public List<ChartDataPointDto> CompletionByMonth { get; set; } = new();
        public List<ChartDataPointDto> WorkOrdersByStatus { get; set; } = new();
        public List<ChartDataPointDto> WorkOrdersByType { get; set; } = new();
        public List<MultiSeriesChartPointDto> DispatchesPerTech { get; set; } = new();
        public List<ChartDataPointDto> ConsumedVsPlanned { get; set; } = new();
        public List<RagTableItemDto> TechnicianTable { get; set; } = new();
    }

    public class FinanceReportDto
    {
        public List<ReportKpiDto> Kpis { get; set; } = new();
        public List<ChartDataPointDto> InvoiceStatusDonut { get; set; } = new();
        public List<ChartDataPointDto> ExpensesByCategory { get; set; } = new();
        public List<RagTableItemDto> InvoiceTable { get; set; } = new();
    }

    public class HrReportDto
    {
        public List<ChartDataPointDto> HeadcountByDepartment { get; set; } = new();
        public List<ChartDataPointDto> SalaryByDepartment { get; set; } = new();
        public List<ChartDataPointDto> PerformanceDistribution { get; set; } = new();
        public List<MultiSeriesChartPointDto> HiringVsTurnover { get; set; } = new();
        public List<RagTableItemDto> EmployeeTable { get; set; } = new();
    }

    public class PurchaseReportDto
    {
        public List<ChartDataPointDto> SpendBySupplier { get; set; } = new();
        public List<ChartDataPointDto> SpendByCategory { get; set; } = new();
        public List<ChartDataPointDto> ReceiptStatus { get; set; } = new();
        public List<ChartDataPointDto> PoSpendTrend { get; set; } = new();
        public List<RagTableItemDto> PoTable { get; set; } = new();
    }
}
