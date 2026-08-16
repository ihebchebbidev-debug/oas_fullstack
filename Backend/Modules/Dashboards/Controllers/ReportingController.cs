using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Infrastructure;
using MyApi.Modules.Dashboards.DTOs;
using MyApi.Modules.Roles.Services;

namespace MyApi.Modules.Dashboards.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReportingController : ControllerBase
    {
        private readonly ITenantDbContextFactory _dbFactory;
        private readonly ILogger<ReportingController> _logger;
        private readonly IPermissionService _permissionService;

        public ReportingController(
            ITenantDbContextFactory dbFactory,
            ILogger<ReportingController> logger,
            IPermissionService permissionService)
        {
            _dbFactory = dbFactory;
            _logger = logger;
            _permissionService = permissionService;
        }

        private string GetTenant() =>
            Request.Headers.TryGetValue(TenantMiddleware.TenantHeaderName, out var t) ? t.ToString() : "";

        // MainAdminUser bypasses granular permissions (matches UserType claim set in AuthService).
        // Otherwise the user must have <module>:read granted through one of their active roles.
        private async Task<bool> HasReportingReadAsync(string module)
        {
            var userType = User.FindFirst("UserType")?.Value;
            if (string.Equals(userType, "MainAdminUser", StringComparison.OrdinalIgnoreCase))
                return true;

            var idClaim = User.FindFirst("UserId")?.Value
                          ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var userId)) return false;

            return await _permissionService.UserHasPermissionAsync(userId, module, "read");
        }

        // ─── GET /api/reporting/sales ──────────────────────────────────
        [HttpGet("sales")]
        public async Task<IActionResult> GetSalesReport()
        {
            var tenant = GetTenant();
            await using var context = _dbFactory.CreateDbContext(tenant);

            var report = new SalesReportDto();

            // 1. Offers by Status
            var offers = await context.Offers
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            report.OffersByStatus = offers.Select(x => new ChartDataPointDto
            {
                Name = x.Status,
                Value = x.Count
            }).ToList();

            // 2. Sales Orders by Status
            var sales = await context.Sales
                .GroupBy(s => s.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            report.SalesByStatus = sales.Select(x => new ChartDataPointDto
            {
                Name = x.Status,
                Value = x.Count
            }).ToList();

            // 3. Conversion Trend
            var monthAgo6 = DateTime.UtcNow.AddMonths(-6);
            var offersLast6Months = await context.Offers
                .Where(o => o.CreatedDate >= monthAgo6)
                .Select(o => new { o.CreatedDate.Year, o.CreatedDate.Month, o.Status })
                .ToListAsync();

            var groups = offersLast6Months
                .GroupBy(x => new { x.Year, x.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .ToList();

            foreach (var g in groups)
            {
                int total = g.Count();
                int won = g.Count(x =>
                {
                    var s = (x.Status ?? string.Empty).ToLowerInvariant();
                    return s == "accepted" || s == "won";
                });
                decimal rate = total > 0 ? (decimal)won / total * 100m : 0m;

                report.ConversionTrend.Add(new ChartDataPointDto
                {
                    Name = $"{new DateTime(g.Key.Year, g.Key.Month, 1):MMM}",
                    Value = Math.Round(rate, 1),
                    Target = 50m
                });
            }

            // 4. YoY Comparison
            var salesYoy = await context.Sales
                .Where(s => s.CreatedDate.Year >= DateTime.UtcNow.Year - 2)
                .Select(s => new { s.CreatedDate.Year, s.CreatedDate.Month })
                .ToListAsync();

            var yoyGroups = salesYoy.GroupBy(x => x.Month).OrderBy(g => g.Key).ToList();
            var currentYear = DateTime.UtcNow.Year;
            
            foreach (var g in yoyGroups)
            {
                report.YoyComparison.Add(new MultiSeriesChartPointDto
                {
                    Name = $"{new DateTime(2000, g.Key, 1):MMM}",
                    Series1 = g.Count(x => x.Year == currentYear - 2), // 2024
                    Series2 = g.Count(x => x.Year == currentYear - 1), // 2025
                    Series3 = g.Count(x => x.Year == currentYear)      // 2026
                });
            }

            // 5. Orders & Offers by Type — group line items by Type across offers + sales
            var saleItemTypes = await context.SaleItems
                .AsNoTracking()
                .GroupBy(i => i.Type)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();
            var offerItemTypes = await context.OfferItems
                .AsNoTracking()
                .GroupBy(i => i.Type)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();
            report.OrdersByType = saleItemTypes.Concat(offerItemTypes)
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Type) ? "article" : x.Type!)
                .Select(g => new ChartDataPointDto
                {
                    Name = char.ToUpperInvariant(g.Key[0]) + g.Key.Substring(1),
                    Value = g.Sum(x => x.Count)
                })
                .OrderByDescending(x => x.Value)
                .Take(8)
                .ToList();

            // 6. Top Customers
            var topSales = await context.Sales
                .Where(s => s.ContactId != 0)
                .GroupBy(s => s.ContactId)
                .Select(g => new { ContactId = g.Key, Revenue = g.Sum(x => x.TotalAmount) })
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToListAsync();

            if (topSales.Any())
            {
                var contactIds = topSales.Select(x => x.ContactId).ToList();
                var contacts = await context.Contacts.Where(c => contactIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);
                
                foreach(var s in topSales)
                {
                    report.TopCustomers.Add(new RagTableItemDto
                    {
                        Id = s.ContactId,
                        Title = contacts.TryGetValue(s.ContactId, out var cname) ? cname : "Unknown",
                        Amount = s.Revenue,
                        Date = DateTime.UtcNow
                    });
                }
            }

            return Ok(report);
        }

        // ─── GET /api/reporting/service ──────────────────────────────────
        [HttpGet("service")]
        public async Task<IActionResult> GetServiceReport()
        {
            var tenant = GetTenant();
            await using var context = _dbFactory.CreateDbContext(tenant);
            var report = new ServiceReportDto();

            // 1. Completion By Month (ServiceOrders)
            var currentYear = DateTime.UtcNow.Year;
            var soYtd = await context.ServiceOrders
                .Where(s => s.CreatedDate.Year == currentYear)
                .Select(s => new { s.CreatedDate.Month, s.Status })
                .ToListAsync();

            var monthlyGroups = soYtd.GroupBy(s => s.Month).OrderBy(g => g.Key).ToList();
            foreach(var g in monthlyGroups)
            {
                int total = g.Count();
                int completed = g.Count(x => x.Status.ToLower() == "completed" || x.Status.ToLower() == "closed");
                report.CompletionByMonth.Add(new ChartDataPointDto
                {
                    Name = $"{new DateTime(currentYear, g.Key, 1):MMM}",
                    Value = total > 0 ? Math.Round((decimal)completed / total * 100m, 1) : 0m,
                    Target = 90m
                });
            }

            // 2. Work Orders by Status
            var soStatus = await context.ServiceOrders
                .GroupBy(s => s.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();
            report.WorkOrdersByStatus = soStatus.Select(x => new ChartDataPointDto { Name = x.Status, Value = x.Count }).ToList();

            // 3. Work Orders by Type
            var soType = await context.ServiceOrders
                .GroupBy(s => s.ServiceType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();
            report.WorkOrdersByType = soType.Select(x => new ChartDataPointDto { Name = string.IsNullOrEmpty(x.Type) ? "Standard" : x.Type, Value = x.Count }).ToList();

            // 4. Dispatches per Tech (YoY)
            var dispatches = await context.Set<MyApi.Modules.Dispatches.Models.Dispatch>()
                .Where(d => d.CreatedDate.Year >= currentYear - 2)
                .Select(d => new { d.CreatedDate.Year, d.DispatchedBy })
                .ToListAsync();
            var techGroups = dispatches.Where(d => !string.IsNullOrEmpty(d.DispatchedBy)).GroupBy(d => d.DispatchedBy).Take(5).ToList();
            
            foreach(var g in techGroups)
            {
                report.DispatchesPerTech.Add(new MultiSeriesChartPointDto
                {
                    Name = g.Key!,
                    Series1 = g.Count(x => x.Year == currentYear - 2),
                    Series2 = g.Count(x => x.Year == currentYear - 1),
                    Series3 = g.Count(x => x.Year == currentYear)
                });
            }

            // 5. Consumed vs Planned Hours (current year ServiceOrderJobs by ScheduledDate/CompletedDate)
            var jobHours = await context.ServiceOrderJobs
                .AsNoTracking()
                .Select(j => new { j.EstimatedHours, j.ActualHours, j.EstimatedDuration, j.ActualDuration, j.ScheduledDate, j.CompletedDate })
                .ToListAsync();
            var jobsThisYear = jobHours.Where(j =>
                (j.ScheduledDate.HasValue && j.ScheduledDate.Value.Year == currentYear) ||
                (j.CompletedDate.HasValue && j.CompletedDate.Value.Year == currentYear))
                .ToList();
            if (!jobsThisYear.Any()) jobsThisYear = jobHours; // fallback if no year-tagged jobs

            decimal planned = jobsThisYear.Sum(j =>
                j.EstimatedHours ?? (j.EstimatedDuration.HasValue ? (decimal)j.EstimatedDuration.Value / 60m : 0m));
            decimal consumed = jobsThisYear.Sum(j =>
                j.ActualHours ?? (j.ActualDuration.HasValue ? (decimal)j.ActualDuration.Value / 60m : 0m));
            decimal saved = Math.Max(0m, planned - consumed);
            decimal efficiency = consumed > 0m ? Math.Round(planned / consumed * 100m, 0) : 0m;

            report.ConsumedVsPlanned.Add(new ChartDataPointDto { Name = "Efficiency", Value = efficiency });
            report.ConsumedVsPlanned.Add(new ChartDataPointDto { Name = "Planned", Value = Math.Round(planned, 0) });
            report.ConsumedVsPlanned.Add(new ChartDataPointDto { Name = "Consumed", Value = Math.Round(consumed, 0) });
            report.ConsumedVsPlanned.Add(new ChartDataPointDto { Name = "HoursSaved", Value = Math.Round(saved, 0) });

            return Ok(report);
        }

        // ─── GET /api/reporting/finance ──────────────────────────────────
        [HttpGet("finance")]
        public async Task<IActionResult> GetFinanceReport()
        {
            if (!await HasReportingReadAsync("reporting_finance"))
                return StatusCode(403, new { success = false, message = "Missing reporting_finance:read permission" });

            var tenant = GetTenant();
            await using var context = _dbFactory.CreateDbContext(tenant);
            var report = new FinanceReportDto();

            // 1. Invoice Status Donut — consider all sales (any Sale is a potential revenue line)
            var invoices = await context.Sales
                .Where(s => !s.IsDeleted)
                .ToListAsync();

            int paid    = invoices.Count(i => (i.PaymentStatus ?? "").ToLower() == "paid");
            int pending = invoices.Count(i => { var p = (i.PaymentStatus ?? "").ToLower(); return p == "" || p == "pending" || p == "unpaid" || p == "open"; });
            int overdue = invoices.Count(i => (i.PaymentStatus ?? "").ToLower() == "overdue");
            int partial = invoices.Count(i => { var p = (i.PaymentStatus ?? "").ToLower(); return p == "partial" || p == "partially_paid"; });

            if (paid > 0)    report.InvoiceStatusDonut.Add(new ChartDataPointDto { Name = "Paid",    Value = paid });
            if (pending > 0) report.InvoiceStatusDonut.Add(new ChartDataPointDto { Name = "Pending", Value = pending });
            if (partial > 0) report.InvoiceStatusDonut.Add(new ChartDataPointDto { Name = "Partial", Value = partial });
            if (overdue > 0) report.InvoiceStatusDonut.Add(new ChartDataPointDto { Name = "Overdue", Value = overdue });

            // 2. KPIs (only from real data)
            var totalRevenue    = invoices.Sum(i => i.TotalAmount);
            var pendingRevenue  = invoices.Where(i => (i.PaymentStatus ?? "").ToLower() != "paid").Sum(i => i.TotalAmount);

            report.Kpis.Add(new ReportKpiDto { Title = "Total Revenue",      Value = totalRevenue,   FormattedValue = totalRevenue.ToString("N2"),   RagStatus = totalRevenue > 0 ? "green" : "neutral" });
            report.Kpis.Add(new ReportKpiDto { Title = "Outstanding",        Value = pendingRevenue, FormattedValue = pendingRevenue.ToString("N2"), RagStatus = pendingRevenue > 0 ? "yellow" : "neutral" });
            report.Kpis.Add(new ReportKpiDto { Title = "Invoices",           Value = invoices.Count, FormattedValue = invoices.Count.ToString(),     RagStatus = "neutral" });
            report.Kpis.Add(new ReportKpiDto { Title = "Overdue",            Value = overdue,        FormattedValue = overdue.ToString(),            RagStatus = overdue > 0 ? "red" : "green" });

            // 3. Expenses by category (from dispatch expenses)
            var expenseGroups = await context.DispatchExpenses
                .GroupBy(e => e.ExpenseType)
                .Select(g => new { Category = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync();
            report.ExpensesByCategory = expenseGroups
                .OrderByDescending(x => x.Total)
                .Select(x => new ChartDataPointDto { Name = string.IsNullOrEmpty(x.Category) ? "Other" : x.Category, Value = x.Total })
                .ToList();

            // 4. Invoice table (top 10 by amount)
            var topInvoices = invoices
                .OrderByDescending(i => i.TotalAmount)
                .Take(10)
                .ToList();
            var contactIdsInv = topInvoices.Select(i => i.ContactId).Distinct().ToList();
            var contactMapInv = await context.Contacts
                .Where(c => contactIdsInv.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name);
            foreach (var inv in topInvoices)
            {
                var payStatus = (inv.PaymentStatus ?? "pending").ToLower();
                var rag = payStatus == "paid" ? "green"
                    : payStatus == "overdue" ? "red"
                    : payStatus == "partial" ? "yellow"
                    : "neutral";
                report.InvoiceTable.Add(new RagTableItemDto
                {
                    Id = inv.Id,
                    Title = inv.SaleNumber ?? $"#{inv.Id}",
                    Subtitle = contactMapInv.TryGetValue(inv.ContactId, out var cn) ? cn : "—",
                    Amount = inv.TotalAmount,
                    Status = inv.PaymentStatus ?? "pending",
                    RagDot = rag,
                    Date = inv.CreatedDate
                });
            }

            return Ok(report);
        }

        // ─── GET /api/reporting/hr ──────────────────────────────────
        [HttpGet("hr")]
        public async Task<IActionResult> GetHrReport()
        {
            if (!await HasReportingReadAsync("reporting_hr"))
                return StatusCode(403, new { success = false, message = "Missing reporting_hr:read permission" });

            var tenant = GetTenant();
            await using var context = _dbFactory.CreateDbContext(tenant);
            var report = new HrReportDto();

            // Base = all active users (employees). Salary config is optional per user.
            var users = await context.Users
                .Where(u => u.IsActive && !u.IsDeleted)
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.CreatedDate })
                .ToListAsync();
            var userIdsAll = users.Select(u => u.Id).ToList();

            var configs = await context.HrEmployeeSalaryConfigs
                .Where(x => userIdsAll.Contains(x.UserId))
                .ToListAsync();
            var configByUser = configs.GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => g.First());

            // 1. Headcount by department (falls back to "Unassigned")
            report.HeadcountByDepartment = users
                .GroupBy(u => configByUser.TryGetValue(u.Id, out var c) && !string.IsNullOrWhiteSpace(c.Department) ? c.Department! : "Unassigned")
                .OrderByDescending(g => g.Count())
                .Select(g => new ChartDataPointDto { Name = g.Key, Value = g.Count() })
                .ToList();

            // 2. Salary cost by department (only users with configured salary)
            report.SalaryByDepartment = configs
                .GroupBy(c => string.IsNullOrWhiteSpace(c.Department) ? "Unassigned" : c.Department!)
                .OrderByDescending(g => g.Sum(x => x.GrossSalary))
                .Select(g => new ChartDataPointDto { Name = g.Key, Value = g.Sum(x => x.GrossSalary) })
                .ToList();

            // 3. Performance distribution
            var reviews = await context.HrPerformanceReviews
                .Where(r => !r.IsDeleted && r.Rating != null)
                .Select(r => r.Rating!)
                .ToListAsync();
            report.PerformanceDistribution = reviews
                .GroupBy(r => r)
                .Select(g => new ChartDataPointDto { Name = g.Key, Value = g.Count() })
                .ToList();

            // 4. Hiring vs Turnover (12 months rolling) — hires from config.HireDate or user.CreatedAt
            var start = DateTime.UtcNow.AddMonths(-11);
            start = new DateTime(start.Year, start.Month, 1);
            var hireDates = users
                .Select(u => configByUser.TryGetValue(u.Id, out var c) && c.HireDate.HasValue ? c.HireDate.Value : u.CreatedDate)
                .Where(d => d >= start)
                .ToList();
            var leaveDates = configs
                .Where(c => c.ContractEndDate.HasValue && c.ContractEndDate.Value >= start && c.ContractEndDate.Value <= DateTime.UtcNow)
                .Select(c => c.ContractEndDate!.Value)
                .ToList();
            for (int i = 0; i < 12; i++)
            {
                var m = start.AddMonths(i);
                var hires = hireDates.Count(d => d.Year == m.Year && d.Month == m.Month);
                var leavers = leaveDates.Count(d => d.Year == m.Year && d.Month == m.Month);
                report.HiringVsTurnover.Add(new MultiSeriesChartPointDto
                {
                    Name = m.ToString("MMM"),
                    Series1 = hires,
                    Series2 = leavers,
                    Series3 = hires - leavers // Net headcount change
                });
            }

            // 5. Employee table — every active user, ordered by salary desc then name
            var reviewByUser = await context.HrPerformanceReviews
                .Where(r => !r.IsDeleted && r.Rating != null)
                .GroupBy(r => r.UserId)
                .Select(g => new { UserId = g.Key, Rating = g.OrderByDescending(x => x.CreatedAt).First().Rating })
                .ToDictionaryAsync(x => x.UserId, x => x.Rating);
            var ordered = users
                .Select(u => new {
                    User = u,
                    Cfg = configByUser.TryGetValue(u.Id, out var c) ? c : null,
                })
                .OrderByDescending(x => x.Cfg?.GrossSalary ?? 0)
                .ThenBy(x => x.User.FirstName)
                .Take(50)
                .ToList();
            foreach (var row in ordered)
            {
                var rating = reviewByUser.TryGetValue(row.User.Id, out var r) ? r : null;
                var rag = rating switch
                {
                    "exceeds" or "meets" => "green",
                    "partially_meets" => "yellow",
                    "below" => "red",
                    _ => "neutral"
                };
                var name = (row.User.FirstName + " " + row.User.LastName).Trim();
                report.EmployeeTable.Add(new RagTableItemDto
                {
                    Id = row.User.Id,
                    Title = string.IsNullOrWhiteSpace(name) ? $"User #{row.User.Id}" : name,
                    Subtitle = row.Cfg != null && !string.IsNullOrWhiteSpace(row.Cfg.Department)
                        ? row.Cfg.Department!
                        : (row.Cfg?.Position ?? "—"),
                    Amount = row.Cfg?.GrossSalary ?? 0,
                    Status = rating ?? "—",
                    RagDot = rag,
                    Date = row.Cfg?.HireDate ?? row.User.CreatedDate
                });
            }

            return Ok(report);
        }

        // ─── GET /api/reporting/purchase ──────────────────────────────────
        [HttpGet("purchase")]
        public async Task<IActionResult> GetPurchaseReport()
        {
            var tenant = GetTenant();
            await using var context = _dbFactory.CreateDbContext(tenant);
            var report = new PurchaseReportDto();

            var pos = await context.Set<MyApi.Modules.Purchases.Models.PurchaseOrder>()
                .Where(p => !p.IsDeleted)
                .ToListAsync();

            // 1. Spend by supplier — top 8
            report.SpendBySupplier = pos
                .GroupBy(p => string.IsNullOrWhiteSpace(p.SupplierName) ? "—" : p.SupplierName)
                .Select(g => new ChartDataPointDto { Name = g.Key, Value = g.Sum(x => x.GrandTotal) })
                .OrderByDescending(x => x.Value)
                .Take(8)
                .ToList();

            // 2. Spend by category — group by PO status (no category field on POs)
            report.SpendByCategory = pos
                .GroupBy(p => string.IsNullOrWhiteSpace(p.Status) ? "Other" : p.Status)
                .Select(g => new ChartDataPointDto { Name = g.Key, Value = g.Sum(x => x.GrandTotal) })
                .OrderByDescending(x => x.Value)
                .ToList();

            // 3. Receipt status
            var receipts = await context.Set<MyApi.Modules.Purchases.Models.GoodsReceipt>()
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();
            report.ReceiptStatus = receipts
                .Select(r => new ChartDataPointDto { Name = string.IsNullOrEmpty(r.Status) ? "Other" : r.Status, Value = r.Count })
                .ToList();

            // 4. PO spend trend (12 months)
            var start = DateTime.UtcNow.AddMonths(-11);
            start = new DateTime(start.Year, start.Month, 1);
            var recentPos = pos.Where(p => p.OrderDate >= start).ToList();
            for (int i = 0; i < 12; i++)
            {
                var m = start.AddMonths(i);
                var monthTotal = recentPos
                    .Where(p => p.OrderDate.Year == m.Year && p.OrderDate.Month == m.Month)
                    .Sum(p => p.GrandTotal);
                report.PoSpendTrend.Add(new ChartDataPointDto { Name = m.ToString("MMM"), Value = monthTotal });
            }

            // 5. PO detail table — 10 most recent
            var poTop = pos.OrderByDescending(p => p.OrderDate).Take(10).ToList();
            foreach (var p in poTop)
            {
                var status = (p.Status ?? "").ToLower();
                var rag = status switch
                {
                    "received" or "closed" or "completed" => "green",
                    "approved" or "sent" => "neutral",
                    "cancelled" or "rejected" => "red",
                    "draft" or "pending" => "yellow",
                    _ => "neutral"
                };
                report.PoTable.Add(new RagTableItemDto
                {
                    Id = p.Id,
                    Title = string.IsNullOrEmpty(p.OrderNumber) ? $"#{p.Id}" : p.OrderNumber,
                    Subtitle = p.SupplierName ?? "—",
                    Amount = p.GrandTotal,
                    Status = p.Status ?? "—",
                    RagDot = rag,
                    Date = p.OrderDate
                });
            }

            return Ok(report);
        }
    }
}
